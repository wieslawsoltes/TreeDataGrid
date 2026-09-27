"""Exercise the actual hyphenated-module runner, including fail-closed verdicts."""
import importlib.util
import io
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('browser_test_runner', Path(__file__).with_name('run-uno-browser-tests.py'))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class BrowserDiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.directory = TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def write_test(self, filename='test-uno-browser-probe-a.py', body='self.assertTrue(True)'):
        (self.root / filename).write_text(
            'import unittest\nclass Probe(unittest.TestCase):\n    def test_probe(self):\n        ' + body + '\n')

    def test_hyphenated_modules_are_executed_not_silently_ignored(self):
        self.write_test()
        self.write_test('test-uno-browser-probe-b.py')
        suite = runner.collect(self.root)
        self.assertEqual(2, suite.countTestCases())
        result = unittest.TextTestRunner(stream=io.StringIO()).run(suite)
        self.assertEqual(2, result.testsRun)
        self.assertTrue(result.wasSuccessful())

    def test_missing_modules_fail_closed(self):
        with self.assertRaisesRegex(RuntimeError, 'No browser regression modules'):
            runner.collect(self.root)

    def test_empty_module_cannot_hide_beside_a_valid_module(self):
        self.write_test()
        (self.root / 'test-uno-browser-empty.py').write_text('value = 1\n')
        with self.assertRaisesRegex(RuntimeError, 'contains no tests'):
            runner.collect(self.root)

    def test_colliding_normalized_names_are_rejected(self):
        self.write_test()
        self.write_test('test-uno-browser-probe_a.py')
        with self.assertRaisesRegex(RuntimeError, 'Colliding'):
            runner.collect(self.root)

    def test_import_errors_are_not_treated_as_an_empty_success(self):
        (self.root / 'test-uno-browser-invalid.py').write_text('raise RuntimeError("broken module")\n')
        with self.assertRaisesRegex(RuntimeError, 'broken module'):
            runner.collect(self.root)

    def test_real_runner_rejects_failures_and_skips(self):
        for body, expected in (
            ('self.assertTrue(True)', 0),
            ('self.fail("deliberate failure")', 1),
            ('self.skipTest("deliberate skip")', 1),
        ):
            with self.subTest(body=body):
                self.write_test(body=body)
                suite = runner.collect(self.root)
                output = unittest.TextTestRunner(stream=io.StringIO())
                with patch.object(runner, 'collect', return_value=suite), \
                     patch.object(runner.unittest, 'TextTestRunner', return_value=output):
                    self.assertEqual(expected, runner.main())


if __name__ == '__main__': unittest.main()
