# Avalonia package identity

New Avalonia applications should reference `TreeDataGrid.Controls.Avalonia`. It is the
platform-specific UI package paired with the framework-neutral `TreeDataGrid.Core`
package:

```xml
<PackageReference Include="TreeDataGrid.Controls.Avalonia" Version="x.y.z" />
```

The UI package brings in the exact matching `TreeDataGrid.Core` version transitively.
Use its assembly name in theme resource URIs:

```xml
<StyleInclude Source="avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml" />
```

| Package | Assembly | Purpose |
|---|---|---|
| `TreeDataGrid.Core` | `TreeDataGrid.Core.dll` | Framework-neutral models, columns, sorting, selection and expansion |
| `TreeDataGrid.Controls.Avalonia` | `TreeDataGrid.Avalonia.dll` | Preferred Avalonia controls, presentation, input and themes |
| `TreeDataGrid` | `Avalonia.Controls.TreeDataGrid.dll` | Existing Avalonia package retained for compatibility |

`TreeDataGrid.Controls.Avalonia` source-links the complete UI implementation from
`src/Avalonia.Controls.TreeDataGrid`; it is not a dependency-only wrapper around
`TreeDataGrid`. Both UI packages therefore expose the same Avalonia namespaces and
types but have different assembly and resource identities. Reference one UI package,
not both, in an application.

Existing applications can retain `TreeDataGrid` and the
`avares://Avalonia.Controls.TreeDataGrid/` theme root without changes. Migration is
a package-reference replacement plus changing explicit theme URIs to
`avares://TreeDataGrid.Avalonia/`; C# namespaces and XAML control names remain the
same.

A future `TreeDataGrid.Uno` package can share `TreeDataGrid.Core`; it is intentionally
outside the current Avalonia 11/12 work.
