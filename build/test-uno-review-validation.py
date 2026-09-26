import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('review_validation', Path(__file__).with_name('validate-uno-review.py'))
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)


class ReviewEvidenceTests(unittest.TestCase):
    def test_trx_requires_complete_executed_records(self):
        valid = '<TestRun><Results><UnitTestResult executionId="one" outcome="Passed" /></Results><ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary></TestRun>'
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'result.trx'
            path.write_text(valid)
            self.assertEqual(1, review.verify_trx(path))
            for malformed in (valid.replace('executed="1"', 'executed="0"'), valid.replace('failed="0"', 'failed="1"'),
                              valid.replace('notExecuted="0"', 'notExecuted="1"'), valid.replace('executionId="one"', ''),
                              valid.replace('outcome="Passed"', 'outcome="Failed"'), '<TestRun />'):
                with self.subTest(malformed=malformed):
                    path.write_text(malformed)
                    with self.assertRaises(ValueError): review.verify_trx(path)

    def test_native_requires_exact_registration_set_and_real_markers(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            document = {'registered': 1, 'passed': 1, 'results': [{'suite': 'sample', 'exitCode': 0, 'passed': True, 'log': 'run.log'}]}
            (path / 'summary.json').write_text(json.dumps(document))
            (path / 'run.log').write_text('UNO_SUITE_PASSED: sample;')
            self.assertEqual(1, len(review.verify_native(path, ['sample'])))
            with self.assertRaises(ValueError): review.verify_native(path, ['sample', 'missing'])
            with self.assertRaises(ValueError): review.verify_native(path, ['sample', 'sample'])
            (path / 'run.log').write_text('UNO_SUITE_PASSED: different;')
            with self.assertRaises(ValueError): review.verify_native(path, ['sample'])
            (path / 'run.log').write_text('UNO_SUITE_PASSED: sample;')
            for changes in ({'log': '../outside.log'}, {'exitCode': False}, {'passed': 1}):
                bad = copy.deepcopy(document); bad['results'][0].update(changes)
                (path / 'summary.json').write_text(json.dumps(bad))
                with self.assertRaises(ValueError): review.verify_native(path, ['sample'])

    def test_self_audit_requires_declared_and_supplemental_evidence(self):
        valid = {'schemaVersion': 7, 'unresolvedBaselineTypes': [], 'unresolvedTargetTypes': [],
                 'baselineShapes': 1, 'targetShapes': 1, 'exactNormalizedMatches': 1,
                 'missingOrDifferent': 0, 'additionalOrDifferent': 0,
                 'supplementalMetadata': {'BaselineEntries': 2, 'TargetEntries': 2, 'ExactMatches': 2,
                                           'MissingOrDifferent': 0, 'AdditionalOrDifferent': 0}}
        review.verify_self_audit(valid)
        for changes in ({'schemaVersion': 6}, {'unresolvedBaselineTypes': None}, {'unresolvedTargetTypes': ['missing']},
                        {'missingOrDifferent': 1}, {'baselineShapes': 2}):
            with self.subTest(changes=changes):
                with self.assertRaises(ValueError): review.verify_self_audit({**valid, **changes})
        bad = copy.deepcopy(valid); bad['supplementalMetadata']['AdditionalOrDifferent'] = 1
        with self.assertRaises(ValueError): review.verify_self_audit(bad)


if __name__ == '__main__':
    unittest.main()
