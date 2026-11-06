# Uno / shared Core showcase

This sample uses `TreeDataGrid.Controls.Uno` with actual `TreeDataGrid.Core` sources.
It does not reference Avalonia UI. The default launch opens `Demo/MainWindow`, the Uno
counterpart of `samples/TreeDataGridDemo/MainWindow.axaml`: the same eight tabs, the same
view models (ported to Core sources and Uno columns) and the same shared models. Avalonia
style selectors map to native API: `TreeDataGridRow:nth-child(2n)` is
`AlternatingRowBackground`, and the bold last column uses `CellPrepared`/`CellClearing`.

The previous single-page showcase remains the validation harness. It is used for
`--smoke`, `--suite <name>` and `--harness` runs.

Both demos support `TDG_START_TAB=<index>` to open a tab and `TDG_TOUR=1` to run the same
scripted walkthrough (tabs, sorting, filtering, selection, editing, expansion). Each tour
step prints `TOUR_STEP <name>` so an external tool can capture the window for comparison.

```sh
dotnet build solutions/TreeDataGrid.Uno.slnx -c Release
dotnet run --project samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop
```

Select Wikipedia to load today's live feed. Reload, cancel, and offline buttons
are available. Changing scenarios or unloading the page cancels an active feed
request. Failures are reported with an explicitly synthetic 240-row fallback.
Remote thumbnails are loaded lazily with the sample's User-Agent, then decoded
into model-specific native images so a late completion cannot overwrite another
article after recycling. Add `-- --offline` to avoid the initial feed request.

## Validation

Files opens the current directory by default and accepts another directory path.
It is read-only: the checkbox changes sample selection state, not file contents.
Tree and flat modes share the same nodes; expanded directories are watched for
external changes. Leaving Files, replacing its root, or unloading the page releases
watchers. Initial directory enumeration runs off the UI thread; nested expansion
retains the shared model's synchronous lazy enumeration behavior.

Find Country keeps the complete model list separate from the filtered grid. Select
a country, filter by name/region, and sort headers to see its displayed index and
bring the matching Core model into view. Filtered-out selections are reported.

```sh
dotnet test samples/TreeDataGridUnoSample.Tests/TreeDataGridUnoSample.Tests.csproj -c Release
dotnet run --project samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop -- --smoke --screenshot-dir artifacts/uno
```

The standard smoke suite is network-independent, including an injected delayed
image response. It verifies native decoding, the image request User-Agent, and
late completion after template recycling. File mutation checks operate only in
new temporary fixture directories and delete those fixtures after closing watchers.
`--smoke --wikipedia-live` additionally
checks the external feed and reports live article/image results; it is not part of
deterministic CI. A successful offline fallback does not prove live-network access.

To validate local package consumption, pack Core and controls with the same unique
version into `artifacts/uno-pack`, then pass
`-p:TreeDataGridUnoPackageVersion=<version>` and
`-p:RestoreAdditionalProjectSources=<absolute package directory>` to `dotnet run`.
These switches replace the controls ProjectReference with its PackageReference.
