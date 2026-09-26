import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('review', Path(__file__).with_name('validate-uno-review.py'))
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)


class ReviewPreservationTests(unittest.TestCase):
    def test_success_requires_both_the_positive_and_negative_controls(self):
        valid = dict(declaredRegressionGatePassed=True, removedOrChangedTargetShapes=[],
                     rewrittenExistingRecords=[], lostReferenceMatches=[])
        negative = dict(declaredRegressionGatePassed=False, lostReferenceMatches=['removed'])
        review.verify_preservation(valid, negative)
        for bad in ({}, {**valid, 'declaredRegressionGatePassed': 1}, {**valid, 'lostReferenceMatches': ['lost']},
                    {**valid, 'removedOrChangedTargetShapes': ['removed']}, {**valid, 'rewrittenExistingRecords': ['changed']}):
            with self.subTest(bad=bad):
                with self.assertRaises(ValueError): review.verify_preservation(bad, negative)
        for bad in ({}, {**negative, 'declaredRegressionGatePassed': True}, {**negative, 'lostReferenceMatches': []}):
            with self.subTest(bad=bad):
                with self.assertRaises(ValueError): review.verify_preservation(valid, bad)


if __name__ == '__main__': unittest.main()
