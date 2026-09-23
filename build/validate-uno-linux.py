#!/usr/bin/env python3
"""Validate committed Uno/Core/Avalonia sources on Linux, preserving every result."""
from __future__ import annotations
import json
import os
from pathlib import Path
import signal
import subprocess


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    os.chdir(root)
    logs = Path('artifacts/logs')
    logs.mkdir(parents=True, exist_ok=True)
    outcomes: dict[str, int | str] = {}

    def run(name: str, command: list[str], prerequisite: str | None = None, timeout: float = 240) -> None:
        if prerequisite is not None and outcomes.get(prerequisite) != 0:
            outcomes[name] = 'not-run: prerequisite failed'
            return
        print('START ' + name, flush=True)
        with (logs / (name + '.log')).open('w') as log:
            process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                outcomes[name] = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
                outcomes[name] = 124
        print(f'RESULT {name}={outcomes[name]}', flush=True)
        lines = (logs / (name + '.log')).read_text(errors='replace').splitlines()
        print('\n'.join(lines[-30:]), flush=True)
        Path('artifacts/validation-outcomes.json').write_text(json.dumps(outcomes, indent=2) + '\n')

    for name, project in [('core', 'tests/TreeDataGrid.Core.Tests'), ('uno', 'tests/TreeDataGrid.Uno.Tests'),
                          ('avalonia', 'tests/Avalonia.Controls.TreeDataGrid.Tests'), ('sample-state', 'samples/TreeDataGridUnoSample.Tests'),
                          ('contract-parity', 'tests/TreeDataGrid.Parity.Tests')]:
        extra = ['-p:TreeDataGridUnoTargetFrameworks=net10.0'] if name in ('uno', 'contract-parity') else []
        run(name, ['dotnet', 'test', project, '-c', 'Release', '--logger', 'trx', '--results-directory', f'artifacts/{name}-tests', *extra])
    for name, project in [('native', 'TreeDataGridUnoSample'), ('activity', 'TreeDataGridUnoActivityMonitor')]:
        path = f'samples/{project}/{project}.csproj'
        run(name, ['dotnet', 'build', path, '-c', 'Release', '-f', 'net10.0-desktop'])
        extra = ['--demo'] if name == 'activity' else []
        run(name + '-smoke', ['xvfb-run', '-a', '-s', '-screen 0 1280x800x24', 'dotnet', 'run',
            '--project', path, '--no-build', '-c', 'Release', '-f', 'net10.0-desktop', '--', '--smoke', *extra], prerequisite=name)
    run('isolated-suites', ['python3', 'build/run-uno-native-suites.py', '--jobs', '3'], prerequisite='native', timeout=360)
    summary_file = Path('artifacts/uno-native-suites/summary.json')
    if summary_file.exists():
        summary = json.loads(summary_file.read_text())
        for result in summary['results']:
            if not result['passed']:
                text = (summary_file.parent / result['log']).read_text(errors='replace')
                print('NATIVE_FAILURE ' + result['suite'] + '\n' + '\n'.join(text.splitlines()[:24]), flush=True)
        print(f"NATIVE_SUITE_TOTAL={summary['passed']}/{summary['registered']}", flush=True)
    run('avalonia-api-build', ['dotnet', 'build', 'src/TreeDataGrid.Avalonia/TreeDataGrid.Avalonia.csproj', '-c', 'Release'])
    os.environ['TREEDATAGRID_API_BASELINE_REFERENCES'] = str(root / 'tests/Avalonia.Controls.TreeDataGrid.Tests/bin/Release/net8.0')
    os.environ['TREEDATAGRID_API_TARGET_REFERENCES'] = str(root / 'samples/TreeDataGridUnoSample/bin/Release/net10.0-desktop')
    core = 'src/TreeDataGrid.Core/bin/Release/net8.0/TreeDataGrid.Core.dll'
    target = 'src/TreeDataGrid.Controls.Uno/bin/Release/net10.0-desktop/TreeDataGrid.Controls.Uno.dll'
    baseline = 'src/TreeDataGrid.Avalonia/bin/Release/net8.0/TreeDataGrid.Avalonia.dll'
    run('api-audit', ['dotnet', 'run', '--project', 'tools/TreeDataGrid.ApiAudit', '-c', 'Release', '--',
        baseline, target, core, 'artifacts/api-audit'], prerequisite='avalonia-api-build')
    os.environ['TREEDATAGRID_API_BASELINE_REFERENCES'] = os.environ['TREEDATAGRID_API_TARGET_REFERENCES']
    run('api-self-check', ['dotnet', 'run', '--project', 'tools/TreeDataGrid.ApiAudit', '--no-build', '-c', 'Release', '--',
        target, target, core, 'artifacts/api-self-check', '--strict'], prerequisite='api-audit')
    run('parity-review-tests', ['python3', 'build/test-uno-parity-audit.py'])
    run('parity-review', ['python3', 'build/audit-uno-parity.py'], prerequisite='api-audit')
    Path('artifacts/validation-outcomes.json').write_text(json.dumps(outcomes, indent=2) + '\n')
    print('UNO_VALIDATION_OUTCOMES=' + json.dumps(outcomes), flush=True)
    return 0 if len(outcomes) == 15 and all(value == 0 for value in outcomes.values()) else 1


if __name__ == '__main__':
    raise SystemExit(main())
