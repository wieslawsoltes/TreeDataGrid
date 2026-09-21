# Uno platform implementation and validation targets

This is an implementation checkpoint, **not a platform support certification**.
The newly restored browser and native Windows configurations have not been built
or run. Finish the remaining parity code before executing the validation matrix.

| Target | Sample configuration | Controls package asset |
| --- | --- | --- |
| Desktop (macOS/X11/Win32) | `net10.0-desktop` | `net10.0-desktop` |
| Browser (Uno Skia WebAssembly) | `net10.0-browserwasm` | shared `net10.0` Uno library |
| Native Windows App SDK | `net10.0-windows10.0.26100` | matching Windows library asset |

Both Showcase and Activity Monitor include desktop/browser targets by default.
On Windows they also include native Windows App SDK. Set
`TreeDataGridUnoSampleTargetFrameworks` explicitly to select a platform without
restoring other sample heads. The controls project separately honors
`TreeDataGridUnoTargetFrameworks`; Windows defaults include all three library
assets, while other hosts default to the portable and desktop assets. The package
name remains `TreeDataGrid.Controls.Uno`, with the actual shared `TreeDataGrid.Core`
project/package reference. No legacy model assembly is copied into the Uno port.

## Entry points and rendering

Desktop retains the existing Uno platform host. Browser has a WebAssembly host
entry point for each sample. Windows uses the native generated Application entry
point, unpackaged self-contained Windows App SDK deployment, and an OS/DPI-aware
manifest; it does not start the Skia Win32 host.

Activity Monitor shares the complete chart drawing code across these targets.
Desktop/browser keep `SKCanvasElement`. Native Windows renders that same drawing
code into a retained premultiplied BGRA bitmap and presents it through WinUI Image.
The Windows surface follows XamlRoot rasterization scale, retains buffers at a
stable size, receives pointer hover, and releases buffers/root handlers on unload.
This path necessarily copies pixels; it is not claimed to match Skia's zero-copy
performance. Windows/browser use explicitly labeled demo telemetry; macOS desktop
keeps the live native provider.

## Authored CI lanes (unrun)

The existing desktop test/runtime lanes explicitly select the desktop sample
target. Dedicated browser and Windows lanes build both source-reference samples,
pack Core and Uno controls, then publish both samples as package consumers.
Browser installs `wasm-tools`; Windows packs the native Windows controls asset as
well as portable/desktop assets. These jobs are definitions only: no workflow was
dispatched or pushed during this implementation pass.

## Browser launch and file semantics (implemented, unrun)

The browser entry points accept `?smoke=1&demo=1&offline=1`. The optional
`wikipedia-live=1` enables the existing live-feed checks. Only those boolean flags
are accepted; URLs cannot supply arbitrary arguments, file paths or output paths.
Native applications retain their existing command-line arguments.

Browser smoke state is published as `globalThis.__treeDataGridSmoke` with
`complete`, `passed` and `error` fields. Both caught and unhandled smoke failures
are reported; a later success cannot replace an earlier failure. Results use
source-generated JSON serialization, not string interpolation of exception text.
The page stays open after completion so a browser runner can inspect it. Native
applications retain process/application exit behavior. Screenshots in browser
validation must come from the browser runner; the desktop `--screenshot-dir`
argument is deliberately not accepted from query strings.

Files tree/flat continue using the same source-linked FileTreeNodeModel. Desktop
keeps live watchers. Browser disables watchers and opens a small sample-owned
in-memory sandbox directory. Status text makes clear that these are not host
files; Open / refresh rebuilds a snapshot. Lazy expansion, checked state, metadata,
tree/flat sharing and directory-first sorting are unchanged. Native file checks
now assert either watcher updates or explicit snapshot refresh, without claiming
browser support for host filesystem notifications. The application does not gain
access to arbitrary host folders. Browser file importing/picker UX is not claimed.

Four launch/result unit cases and two watcher-free snapshot cases are authored,
in addition to the adapted native file suite. None has run.

Build/publish success will not prove runtime parity. Required follow-up includes:

- Execute browser launch/smoke-result transport and real browser screenshots/input.
- Execute browser sandbox/snapshot behavior and confirm no watcher/host-filesystem
  assumptions remain. Do not claim Files works simply because its head compiles.
- Browser trimming/AOT, declarative binding metadata, Wikipedia networking and
  all scenario actions; no global trimming exemption is asserted as a solution.
- Windows native launch, charts, keyboard/composition, accessibility, drag/drop,
  themes/high contrast, scaling and independent package resource loading.
- Android/iOS remain outside this restored-head checkpoint, not certified by it.

The full feature ledger remains in `uno-parity-audit.md`; earlier desktop test
results do not cover these new targets or any later uncommitted implementation.
