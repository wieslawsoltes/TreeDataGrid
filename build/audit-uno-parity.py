#!/usr/bin/env python3
"""Exhaustive declared-surface accounting, not a behavioral-parity waiver.

Consumes the existing compiled Roslyn inventories without renormalizing or
removing any raw difference. Shared-Core candidates remain unaccepted candidates.
No application assembly is loaded or executed by this review.
"""
from __future__ import annotations
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import re
import subprocess


def review(baseline: dict, target: dict, differences: list[dict], summary: dict) -> dict:
    required = ('Assembly', 'Kind', 'Raw', 'Normalized', 'Identity', 'DeclaringType', 'MetadataName')
    for surface in (baseline, target):
        if not isinstance(surface.get('Entries'), list):
            raise ValueError('Missing compiled entry inventory')
        for entry in surface['Entries']:
            if any(not isinstance(entry.get(key), str) for key in required):
                raise ValueError('Malformed compiled API entry')
        if surface.get('UnresolvedTypes'):
            raise ValueError('Resolve metadata dependencies before classifying parity')
    left = {entry['Normalized']: entry for entry in baseline['Entries']}
    right = {entry['Normalized']: entry for entry in target['Entries']}
    missing = left.keys() - right.keys()
    by_shape = {}
    for difference in differences:
        shape = difference['Baseline']['Normalized']
        if shape in by_shape:
            raise ValueError('Duplicate raw difference: ' + shape)
        if shape not in left or difference['Baseline'] != left[shape]:
            raise ValueError('A classified difference does not match its input inventory')
        if any(candidate['Normalized'] not in right for candidate in difference['Candidates']):
            raise ValueError('A classified candidate is not in the target inventory')
        by_shape[shape] = difference
    if by_shape.keys() != missing:
        raise ValueError('Classification lost or invented a raw difference')
    counts = {'baselineShapes': len(left), 'targetShapes': len(right),
              'exactNormalizedMatches': len(left.keys() & right.keys()),
              'missingOrDifferent': len(missing),
              'additionalOrDifferent': len(right.keys() - left.keys())}
    if any(summary.get(key) != value for key, value in counts.items()):
        raise ValueError('Raw summary does not match its complete inventories')
    core_types = defaultdict(list)
    for entry in target['Entries']:
        if entry['Assembly'] == 'TreeDataGrid.Core' and entry['Identity'].startswith('T:'):
            core_types[entry['MetadataName']].append(entry)
    baseline_types = {entry['Identity']: entry for entry in baseline['Entries'] if entry['Identity'].startswith('T:')}
    target_types = {entry['Identity'] for entry in target['Entries'] if entry['Identity'].startswith('T:')}
    records = []
    for shape in sorted(left):
        entry = left[shape]
        owner = entry['DeclaringType']
        difference = by_shape.get(shape)
        owner_entry = baseline_types.get(owner)
        candidates = core_types.get(owner_entry['MetadataName'], []) if owner_entry else []
        # Same simple name is a search lead only. Keep arity, full identity and
        # signature visible; never auto-map combined legacy models to Core.
        if difference is None:
            category = 'exact-declared-shape'
        elif owner.startswith('T:CompiledAvaloniaXaml.'):
            category = 'framework-generated-export-review'
        elif owner not in target_types and candidates:
            category = 'shared-core-candidate-review'
        elif owner not in target_types:
            category = 'missing-exported-owner-review'
        else:
            category = difference['Category']
        records.append({'baseline': entry, 'category': category,
                        'rawDifference': difference,
                        'coreOwnerCandidates': candidates if difference else [],
                        'equivalenceAccepted': False})
    categories = dict(sorted(Counter(item['category'] for item in records).items()))
    owners = []
    grouped = defaultdict(list)
    for record in records:
        grouped[record['baseline']['DeclaringType']].append(record)
    for owner, entries in sorted(grouped.items()):
        unresolved = [entry for entry in entries if entry['rawDifference'] is not None]
        owners.append({'identity': owner, 'declaredShapes': len(entries),
                       'exactShapes': len(entries) - len(unresolved),
                       'unresolvedShapes': len(unresolved),
                       'categories': dict(sorted(Counter(entry['category'] for entry in unresolved).items()))})
    return {'schemaVersion': 1, 'scope': 'All declared public/protected shapes in the supplied compiled inventories',
            'rawCounts': counts, 'reviewCategories': categories,
            'allBaselineShapesAccountedFor': len(records) == len(left),
            'rawDifferencesPreserved': sum(item['rawDifference'] is not None for item in records) == len(missing),
            'inputs': {'baseline': baseline['Inputs'], 'target': target['Inputs']},
            'owners': owners, 'records': records,
            'additionalTargetDeclarations': [right[shape] for shape in sorted(right.keys() - left.keys())],
            'completeApiParityProven': False, 'completeBehavioralParityProven': False,
            'limitations': ['Same-name Core candidates do not establish signature or behavior equivalence.',
                            'Inherited external framework members and custom attributes are not in the input inventory.',
                            'Matching declared shapes do not prove defaults, event ordering, native input, rendering or performance.']}


def source_inventory(root: Path) -> dict:
    # Audit scope is tracked source, not stale bin/obj or generated build output.
    paths = subprocess.check_output(['git', 'ls-files', '-z', '--', 'src/TreeDataGrid.Core',
        'src/Avalonia.Controls.TreeDataGrid', 'src/TreeDataGrid.Controls.Uno'], cwd=root).decode().split('\0')
    files, boundaries = [], []
    pattern = re.compile(r'\b(?:NotImplementedException|PlatformNotSupportedException|TODO|FIXME)\b')
    for name in sorted(filter(None, paths)):
        path = root / name
        if path.suffix not in ('.cs', '.xaml', '.axaml', '.csproj'):
            continue
        data = path.read_bytes()
        text = data.decode('utf-8-sig')
        files.append({'path': name, 'sha256': hashlib.sha256(data).hexdigest(), 'lines': len(text.splitlines())})
        for number, line in enumerate(text.splitlines(), 1):
            if pattern.search(line):
                boundaries.append({'path': name, 'line': number, 'text': line.strip(),
                                   'status': 'source-review-required-not-automatically-a-defect'})
    return {'files': files, 'explicitBoundaries': boundaries,
            'note': 'Complete tracked-file inventory and textual boundary scan; not a claim of line-by-line semantic verification.'}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, default=Path('artifacts/api-audit'))
    parser.add_argument('--output', type=Path, default=Path('artifacts/parity-review'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    read = lambda name: json.loads((args.input / name).read_text())
    report = review(read('avalonia.json'), read('uno.json'), read('classified-differences.json'), read('summary.json'))
    report['revision'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    report['sourceInventory'] = source_inventory(root)
    report['evidence'] = {}
    for name, relative in [('nativeSuites', 'artifacts/uno-native-suites/summary.json'),
                           ('validationOutcomesAtReview', 'artifacts/validation-outcomes.json')]:
        path = root / relative
        report['evidence'][name] = json.loads(path.read_text()) if path.exists() else None
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'review.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')
    lines = ['# Compiled Uno parity review', '', 'Revision: `' + report['revision'] + '`.', '',
             '**Every raw difference is retained. Candidate mappings are not accepted equivalences.**', '',
             '| Declaring type | Declared | Exact | Requires review |', '| --- | ---: | ---: | ---: |']
    for owner in report['owners']:
        lines.append(f"| `{owner['identity']}` | {owner['declaredShapes']} | {owner['exactShapes']} | {owner['unresolvedShapes']} |")
    lines.extend(['', '## Missing declarations on existing types', ''])
    for record in report['records']:
        if record['category'] == 'member-not-declared-on-matched-type':
            lines.append('- `' + record['baseline']['Normalized'].replace('`', "'") + '`')
    (args.output / 'review.md').write_text('\n'.join(lines) + '\n')
    print('UNO_PARITY_REVIEW=' + json.dumps({key: report[key] for key in ('revision', 'rawCounts', 'reviewCategories',
        'allBaselineShapesAccountedFor', 'rawDifferencesPreserved', 'completeApiParityProven')}))
    print('UNO_PARITY_MISSING_MEMBERS=' + json.dumps([record['baseline']['Normalized'] for record in report['records']
        if record['category'] == 'member-not-declared-on-matched-type']))
    print('UNO_PARITY_MISSING_OWNERS=' + json.dumps([owner for owner in report['owners']
        if 'missing-exported-owner-review' in owner['categories']]))
    print('UNO_PARITY_SOURCE_BOUNDARIES=' + json.dumps(report['sourceInventory']['explicitBoundaries']))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
