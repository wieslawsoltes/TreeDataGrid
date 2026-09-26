#!/usr/bin/env python3
"""Run every registered Uno native suite in a fresh X11 process; fail closed."""
from __future__ import annotations
import argparse
import concurrent.futures
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import time


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path('artifacts/uno-native-suites'))
    parser.add_argument('--jobs', type=int, default=3)
    parser.add_argument('--timeout', type=float, default=90)
    parser.add_argument('--suite', action='append')
    args = parser.parse_args()
    if args.jobs < 1 or args.timeout <= 0:
        parser.error('jobs and timeout must be positive')
    root = Path(__file__).resolve().parent.parent
    source = root / 'samples/TreeDataGridUnoSample/App.Validation.cs'
    names = re.findall(r'case "([a-z-]+)":', source.read_text())
    if not names or len(names) != len(set(names)):
        raise RuntimeError('Native suite registration is empty or duplicated')
    if args.suite:
        unknown = set(args.suite) - set(names)
        if unknown:
            parser.error(f'Unknown suites: {sorted(unknown)}')
        names = [name for name in names if name in args.suite]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    assembly = root / 'samples/TreeDataGridUnoSample/bin/Release/net10.0-desktop/TreeDataGridUnoSample.dll'
    if not assembly.is_file():
        raise FileNotFoundError(f'Build the Release desktop sample first: {assembly}')

    def run(name: str) -> dict:
        destination = output / name
        destination.mkdir(exist_ok=True)
        log_path = destination / 'runtime.log'
        command = ['xvfb-run', '-a', '-s', '-screen 0 1280x800x24', 'dotnet', str(assembly),
                   '--smoke', '--suite', name, '--screenshot-dir', str(destination)]
        started = time.monotonic()
        with log_path.open('w') as log:
            process = subprocess.Popen(command, cwd=root, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                code = process.wait(timeout=args.timeout)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
                code = 124
        text = log_path.read_text(errors='replace')
        marker = f'UNO_SUITE_PASSED: {name};'
        passed = code == 0 and marker in text
        result = {'suite': name, 'exitCode': code, 'passed': passed,
                  'elapsedSeconds': round(time.monotonic() - started, 3),
                  'log': str(log_path.relative_to(output))}
        print(json.dumps(result), flush=True)
        if not passed:
            print('\n'.join(text.splitlines()[-20:]), flush=True)
        return result

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.jobs) as executor:
        results = list(executor.map(run, names))
    report = {'registered': len(names), 'passed': sum(item['passed'] for item in results),
              'results': results,
              'timingNote': 'Elapsed durations are test execution diagnostics, not a paired framework performance benchmark.'}
    (output / 'summary.json').write_text(json.dumps(report, indent=2) + '\n')
    print('UNO_NATIVE_SUITE_SUMMARY=' + json.dumps(report), flush=True)
    return 0 if all(item['passed'] for item in results) else 1


if __name__ == '__main__':
    raise SystemExit(main())
