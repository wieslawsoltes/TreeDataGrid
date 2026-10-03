#!/usr/bin/env python3
"""Permanent fail-closed API preservation gate and review negative controls.

Build the pinned pre-fix source with the same SDK and production reader as the
candidate. Overlay only the two new test fixtures for baseline behavioral proof.
No baseline source is patched and no existing cross-framework gap is waived.
"""
from __future__ import annotations
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

BASELINE = '5be0e5638cfd6be70ffe88839b695b790f013235'
FIXTURES = {
    'core': ('tests/TreeDataGrid.Core.Tests/ReviewCleanupTests.cs', 'tests/TreeDataGrid.Core.Tests', 'FullyQualifiedName~ReviewCleanupTests', 22),
    'parity': ('tests/TreeDataGrid.Parity.Tests/FluentComparisonLifetimeParityTests.cs', 'tests/TreeDataGrid.Parity.Tests', 'FullyQualifiedName~FluentComparisonLifetimeParityTests', 12),
}


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    output = root / 'artifacts/review-regressions'
    output.mkdir(parents=True, exist_ok=True)
    results = {'baseline': BASELINE, 'candidate': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(), 'completeApiParityProven': False}

    def run(name, command, cwd, env=None, check=True):
        with (output / (name + '.log')).open('w') as log:
            code = subprocess.run(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=300).returncode
        print('UNO_REVIEW_STAGE=' + json.dumps({'name': name, 'code': code}), flush=True)
        if check and code:
            raise RuntimeError(name + ' failed; see retained log')
        return code

    try:
        run('gate-tests', ['python3', 'build/test-uno-api-regression.py', '-v'], root)
        run('preservation-evidence-tests', ['python3', 'build/test-uno-review-preservation.py', '-v'], root)
        subprocess.run(['git', 'merge-base', '--is-ancestor', BASELINE, 'HEAD'], cwd=root, check=True)
        reader_trees = {name: subprocess.check_output(['git', 'rev-parse', ref + ':tools/TreeDataGrid.ApiAudit'], cwd=root, text=True).strip()
                        for name, ref in (('baseline', BASELINE), ('candidate', 'HEAD'))}
        # Comparing both assemblies with a changed reader could conceal a change
        # in normalization. Reader changes require an explicit baseline review.
        if len(set(reader_trees.values())) != 1:
            raise ValueError('API reader changed; review the reader and preservation baseline explicitly')
        results['unchangedReaderSourceTrees'] = reader_trees
        with tempfile.TemporaryDirectory(prefix='tdg-reviewed-baseline-') as temp:
            worktree = Path(temp) / 'source'
            subprocess.run(['git', 'worktree', 'add', '--detach', str(worktree), BASELINE], cwd=root, check=True)
            try:
                run('reader-build', ['dotnet', 'build', 'tools/TreeDataGrid.ApiAudit', '-c', 'Release'], root)
                reader = root / 'tools/TreeDataGrid.ApiAudit/bin/Release/net10.0/TreeDataGrid.ApiAudit.dll'
                if not reader.exists():
                    candidates = list((root / 'tools/TreeDataGrid.ApiAudit/bin/Release').glob('net*/TreeDataGrid.ApiAudit.dll'))
                    if len(candidates) != 1: raise ValueError('Ambiguous API reader output')
                    reader = candidates[0]
                for label, tree in [('baseline', worktree), ('candidate', root)]:
                    run(label + '-build', ['dotnet', 'build', 'tests/TreeDataGrid.Parity.Tests', '-c', 'Release', '-p:TreeDataGridUnoTargetFrameworks=net10.0'], tree)
                    references = str(tree / 'tests/TreeDataGrid.Parity.Tests/bin/Release/net10.0')
                    env = dict(os.environ, TREEDATAGRID_API_BASELINE_REFERENCES=references, TREEDATAGRID_API_TARGET_REFERENCES=references)
                    run(label + '-audit', ['dotnet', str(reader),
                        str(tree / 'src/TreeDataGrid.Avalonia/bin/Release/net8.0/TreeDataGrid.Avalonia.dll'),
                        str(tree / 'src/TreeDataGrid.Controls.Uno/bin/Release/net10.0/TreeDataGrid.Controls.Uno.dll'),
                        str(tree / 'src/TreeDataGrid.Core/bin/Release/net8.0/TreeDataGrid.Core.dll'), str(output / (label + '-api'))], tree, env)
                run('api-preservation', ['python3', 'build/check-uno-api-regression.py', '--before', str(output / 'baseline-api'),
                    '--after', str(output / 'candidate-api'), '--output', str(output / 'api-preservation.json')], root)
                results['apiPreservation'] = json.loads((output / 'api-preservation.json').read_text())
                # Same-count real-inventory mutation: a new shape cannot hide the
                # disappearance of an old exact reference match.
                negative = output / 'removed-export-control'
                if negative.exists(): shutil.rmtree(negative)
                shutil.copytree(output / 'candidate-api', negative)
                u = json.loads((negative / 'uno.json').read_text()); a = json.loads((negative / 'avalonia.json').read_text())
                left = {entry['Normalized'] for entry in a['Entries']}
                chosen = next(entry for entry in u['Entries'] if entry['Normalized'] in left)
                chosen['Normalized'] += ' REVIEW_REMOVED_EXPORT'
                chosen['Raw'] += ' REVIEW_REMOVED_EXPORT'
                right = {entry['Normalized'] for entry in u['Entries']}
                summary = json.loads((negative / 'summary.json').read_text())
                summary.update(exactNormalizedMatches=len(left & right), missingOrDifferent=len(left - right), additionalOrDifferent=len(right - left))
                (negative / 'uno.json').write_text(json.dumps(u)); (negative / 'summary.json').write_text(json.dumps(summary))
                code = run('removed-export-control', ['python3', 'build/check-uno-api-regression.py', '--before', str(output / 'baseline-api'),
                    '--after', str(negative), '--output', str(output / 'removed-export-control.json')], root, check=False)
                if code != 1: raise ValueError('Removed-export negative control did not fail')
                results['removedExportControlRejected'] = True
                for label, tree in [('baseline', worktree), ('candidate', root)]:
                    results[label + 'Tests'] = {}
                    for group, (fixture, project, test_filter, expected) in FIXTURES.items():
                        path = tree / fixture
                        copied = label == 'baseline'
                        if copied:
                            if path.exists(): raise ValueError('Baseline fixture unexpectedly exists')
                            shutil.copyfile(root / fixture, path)
                        try:
                            destination = output / (label + '-' + group)
                            code = run(label + '-' + group, ['dotnet', 'test', project, '-c', 'Release', '-p:TreeDataGridUnoTargetFrameworks=net10.0',
                                '--filter', test_filter, '--logger', 'trx;LogFileName=result.trx', '--results-directory', str(destination)], tree, check=False)
                            doc = ET.parse(destination / 'result.trx')
                            counters = doc.find('.//{*}Counters')
                            if counters is None: raise ValueError('No executed test counters')
                            counts = {key: int(value) for key, value in counters.attrib.items()}
                            if counts['total'] != expected or counts['executed'] != expected or counts['notExecuted']:
                                raise ValueError('Missing or skipped review regressions')
                            if code != (1 if counts['failed'] else 0): raise ValueError('Test command failed outside assertions')
                            if label == 'candidate' and counts['failed']: raise ValueError('Candidate review tests failed')
                            results[label + 'Tests'][group] = counts
                        finally:
                            if copied: path.unlink()
                if not any(x['failed'] for x in results['baselineTests'].values()):
                    raise ValueError('No baseline defect reproduced')
                for tree in (root, worktree):
                    subprocess.run(['git', 'diff', '--exit-code'], cwd=tree, check=True)
                    subprocess.run(['git', 'diff', '--cached', '--exit-code'], cwd=tree, check=True)
                results['passed'] = True
            finally:
                subprocess.run(['git', 'worktree', 'remove', '--force', str(worktree)], cwd=root, check=True)
    except Exception as error:
        results['passed'] = False
        results['error'] = f'{type(error).__name__}: {error}'
    finally:
        (output / 'summary.json').write_text(json.dumps(results, indent=2) + '\n')
        print('UNO_REVIEW_REGRESSIONS=' + json.dumps(results), flush=True)
    return 0 if results.get('passed') else 1


if __name__ == '__main__': raise SystemExit(main())
