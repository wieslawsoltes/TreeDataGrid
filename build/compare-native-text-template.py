#!/usr/bin/env python3
"""Compare exact active-state native revisions without relaxing the existing gate."""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import signal
import statistics
import subprocess
import tempfile

OPERATIONS = ('distant-diagonal-scroll', 'replace-visible-row', 'resize-visible-column', 'scroll-x', 'scroll-y', 'sort')
METRICS = ('SynchronousUiMilliseconds', 'SynchronousUiAllocatedBytes', 'SettledMilliseconds')
BASELINE = 'f1fb840ac12aca8851fd5d55589b80ece72bf6fc'


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', default=BASELINE)
    parser.add_argument('--candidate', default='HEAD')
    parser.add_argument('--output', type=Path, default=Path('artifacts/text-template-native-comparison'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    def git(*values: str) -> str:
        return subprocess.check_output(['git', '-C', str(root), *values], text=True).strip()
    revisions = {label: git('rev-parse', ref + '^{commit}') for label, ref in
                 (('baseline', args.baseline), ('candidate', args.candidate))}
    if revisions['baseline'] == revisions['candidate']: raise ValueError('Distinct revisions are required.')
    unchanged = ('benchmarks/TreeDataGrid.Parity.*', 'build/run-native-parity.py')
    if git('diff', '--name-only', revisions['baseline'], revisions['candidate'], '--', *unchanged):
        raise ValueError('The benchmark or acceptance collector changed between inputs.')
    # The first historical comparison mixed inactive old states with active flat
    # states. Retain that result, but require equivalent active states in this run.
    verification = subprocess.check_output(['python3', str(root / 'build/verify-flat-text-template.py'),
                                            '--baseline', revisions['baseline']], cwd=root, text=True)
    print(verification, end='', flush=True)
    prefix = 'UNO_TEXT_TEMPLATE_INPUT='
    records = [json.loads(line[len(prefix):]) for line in verification.splitlines() if line.startswith(prefix)]
    if len(records) != 1 or records[0].get('baseline') != revisions['baseline'] or records[0].get('bothMeasuredTemplatesHaveRootStates') is not True:
        raise ValueError('No exact active-state input verification was returned.')
    manifest = {'schemaVersion': 2, 'revisions': revisions,
        'order': ['baseline', 'candidate', 'candidate', 'baseline'],
        'runnerBlob': git('rev-parse', revisions['baseline'] + ':build/run-native-parity.py'),
        'inputVerification': records[0],
        'runtimeEnvironmentOverrides': {},
        'independentRatioBudgetUnchanged': 1.1,
        'scope': 'Exact native controls with equivalent active visual states; ABBA revision order and AB/BA framework order within each pass. Historical inactive-state comparison is preserved separately. Not frame-rate/GPU completion, confidence intervals or all-feature acceptance.'}
    (output / 'input.json').write_text(json.dumps(manifest, indent=2) + '\n')
    worktrees = []
    reports = {label: [] for label in revisions}
    reference_configuration = None
    fingerprints = {}
    frame_sequences = {'Uno': None, 'Avalonia': None}
    all_frames_match = True
    try:
        with tempfile.TemporaryDirectory(prefix='tdg-native-template-') as temporary:
            trees = {}
            for label, revision in revisions.items():
                tree = Path(temporary) / label
                git('worktree', 'add', '--detach', str(tree), revision)
                worktrees.append(tree)
                trees[label] = tree
            for ordinal, label in enumerate(manifest['order']):
                tree = trees[label]
                destination = output / f'{ordinal:02d}-{label}'
                source = tree / 'artifacts/native-parity'
                if source.exists(): shutil.rmtree(source)
                command = ['python3', '-u', 'build/run-native-parity.py', '--pairs', '2', '--columns', '64',
                           '--iterations', '25', '--max-ratio', '1.10']
                with (output / f'{ordinal:02d}-{label}.log').open('w') as log:
                    process = subprocess.Popen(command, cwd=tree, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                    try: code = process.wait(timeout=900)
                    except subprocess.TimeoutExpired:
                        os.killpg(process.pid, signal.SIGKILL)
                        process.wait()
                        code = 124
                if source.exists(): shutil.copytree(source, destination)
                (output / f'{ordinal:02d}-{label}-exit.json').write_text(json.dumps({'exit': code}) + '\n')
                outcomes = json.loads((destination / 'outcomes.json').read_text())
                expected_keys = {'build-Avalonia', 'build-Uno', '00-Avalonia', '00-Uno', '01-Avalonia', '01-Uno'}
                if set(outcomes) != expected_keys or any(value != 0 for value in outcomes.values()):
                    raise ValueError(f'{label} build or native execution failed; preserve its original logs.')
                summary = json.loads((destination / 'summary.json').read_text())
                if summary['revision'] != revisions[label] or summary['diagnosticBudget'] != 1.1:
                    raise ValueError('Wrong revision or modified native budget.')
                if code != (0 if summary['diagnosticBudgetMet'] is True else 1):
                    raise ValueError('Exit status does not match the original independent gate outcome.')
                for framework in ('Avalonia', 'Uno'):
                    for pair in (0, 1):
                        report = json.loads((destination / f'{pair:02d}-{framework}.json').read_text())
                        if report['revision'] != revisions[label] or report['framework'] != framework or len(report['measurements']) != 150:
                            raise ValueError('Incomplete or mislabeled native measurements.')
                        configuration = {key: report[key] for key in ('workload', 'runtime', 'architecture', 'os', 'serverGc')}
                        if reference_configuration is None: reference_configuration = configuration
                        if reference_configuration != configuration: raise ValueError('Runtime or workload configuration changed.')
                        values = report['measurements']
                        if sorted(set(value['Operation'] for value in values)) != list(OPERATIONS):
                            raise ValueError('Unexpected operation set.')
                        for operation in OPERATIONS:
                            if sum(value['Operation'] == operation for value in values) != 25:
                                raise ValueError('Incomplete operation samples.')
                        for value in values:
                            if value['Frame']['Error'] is not None: raise ValueError('Native correctness assertion failed.')
                            for metric in METRICS:
                                if not math.isfinite(value[metric]) or value[metric] < 0: raise ValueError('Invalid measurement.')
                        frames = [value['Frame'] for value in values]
                        if frame_sequences[framework] is None: frame_sequences[framework] = frames
                        elif frames != frame_sequences[framework]: all_frames_match = False
                        reports[label].append(report)
                current = {}
                for framework, target in (('Avalonia', 'net10.0'), ('Uno', 'net10.0-desktop')):
                    folder = tree / f'benchmarks/TreeDataGrid.Parity.{framework}/bin/Release/{target}'
                    libraries = sorted(folder.glob('*TreeDataGrid*.dll'))
                    if not libraries: raise ValueError('Missing built native library fingerprint.')
                    for library in libraries:
                        with library.open('rb') as stream:
                            current[f'{framework}/{library.name}'] = hashlib.file_digest(stream, 'sha256').hexdigest()
                if label in fingerprints and fingerprints[label] != current:
                    raise ValueError('Built library bytes changed between measured passes of the same revision.')
                fingerprints[label] = current
                subprocess.run(['git', '-C', str(tree), 'diff', '--exit-code'], check=True)
                subprocess.run(['git', '-C', str(tree), 'diff', '--cached', '--exit-code'], check=True)
                print('UNO_NATIVE_TEMPLATE_PASS=' + json.dumps({'ordinal': ordinal, 'revision': revisions[label],
                    'nativeGatePassed': summary['diagnosticBudgetMet'], 'allProcessesSucceeded': True}), flush=True)
            comparisons = []
            for framework in ('Uno', 'Avalonia'):
                for operation in OPERATIONS:
                    entry = {'framework': framework, 'operation': operation}
                    for metric in METRICS:
                        values = {label: [value[metric] for report in runs if report['framework'] == framework
                                         for value in report['measurements'] if value['Operation'] == operation]
                                  for label, runs in reports.items()}
                        medians = {label: statistics.median(data) for label, data in values.items()}
                        entry[metric] = {'samplesPerRevision': 100, 'median': medians,
                            'candidateOverBaseline': medians['candidate'] / medians['baseline'] if medians['baseline'] else None,
                            'p95': {label: sorted(data)[math.ceil(.95 * len(data)) - 1] for label, data in values.items()},
                            'perHostMedians': {label: [statistics.median(value[metric] for value in report['measurements']
                                if value['Operation'] == operation) for report in runs if report['framework'] == framework]
                                for label, runs in reports.items()}}
                    comparisons.append(entry)
            result = {**manifest, 'environment': reference_configuration, 'libraryHashes': fingerprints,
                'hostsCompleted': 16, 'orderedFramesIdentical': all_frames_match,
                'comparisons': comparisons, 'collectionCompleted': True, 'completePerformanceParityProven': False}
            (output / 'summary.json').write_text(json.dumps(result, indent=2) + '\n')
            print('UNO_NATIVE_TEMPLATE_COMPARISON=' + json.dumps(result), flush=True)
            if not all_frames_match: raise ValueError('Ordered native frame evidence differs; do not claim equivalent work.')
        return 0
    except Exception as error:
        (output / 'failure.json').write_text(json.dumps({'type': type(error).__name__, 'message': str(error)}, indent=2) + '\n')
        raise
    finally:
        for tree in reversed(worktrees):
            subprocess.run(['git', '-C', str(root), 'worktree', 'remove', '--force', str(tree)], check=False)
        subprocess.run(['git', '-C', str(root), 'worktree', 'prune'], check=False)


if __name__ == '__main__': raise SystemExit(main())
