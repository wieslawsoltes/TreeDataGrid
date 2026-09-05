# Uno / shared Core showcase

This work-in-progress desktop sample uses `TreeDataGrid.Controls.Uno` with actual
`TreeDataGrid.Core` sources. It does not reference Avalonia UI. Countries, People,
template items, Wikipedia feed DTOs, JSON metadata, and the offline file icon are
source-linked from the existing demo. Platform image implementations are separate
partials; the shared feed file contains no UI types.

Available scenarios: Countries, editable People hierarchy, Templates,
variable-height Countries, and Wikipedia. This is not the complete showcase or
Activity Monitor yet; see [remaining parity work](../../docs/uno-port-status.md).

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

```sh
dotnet test samples/TreeDataGridUnoSample.Tests/TreeDataGridUnoSample.Tests.csproj -c Release
dotnet run --project samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop -- --smoke --screenshot-dir artifacts/uno
```

The standard smoke suite is network-independent, including an injected delayed
image response. It verifies native decoding, the image request User-Agent, and
late completion after template recycling. `--smoke --wikipedia-live` additionally
checks the external feed and reports live article/image results; it is not part of
deterministic CI. A successful offline fallback does not prove live-network access.

To validate local package consumption, pack Core and controls with the same unique
version into `artifacts/uno-pack`, then pass
`-p:TreeDataGridUnoPackageVersion=<version>` and
`-p:RestoreAdditionalProjectSources=<absolute package directory>` to `dotnet run`.
These switches replace the controls ProjectReference with its PackageReference.
