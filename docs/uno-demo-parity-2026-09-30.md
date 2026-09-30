# Uno demo parity, visual comparison and performance (2026-09-30)

This round compared the Avalonia and Uno samples side by side on macOS (desktop) and in
the browser (WebAssembly), fixed the differences it found, and profiled the paired
performance workload. It does not claim complete API or performance parity.

## Same demo on both frameworks

The Uno sample's default launch now opens `Demo/MainWindow`, the counterpart of
`samples/TreeDataGridDemo/MainWindow.axaml`: the same eight tabs (Template Column Reuse,
People, Countries, Find Displayed Row, BringIntoView, Files, Wikipedia, Drag/Drop), the same
view models ported to Core sources and Uno columns, and the same shared models. The previous
single-page showcase remains the validation harness for `--smoke`, `--suite` and `--harness`.

| Avalonia construct | Uno adaptation |
| --- | --- |
| `TabControl` | `TabView` (stretched, non-closable items) |
| `DockPanel` | `Grid` rows/columns |
| `#name.IsChecked` bindings | `ElementName` bindings with a visibility converter |
| `x:Static` | `CountryRegions` resource object |
| `MultiBinding` file icon | `IsDirectory` visibility plus `IsExpanded` folder converter |
| `TreeDataGridRow:nth-child(2n)` style | `AlternatingRowBackground` (new) |
| `:nth-last-child(1)` bold cell/header styles | `CellPrepared`/`CellClearing` attached helper |

`TDG_START_TAB=<index>` opens a tab and `TDG_TOUR=1` runs an identical scripted walkthrough in
both apps (every tab, sort, filter, row and cell selection, template and text editing, collapse,
folder expansion, flat files, find/bring-into-view). Each step prints `TOUR_STEP <name>`; the
windows were captured with `screencapture` and compared side by side. The browser build was
also driven with real pointer input: header sorting, click and Shift-click selection,
double-click editing and row drag-and-drop all work.

## Defects found and fixed

| Defect | Fix |
| --- | --- |
| Hierarchical or variable-height grids with star columns hung the UI thread | Grid and presenter now use the same viewport fallback before the scroll viewer is arranged; measure repetition is capped |
| Selected rows never showed their highlight; generic and expander cells ignored selection/current/validation/editing states | Visual state groups moved to the template roots |
| Avalonia-style `new FlatTreeDataGridSource<T>(...) { Columns = { new TextColumn<...>(...) } }` did not compile | `ColumnList` `Add`/`Insert` extensions and `Uno.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn<TModel>` |
| No filtering on Core sources | `Filter`, `RefreshFilter`, `IsFiltered` on shared flat and hierarchical sources |
| Wikipedia thumbnails never appeared in the demo | Image re-subscribes after the model's decode completes |
| People hierarchy empty in the trimmed browser build | Uno generates binding accessors only for public `[Bindable]` types; shared models made public, internal view models preserved with `DynamicDependency` |
| Browser build failed (`Application.Exit`) | Guarded for WebAssembly |

The API gate accepts exactly the six reviewed Core filtering declarations as reference
additions; removals, changed records and any other addition still fail.

## Performance changes

Two presentation changes remove framework work rather than TreeDataGrid bookkeeping:

- **Source resets (sorting) keep rows visible until the next layout rebinds them.**
  Collapsing and re-showing every visible row made the Skia compositor compute damage
  regions for each change.
- **Cell text is laid out once per value.** Uno's `TextBlock` re-creates its layout during
  arrange whenever the arranged size differs from its measure constraint, which vertical
  centering always caused. `TreeDataGridCellContentPanel` stretches `PART_Text` and places it
  at the centering offset. Uno rounds an arrange rectangle's position before adding the
  margin, so the panel passes the position already rounded the way native centering rounds
  it; the 16-state native pixel comparison passes on both 1x Linux and 2x macOS.

Paired workload, Uno/Avalonia median ratios on the Linux CI runner (2 pairs; final
revision `952bc9da`):

| Operation | Previous gate (`0cc1ca9b`) time / alloc | Now time / alloc | Now Avalonia / Uno ms |
| --- | ---: | ---: | ---: |
| Visible-row replacement | 1.62 / 2.67 | 0.92 / 1.35 | 1.145 / 1.050 |
| Visible-column resizing | 1.78 / 1.52 | 0.75 / 0.76 | 2.218 / 1.652 |
| Sorting | 1.51 / 0.62 | 1.20 / 0.32 | 19.943 / 23.907 |
| Vertical scrolling | 2.74 / 1.93 | 3.73 / 1.08 | 0.508 / 1.895 |
| Distant diagonal scrolling | 4.23 / 1.92 | 4.80 / 1.03 | 1.326 / 6.368 |
| Horizontal scrolling | 2.61 / 0.70 | 3.44 / 0.70 | 0.208 / 0.716 |

Hosted-runner Avalonia medians also vary between runs (e.g. sorting 42 ms and 20 ms in two
runs of this series), so scrolling ratios are compared across runs with caution.

Local macOS medians (4 pairs, 40 iterations; Avalonia's own medians vary up to 2x between
runs on this machine):

| Operation | Avalonia ms | Uno ms | Ratio | Avalonia B | Uno B | Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Distant diagonal scrolling | 1.489 | 7.429 | 4.99 | 534104 | 566748 | 1.06 |
| Visible-row replacement | 0.996 | 1.505 | 1.51 | 39968 | 58936 | 1.47 |
| Visible-column resizing | 0.672 | 2.079 | 3.10 | 89352 | 72984 | 0.82 |
| Horizontal scrolling | 0.235 | 0.863 | 3.67 | 20896 | 14960 | 0.72 |
| Vertical scrolling | 0.508 | 1.695 | 3.33 | 104128 | 110352 | 1.06 |
| Sorting | 8.250 | 17.189 | 2.08 | 1706152 | 565808 | 0.33 |

At the start of this round the same local Uno medians were 9.59, 2.39, 3.58, 0.82, 2.16 and
39.3 ms respectively.

## Continuation: direct cell text rendering

The per-value `TextBlock` layout was the largest remaining cost, so text cells now draw
their text directly with Skia ([design](uno-direct-text-rendering.md)). `PART_Text` stays in
the templates as the source of the text's appearance and still renders anything outside the
direct path; the new `direct-text` native suite requires identical sizes and pixels against
the text block in 240 states. Related changes:

- `PART_Text.Text` is assigned only while the text block displays the text (assigning it
  requested a frame under the compositor lock); the presenter's automation peer exposes the
  displayed text.
- Shaped strings and their arranged glyph runs are cached per font and shared by cells
  showing the same value.
- Value getters are compiled when a column presentation is created, as Avalonia's
  `ColumnBase` does, instead of on the first realization of each column while scrolling.
- The Uno parity host's text verification no longer produces garbage that was collected
  inside later measured intervals.
- Browser fixes: a font that finished loading after the first rows no longer leaves
  recycled rows blank, and validation checks use bindable sources and the displayed text.

Linux CI, Uno/Avalonia median ratios (2 pairs). Hosted runners differ by up to 2x between
runs (Avalonia's own diagonal median was 2.6 ms in one run and 1.3 ms in the next):

| Operation | Round start (`952bc9da`) | `f2242215` | `43adafed` | `b6c3ba70` | Allocation ratio now |
| --- | ---: | ---: | ---: | ---: | ---: |
| Visible-row replacement | 0.92 | 0.69 | 0.77 | 0.59 | 0.08 |
| Visible-column resizing | 0.75 | 0.63 | 1.35 | 0.74 | 0.07 |
| Sorting | 1.20 | 0.78 | 1.23 | 0.80 | 0.04 |
| Vertical scrolling | 3.73 | 1.29 | 1.62 | 1.78 | 0.14 |
| Distant diagonal scrolling | 4.80 | 1.99 | 2.58 | 2.42 | 0.09 |
| Horizontal scrolling | 3.44 | 2.58 | 2.64 | 2.65 | 0.69 |

Uno's own diagonal median fell from 6.4 ms to 3.4 ms and its vertical median from 1.9 ms
to 1.3 ms.

## Remaining gap

Allocations are now 1.4-25x lower than Avalonia's. Scrolling is still 1.3-2.7x slower, and
the remaining time is Uno framework work rather than TreeDataGrid's:

- A scroll step changes as little layout as it can: a vertical step moves 3 recycled rows
  and a horizontal step 15 recycled cells out of about 1,300 elements.
- `ScrollViewer.ChangeView` alone costs about 0.4 ms per step on macOS (more than
  Avalonia's whole horizontal step): Uno's scroll presenter re-anchors the content visual,
  marks the whole content subtree's matrices dirty and enqueues an arrange under a
  dispatcher lock shared with the render thread.
- Each moved or repainted element releases its recorded Skia picture
  (`Visual.InvalidatePaint`), the largest single cost of the diagonal jump; the text block
  path paid it as well.
- On macOS, Uno's always-enabled accessibility mirror updates native elements for every
  recycled cell; the Linux CI runner has no accessibility tree.

TreeDataGrid's own code (realization, recycling, bindings, text shaping) is a small share
of the remaining scroll samples. [Uno performance findings](uno-performance-findings.md)
lists every issue with TreeDataGrid and Uno references and proposed Uno changes. The API audit's remaining differences mostly stem from the
documented use of the shared Core `IndexPath`/`IRow` types and from Avalonia-only
framework overrides.

Other known differences: `TabView` looks different from Avalonia's `TabControl`; Uno's
default font (Open Sans) differs from the platform font Avalonia uses on macOS; Uno draws
Wikipedia bitmaps at one DIP per pixel.
