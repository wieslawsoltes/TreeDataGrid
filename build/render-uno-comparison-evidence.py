#!/usr/bin/env python3
"""Recompute complete ABBA tables from raw hosts and reconcile collector summaries.

No workload or slow sample is discarded. Generated evidence is not a new benchmark
run and does not establish causal significance or native-frame performance.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics

ORDER = ('baseline', 'candidate', 'candidate', 'baseline')


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def derive(folder: Path) -> dict:
    provenance = read(folder / 'input.json')
    summary = read(folder / 'summary.json')
    if summary.get('collectionSucceeded') is not True or provenance['processOrder'] != list(ORDER):
        raise ValueError('Incomplete collection or unexpected host order')
    if summary['revisions'] != provenance['revisions']:
        raise ValueError('Source revisions differ between records')
    reported = {row['operation']: row for row in summary['comparisons']}
    if not reported or len(reported) != len(summary['comparisons']):
        raise ValueError('Missing or duplicated summary workloads')
    hosts = {'baseline': [], 'candidate': []}
    files = {}
    fingerprints = {}
    for index, label in enumerate(ORDER):
        path = folder / f'{index:02d}-{label}.json'
        report = read(path)
        files[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
        if report['revision'] != provenance['revisions'][label]:
            raise ValueError('Wrong raw host revision')
        if fingerprints.setdefault(label, report['librarySha256']) != report['librarySha256']:
            raise ValueError('Library changed between hosts')
        if report['librarySha256'] != summary['librarySha256'][label]:
            raise ValueError('Library fingerprint differs from summary')
        if {sample['Operation'] for sample in report['samples']} != set(reported):
            raise ValueError('Raw and summarized workloads differ')
        for operation in reported:
            samples = [row for row in report['samples'] if row['Operation'] == operation]
            if sorted(row['Iteration'] for row in samples) != list(range(report['sampleCount'])):
                raise ValueError('Missing/duplicated raw iterations')
            for row in samples:
                if type(row['Count']) is not int or row['Count'] <= 0 or row['Count'] != report['count']:
                    raise ValueError('Invalid operation count')
                for name in ('Milliseconds', 'AllocatedBytes'):
                    if type(row[name]) not in (int, float) or not math.isfinite(row[name]) or row[name] < 0:
                        raise ValueError('Invalid raw measurement')
        hosts[label].append(report)
    comparisons = []
    for operation, original in reported.items():
        result = {'operation': operation}
        checksums = {row['Checksum'] for runs in hosts.values() for host in runs for row in host['samples'] if row['Operation'] == operation}
        if len(checksums) != 1:
            raise ValueError('Executed-work checksums differ')
        for metric in ('Milliseconds', 'AllocatedBytes'):
            values = {label: [row[metric] / row['Count'] for host in runs for row in host['samples']
                              if row['Operation'] == operation] for label, runs in hosts.items()}
            medians = {label: statistics.median(data) for label, data in values.items()}
            derived = {
                'samplesPerRevision': len(values['baseline']),
                'median': medians,
                'candidateOverBaseline': medians['candidate'] / medians['baseline'] if medians['baseline'] else None,
                'p95': {label: sorted(data)[math.ceil(.95 * len(data)) - 1] for label, data in values.items()},
                'perPassMedians': {label: [statistics.median(row[metric] / row['Count'] for row in host['samples']
                    if row['Operation'] == operation) for host in runs] for label, runs in hosts.items()},
            }
            if len(values['candidate']) != len(values['baseline']):
                raise ValueError('Unequal sample counts')
            # Use the collector's exact operation order; no rounding or tolerance
            # is applied before verifying its stored summary.
            if derived != original[metric + 'PerOperation']:
                raise ValueError('Collector summary differs from raw measurements: ' + operation + '/' + metric)
            result[metric + 'PerOperation'] = derived
        comparisons.append(result)
    manifest = []
    for path in sorted(folder.rglob('*')):
        if not path.is_file(): continue
        if not path.resolve().is_relative_to(folder.resolve()):
            raise ValueError('Evidence file escapes its root')
        manifest.append({'path': str(path.relative_to(folder)), 'bytes': path.stat().st_size,
                         'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    return {'schemaVersion': 1, 'sourceRevisions': provenance['revisions'],
            'apiReaderTrees': provenance['apiReaderTrees'], 'librarySha256': fingerprints,
            'comparisons': comparisons, 'rawHostSha256': files, 'manifest': manifest,
            'extractedFileCount': len(manifest), 'collectorSummaryReconciled': True,
            'newBenchmarkRun': False, 'completePerformanceParityProven': False}


def markdown(report: dict) -> str:
    lines = ['# Recomputed comparison evidence', '',
        'Generated from all archived raw hosts; collector medians, p95 and pass medians reconcile exactly.',
        'This is a historical evidence repair, not a new measurement or universal speedup claim.', '',
        '| Workload | Baseline ns | Candidate ns | Baseline bytes | Candidate bytes |',
        '| --- | ---: | ---: | ---: | ---: |']
    for row in report['comparisons']:
        time = row['MillisecondsPerOperation']['median']; allocation = row['AllocatedBytesPerOperation']['median']
        lines.append(f"| {row['operation']} | {time['baseline'] * 1e6:.4f} | {time['candidate'] * 1e6:.4f} | {allocation['baseline']:g} | {allocation['candidate']:g} |")
    lines += ['', '## Per-pass medians and p95', '',
              '| Workload | Baseline pass medians ns | Candidate pass medians ns | Baseline p95 ns | Candidate p95 ns |',
              '| --- | --- | --- | ---: | ---: |']
    for row in report['comparisons']:
        time = row['MillisecondsPerOperation']
        passes = {key: ', '.join(f'{v * 1e6:.4f}' for v in values) for key, values in time['perPassMedians'].items()}
        lines.append(f"| {row['operation']} | {passes['baseline']} | {passes['candidate']} | {time['p95']['baseline'] * 1e6:.4f} | {time['p95']['candidate'] * 1e6:.4f} |")
    lines += ['', f"Extracted evidence files: {report['extractedFileCount']}.", '',
              'The JSON report retains every input file hash, source revision, reader tree and library fingerprint.', '']
    return '\n'.join(lines)


def repair_checkpoint(checkpoint: dict, evidence: dict, artifact: dict, failed_artifact: dict) -> dict:
    result = json.loads(json.dumps(checkpoint))
    comparison = result['controlledComparison']
    if evidence['sourceRevisions']['candidate'] != result['testedCommit']:
        raise ValueError('Checkpoint does not describe these measured sources')
    trees = set(evidence['apiReaderTrees'].values())
    if len(trees) != 1: raise ValueError('Reader implementations differed')
    comparison['apiReaderSourceTreeBothInputs'] = next(iter(trees))
    comparison['medians'] = []
    for row in evidence['comparisons']:
        time = row['MillisecondsPerOperation']; allocation = row['AllocatedBytesPerOperation']
        comparison['medians'].append({'operation': row['operation'],
            'baselineNs': time['median']['baseline'] * 1e6, 'candidateNs': time['median']['candidate'] * 1e6,
            'baselineBytes': allocation['median']['baseline'], 'candidateBytes': allocation['median']['candidate']})
    comparison['derivedPerPassNanoseconds'] = {row['operation']: {key: [v * 1e6 for v in values]
        for key, values in row['MillisecondsPerOperation']['perPassMedians'].items()} for row in evidence['comparisons']}
    comparison['artifact'] = {'id': artifact['id'], 'name': artifact['name'], 'files': evidence['extractedFileCount'],
        'bytes': artifact['size_in_bytes'], 'sha256': artifact['digest'].removeprefix('sha256:'),
        'fileCountSource': 'Extracted archived files, counted independently in the repair workflow'}
    result['initialFailurePreserved']['comparisonArtifact'] = {'id': failed_artifact['id'],
        'bytes': failed_artifact['size_in_bytes'], 'sha256': failed_artifact['digest'].removeprefix('sha256:')}
    result['evidenceRepair'] = {'rawHostsReconciled': True, 'manuallyTranscribedMediansReplaced': True,
        'historicalPlatformSnapshotRetained': True,
        'note': 'Only the numerical/source-artifact record is regenerated. The historical platform snapshot remains historical; current validation is recorded separately.'}
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--checkpoint', type=Path)
    parser.add_argument('--artifact-metadata', type=Path)
    args = parser.parse_args()
    if args.output.resolve().is_relative_to(args.input.resolve()):
        raise ValueError('Generated output must not contaminate the evidence inputs')
    report = derive(args.input)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'comparison-evidence.json').write_text(json.dumps(report, indent=2) + '\n')
    (args.output / 'comparison-evidence.md').write_text(markdown(report))
    if args.checkpoint:
        metadata = read(args.artifact_metadata)
        corrected = repair_checkpoint(read(args.checkpoint), report, metadata['comparison'], metadata['initialFailure'])
        (args.output / args.checkpoint.name).write_text(json.dumps(corrected, indent=2) + '\n')
    print('UNO_GENERATED_COMPARISON_EVIDENCE=' + json.dumps({key: report[key] for key in
        ('sourceRevisions', 'apiReaderTrees', 'comparisons', 'extractedFileCount', 'collectorSummaryReconciled')}))
    return 0


if __name__ == '__main__': raise SystemExit(main())
