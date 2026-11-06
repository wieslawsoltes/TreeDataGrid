import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('render', Path(__file__).with_name('render-uno-comparison-evidence.py'))
render = importlib.util.module_from_spec(spec); spec.loader.exec_module(render)


class EvidenceRenderTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        revisions = {'baseline': 'old', 'candidate': 'new'}
        self.write('input.json', {'processOrder': list(render.ORDER), 'revisions': revisions, 'apiReaderTrees': {'baseline': 'reader', 'candidate': 'reader'}})
        summary = {'collectionSucceeded': True, 'revisions': revisions, 'librarySha256': {'baseline': 'a', 'candidate': 'b'}, 'comparisons': []}
        for index, label in enumerate(render.ORDER):
            value = 2 if label == 'baseline' else 1
            self.write(f'{index:02d}-{label}.json', {'revision': revisions[label], 'librarySha256': summary['librarySha256'][label],
                'sampleCount': 2, 'count': 2, 'samples': [{'Operation': 'control', 'Iteration': i, 'Count': 2, 'Checksum': 9,
                    'Milliseconds': value * 2, 'AllocatedBytes': 144} for i in range(2)]})
        summary['comparisons'] = [{'operation': 'control',
            'MillisecondsPerOperation': {'samplesPerRevision': 4, 'median': {'baseline': 2.0, 'candidate': 1.0}, 'candidateOverBaseline': .5,
                'p95': {'baseline': 2.0, 'candidate': 1.0}, 'perPassMedians': {'baseline': [2.0, 2.0], 'candidate': [1.0, 1.0]}},
            'AllocatedBytesPerOperation': {'samplesPerRevision': 4, 'median': {'baseline': 72.0, 'candidate': 72.0}, 'candidateOverBaseline': 1.0,
                'p95': {'baseline': 72.0, 'candidate': 72.0}, 'perPassMedians': {'baseline': [72.0, 72.0], 'candidate': [72.0, 72.0]}}}]
        self.write('summary.json', summary)

    def write(self, name, data): (self.root / name).write_text(json.dumps(data))
    def mutate(self, name, action):
        data = render.read(self.root / name); action(data); self.write(name, data)

    def test_all_hosts_recompute_summary_and_formatted_allocation(self):
        result = render.derive(self.root)
        self.assertEqual(6, result['extractedFileCount'])
        self.assertIn('| 72 | 72 |', render.markdown(result))
        self.assertEqual(4, len(result['rawHostSha256']))

    def test_false_summary_is_rejected_not_reprinted(self):
        self.mutate('summary.json', lambda x: x['comparisons'][0]['AllocatedBytesPerOperation']['median'].__setitem__('candidate', 80))
        with self.assertRaises(ValueError): render.derive(self.root)

    def test_changed_per_pass_median_is_rejected(self):
        self.mutate('summary.json', lambda x: x['comparisons'][0]['MillisecondsPerOperation']['perPassMedians'].__setitem__('candidate', [10, 1]))
        with self.assertRaises(ValueError): render.derive(self.root)

    def test_missing_or_duplicate_iteration_is_rejected(self):
        self.mutate('01-candidate.json', lambda x: x['samples'][1].__setitem__('Iteration', 0))
        with self.assertRaises(ValueError): render.derive(self.root)

    def test_wrong_revision_is_rejected(self):
        self.mutate('01-candidate.json', lambda x: x.__setitem__('revision', 'wrong'))
        with self.assertRaises(ValueError): render.derive(self.root)

    def test_wrong_fingerprint_is_rejected(self):
        self.mutate('02-candidate.json', lambda x: x.__setitem__('librarySha256', 'wrong'))
        with self.assertRaises(ValueError): render.derive(self.root)

    def test_incomplete_collection_is_rejected(self):
        self.mutate('summary.json', lambda x: x.__setitem__('collectionSucceeded', False))
        with self.assertRaises(ValueError): render.derive(self.root)

    def test_changed_executed_work_is_rejected(self):
        self.mutate('01-candidate.json', lambda x: x['samples'][0].__setitem__('Checksum', 10))
        with self.assertRaises(ValueError): render.derive(self.root)


if __name__ == '__main__': unittest.main()
