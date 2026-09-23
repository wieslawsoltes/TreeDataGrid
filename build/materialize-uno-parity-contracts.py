#!/usr/bin/env python3
"""Explicit maintainer operation: materialize reviewed MIT contract ports.

Original inputs and generated C# are committed together. Ordinary builds do not
run this tool or require network access. External C# inputs are hash-pinned;
no downloaded executable or script is run.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import time
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
INPUTS = {
    'ISelectionModel.cs': '4c2a355bb5258bbdfecec7161f274ac6a1cd1f0b',
    'ReadOnlySelectionListBase.cs': 'f603fd81686440d75352250d0bf4c9e3baeee182',
    'SelectedIndexes.cs': 'b742d91e2182a4fcf9b3d0165102a615cb6e54e7',
    'SelectedItems.cs': 'a892876b295545afaac46214751688db2c0b141f',
    'SelectionModel.cs': 'd191f79801b1858c76b0d81623fc739894999435',
    'SelectionModelIndexesChangedEventArgs.cs': '621fba48e8ce757a0dcdc3d27ef23fa842dd7669',
    'SelectionModelSelectionChangedEventArgs.cs': '8f6d2568471f676cffc37db91d24adeb935f4f78',
    'SelectionNodeBase.cs': 'c50f77830f1db5dd2663afe0ffc91d26baa539ce',
}
HEADER = '// Adapted from Avalonia 12.0.0 (MIT). Copyright (c) .NET Foundation and Contributors.\n// See build/uno-parity-inputs/upstream and docs/uno-contract-materialization.json.\n'
outputs: list[str] = []
check = False

def put(path: str, text: str) -> None:
    data = text.encode('utf-8')
    target = ROOT / path
    if check:
        if not target.exists() or target.read_bytes() != data:
            raise ValueError('Generated source differs: ' + path)
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    outputs.append(path)

def replace_once(text: str, old: str, new: str) -> str:
    if text.count(old) != 1:
        raise ValueError('Expected one transformation anchor: ' + old[:100])
    return text.replace(old, new, 1)

def method(text: str, signature: str) -> str:
    start = text.index(signature)
    begin = text.index('{', start)
    depth, end = 1, begin + 1
    # These specific reviewed methods have balanced braces in strings/comments.
    while depth:
        if end >= len(text):
            raise ValueError('Unterminated method: ' + signature)
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]

def upstream(name: str, sha: str) -> str:
    relative = 'build/uno-parity-inputs/upstream/' + name + '.txt'
    path = ROOT / relative
    if path.exists():
        data = path.read_bytes()
    else:
        if check:
            raise ValueError('Missing pinned input: ' + relative)
        url = 'https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.0/src/Avalonia.Controls/Selection/' + name
        for attempt in range(3):
            try:
                request = urllib.request.Request(url, headers={'User-Agent': 'TreeDataGrid-contract-materializer'})
                with urllib.request.urlopen(request, timeout=30) as response:
                    data = response.read(1024 * 1024)
                break
            except OSError:
                if attempt == 2:
                    raise
                time.sleep(attempt + 1)
    actual = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    if actual != sha:
        raise ValueError(f'Pinned upstream input mismatch: {name}: {actual}')
    if not check:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    outputs.append(relative)
    return data.decode('utf-8-sig')

def convert(text: str) -> str:
    text = text.replace('Avalonia.Controls.Selection', 'TreeDataGridCore.Selection')
    text = text.replace('using Avalonia.Collections;\n', '')
    text = re.sub(r'\bItemsSourceView\b', 'TreeDataGridItemsSourceView', text)
    for old, new in [('SelectedIndexes', 'FlatSelectedIndexes'), ('SelectedItems', 'FlatSelectedItems'),
                     ('ReadOnlySelectionListBase', 'FlatReadOnlySelectionListBase')]:
        # Rename helper types/constructors, not the public properties/event fields.
        if old == 'ReadOnlySelectionListBase':
            text = re.sub(r'\b' + old + r'\b', new, text)
        else:
            text = re.sub(r'\b' + old + r'(?=<)', new, text)
            text = re.sub(r'\b(public|protected) ' + old + r'\(', r'\1 ' + new + '(', text)
    return text

def main() -> None:
    global check
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    check = parser.parse_args().check
    originals = {name: upstream(name, sha) for name, sha in INPUTS.items()}
    base = originals['SelectionNodeBase.cs']
    apply = method(base, 'protected virtual void OnSourceCollectionChanged(NotifyCollectionChangedEventArgs e)')
    apply = apply.replace('protected virtual void OnSourceCollectionChanged', 'private void ApplyFlatCollectionChange')
    apply = apply.replace('List<T>? removed', 'List<T?>? removed')
    validator = method(base, 'private protected virtual bool IsValidCollectionChange(NotifyCollectionChangedEventArgs e)')
    validator = validator.replace('private protected virtual bool IsValidCollectionChange', 'private bool IsValidRangeCollectionChange')
    for name, original in originals.items():
        if name == 'SelectionNodeBase.cs':
            continue  # Reuse existing Core ranges, source view and listener.
        text = convert(original)
        if name == 'ReadOnlySelectionListBase.cs':
            text = text.replace('EventArgsCache.ResetCollectionChanged', 's_reset')
            text = replace_once(text, '    public abstract T? this[int index]',
                '    private static readonly NotifyCollectionChangedEventArgs s_reset = new(NotifyCollectionChangedAction.Reset);\n    public abstract T? this[int index]')
        if name == 'SelectionModel.cs':
            text = text.replace('protected override CollectionChangeState OnItemsAdded', 'private protected override CollectionChangeState OnItemsAdded')
            text = text.replace('private protected override bool IsValidCollectionChange', 'private protected virtual bool IsValidCollectionChange')
            text = text.replace('base.IsValidCollectionChange(e)', 'IsValidRangeCollectionChange(e)')
            text = text.replace('base.OnSourceCollectionChanged(e);', 'ApplyFlatCollectionChange(e);')
            text = text.replace('IReadOnlyList<T> deselectedItems', 'IReadOnlyList<T?> deselectedItems')
            text = text.replace('List<T>? removed', 'List<T?>? removed')
            text = text.replace('new List<T> {', 'new List<T?> {')
            text = text.replace('IReadOnlyList<T>? DeselectedItems', 'IReadOnlyList<T?>? DeselectedItems')
            text = replace_once(text, '        public record struct BatchUpdateOperation',
                '        // Preserve the pinned flat contract; tree move behavior is unchanged.\n'
                '        ' + apply + '\n\n        ' + validator + '\n\n        public record struct BatchUpdateOperation')
        destination = {'SelectionModel.cs': 'FlatSelectionModel.cs',
                       'ReadOnlySelectionListBase.cs': 'FlatReadOnlySelectionListBase.cs',
                       'SelectedItems.cs': 'FlatSelectedItems.cs',
                       'SelectedIndexes.cs': 'FlatSelectedIndexes.cs'}.get(name, name)
        put('src/TreeDataGrid.Core/Selection/' + destination, HEADER + text)
    base_path = 'src/TreeDataGrid.Core/Selection/SelectionNodeBase.cs'
    core_base = (ROOT / base_path).read_text(encoding='utf-8-sig')
    for operation in ('OnItemsAdded', 'OnItemsRemoved'):
        old = 'private protected CollectionChangeState ' + operation
        new = 'private protected virtual CollectionChangeState ' + operation
        if old in core_base:
            core_base = replace_once(core_base, old, new)
        elif core_base.count(new) != 1:
            raise ValueError('Unexpected Core collection hook: ' + operation)
    put(base_path, core_base)
    for name in ('ITreeDataGridColumnSelectionModel.cs', 'TreeDataGridColumnSelectionModel.cs'):
        original = (ROOT / 'src/Avalonia.Controls.TreeDataGrid/Selection' / name).read_text(encoding='utf-8-sig')
        put('src/TreeDataGrid.Controls.Uno/Selection/' + name,
            HEADER + 'using TreeDataGridCore.Selection;\n' + original.replace('Avalonia.Controls', 'Uno.Controls'))
    visitor = (ROOT / 'src/Avalonia.Controls.TreeDataGrid/Experimental/Data/Core/Parsers/ExpressionChainVisitor.cs').read_text(encoding='utf-8-sig')
    put('src/TreeDataGrid.Controls.Uno/Experimental/Data/Core/Parsers/ExpressionChainVisitor.cs',
        HEADER + visitor.replace('namespace Avalonia.Data.Core.Parsers', 'namespace Uno.Data.Core.Parsers'))
    observable = (ROOT / 'src/Avalonia.Controls.TreeDataGrid/Experimental/Data/Core/LightweightObservableBase.cs').read_text(encoding='utf-8-sig')
    observable = observable.replace('namespace Avalonia.Experimental.Data.Core', 'namespace Uno.Experimental.Data.Core')
    observable = observable.replace('lock (this)', 'lock (_gate)').replace('return Disposable.Empty;', 'return EmptySubscription.Instance;')
    observable = replace_once(observable, '        private Exception? _error;',
        '        private readonly object _gate = new();\n        private sealed class EmptySubscription : IDisposable\n        {\n            internal static readonly EmptySubscription Instance = new();\n            public void Dispose() { }\n        }\n        private Exception? _error;')
    put('src/TreeDataGrid.Controls.Uno/Experimental/Data/Core/LightweightObservableBase.cs', HEADER + observable)
    for name in ('FlatSelectionParityTests.cs', 'ObservableContractParityTests.cs'):
        put('tests/TreeDataGrid.Parity.Tests/' + name,
            (ROOT / 'build/uno-parity-inputs' / (name + '.txt')).read_text(encoding='utf-8'))
    manifest = {'schemaVersion': 1, 'upstreamRelease': 'Avalonia 12.0.0', 'upstreamGitBlobs': INPUTS,
        'normalBuildRequiresNetwork': False, 'sharedCoreRangeEngine': True,
        'treeSelectionAlgorithmBodiesChanged': False, 'completePortParityProven': False,
        'notes': ['Flat selection preserves the pinned reference collection semantics, including its move policy.',
                  'The two Core collection hooks became virtual; their bodies and tree algorithms are unchanged.',
                  'Native column selection exposes the Core flat selection interface and event types.',
                  'Visitor and observable are native extension APIs, not replacements for existing cell bindings.'],
        'outputs': sorted(set(outputs))}
    put('docs/uno-contract-materialization.json', json.dumps(manifest, indent=2) + '\n')
    (ROOT / 'artifacts').mkdir(exist_ok=True)
    (ROOT / 'artifacts/uno-contract-outputs.json').write_text(json.dumps(sorted(set(outputs)), indent=2) + '\n')
    print('UNO_CONTRACT_MATERIALIZATION=' + json.dumps({'outputs': len(set(outputs)), 'check': check}))

if __name__ == '__main__':
    main()
