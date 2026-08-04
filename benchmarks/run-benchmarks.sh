#!/usr/bin/env sh

set -eu

repo_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
project="$repo_dir/benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks.csproj"
dll="$repo_dir/benchmarks/Avalonia.Controls.TreeDataGrid.Benchmarks/bin/Release/net8.0/Avalonia.Controls.TreeDataGrid.Benchmarks.dll"

package_root=${NUGET_PACKAGES:-$(dotnet nuget locals global-packages --list | sed 's/^[^:]*: *//')}
NUGET_PACKAGES="${package_root%/}/"
export NUGET_PACKAGES

dotnet build "$project" -c Release -p:DisableSourceLink=true
dotnet "$dll" "$@"
