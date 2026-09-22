#!/usr/bin/env python3
"""Run matched native UI workloads on one machine, alternating process order."""
from __future__ import annotations
import argparse
import json
import math
import os
from pathlib import Path
import platform
import signal
import statistics
import subprocess
import sys


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pairs', type=int, default=2)
    parser.add_argument('--columns', type=int, default=64)
    parser.add_argument('--iterations', type=int, default=25)
    parser.add_argument('--max-ratio', type=float, default=None,
                        help='Optional hard diagnostic budget for both median synchronous time and allocation ratios.')
    args = parser.parse_args()
    if not 1 <= args.pairs <= 20 or not 8 <= args.columns <= 1000 or not 5 <= args.iterations <= 500:
        parser.error('Invalid pair, column or iteration count.')
    if args.max_ratio is not None and (not math.isfinite(args.max_ratio) or args.max_ratio <= 0):
        parser.error('--max-ratio must be positive and finite.')
    root = Path(__file__).resolve().parent.parent
    os.chdir(root)
    output = root / 'artifacts/native-parity'
    output.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env.update(PARITY_COLUMNS=str(args.columns), PARITY_ITERATIONS=str(args.iterations),
               PARITY_REVISION=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
               DOTNET_NOLOGO='true', DOTNET_CLI_TELEMETRY_OPTOUT='true')
    projects = {
        'Avalonia': ('benchmarks/TreeDataGrid.Parity.Avalonia/TreeDataGrid.Parity.Avalonia.csproj', 'net10.0'),
        'Uno': ('benchmarks/TreeDataGrid.Parity.Uno/TreeDataGrid.Parity.Uno.csproj', 'net10.0-desktop'),
    }
    outcomes = {}
    reports = []

    def execute(name: str, command: list[str], timeout: int = 300) -> bool:
        path = output / (name + '.log')
        with path.open('w') as log:
            child = subprocess.Popen(command, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try: code = child.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
                code = 124
        outcomes[name] = code
        (output / 'outcomes.json').write_text(json.dumps(outcomes, indent=2))
        print(f'{name}: exit={code}', flush=True)
        if code: print('\n'.join(path.read_text(errors='replace').splitlines()[-30:]), flush=True)
        return code == 0

    for framework, (project, target) in projects.items():
        execute('build-' + framework, ['dotnet', 'build', project, '-c', 'Release', '-f', target])
    if any(outcomes.values()): return 1
    font = subprocess.check_output(['fc-match', '-f', '%{family}\n', 'DejaVu Sans'], text=True).strip()
    if font != 'DejaVu Sans': raise RuntimeError('The benchmark requires the exact DejaVu Sans font, found ' + font)
    for pair in range(args.pairs):
        order = ['Avalonia', 'Uno'] if pair % 2 == 0 else ['Uno', 'Avalonia']
        pair_reports = []
        for framework in order:
            name = f'{pair:02d}-{framework}'
            report_file = output / (name + '.json')
            report_file.unlink(missing_ok=True)
            env['PARITY_OUTPUT'] = str(report_file)
            project, target = projects[framework]
            if not execute(name, ['xvfb-run', '-a', '-s', '-screen 0 1280x800x24', 'dotnet', 'run',
                                 '--project', project, '-c', 'Release', '-f', target, '--no-build']):
                return 1
            report = json.loads(report_file.read_text())
            if report['framework'] != framework or len(report['measurements']) != 6 * args.iterations:
                raise RuntimeError('Missing/wrong benchmark measurements: ' + name)
            for value in report['measurements']:
                if value['Frame']['Error'] is not None: raise RuntimeError('Invalid geometry/correctness sample.')
                for metric in ['SynchronousUiMilliseconds', 'SynchronousUiAllocatedBytes', 'SettledMilliseconds']:
                    if not math.isfinite(value[metric]) or value[metric] < 0: raise RuntimeError('Invalid metric: ' + metric)
            pair_reports.append(report)
            reports.append(report)
        for key in ['workload', 'revision', 'runtime', 'architecture', 'os', 'serverGc']:
            if pair_reports[0][key] != pair_reports[1][key]: raise RuntimeError('Unmatched host configuration: ' + key)
    comparisons = []
    operations = sorted({sample['Operation'] for report in reports for sample in report['measurements']})
    for operation in operations:
        item = {'operation': operation}
        for metric in ['SynchronousUiMilliseconds', 'SynchronousUiAllocatedBytes', 'SettledMilliseconds']:
            values = {framework: [sample[metric] for report in reports if report['framework'] == framework
                                  for sample in report['measurements'] if sample['Operation'] == operation]
                      for framework in projects}
            medians = {framework: statistics.median(samples) for framework, samples in values.items()}
            ratio = medians['Uno'] / medians['Avalonia'] if medians['Avalonia'] else None
            item[metric] = {'median': medians, 'unoOverAvalonia': ratio,
                           'p95': {framework: sorted(samples)[math.ceil(.95 * len(samples)) - 1] for framework, samples in values.items()}}
        comparisons.append(item)
    budget = args.max_ratio
    accepted = budget is None or all(
        item[metric]['unoOverAvalonia'] is not None and item[metric]['unoOverAvalonia'] <= budget
        for item in comparisons for metric in ['SynchronousUiMilliseconds', 'SynchronousUiAllocatedBytes'])
    summary = {'schemaVersion': 1, 'machine': platform.platform(), 'processor': platform.processor(),
               'revision': env['PARITY_REVISION'], 'pairs': args.pairs, 'order': 'AB/BA alternating', 'font': font,
               'scope': reports[0]['scope'], 'completePerformanceParityProven': False,
               'diagnosticBudget': budget, 'diagnosticBudgetMet': accepted if budget is not None else None,
               'comparisons': comparisons}
    (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('NATIVE_PARITY_SUMMARY=' + json.dumps(summary), flush=True)
    return 0 if accepted else 1


if __name__ == '__main__':
    sys.exit(main())
