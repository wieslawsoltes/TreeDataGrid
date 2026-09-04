# `TreeDataGrid.Controls.Avalonia` Installation

- Add the `TreeDataGrid.Controls.Avalonia` NuGet package to new projects.
- Add its theme to `App.xaml` (the `StyleInclude` in the following markup):

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AvaloniaApplication.App">
  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml"/>
  </Application.Styles>
</Application>
```

Existing applications may keep the `TreeDataGrid` package and
`avares://Avalonia.Controls.TreeDataGrid/...` theme URI. Both packages expose the
same Avalonia API; reference only one of them. See
[Build and Package](build-and-package.md) for the package identity table.
