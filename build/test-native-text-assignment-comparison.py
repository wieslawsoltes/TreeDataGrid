"""Negative controls for the exact-source native text publication experiment."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('native_text_assignment_comparison',
    Path(__file__).with_name('compare-native-text-assignment.py'))
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class NativeTextAssignmentComparisonTests(unittest.TestCase):
    def test_only_the_expected_runtime_change_is_accepted(self):
        collector.validate_changed_paths([collector.RUNTIME])
        collector.validate_changed_paths(sorted(collector.ALLOWED))

    def test_empty_duplicate_or_unrelated_changes_are_rejected(self):
        for paths in ([], [collector.RUNTIME, collector.RUNTIME],
                      [collector.RUNTIME, collector.TEMPLATE],
                      [collector.RUNTIME, 'src/TreeDataGrid.Core/FlatTreeDataGridSource.cs'],
                      [collector.RUNTIME, 'build/run-native-parity.py']):
            with self.subTest(paths=paths), self.assertRaises(ValueError):
                collector.validate_changed_paths(paths)

    def test_complete_native_report_is_accepted(self):
        collector.validate_host(self.report(), 'a' * 40, 'Uno')

    def test_partial_mislabeled_failed_or_nonfinite_reports_are_rejected(self):
        def wrong_revision(report): report['revision'] = 'b' * 40
        def wrong_framework(report): report['framework'] = 'Avalonia'
        def missing(report): report['measurements'].pop()
        def wrong_work(report): report['measurements'][0]['Operation'] = 'sort'
        def failed(report): report['measurements'][0]['Frame']['Error'] = 'incorrect identity'
        def nonfinite(report): report['measurements'][0]['SettledMilliseconds'] = float('nan')
        def negative(report): report['measurements'][0]['SynchronousUiAllocatedBytes'] = -1
        for mutate in (wrong_revision, wrong_framework, missing, wrong_work, failed, nonfinite, negative):
            with self.subTest(mutation=mutate.__name__):
                report = self.report()
                mutate(report)
                with self.assertRaises(ValueError): collector.validate_host(report, 'a' * 40, 'Uno')

    @staticmethod
    def report():
        return {'revision': 'a' * 40, 'framework': 'Uno', 'measurements': [
            {'Operation': operation, 'Frame': {'Error': None},
             'SynchronousUiMilliseconds': 1.0, 'SynchronousUiAllocatedBytes': 1,
             'SettledMilliseconds': 2.0}
            for operation in collector.OPERATIONS for _ in range(25)]}


if __name__ == '__main__': unittest.main()
