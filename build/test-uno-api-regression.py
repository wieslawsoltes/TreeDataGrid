import copy
import importlib.util
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

spec = importlib.util.spec_from_file_location('gate', Path(__file__).with_name('check-uno-api-regression.py'))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


def entry(name):
    return dict(Assembly='Contract', Kind='Method', Raw=name, Normalized=name,
                Identity='M:' + name, DeclaringType='T:Owner', MetadataName=name)


def fixture(path, target=None):
    reference = [entry('A'), entry('B')]
    target = reference if target is None else target
    summary = dict(schemaVersion=7, mode='declared', namespaceMappings={'A': 'UI'},
                   namespaceNormalization='roots', declaredInterfaceOrdering='normalized',
                   scopeAccounting=dict(normalizationCollisions=0, ScopeCountsAreAdditive=True),
                   unresolvedBaselineTypes=[], unresolvedTargetTypes=[])
    left, right = {x['Normalized'] for x in reference}, {x['Normalized'] for x in target}
    summary.update(baselineShapes=len(left), targetShapes=len(right), exactNormalizedMatches=len(left & right),
                   missingOrDifferent=len(left - right), additionalOrDifferent=len(right - left))
    (path / 'summary.json').write_text(json.dumps(summary))
    for name, entries in [('avalonia', reference), ('uno', target)]:
        (path / (name + '.json')).write_text(json.dumps(dict(Inputs=[dict(Name='Contract', Sha256='a' * 64)], Entries=entries, UnresolvedTypes=[])))


class RegressionGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.before = Path(self.temp.name) / 'before'; self.before.mkdir()
        self.after = Path(self.temp.name) / 'after'; self.after.mkdir()
        fixture(self.before); fixture(self.after)

    def result(self):
        return gate.compare(gate.load_audit(self.before), gate.load_audit(self.after))

    def mutate(self, file, change):
        path = self.after / file
        data = json.loads(path.read_text()); change(data); path.write_text(json.dumps(data))

    def test_identical_inventory_passes(self):
        self.assertTrue(self.result()['declaredRegressionGatePassed'])

    def test_removed_match_cannot_hide_behind_an_added_export_and_equal_count(self):
        fixture(self.after, [entry('A'), entry('C')])
        result = self.result()
        self.assertFalse(result['declaredRegressionGatePassed'])
        self.assertEqual(['B'], result['lostReferenceMatches'])
        self.assertEqual(['C'], result['addedTargetShapes'])

    def test_nonreference_export_is_also_protected(self):
        fixture(self.before, [entry('A'), entry('B'), entry('Extra')])
        self.assertFalse(self.result()['declaredRegressionGatePassed'])

    def test_additive_api_is_allowed_without_waiving_existing_gaps(self):
        fixture(self.before, [entry('A')])
        self.assertTrue(self.result()['declaredRegressionGatePassed'])
        self.assertEqual(['B'], self.result()['newReferenceMatches'])

    def test_same_normalized_shape_cannot_hide_raw_or_owner_changes(self):
        for field in ('Raw', 'Identity', 'DeclaringType', 'MetadataName', 'Kind'):
            with self.subTest(field=field):
                fixture(self.after)
                self.mutate('uno.json', lambda x: x['Entries'][0].__setitem__(field, 'changed'))
                self.assertFalse(self.result()['declaredRegressionGatePassed'])

    def test_normalization_changes_require_review(self):
        self.mutate('summary.json', lambda x: x.__setitem__('namespaceMappings', {'A': 'Other'}))
        with self.assertRaises(ValueError): self.result()

    def test_reference_changes_require_review(self):
        self.mutate('avalonia.json', lambda x: x['Entries'][0].__setitem__('Raw', 'changed'))
        with self.assertRaises(ValueError): self.result()

    def test_missing_or_malformed_dependency_lists_fail_closed(self):
        for bad in (None, 0, '', {}, ['Missing.Assembly']):
            with self.subTest(bad=bad):
                fixture(self.after)
                self.mutate('uno.json', lambda x: x.__setitem__('UnresolvedTypes', bad))
                with self.assertRaises(ValueError): self.result()

    def test_summary_dependency_failure_is_not_ignored(self):
        self.mutate('summary.json', lambda x: x.__setitem__('unresolvedTargetTypes', ['Missing']))
        with self.assertRaises(ValueError): self.result()

    def test_duplicate_shapes_are_rejected_even_when_identical(self):
        self.mutate('uno.json', lambda x: x['Entries'].append(copy.deepcopy(x['Entries'][0])))
        with self.assertRaises(ValueError): self.result()

    def test_duplicate_json_keys_are_rejected(self):
        (self.after / 'summary.json').write_text('{"schemaVersion":7,"schemaVersion":7}')
        with self.assertRaises(ValueError): self.result()

    def test_forged_counts_and_boolean_counts_are_rejected(self):
        for bad in (99, True):
            fixture(self.after)
            self.mutate('summary.json', lambda x: x.__setitem__('targetShapes', bad))
            with self.assertRaises(ValueError): self.result()

    def test_normalization_collisions_and_bad_scope_counts_are_rejected(self):
        for key, value in [('normalizationCollisions', 1), ('normalizationCollisions', False), ('ScopeCountsAreAdditive', False)]:
            fixture(self.after)
            self.mutate('summary.json', lambda x: x['scopeAccounting'].__setitem__(key, value))
            with self.assertRaises(ValueError): self.result()

    def test_missing_assembly_provenance_is_rejected(self):
        self.mutate('uno.json', lambda x: x.__setitem__('Inputs', []))
        with self.assertRaises(ValueError): self.result()

    def test_malformed_record_is_rejected(self):
        self.mutate('uno.json', lambda x: x['Entries'][0].pop('DeclaringType'))
        with self.assertRaises(ValueError): self.result()

    def test_hash_changes_alone_do_not_claim_an_api_change(self):
        self.mutate('uno.json', lambda x: x['Inputs'][0].__setitem__('Sha256', 'b' * 64))
        self.assertTrue(self.result()['declaredRegressionGatePassed'])


if __name__ == '__main__': unittest.main()
