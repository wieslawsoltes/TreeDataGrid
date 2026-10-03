# WinUI 3 build of the Uno port

`TreeDataGrid.Controls.WinUI` is the Uno port compiled as a plain WinUI 3 (Windows App SDK)
library: the sources, theme and build targets of `TreeDataGrid.Controls.Uno` are linked
unchanged and built with `Microsoft.NET.Sdk` and `Microsoft.WindowsAppSDK`, without Uno.Sdk.
Samples and tests link the Uno ones the same way. This page tracks the WinUI/Uno differences
found while making it work, how each is handled, and what remains open.

## Projects

| Project | Links | Notes |
| --- | --- | --- |
| `src/TreeDataGrid.Controls.WinUI` | `src/TreeDataGrid.Controls.Uno/**/*.cs`, `Themes/Generic.xaml` | Same Windows App SDK (1.7.250909003) and Windows SDK (26100) as the Uno projects' Windows target |
| `samples/TreeDataGridWinUISample` | the Uno sample's sources, XAML and assets | Unpackaged, self-contained; console subsystem so `--smoke`/`--suite` report on stdout |
| `samples/TreeDataGridWinUIActivityMonitor` | the Uno Activity Monitor | As above |
| `tests/TreeDataGrid.WinUI.Tests` | `tests/TreeDataGrid.Uno.Tests/**/*.cs` | WinUI executable running the xunit tests in-process on the XAML UI thread |
| `samples/TreeDataGridWinUISample.Tests` | the Uno sample tests | `dotnet test` (no XAML runtime needed) |

Shared settings live in `build/WinUI.props`; `solutions/TreeDataGrid.WinUI.slnx` groups them.

## Building and validating

`build/validate-winui.ps1` builds everything with MSBuild, runs the WinUI unit tests, the sample
tests, the Activity Monitor smoke run and every native suite registered in
`samples/TreeDataGridUnoSample/App.Validation.cs` in a fresh process. Run it in an interactive
desktop session (the samples open windows).

Requirements found on a clean Windows 11 ARM64 machine with Build Tools 2026:

- WinUI class libraries with XAML need Visual Studio's MSBuild with the MSIX packaging tools
  (`Microsoft.VisualStudio.ComponentGroup.MSIX.Packaging`): the Windows App SDK takes its PRI
  tasks (`Microsoft.Build.Packaging.Pri.Tasks.dll`) from there. `dotnet build` and Build Tools
  without that component fail with MSB4062.
- An incremental build does not regenerate an application's `XamlTypeInfo.g.cs` when only a
  referenced library changed; clean the application's `obj` after changing library XAML types.
- A provider class added to a library (see #9) only reaches an application after that
  regeneration.

## Differences from Uno and how they are handled

| # | Area | WinUI behavior | Uno behavior | Handling |
| --- | --- | --- | --- | --- |
| 1 | Build | Uno.Sdk sets `UseWinUI` only for application heads | — | The XAML rewrite hooks WinUI's markup compiler targets instead of checking `UseWinUI` |
| 2 | XAML namespaces | No global namespace; `XmlnsDefinitionAttribute` does not exist | Global namespace via `[XmlnsDefinition]` | Attributes compiled only for Uno; the build-time rewrite adds `using:` prefixes on Windows App SDK heads |
| 3 | Packaging | `AddXamlFilesToNugetPackage` packs Page items by project-relative path; compiled XAML ships in the `.pri` | — | Rewritten copies are `Pack=false`; package checks require the `.pri` |
| 4 | App assets | `StorageFile.GetFileFromApplicationUriAsync("ms-appx:///…")` throws in unpackaged apps | Supported | Samples read assets through `SampleAssets` (app folder on Windows) |
| 5 | Theme | A `Geometry` resource cannot be shared by several elements (`XamlParseException`) | Shareable | Sort icons and chevrons own their geometry in each template instance |
| 6 | Layout | `EffectiveViewport` can have a negative width/height for elements outside the viewport | Never negative | The presenter treats a negative size as empty |
| 7 | Bindings | `Binding.ElementName` is `""` when unset (type `string`) | `null` (type `object`) | Checks use `is not null and not ""`; previously every WinUI write fell back to `UpdateSource` |
| 8 | Bindings | Clearing a binding (or its source) from within its own `UpdateSource` crashes natively (0xC0000005); setter exceptions inside `UpdateSource` are swallowed | Tolerated; exceptions propagate | The probe inside `UpdateSource` is never mutated (a reentrant retarget/suspend/dispose sets it aside); writes go through the explicit path writer |
| 9 | XAML types | A class used only from code has no XAML metadata, so WinUI treats it as its nearest WinRT base (`Control`) and rejects the theme style ("Cannot apply a Style with TargetType … to an object of type Control") | Not checked | `TreeDataGridXamlMetadataProvider` (Windows only) describes code-only subclasses of library types; apps pick it up automatically |
| 10 | XAML types | Applications generate their own member-less entries for the library's generic presenter bases, so theme bindings to their members fail ("Failed to assign to property …PresenterBase`1<…>.ElementFactory") | Not applicable | `Windows/XamlMetadata.xaml` (never loaded) makes the library's generated metadata complete, and `[FullXamlMetadataProvider]` makes applications defer to it |
| 11 | Errors | An exception escaping a XAML callback ends the process with a stowed exception (0xC000027B) before output is flushed | Reported normally | Sample/test apps print `UnhandledException` before exiting; Windows smoke runs also log `DebugSettings` resource and binding failures |
| 12 | Unit tests | Dependency objects need a running XAML application on its UI thread | Work in a plain test host | `TreeDataGrid.WinUI.Tests` hosts the xunit tests in a WinUI application |
| 13 | Element tree | `FrameworkElement.Parent` and `VisualTreeHelper.GetParent` are both null for an element whose panel is not connected to a window (never loaded, or a replaced template), although the panel still owns it and adding it elsewhere fails (0x800F1000) | The parent is always reported | `ElementParent.HostParent()` falls back to the container recorded when the element factory handed the element out, confirmed against the container's children |
| 14 | Automation | `RaisePropertyChangedEvent` rejects null values and the `CanSelectMultiple`/`Selection` properties (E_INVALIDARG) | Accepted | The grid peer raises `SelectionPatternOnInvalidated` on Windows; value changes pass `""` for an absent value |
| 15 | Layout timing | `SizeChanged`, `EffectiveViewportChanged`, `Loaded`/`Unloaded` and focus events are raised after the layout pass, not during it | Raised synchronously | A grid resized and given a source in the same step measures once with its previous viewport (more cells are created than needed). Runtime checks let a resize settle and wait for the condition instead of assuming one layout pass |
| 16 | Scrolling | `StartBringIntoView`/`ChangeView` are applied later; `UpdateLayout` right after still sees the previous viewport. `Arrange` outside a layout pass does not change an element's reported bounds | Applied during `UpdateLayout` | `BringIntoView` asks for the target's known or estimated position when the target is not realized, and keeps the request until the next viewport notification (at most one second and eight passes) to refine it |
| 17 | Focus | Giving an element keyboard focus brings it into view asynchronously, after code that runs in the same turn | No deferred request | The focus checks let that request finish before scrolling away. Applications that focus and then scroll in the same turn see the same effect |
| 18 | Layout rounding | Rounds a midpoint up | `Math.Round` (to even) | `TreeDataGridCellContentPanel` rounds the centered text offset the same way as the platform |
| 19 | Rendering | `RenderTargetBitmap` covers the layout slots of descendants, so it is taller than an element whose text is arranged in a slot extending below it | Covers the element | The template parity check crops to the element and requires the rest to be empty. On a machine without a GPU (CI) the same rounded border is antialiased one level differently at two screen positions, so the check accepts a difference of one level per channel on Windows |
| 20 | Recycling | A horizontal jump is processed inside one layout pass, so recycled cells are reused directly without a `Visibility` change | Recycled before the measure, so cells are collapsed and shown again | The check asserts that no unused cell stays visible |
| 21 | Interop | Every dependency property write crosses the WinRT boundary and allocates, even for an unchanged value | No allocation | Allocation budgets in `header-lifetime` and `cell-content-layout` are larger on Windows |
| 22 | Strings | A string read back from a dependency property is a new instance (HSTRING marshaling) | Same instance | Identity checks compare by value on Windows |
| 23 | Data context | `RegisterPropertyChangedCallback(DataContextProperty)` is not raised for inherited changes | Raised | Checks observe `DataContextChanged` |
| 24 | Text box | Assigning `TextBox.Text` from within its own `TextChanged` is overwritten when the outer assignment completes | Nested assignment wins | `TreeDataGridCell.SetEditorText` reapplies the newest text after a nested assignment |

## Validation status

Windows 11 ARM64 VM (Parallels), Build Tools 2026 18.10, .NET 10.0.401.

| Check | Status |
| --- | --- |
| Library, samples and test host build | Pass |
| Linked Uno unit tests (`TreeDataGrid.WinUI.Tests`) | 1,101 pass (`NativeBindingMetadataTests` and `XamlNamespaceTests` are Uno-only and not linked) |
| Sample tests | Pass |
| Activity Monitor smoke | Pass |
| Native suites | 66 of 66 pass |

`build/validate-winui.ps1` reports `WINUI_VALIDATION_PASSED`; the `winui` job in
`.github/workflows/uno.yml` runs the same script, packs `TreeDataGrid.Controls.WinUI` and checks
the package with `build/verify-winui-package.py`.

## Open issues

- **Release.** `TreeDataGrid.Controls.WinUI` is packed and verified in CI but is not part of
  `.github/workflows/release.yml`, so it is not published to NuGet.
- **Not verified.** Packaged (MSIX) applications, x64 on real hardware, screen readers, and
  physical keyboard, pointer and touch input.
- **Timing-dependent checks.** Differences #15–#17 make several runtime checks wait for a
  condition (`SampleWait.UntilAsync`, at most three seconds) on Windows instead of asserting
  after one layout pass. `focus` and `standalone-row` failed intermittently before those waits
  were added; a slow machine could still exceed them.
- **Bring into view (#16).** A request for an unrealized row is completed over the following
  viewport notifications. While it is incomplete (at most one second), a user scroll can be
  pulled back to the requested row.
