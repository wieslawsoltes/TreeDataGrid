# `TreeDataGridUnoActivityMonitor`

`TreeDataGridUnoActivityMonitor` is a second Uno sample for this repository. It showcases:

- a macOS-inspired Activity Monitor shell
- one `TreeDataGrid` per metric tab: CPU, Memory, Energy, Disk, and Network
- sample-local SkiaSharp charts for live trend rendering
- a native macOS desktop telemetry provider on `net10.0-desktop`
- a demo telemetry provider for non-macOS desktop, WebAssembly, Android, and iOS

## Target Frameworks

The sample uses:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-browserwasm;net10.0-desktop</TargetFrameworks>
```

The underlying `TreeDataGrid.Uno` source port remains on `net9.0`.

## Build

Build the desktop head:

```bash
dotnet build samples/TreeDataGridUnoActivityMonitor/TreeDataGridUnoActivityMonitor.csproj -c Release -f net10.0-desktop
```

Smoke-run the desktop head and exit automatically:

```bash
cd samples/TreeDataGridUnoActivityMonitor
dotnet run -c Release -f net10.0-desktop -- --exit
```

## Telemetry

- On macOS desktop, the sample uses native process, host, filesystem, and interface APIs to populate the grids.
- On other targets, the sample runs with a deterministic demo provider and shows a `Demo telemetry` status badge in the shell.

## Notes

- The desktop smoke run can log Uno RemoteControl and Hot Design dev-server warnings when no IDE endpoint is attached.
- The Uno desktop template may also emit a non-fatal icon warning from `Package.appxmanifest`.
