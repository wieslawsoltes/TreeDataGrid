#!/usr/bin/env python3
"""Verify the Core, compatibility, and Avalonia package graph and payloads."""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


EXPECTED_ASSEMBLIES = {
    "TreeDataGrid.Core": "TreeDataGrid.Core.dll",
    "TreeDataGrid": "Avalonia.Controls.TreeDataGrid.dll",
    "TreeDataGrid.UI.Avalonia": "TreeDataGrid.Avalonia.dll",
}


def fail(message: str) -> None:
    raise SystemExit(f"Package verification failed: {message}")


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def read_package(path: Path) -> tuple[str, str, dict[str, str], set[str]]:
    with zipfile.ZipFile(path) as archive:
        names = set(archive.namelist())
        nuspecs = [name for name in names if name.endswith(".nuspec")]
        if len(nuspecs) != 1:
            fail(f"{path.name} contains {len(nuspecs)} nuspec files")
        root = ET.fromstring(archive.read(nuspecs[0]))

    metadata = next((item for item in root.iter() if local_name(item.tag) == "metadata"), None)
    if metadata is None:
        fail(f"{path.name} has no metadata")

    values = {
        local_name(item.tag): (item.text or "").strip()
        for item in metadata
        if local_name(item.tag) in {"id", "version"}
    }
    dependencies = {
        item.attrib["id"]: item.attrib.get("version", "")
        for item in metadata.iter()
        if local_name(item.tag) == "dependency" and "id" in item.attrib
    }
    return values.get("id", ""), values.get("version", ""), dependencies, names


def main() -> None:
    if len(sys.argv) not in {2, 3}:
        fail("usage: verify-package-layout.py <package-directory> [version]")

    package_dir = Path(sys.argv[1])
    requested_version = sys.argv[2] if len(sys.argv) == 3 else None
    package_files = [
        path for path in package_dir.glob("*.nupkg") if not path.name.endswith(".snupkg")
    ]
    packages: dict[str, tuple[Path, str, dict[str, str], set[str]]] = {}

    for path in package_files:
        package_id, version, dependencies, payload = read_package(path)
        if package_id in EXPECTED_ASSEMBLIES:
            if package_id in packages:
                fail(f"duplicate {package_id} packages")
            packages[package_id] = (path, version, dependencies, payload)

    missing = set(EXPECTED_ASSEMBLIES) - set(packages)
    if missing:
        fail(f"missing packages: {', '.join(sorted(missing))}")

    versions = {details[1] for details in packages.values()}
    if len(versions) != 1:
        fail(f"package versions differ: {', '.join(sorted(versions))}")
    version = versions.pop()
    if requested_version is not None and version != requested_version:
        fail(f"expected version {requested_version}, found {version}")

    for package_id, assembly in EXPECTED_ASSEMBLIES.items():
        path, _, dependencies, payload = packages[package_id]
        matching_assemblies = {
            name for name in payload if name.startswith("lib/") and name.endswith(".dll")
        }
        if not any(name.endswith(f"/{assembly}") for name in matching_assemblies):
            fail(f"{path.name} does not contain {assembly}")
        other_ui_assembly = (
            "Avalonia.Controls.TreeDataGrid.dll"
            if package_id == "TreeDataGrid.UI.Avalonia"
            else "TreeDataGrid.Avalonia.dll"
        )
        if package_id != "TreeDataGrid.Core" and any(
            name.endswith(f"/{other_ui_assembly}") for name in matching_assemblies
        ):
            fail(f"{path.name} also contains {other_ui_assembly}")
        symbol_path = package_dir / f"{package_id}.{version}.snupkg"
        if not symbol_path.is_file():
            fail(f"missing symbol package {symbol_path.name}")

        if package_id == "TreeDataGrid.Core":
            if "Avalonia" in dependencies:
                fail("TreeDataGrid.Core depends on Avalonia")
        elif "TreeDataGrid.Core" not in dependencies:
            fail(f"{package_id} does not depend on TreeDataGrid.Core")
        elif "Avalonia" not in dependencies:
            fail(f"{package_id} does not depend on Avalonia")

    legacy_dependencies = packages["TreeDataGrid"][2]
    avalonia_dependencies = packages["TreeDataGrid.UI.Avalonia"][2]
    if "TreeDataGrid.UI.Avalonia" in legacy_dependencies:
        fail("compatibility package depends on TreeDataGrid.UI.Avalonia")
    if "TreeDataGrid" in avalonia_dependencies:
        fail("TreeDataGrid.UI.Avalonia depends on the compatibility package")

    for package_id in ("TreeDataGrid", "TreeDataGrid.UI.Avalonia"):
        if packages[package_id][2].get("TreeDataGrid.Core") != version:
            fail(f"{package_id} does not require TreeDataGrid.Core {version}")

    print(
        f"Verified TreeDataGrid.Core, TreeDataGrid, and TreeDataGrid.UI.Avalonia {version}: "
        "independent UI packages with matching Core dependencies and symbol packages."
    )


if __name__ == "__main__":
    main()
