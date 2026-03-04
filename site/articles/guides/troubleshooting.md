---
title: "Troubleshooting Guide"
---

# Troubleshooting Guide

Use this guide when TreeDataGrid behaves unexpectedly in runtime, interaction, or styling.

## Troubleshooting

Use the sections below to isolate common setup, data, interaction, and
performance issues quickly.

## Quick Triage Checklist

1. Confirm package and theme setup from [Installation](../getting-started/installation.md).
2. Confirm `TreeDataGrid.Source` is not null.
3. Confirm source `Items` and `Columns` are populated.
4. Confirm selection model points at the same `Items` source.
5. Reproduce with a minimal quickstart sample.

## Control Renders But Looks Unstyled

Likely cause:

- missing TreeDataGrid theme include

Fix:

- add `avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml` or `Generic.axaml`
- ensure include is loaded before window containing `TreeDataGrid`

## Rows Do Not Appear

Likely causes:

- `Source` not assigned/bound
- empty `Columns` collection
- invalid expander column setup for hierarchical source

Fix:

- bind `Source` and verify DataContext creation path
- ensure at least one column exists
- for hierarchical sources, include exactly one [`HierarchicalExpanderColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn`1)

## Selection Behaves Incorrectly

Likely causes:

- custom selection model not wired to same `Items`
- mixing row-selection and cell-selection APIs
- relying on visible row index instead of `IndexPath`

Fix:

- ensure `Selection.Source == Items`
- use only row APIs when row model is active; cell APIs when cell model is active
- persist model addresses (`IndexPath`, `CellIndex`)

## Drag/Drop Is Disabled Unexpectedly

Likely causes:

- source currently sorted
- source does not support required move semantics
- drop position invalid (`Inside` on flat source)

Fix:

- clear sort before automatic row move workflows
- verify source supports mutable list operations for children/items
- validate target/drop position rules

## Expansion Problems in Hierarchical Mode

Likely causes:

- invalid child selector returning null/unstable collections
- trying to resolve row by visible index after expansion state changed

Fix:

- use stable child collections
- convert between model and visible indexes via `IRows` conversion methods
- use `TryGetModelAt(IndexPath, out TModel)` for model lookup

## Editing Does Not Start or Commit

Likely causes:

- `CanEdit` false for cell model
- `BeginEditGestures` excludes actual user gesture
- focus leaves editing template unexpectedly

Fix:

- verify cell model is editable
- set `BeginEditGestures` appropriately
- validate template focus path and commit/cancel handling

## High CPU/Memory While Scrolling

Likely causes:

- heavy cell templates
- custom element factory recycle-key mismatch
- custom visuals retaining per-row state after reuse

Fix:

- simplify templates and reduce heavy bindings/converters
- align factory create/recycle key mapping
- reset state in realize/unrealize lifecycle

## Helpful Validation Commands

```bash
dotnet build Avalonia.Controls.TreeDataGrid.slnx -c Release
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj -c Release
./check-docs.sh
```

## Troubleshooting Patterns Not Covered Above

- Issue category is not listed
Cause: this guide focuses on the most common setup/runtime issues.
Fix: follow the quick triage checklist and then inspect feature-specific guides.

- Fix did not resolve behavior
Cause: multiple interacting issues (theme, source wiring, selection mode, sorting state).
Fix: validate using the snippets page and minimal source setup.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.ITreeDataGridSource>
- <xref:Avalonia.Controls.IndexPath>

## Related

- [Validation Snippets](validation-snippets.md)
- [Diagnostics and Testing](../advanced/diagnostics-and-testing.md)
- [Getting Started Overview](../getting-started/overview.md)
- [TreeDataGrid Glossary](../concepts/glossary.md)
