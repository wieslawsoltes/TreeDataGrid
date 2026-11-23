#!/usr/bin/env python3
"""Verify the TreeDataGrid.Controls.WinUI package: Windows App SDK build, Core dependency and symbols."""

from __future__ import annotations

import argparse
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

PACKAGE_ID = "TreeDataGrid.Controls.WinUI"
ASSEMBLY = "TreeDataGrid.Controls.WinUI.dll"
FRAMEWORK = "lib/net10.0-windows10.0."
BUILD_TRANSITIVE = (f"buildTransitive/{PACKAGE_ID}.targets", "buildTransitive/TreeDataGrid.Controls.Uno.targets",
                    "buildTransitive/TreeDataGridImplicitXaml.cs")


def fail(message: str) -> None:
    raise SystemExit(f"WinUI package verification failed: {message}")


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_dir", type=Path)
    parser.add_argument("version")
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
    libraries = {entry.split("/")[1] for entry in names if entry.startswith("lib/")}
    if len(libraries) != 1 or not any(entry.startswith(FRAMEWORK) and entry.endswith("/" + ASSEMBLY) for entry in names):
        fail(f"expected only the Windows App SDK build ({FRAMEWORK}*), found {sorted(libraries)}")
    # The compiled theme (XBF) travels in the package resource index.
    if not any(entry.startswith(FRAMEWORK) and entry.endswith(f"/{PACKAGE_ID}.pri") for entry in names):
        fail(f"no {PACKAGE_ID}.pri")
    if any(entry.endswith("/TreeDataGrid.Controls.Uno.dll") or "Avalonia" in entry.rsplit("/", 1)[-1] for entry in names):
        fail("the WinUI package contains an Uno or Avalonia assembly")
    for entry in ("THIRD-PARTY-NOTICES.md",) + BUILD_TRANSITIVE:
        if entry not in names:
            fail(f"{entry} is not packed")

    groups = [item for item in metadata.iter() if local_name(item.tag) == "group"]
    if not groups:
        fail("no dependency groups")
    for group in groups:
        dependencies = {item.attrib["id"]: item.attrib.get("version", "")
                        for item in group if local_name(item.tag) == "dependency"}
        framework = group.attrib.get("targetFramework", "?")
        if dependencies.get("TreeDataGrid.Core") != args.version:
            fail(f"{framework} does not require TreeDataGrid.Core {args.version}")
        if "Microsoft.WindowsAppSDK" not in dependencies:
            fail(f"{framework} does not depend on Microsoft.WindowsAppSDK")
        if any(dependency.startswith(("Uno.", "Avalonia")) for dependency in dependencies):
            fail(f"{framework} depends on an Uno or Avalonia package")

    print(f"Verified {PACKAGE_ID} {args.version}: Windows App SDK build, resource index, "
          "TreeDataGrid.Core dependency, notices, XAML build targets and symbol package.")


if __name__ == "__main__":
    main()
