import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('record', Path(__file__).with_name('check-uno-evidence-record.py'))
record = importlib.util.module_from_spec(spec); spec.loader.exec_module(record)


class EvidenceRecordTests(unittest.TestCase):
    def setUp(self):
        self.derived = {'collectorSummaryReconciled': True, 'sourceRevisions': {'baseline': 'old', 'candidate': 'new'},
            'apiReaderTrees': {'baseline': 'reader', 'candidate': 'reader'}, 'extractedFileCount': 51,
            'comparisons': [{'operation': 'live-int-raw',
                'MillisecondsPerOperation': {'median': {'baseline': .0001, 'candidate': .00005}, 'perPassMedians': {'candidate': [.00004, .00006]}},
                'AllocatedBytesPerOperation': {'median': {'baseline': 72, 'candidate': 40}}}]}
        self.metadata = {'comparison': {'id': 1, 'size_in_bytes': 2, 'digest': 'sha256:aaa'},
                         'initialFailure': {'id': 3, 'size_in_bytes': 4, 'digest': 'sha256:bbb'}}
        self.checkpoint = {'testedCommit': 'new', 'controlledComparison': {'baseline': 'old', 'apiReaderSourceTreeBothInputs': 'reader',
            'medians': [{'operation': 'live-int-raw', 'baselineNs': 100, 'candidateNs': 50, 'baselineBytes': 72, 'candidateBytes': 40}],
            'liveIntegerCandidatePassMediansNs': [40, 60], 'artifact': {'id': 1, 'bytes': 2, 'sha256': 'aaa', 'files': 51}},
            'initialFailurePreserved': {'comparisonArtifact': {'id': 3, 'bytes': 4, 'sha256': 'bbb'}}}

    def test_correct_record_passes(self):
        record.verify(self.checkpoint, self.derived, self.metadata)

    def test_wrong_allocation_is_not_a_rounding_difference(self):
        self.checkpoint['controlledComparison']['medians'][0]['baselineBytes'] = 80
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)

    def test_wrong_timing_is_rejected(self):
        self.checkpoint['controlledComparison']['medians'][0]['candidateNs'] = 51
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)

    def test_wrong_pass_medians_are_rejected(self):
        self.checkpoint['controlledComparison']['liveIntegerCandidatePassMediansNs'] = [59, 41]
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)

    def test_wrong_reader_tree_is_rejected(self):
        self.checkpoint['controlledComparison']['apiReaderSourceTreeBothInputs'] = 'wrong'
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)

    def test_wrong_extracted_file_count_is_rejected(self):
        self.checkpoint['controlledComparison']['artifact']['files'] = 43
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)

    def test_wrong_failure_artifact_is_rejected(self):
        self.checkpoint['initialFailurePreserved']['comparisonArtifact']['id'] = 99
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)

    def test_missing_workload_is_rejected(self):
        self.checkpoint['controlledComparison']['medians'] = []
        with self.assertRaises(ValueError): record.verify(self.checkpoint, self.derived, self.metadata)


if __name__ == '__main__': unittest.main()
