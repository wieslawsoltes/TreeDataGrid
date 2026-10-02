# Uno port on the shared Core

`TreeDataGrid.Controls.Uno` is the Uno Platform (WinUI API) presentation of TreeDataGrid,
parallel to `TreeDataGrid.Controls.Avalonia`. Both use the same `TreeDataGrid.Core`
package: sources, rows, columns, sorting, filtering, expansion and row/cell selection are
the Core objects themselves, not copies.

The port's goal is functional parity with the Avalonia control. This page records what is
done, how it is validated, and what remains.

## Packages and namespaces

| Package | Contents |
| --- | --- |
| `TreeDataGrid.Core` | Framework-neutral sources, rows, columns, selection (namespace `TreeDataGridCore`) |
| `TreeDataGrid.Controls.Uno` | The Uno control, primitives, columns, themes (namespace `Uno.Controls`, parallel to `Avalonia.Controls`) |

The Uno package is released with the others (see `.github/workflows/release.yml`). It
contains three builds:

| Target | Package folder | Used by |
| --- | --- | --- |
| `net10.0` | `lib/net10.0` | Uno Skia WebAssembly (and reference builds) |
| `net10.0-desktop` | `lib/net10.0-desktop1.0` | Uno Skia desktop (macOS, Linux X11/framebuffer, Win32) |
| `net10.0-windows10.0.26100` | `lib/net10.0-windows10.0.26100` | Native Windows App SDK |

`build/verify-uno-package.py` checks the three builds, the `TreeDataGrid.Core` dependency,
the licence files and the symbol package. The controls project honours
`TreeDataGridUnoTargetFrameworks`; Windows hosts build all three targets, other hosts the
first two. Samples honour `TreeDataGridUnoSampleTargetFrameworks`.

## Usage

```csharp
using TreeDataGridCore;
using Uno.Controls;

var source = new FlatTreeDataGridSource<Person>(people)
{
    Columns =
    {
        new Uno.Controls.Models.TreeDataGrid.TextColumn<Person, string>("Name", x => x.Name),
        new Uno.Controls.Models.TreeDataGrid.TextColumn<Person, int>("Age", x => x.Age),
    },
};
grid.Source = source;   // or Model
```

In XAML the control, columns and primitives need no `xmlns` prefix, as in Avalonia:

```xml
<Page x:Class="App.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Page.Resources>
    <Style TargetType="TreeDataGridRow">
      <Setter Property="MinHeight" Value="28" />
    </Style>
  </Page.Resources>
  <TreeDataGrid ItemsSource="{Binding People}">
    <TreeDataGrid.ColumnDefinitions>
      <TreeDataGridTextColumn Header="Name" Binding="{Binding Name}" />
      <TreeDataGridTextColumn Header="Age" Binding="{Binding Age}" />
    </TreeDataGrid.ColumnDefinitions>
  </TreeDataGrid>
</Page>
```

The package registers `Uno.Controls` and `Uno.Controls.Primitives` for Uno's global XAML
namespace (`[XmlnsDefinition]`, used when `UnoEnableImplicitXamlNamespaces` is on, the Uno.Sdk
default), so Uno heads compile unprefixed elements directly. Two cases cannot resolve them, and
the package's `buildTransitive` targets handle them with an intermediate copy of the file in
`obj/` that only inserts explicit `using:` prefixes (every line keeps its number; source files
are not changed):

- Uno's generator does not look up type names given as strings (`TargetType`,
  `Setter.Property`) in the global namespace. On Uno heads only those values get a prefix, and
  only files containing them are copied; XAML Hot Reload does not see edits to such files until
  the next build.
- WinUI's markup compiler (Windows App SDK heads) has no global namespace, so every file using
  an unprefixed TreeDataGrid type is copied, and compiler messages name the copy at the same
  line.

Explicit prefixes (`xmlns:tdg="using:Uno.Controls"`) keep working, and
`TreeDataGridImplicitXamlNamespaces=false` turns the copies off. XAML loaded at runtime with
`XamlReader.Load` still needs explicit prefixes.

Ported Avalonia code needs these substitutions: `Avalonia.Controls` → `Uno.Controls` for the
control, columns and primitives, and `TreeDataGridCore` for sources, `IndexPath`, `CellIndex`,
rows and selection models. Column guides:
[typed columns](uno-typed-column-contract.md) ·
[custom value columns](uno-custom-value-columns.md) ·
[tri-state sorting](uno-tristate-sorting.md).

## Samples

`samples/TreeDataGridUnoSample` opens the same eight-tab demo as `samples/TreeDataGridDemo`
(Template Column Reuse, People, Countries, Find Displayed Row, BringIntoView, Files,
Wikipedia, Drag/Drop) with the same view models and column configuration. `--smoke`,
`--suite <name>` and `--harness` run the validation harness instead. `TDG_START_TAB=<index>`
opens a tab, and `TDG_TOUR=1` runs the same scripted walkthrough as the Avalonia demo.
`samples/TreeDataGridUnoActivityMonitor` is a second, larger consumer.

The Uno demo has one extra tab, **Custom Cell Rendering**: the same element-factory
extension point as Avalonia's `TreeDataGridElementFactory` creates `SkiaTextCell`, a
`TreeDataGridTextCell` whose text is drawn by an Uno `SKCanvasElement`
(`samples/TreeDataGridUnoShared/SkiaTextCells.cs`, public APIs only). The tab switches
between template and custom cells and compares scrolling (`TDG_CUSTOM_CELLS=skia`,
`TDG_CUSTOM_CELLS_BENCHMARK=1` for headless runs); the parity host measures the same cells
with `PARITY_UNO_CELLS=skia`. The `custom-cell-rendering` native suite checks that recycled
custom cells draw current text.

| Avalonia demo construct | Uno adaptation |
| --- | --- |
| `TabControl` | `TabView` (stretched, non-closable items) |
| `DockPanel` | `Grid` rows/columns |
| `#name.IsChecked` bindings | `ElementName` bindings with a visibility converter |
| `x:Static` | Resource object (`CountryRegions`) |
| `MultiBinding` file icon | `IsDirectory` visibility plus an `IsExpanded` folder converter |
| `TreeDataGridRow:nth-child(2n)` style | `AlternatingRowBackground` |
| `:nth-last-child(1)` cell/header styles | `CellPrepared`/`CellClearing` attached helper |

## Platform status

| Platform | Status |
| --- | --- |
| Skia desktop (macOS, Linux X11) | Runs; all native validation suites, smoke and package consumers pass in CI (Linux X11) and locally (macOS) |
| Skia WebAssembly | Runs; trimmed consumers pass smoke validation and real browser input on Chromium, Firefox and WebKit |
| Windows App SDK | Builds, packs and publishes as a package consumer in CI; not yet run |
| Android, iOS | Not targeted |

Without WebGL (Firefox on the CI runners) Uno draws through its software canvas renderer, whose
blit reads a view of the WebAssembly heap that becomes unusable if the heap grows during a frame
(`InvalidStateError` from `SoftwareBrowserRenderer.blitSoftware`). The WebAssembly samples start
with a 256 MB heap (`EmccInitialHeapSize`) so it does not grow after startup; applications that
expect browsers without WebGL can do the same until Uno rebuilds that view for every blit.

## Differences from Avalonia

Framework mappings (WinUI has no equivalent of the Avalonia construct):

- Avalonia styled properties, selectors and control themes become dependency properties,
  control templates and visual states. Visual state groups must be declared on the template
  root (Uno only reads groups there). Theme brush names are kept.
- Row drag uses `DataPackageOperation` (`AcceptedOperation`/`AllowedOperations`) instead of
  `DragEffects`, and native data packages instead of `DataTransfer`. Row drag events are CLR
  events: WinUI applications cannot register custom routed events. The drag shows no content
  image, as in Avalonia.
- Avalonia-only overrides (`OnAttachedToVisualTree`, `OnPropertyChanged(AvaloniaPropertyChangedEventArgs)`,
  `OnMeasureInvalidated`, …) have WinUI counterparts (`Loaded`/`Unloaded`, dependency-property
  callbacks, `MeasureOverride`).
- Native `ScrollViewer`, automation peers and binding objects replace the Avalonia ones.

Visual differences in the samples: `TabView` looks different from `TabControl`; Uno's default
font (Open Sans) differs from the platform font Avalonia uses on macOS; Uno draws bitmaps at
one DIP per pixel.

## Remaining parity work

**Public API shape.** The compiled API audit (`tools/TreeDataGrid.ApiAudit`, run by the
validation report job) compares Avalonia and Uno declarations after namespace normalization:
1,070 of 1,851 Avalonia declarations match exactly. Most of the 781 differences have three
causes:

- Avalonia exposes Core-equivalent types in its own namespaces (sources, `IndexPath`,
  `CellIndex`, rows, selection models and their event args: 42 types); Uno uses the
  `TreeDataGridCore` types directly. Uno-namespace wrapper types over Core would close this
  without copying state.
- Signatures that use Core `IRow`/`IndexPath`/`GridLength` where Avalonia uses its own types
  (360 declarations).
- Avalonia-only framework overrides listed above.

Portable members still missing on the Uno side: `ITreeDataGridRows.ModelIndexToRowIndex` and
`RowIndexToModelIndex`, `IExpanderCell.Content`/`Row` re-declarations, the
`HierarchicalExpanderColumn<TModel>` members (`Width`, `ActualWidth`, `Inner`, `CreateCell`,
`HasChildren`, `GetChildModels`, `GetComparison`, …), the `ColumnBase<TModel>.Width` /
`ActualWidth` accessor shape, and the `TreeDataGridPresentation<TModel>` selection overrides.

**Verification not yet done.** Running the Windows App SDK target; screen-reader acceptance
(VoiceOver, Narrator, NVDA) and multi-monitor scaling; physical keyboard, pointer and touch
input on desktop (browser input is covered).

## Performance

The paired workload (`benchmarks/TreeDataGrid.Parity.*`, `build/run-native-parity.py`) runs
the same operations on both frameworks on the same machine. CI reports the ratios in the job
summary; the 1.10 budget is recorded but not enforced. Latest Linux CI ratios (Uno time ÷
Avalonia time): row replacement 1.0, column resize 1.5, sort 1.4, horizontal scroll 2.9,
diagonal jump 4.6, vertical scroll 4.1; allocations 0.3–1.3. Most of the scroll time is
`TextBlock` text layout and Uno's layout, composition and scrolling code. See
[Uno performance findings](uno-performance-findings.md) for each issue and the Uno changes
that would close it. Applications that need faster scrolling today can draw cells
themselves through the element factory, as the Custom Cell Rendering sample does: on macOS
it halves vertical scrolling and diagonal-jump time compared with template cells.

## Validation

All of these run in `.github/workflows/uno.yml` on every pull request.

```sh
# Unit tests
dotnet test tests/TreeDataGrid.Core.Tests -c Release
dotnet test tests/TreeDataGrid.Uno.Tests -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
dotnet test samples/TreeDataGridUnoSample.Tests -c Release

# Native suites (each in a fresh process) and one native suite on its own
python3 build/run-uno-native-suites.py --jobs 3
dotnet run --project samples/TreeDataGridUnoSample -c Release -f net10.0-desktop -- --smoke --suite selection

# Published trimmed browser consumers with real input (Playwright 1.57.0)
python3 build/run-uno-browser.py --showcase <wwwroot> --monitor <wwwroot> --browser firefox

# Paired performance against Avalonia (add --local on a desktop session)
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10 --report-only
```

The validation report job (`build/validate-uno-linux.py`) runs all .NET tests, the native
suites, the canonical stages and the API regression gate (`build/check-uno-api-regression.py`)
over a committed-source snapshot. The contract job regenerates
`docs/uno-contract-materialization.json` from `build/uno-parity-inputs/` and requires it to be
unchanged.
