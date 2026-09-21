# Compiled TreeDataGrid API inventory

This tool reads PE metadata through the Roslyn libraries shipped in the selected
.NET SDK. It does not load or execute the UI assemblies, evaluate static fields,
instantiate controls, or require an additional NuGet dependency.

```sh
dotnet build src/TreeDataGrid.Avalonia/TreeDataGrid.Avalonia.csproj -c Release
dotnet build samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop
dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- \
  src/TreeDataGrid.Avalonia/bin/Release/net8.0/TreeDataGrid.Avalonia.dll \
  src/TreeDataGrid.Controls.Uno/bin/Release/net10.0-desktop/TreeDataGrid.Controls.Uno.dll \
  src/TreeDataGrid.Core/bin/Release/net8.0/TreeDataGrid.Core.dll \
  artifacts/api-audit
```

Outputs are sorted deterministically: raw and namespace-normalized public/protected
declarations, methods and parameter defaults, generic constraints, properties,
events, constant values, base types and implemented interfaces. Input assembly
SHA-256 hashes and unresolved metadata types are recorded. Only the two explicit
control-namespace prefixes are normalized. Framework types, relocated Core APIs,
property systems and inherited behavior are **not automatically waived**.

`missing-or-different.txt` is a work list, not a verdict that every entry is an
unimplemented feature. A changed native type or a Core relocation needs an explicit
mapping and separate compatibility evidence. Matching shapes likewise do not prove
functional, binary, input or performance equivalence. No numeric "100% parity"
claim is generated. `--strict` returns nonzero for baseline shapes absent from the
target or unresolved signature types. The default audit mode emits the complete
report without pretending that known framework differences are build failures.

For a deterministic sanity check, use the same assembly for both UI inputs; the
two normalized inventories must then match exactly. Compare generated JSON/text
files rather than assembly hashes between builds with different assembly versions.
