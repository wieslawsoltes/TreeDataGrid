# Compiled TreeDataGrid API inventory

This tool reads PE metadata through the Roslyn libraries shipped in the selected
.NET SDK. It does not execute UI assemblies, evaluate static properties, instantiate
controls, or require an additional NuGet dependency.

Build the native sample and Avalonia tests first so their output directories
contain the exact framework dependencies used in the comparison:

```sh
dotnet build tests/Avalonia.Controls.TreeDataGrid.Tests -c Release
dotnet build src/TreeDataGrid.Avalonia/TreeDataGrid.Avalonia.csproj -c Release
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop \
  dotnet build samples/TreeDataGridUnoSample -c Release -f net10.0-desktop

TREEDATAGRID_API_BASELINE_REFERENCES=tests/Avalonia.Controls.TreeDataGrid.Tests/bin/Release/net8.0 \
TREEDATAGRID_API_TARGET_REFERENCES=samples/TreeDataGridUnoSample/bin/Release/net10.0-desktop \
  dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- \
  src/TreeDataGrid.Avalonia/bin/Release/net8.0/TreeDataGrid.Avalonia.dll \
  src/TreeDataGrid.Controls.Uno/bin/Release/net10.0-desktop/TreeDataGrid.Controls.Uno.dll \
  src/TreeDataGrid.Core/bin/Release/net8.0/TreeDataGrid.Core.dll \
  artifacts/api-audit
```

The reference-directory environment variables accept platform path-separated lists
(`:` on Unix, `;` on Windows). Input assemblies take priority, followed by supplied
reference directories, adjacent assemblies and trusted platform assemblies. Native
DLLs without managed metadata are ignored. Missing directories or invalid inputs
fail explicitly. Both unresolved signature-type lists must be empty before treating
the metadata inventory as fully resolved.

Outputs are sorted deterministically: raw and namespace-normalized public/protected
declarations, methods and parameter defaults, generic constraints, properties,
events, constant values, base types and implemented interfaces. Input assembly
SHA-256 hashes are recorded. Only the two explicit control-namespace prefixes are
normalized. Framework types, relocated Core APIs, property systems and inherited
behavior are **not automatically waived**.

`missing-or-different.txt` is a work list, not a verdict that every entry is an
unimplemented feature. A changed native type or Core relocation needs an explicit
mapping and separate compatibility evidence. Matching declarations do not prove
functional, binary, input or performance equivalence. No numeric full-parity claim
is generated. `--strict` returns nonzero for baseline shapes absent from the target
or unresolved signature types. Default audit mode emits the complete difference
report, not an acceptance certificate.

For a deterministic sanity check, use the same assembly for both UI inputs and
supply its dependencies in both reference-directory variables. A strict self-diff
must have zero differences and zero unresolved types. Compare generated JSON/text
files rather than assembly hashes between builds with different assembly versions.
The Linux validation driver runs this check automatically after the comparison.
