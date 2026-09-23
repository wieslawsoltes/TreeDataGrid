import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('parity_audit', Path(__file__).with_name('audit-uno-parity.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def entry(identity, shape=None, assembly='View'):
    owner = identity if identity.startswith('T:') else 'T:' + identity[2:].rsplit('.', 1)[0]
    return {'Assembly': assembly, 'Kind': 'NamedType' if identity.startswith('T:') else 'Method',
            'Raw': shape or identity, 'Normalized': shape or identity, 'Identity': identity,
            'DeclaringType': owner, 'MetadataName': identity.rsplit('.', 1)[-1]}


def run(left, right, missing=None, summary=None):
    l = {item['Normalized'] for item in left}
    r = {item['Normalized'] for item in right}
    differences = missing if missing is not None else [
        {'Baseline': item, 'Category': 'member-not-declared-on-matched-type', 'Candidates': []}
        for item in left if item['Normalized'] not in r]
    counts = summary or {'baselineShapes': len(l), 'targetShapes': len(r), 'exactNormalizedMatches': len(l & r),
                         'missingOrDifferent': len(l - r), 'additionalOrDifferent': len(r - l)}
    surface = lambda entries: {'Inputs': [], 'Entries': entries, 'UnresolvedTypes': []}
    return audit.review(surface(left), surface(right), differences, counts)


class ReviewTests(unittest.TestCase):
    def test_exact_shapes_are_accounted_without_claiming_behavior(self):
        item = entry('T:UI.Controls.Widget')
        result = run([item], [item])
        self.assertTrue(result['allBaselineShapesAccountedFor'])
        self.assertEqual('exact-declared-shape', result['records'][0]['category'])
        self.assertFalse(result['completeBehavioralParityProven'])

    def test_core_name_candidate_does_not_remove_a_difference(self):
        old = entry('T:UI.Controls.Models.TreeDataGrid.IRow`1')
        core = entry('T:TreeDataGridCore.Models.IRow`1', assembly='TreeDataGrid.Core')
        result = run([old, core], [core])
        item = next(item for item in result['records'] if item['baseline'] == old)
        self.assertEqual('shared-core-candidate-review', item['category'])
        self.assertEqual([core], item['coreOwnerCandidates'])
        self.assertFalse(item['equivalenceAccepted'])
        self.assertIsNotNone(item['rawDifference'])
        self.assertEqual(1, result['rawCounts']['missingOrDifferent'])

    def test_core_candidate_preserves_generic_arity(self):
        result = run([entry('T:UI.Controls.IRow`2')], [entry('T:TreeDataGridCore.IRow`1', assembly='TreeDataGrid.Core')])
        self.assertEqual('missing-exported-owner-review', result['records'][0]['category'])

    def test_member_of_core_candidate_is_preserved(self):
        owner = entry('T:UI.Controls.Model')
        member = entry('M:UI.Controls.Model.Get')
        result = run([owner, member], [entry('T:TreeDataGridCore.Model', assembly='TreeDataGrid.Core')])
        self.assertEqual(2, sum(item['category'] == 'shared-core-candidate-review' for item in result['records']))
        self.assertTrue(result['rawDifferencesPreserved'])

    def test_generated_exports_remain_visible(self):
        result = run([entry('T:CompiledAvaloniaXaml.Loader')], [])
        self.assertEqual('framework-generated-export-review', result['records'][0]['category'])
        self.assertEqual(1, result['rawCounts']['missingOrDifferent'])

    def test_missing_classification_fails_closed(self):
        with self.assertRaisesRegex(ValueError, 'lost or invented'):
            run([entry('T:UI.Missing')], [], missing=[])

    def test_duplicate_classification_fails_closed(self):
        item = entry('T:UI.Missing')
        diff = {'Baseline': item, 'Category': 'missing', 'Candidates': []}
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            run([item], [], missing=[diff, diff])

    def test_invented_candidate_fails_closed(self):
        item = entry('T:UI.Missing')
        with self.assertRaisesRegex(ValueError, 'candidate'):
            run([item], [], missing=[{'Baseline': item, 'Category': 'missing', 'Candidates': [entry('T:Fake')]}])

    def test_summary_must_match_full_inputs(self):
        with self.assertRaisesRegex(ValueError, 'summary'):
            run([entry('T:UI.Missing')], [], summary={'baselineShapes': 99})

    def test_target_additions_are_not_lost(self):
        extra = entry('T:UI.New')
        result = run([], [extra])
        self.assertEqual([extra], result['additionalTargetDeclarations'])
        self.assertEqual(1, result['rawCounts']['additionalOrDifferent'])

    def test_existing_owner_missing_member_stays_actionable(self):
        owner = entry('T:UI.Controls.Grid')
        member = entry('M:UI.Controls.Grid.Edit')
        result = run([owner, member], [owner])
        self.assertEqual(1, result['reviewCategories']['member-not-declared-on-matched-type'])

    def test_unresolved_metadata_is_rejected(self):
        surface = {'Inputs': [], 'Entries': [], 'UnresolvedTypes': ['Missing.Dependency']}
        with self.assertRaisesRegex(ValueError, 'dependencies'):
            audit.review(surface, surface, [], {})


if __name__ == '__main__':
    unittest.main()
