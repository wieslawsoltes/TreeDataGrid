#!/usr/bin/env python3
"""Run every hyphen-named browser regression module; reject empty/partial discovery.

unittest discovery ignores filenames that are not Python identifiers, even when
its glob matches them. Load this repository's test-uno-browser*.py modules by file
location instead. A successful process that executed zero tests is not acceptance.
"""
from __future__ import annotations

import importlib.util
from pathlib import Path
import sys
import unittest


def collect(directory: Path) -> unittest.TestSuite:
    paths = sorted(directory.glob('test-uno-browser*.py'))
    if not paths:
        raise RuntimeError('No browser regression modules found.')
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()
    names: set[str] = set()
    for path in paths:
        name = path.stem.replace('-', '_')
        if name in names:
            raise RuntimeError(f'Colliding browser regression module: {path.name}')
        names.add(name)
        spec = importlib.util.spec_from_file_location(name, path)
        if spec is None or spec.loader is None:
            raise RuntimeError(f'Cannot load browser regression module: {path}')
        module = importlib.util.module_from_spec(spec)
        sys.modules[name] = module
        spec.loader.exec_module(module)
        tests = loader.loadTestsFromModule(module)
        count = tests.countTestCases()
        if not count:
            raise RuntimeError(f'Browser regression module contains no tests: {path.name}')
        print(f'UNO_BROWSER_TEST_MODULE={path.name}; cases={count}', flush=True)
        suite.addTests(tests)
    if loader.errors:
        raise RuntimeError('\n'.join(loader.errors))
    return suite


def main() -> int:
    suite = collect(Path(__file__).resolve().parent)
    expected = suite.countTestCases()
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    complete = result.testsRun == expected and not result.skipped
    print(f'UNO_BROWSER_DRIVER_TESTS: expected={expected}; executed={result.testsRun}; skipped={len(result.skipped)}', flush=True)
    return 0 if expected > 0 and complete and result.wasSuccessful() else 1


if __name__ == '__main__':
    raise SystemExit(main())
