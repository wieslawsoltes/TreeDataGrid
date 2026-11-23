#!/usr/bin/env python3
"""Exact-source native text-publication experiment; does not waive the native gate."""
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

BASELINE = 'fc4cd6bc3625ec98347056f2e0786d967e88e305'
RUNTIME = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCell.Render.cs'
TEMPLATE = 'src/TreeDataGrid.Controls.Uno/Themes/Generic.xaml'
OPERATIONS = ('distant-diagonal-scroll', 'replace-visible-row', 'resize-visible-column', 'scroll-x', 'scroll-y', 'sort')
METRICS = ('SynchronousUiMilliseconds', 'SynchronousUiAllocatedBytes', 'SettledMilliseconds')
ALLOWED = {RUNTIME, 'samples/TreeDataGridUnoSample/NativeTextAssignmentRuntimeChecks.cs',
    'samples/TreeDataGridUnoSample/TextTemplateParityView.xaml.cs',
    'build/compare-native-text-assignment.py', 'build/test-native-text-assignment-comparison.py',
    '.github/workflows/uno-text-assignment-comparison.yml'}


def validate_changed_paths(paths: list[str]) -> None:
    if RUNTIME not in paths or len(paths) != len(set(paths)) or set(paths) - ALLOWED:
        raise ValueError('The candidate must change only the reviewed renderer and its own tests/experiment tooling.')


def validate_host(report: dict, revision: str, framework: str) -> None:
    if report['revision'] != revision or report['framework'] != framework:
        raise ValueError('Wrong native source revision or framework.')
    values = report['measurements']
    if len(values) != 150 or sorted({value['Operation'] for value in values}) != list(OPERATIONS):
        raise ValueError('Incomplete native operation set.')
    for operation in OPERATIONS:
        if sum(value['Operation'] == operation for value in values) != 25:
            raise ValueError('Incomplete operation samples.')
    for value in values:
        if value['Frame']['Error'] is not None: raise ValueError('Native correctness assertion failed.')
        for metric in METRICS:
            if not math.isfinite(value[metric]) or value[metric] < 0:
                raise ValueError('Invalid native measurement.')


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline', default=BASELINE)
    parser.add_argument('--candidate', default='HEAD')
    parser.add_argument('--output', type=Path, default=Path('artifacts/native-text-assignment'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    def git(*values: str) -> str:
        return subprocess.check_output(['git', '-C', str(root), *values], text=True).strip()
    revisions = {label: git('rev-parse', ref + '^{commit}') for label, ref in
                 (('baseline', args.baseline), ('candidate', args.candidate))}
    if revisions['baseline'] != BASELINE or revisions['candidate'] == BASELINE:
        raise ValueError('Use the pinned active-state, geometry-correct baseline and a distinct candidate.')
    changed = git('diff', '--name-only', revisions['baseline'], revisions['candidate']).splitlines()
    validate_changed_paths(changed)
    template_blob = git('rev-parse', BASELINE + ':' + TEMPLATE)
    if template_blob != git('rev-parse', revisions['candidate'] + ':' + TEMPLATE):
        raise ValueError('Measured native templates differ.')
    runner_blob = git('rev-parse', BASELINE + ':build/run-native-parity.py')
    if runner_blob != git('rev-parse', revisions['candidate'] + ':build/run-native-parity.py'):
        raise ValueError('The measured runner changed.')
    manifest = {'schemaVersion': 1, 'revisions': revisions,
        'order': ['baseline', 'candidate', 'candidate', 'baseline'], 'changedPaths': changed,
        'templateBlob': template_blob, 'runnerBlob': runner_blob,
        'runtimeSourceBefore': git('rev-parse', BASELINE + ':' + RUNTIME),
        'runtimeSourceAfter': git('rev-parse', revisions['candidate'] + ':' + RUNTIME),
        'collectorSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'runtimeEnvironmentOverrides': {}, 'independentRatioBudgetUnchanged': 1.1,
        'scope': 'One renderer-source change, identical active-state native templates and benchmark inputs, ABBA revision order with alternating AB/BA framework order within each pass. Synchronous UI/layout and settlement, not GPU completion or frame rate. Pooled diagnostics are not confidence intervals or full causal attribution.'}
    (output / 'input.json').write_text(json.dumps(manifest, indent=2) + '\n')
    worktrees: list[Path] = []
    reports: dict[str, list[dict]] = {label: [] for label in revisions}
    environment = None
    fingerprints = {}
    frame_sequences = {'Uno': None, 'Avalonia': None}
    frames_match = True
    gates = []
    try:
        with tempfile.TemporaryDirectory(prefix='tdg-native-text-') as temporary:
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
                keys = {'build-Avalonia', 'build-Uno', '00-Avalonia', '00-Uno', '01-Avalonia', '01-Uno'}
                if set(outcomes) != keys or any(value != 0 for value in outcomes.values()):
                    raise ValueError('Native build/execution failed; original outcomes and logs are retained.')
                summary = json.loads((destination / 'summary.json').read_text())
                if summary['revision'] != revisions[label] or summary['diagnosticBudget'] != 1.1:
                    raise ValueError('Native gate revision or threshold changed.')
                if type(summary['diagnosticBudgetMet']) is not bool or code != (0 if summary['diagnosticBudgetMet'] else 1):
                    raise ValueError('Execution exit does not match the independent gate verdict.')
                gates.append({'ordinal': ordinal, 'label': label, 'passed': summary['diagnosticBudgetMet']})
                for framework in ('Avalonia', 'Uno'):
                    for pair in (0, 1):
                        report = json.loads((destination / f'{pair:02d}-{framework}.json').read_text())
                        validate_host(report, revisions[label], framework)
                        configuration = {key: report[key] for key in ('workload', 'runtime', 'architecture', 'os', 'serverGc')}
                        if environment is None: environment = configuration
                        if environment != configuration: raise ValueError('Runtime or workload configuration differs.')
                        frames = [value['Frame'] for value in report['measurements']]
                        if frame_sequences[framework] is None: frame_sequences[framework] = frames
                        elif frame_sequences[framework] != frames: frames_match = False
                        reports[label].append(report)
                current = {}
                for framework, target in (('Avalonia', 'net10.0'), ('Uno', 'net10.0-desktop')):
                    folder = tree / f'benchmarks/TreeDataGrid.Parity.{framework}/bin/Release/{target}'
                    libraries = sorted(folder.glob('*TreeDataGrid*.dll'))
                    if not libraries: raise ValueError('Missing compiled-library fingerprints.')
                    for library in libraries:
                        with library.open('rb') as stream:
                            current[f'{framework}/{library.name}'] = hashlib.file_digest(stream, 'sha256').hexdigest()
                if label in fingerprints and fingerprints[label] != current:
                    raise ValueError('Compiled bytes changed between passes of the same source.')
                fingerprints[label] = current
                subprocess.run(['git', '-C', str(tree), 'diff', '--exit-code'], check=True)
                subprocess.run(['git', '-C', str(tree), 'diff', '--cached', '--exit-code'], check=True)
                print('UNO_NATIVE_ASSIGNMENT_PASS=' + json.dumps({'ordinal': ordinal, 'revision': revisions[label],
                    'nativeGatePassed': summary['diagnosticBudgetMet'], 'allProcessesSucceeded': True}), flush=True)
            comparisons = []
            for framework in ('Uno', 'Avalonia'):
                for operation in OPERATIONS:
                    entry = {'framework': framework, 'operation': operation}
                    for metric in METRICS:
                        values = {label: [value[metric] for report in runs if report['framework'] == framework
                                         for value in report['measurements'] if value['Operation'] == operation]
                                  for label, runs in reports.items()}
                        if any(len(data) != 100 for data in values.values()): raise ValueError('Incomplete pooled sample count.')
                        medians = {label: statistics.median(data) for label, data in values.items()}
                        entry[metric] = {'samplesPerRevision': 100, 'median': medians,
                            'candidateOverBaseline': medians['candidate'] / medians['baseline'] if medians['baseline'] else None,
                            'p95': {label: sorted(data)[math.ceil(.95 * len(data)) - 1] for label, data in values.items()},
                            'perHostMedians': {label: [statistics.median(value[metric] for value in report['measurements']
                                if value['Operation'] == operation) for report in runs if report['framework'] == framework]
                                for label, runs in reports.items()}}
                    comparisons.append(entry)
            result = {**manifest, 'environment': environment, 'libraryHashes': fingerprints, 'nativeGates': gates,
                'hostsCompleted': 16, 'orderedFramesIdentical': frames_match, 'comparisons': comparisons,
                'collectionCompleted': True, 'completePerformanceParityProven': False}
            (output / 'summary.json').write_text(json.dumps(result, indent=2) + '\n')
            print('UNO_NATIVE_ASSIGNMENT_COMPARISON=' + json.dumps(result), flush=True)
            if not frames_match: raise ValueError('Ordered native frame evidence differs; equivalent work is not established.')
        return 0
    except Exception as error:
        (output / 'failure.json').write_text(json.dumps({'type': type(error).__name__, 'message': str(error)}, indent=2) + '\n')
        raise
    finally:
        for tree in reversed(worktrees):
            subprocess.run(['git', '-C', str(root), 'worktree', 'remove', '--force', str(tree)], check=False)
        subprocess.run(['git', '-C', str(root), 'worktree', 'prune'], check=False)


if __name__ == '__main__': raise SystemExit(main())
