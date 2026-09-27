"""Reject incomplete or cross-revision browser matrices and altered consumer bundles."""
import copy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from uno_browser_artifacts import CASES, ENGINES, MANIFEST, load, verify_bundle, verify_matrix, write_manifest
from uno_browser_input import STAGES


class BrowserArtifactTests(unittest.TestCase):
    def setUp(self):
        self.directory = TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.bundle = self.root / 'bundle'
        for sample in ('showcase', 'monitor'):
            directory = self.bundle / sample / 'wwwroot'
            directory.mkdir(parents=True)
            (directory / 'index.html').write_text('<canvas></canvas>')
            (directory / 'sample.wasm').write_bytes(b'\0asm\x01\0\0\0')
        self.revision = 'a' * 40
        write_manifest(self.bundle, self.revision)
        self.identity = verify_bundle(self.bundle, self.revision)
        self.reports = self.root / 'reports'
        for engine in ENGINES:
            directory = self.reports / engine
            directory.mkdir(parents=True)
            (directory / 'consumer-identity.json').write_text(json.dumps(self.identity))
            cases = []
            for name in CASES:
                result = {'name': name, 'passed': True, 'failures': [], 'canvasCount': 1,
                    'crossOriginIsolated': True, 'result': {'complete': True, 'passed': True}}
                if name.startswith('showcase-input'):
                    result.update(deviceScaleFactor=int(name[-1]), inputSteps=list(STAGES))
                cases.append(result)
            (directory / 'summary.json').write_text(json.dumps({'browserEngine': engine, 'revision': self.revision,
                'passed': True, 'browserVersion': 'fixture', 'results': cases}))

    def test_identical_complete_matrix_passes(self):
        self.assertEqual(12, verify_matrix(self.reports, self.identity)['cases'])

    def test_modified_bundle_file_is_rejected(self):
        (self.bundle / 'showcase/wwwroot/sample.wasm').write_bytes(b'changed')
        with self.assertRaises(ValueError): verify_bundle(self.bundle, self.revision)

    def test_added_and_removed_files_are_rejected(self):
        added = self.bundle / 'extra.js'
        added.write_text('extra')
        with self.assertRaises(ValueError): verify_bundle(self.bundle, self.revision)
        added.unlink()
        (self.bundle / 'monitor/wwwroot/sample.wasm').unlink()
        with self.assertRaises(ValueError): verify_bundle(self.bundle, self.revision)

    def test_wrong_revision_is_rejected(self):
        with self.assertRaises(ValueError): verify_bundle(self.bundle, 'b' * 40)

    def test_duplicate_manifest_fields_are_rejected(self):
        (self.bundle / MANIFEST).write_text('{"revision":"a","revision":"b"}')
        with self.assertRaises(ValueError): verify_bundle(self.bundle, self.revision)

    def test_missing_engine_is_not_a_successful_matrix(self):
        (self.reports / 'webkit/summary.json').unlink()
        with self.assertRaises(FileNotFoundError): verify_matrix(self.reports, self.identity)

    def test_different_bundle_is_rejected_even_with_successful_browser_cases(self):
        wrong = dict(self.identity, manifestSha256='b' * 64)
        (self.reports / 'firefox/consumer-identity.json').write_text(json.dumps(wrong))
        with self.assertRaises(ValueError): verify_matrix(self.reports, self.identity)

    def test_incomplete_or_forged_case_evidence_is_rejected(self):
        path = self.reports / 'webkit/summary.json'
        original = load(path)
        def missing_case(summary): summary['results'].pop()
        def wrong_engine(summary): summary['browserEngine'] = 'chromium'
        def wrong_revision(summary): summary['revision'] = 'b' * 40
        def missing_input(summary): summary['results'][2]['inputSteps'].pop()
        def wrong_scale(summary): summary['results'][3]['deviceScaleFactor'] = 1
        def incomplete_app(summary): summary['results'][0]['result']['complete'] = False
        def hidden_failure(summary): summary['results'][0]['failures'] = ['asset failure']
        def hidden_canvas(summary): summary['results'][0]['canvasCount'] = 0
        def hidden_isolation(summary): summary['results'][0]['crossOriginIsolated'] = False
        for mutation in (missing_case, wrong_engine, wrong_revision, missing_input, wrong_scale,
                         incomplete_app, hidden_failure, hidden_canvas, hidden_isolation):
            with self.subTest(mutation=mutation.__name__):
                summary = copy.deepcopy(original)
                mutation(summary)
                path.write_text(json.dumps(summary))
                with self.assertRaises(ValueError): verify_matrix(self.reports, self.identity)


if __name__ == '__main__': unittest.main()
