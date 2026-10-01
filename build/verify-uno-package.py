#!/usr/bin/env python3
"""Verify the TreeDataGrid.Controls.Uno package: frameworks, Core dependency and symbols."""

from __future__ import annotations

import argparse
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

PACKAGE_ID = "TreeDataGrid.Controls.Uno"
ASSEMBLY = "TreeDataGrid.Controls.Uno.dll"
# NuGet folder prefixes for the reference/WebAssembly, Skia desktop and Windows App SDK builds.
FRAMEWORKS = {"net10.0": "lib/net10.0/", "desktop": "lib/net10.0-desktop", "windows": "lib/net10.0-windows10.0."}
BUILD_TRANSITIVE = (f"buildTransitive/{PACKAGE_ID}.targets", "buildTransitive/TreeDataGridImplicitXaml.cs")


def fail(message: str) -> None:
    raise SystemExit(f"Uno package verification failed: {message}")


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_dir", type=Path)
    parser.add_argument("version")
    parser.add_argument("--without-windows", action="store_true",
                        help="Allow a package packed off Windows, without the Windows App SDK build.")
    args = parser.parse_args()

    package = args.package_dir / f"{PACKAGE_ID}.{args.version}.nupkg"
    symbols = args.package_dir / f"{PACKAGE_ID}.{args.version}.snupkg"
    if not package.is_file():
        fail(f"missing {package.name}")
    if not symbols.is_file():
        fail(f"missing symbol package {symbols.name}")

    with zipfile.ZipFile(package) as archive:
        names = set(archive.namelist())
        nuspecs = [name for name in names if name.endswith(".nuspec")]
        if len(nuspecs) != 1:
            fail(f"{package.name} contains {len(nuspecs)} nuspec files")
        metadata = next(item for item in ET.fromstring(archive.read(nuspecs[0])).iter()
                        if local_name(item.tag) == "metadata")

    values = {local_name(item.tag): (item.text or "").strip() for item in metadata}
    if values.get("id") != PACKAGE_ID or values.get("version") != args.version:
        fail(f"unexpected identity {values.get('id')} {values.get('version')}")

    required = dict(FRAMEWORKS)
    if args.without_windows:
        required.pop("windows")
    for name, prefix in required.items():
        if not any(entry.startswith(prefix) and entry.endswith("/" + ASSEMBLY) for entry in names):
            fail(f"no {ASSEMBLY} for the {name} build ({prefix}*)")
    if any(entry.endswith("/Avalonia.Controls.TreeDataGrid.dll") or entry.endswith("/TreeDataGrid.Avalonia.dll")
           for entry in names):
        fail("the Uno package contains an Avalonia assembly")
    if "THIRD-PARTY-NOTICES.md" not in names:
        fail("THIRD-PARTY-NOTICES.md is not packed")
    # Prefixless XAML for Windows App SDK heads and Style TargetType values on Uno heads.
    for entry in BUILD_TRANSITIVE:
        if entry not in names:
            fail(f"{entry} is not packed")

    if not args.without_windows:
        # WinUI loads the packed theme XAML as-is: it must be the copy with explicit prefixes.
        themes = [entry for entry in names
                  if entry.startswith(FRAMEWORKS["windows"]) and entry.endswith("/Themes/Generic.xaml")]
        if len(themes) != 1:
            layout = sorted(entry for entry in names if entry.startswith(FRAMEWORKS["windows"])
                            and entry.rsplit(".", 1)[-1].lower() in ("xaml", "xbf", "pri"))
            fail(f"expected one Windows App SDK Themes/Generic.xaml, found {themes}; XAML layout: {layout}")
        with zipfile.ZipFile(package) as archive:
            theme = ET.fromstring(archive.read(themes[0]))
        presentation = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
        unprefixed = sorted({element.tag[len(presentation):] for element in theme.iter()
                             if element.tag.startswith(presentation + "TreeDataGrid")})
        if unprefixed:
            fail(f"the Windows App SDK theme uses TreeDataGrid types without a prefix: {unprefixed}")

    groups = [item for item in metadata.iter() if local_name(item.tag) == "group"]
    if not groups:
        fail("no dependency groups")
    for group in groups:
        dependencies = {item.attrib["id"]: item.attrib.get("version", "")
                        for item in group if local_name(item.tag) == "dependency"}
        framework = group.attrib.get("targetFramework", "?")
        if dependencies.get("TreeDataGrid.Core") != args.version:
            fail(f"{framework} does not require TreeDataGrid.Core {args.version}")
        if any(dependency.startswith("Avalonia") or dependency == "TreeDataGrid" for dependency in dependencies):
            fail(f"{framework} depends on an Avalonia package")

    print(f"Verified {PACKAGE_ID} {args.version}: {', '.join(required)} builds, "
          "TreeDataGrid.Core dependency, notices, XAML build targets and symbol package.")


if __name__ == "__main__":
    main()
