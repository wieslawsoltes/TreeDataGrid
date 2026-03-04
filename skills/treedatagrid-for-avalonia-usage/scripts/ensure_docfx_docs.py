#!/usr/bin/env python3
"""Ensure TreeDataGrid DocFX docs are available locally."""

from __future__ import annotations

import argparse
import os
import pathlib
import shutil
import subprocess
import sys

DEFAULT_REPO_URL = "https://github.com/wieslawsoltes/TreeDataGrid.git"
DEFAULT_REF = "HEAD"
REQUIRED_MARKERS = (
    "docfx/articles/toc.yml",
    "docfx/api/toc.yml",
)
SPARSE_PATHS = (
    "docfx",
    "src",
    "Directory.Build.props",
    "NuGet.config",
)


def _codex_home() -> pathlib.Path:
    env_value = os.environ.get("CODEX_HOME")
    if env_value:
        return pathlib.Path(env_value).expanduser().resolve()
    return pathlib.Path.home() / ".codex"


def _default_cache_dir() -> pathlib.Path:
    return _codex_home() / "cache" / "treedatagrid-docfx"


def _normalize_docs_root(path: pathlib.Path) -> pathlib.Path:
    resolved = path.expanduser().resolve()
    if (resolved / "docfx").is_dir():
        return resolved
    if resolved.name == "docfx" and resolved.is_dir():
        return resolved.parent
    return resolved


def _missing_markers(docs_root: pathlib.Path) -> list[str]:
    missing: list[str] = []
    for marker in REQUIRED_MARKERS:
        if not (docs_root / marker).exists():
            missing.append(marker)
    return missing


def _has_treedatagrid_signature(docs_root: pathlib.Path) -> bool:
    # Generated API output marker.
    if (docs_root / "docfx" / "api" / "Avalonia.Controls.TreeDataGrid.yml").exists():
        return True

    # Source metadata marker from docfx config.
    docfx_json = docs_root / "docfx" / "docfx.json"
    if docfx_json.exists():
        text = docfx_json.read_text(encoding="utf-8", errors="ignore")
        if "Avalonia.Controls.TreeDataGrid.csproj" in text:
            return True

    return False


def _is_valid_docs_root(docs_root: pathlib.Path) -> bool:
    return not _missing_markers(docs_root) and _has_treedatagrid_signature(docs_root)


def _iter_self_and_parents(path: pathlib.Path) -> list[pathlib.Path]:
    resolved = path.expanduser().resolve()
    return [resolved, *resolved.parents]


def _candidate_roots(cache_dir: pathlib.Path) -> list[pathlib.Path]:
    candidates: list[pathlib.Path] = []

    env_docs_root = os.environ.get("TREE_DATAGRID_DOCS_ROOT")
    if env_docs_root:
        candidates.append(pathlib.Path(env_docs_root))

    candidates.extend(_iter_self_and_parents(pathlib.Path.cwd()))

    script_dir = pathlib.Path(__file__).resolve().parent
    candidates.extend(_iter_self_and_parents(script_dir))

    candidates.append(cache_dir)

    unique: list[pathlib.Path] = []
    seen: set[pathlib.Path] = set()
    for candidate in candidates:
        normalized = _normalize_docs_root(candidate)
        if normalized in seen:
            continue
        seen.add(normalized)
        unique.append(normalized)

    return unique


def _run_git(args: list[str], *, cwd: pathlib.Path | None = None) -> None:
    cmd = ["git", *args]
    completed = subprocess.run(
        cmd,
        cwd=str(cwd) if cwd else None,
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        stderr = completed.stderr.strip()
        stdout = completed.stdout.strip()
        details = stderr or stdout or "git command failed"
        raise RuntimeError(f"{' '.join(cmd)} failed: {details}")


def _run_command(cmd: list[str], *, cwd: pathlib.Path) -> tuple[int, str]:
    completed = subprocess.run(
        cmd,
        cwd=str(cwd),
        capture_output=True,
        text=True,
        check=False,
    )
    stderr = completed.stderr.strip()
    stdout = completed.stdout.strip()
    details = stderr or stdout
    return completed.returncode, details


def _generate_api_metadata(docs_root: pathlib.Path) -> None:
    api_toc = docs_root / "docfx" / "api" / "toc.yml"
    if api_toc.exists():
        return

    docfx_json = docs_root / "docfx" / "docfx.json"
    if not docfx_json.exists():
        raise RuntimeError(f"Missing {docfx_json}")

    commands = [
        ["docfx", "metadata", "docfx/docfx.json"],
        ["dotnet", "tool", "run", "docfx", "metadata", "docfx/docfx.json"],
    ]

    failures: list[str] = []
    for cmd in commands:
        try:
            code, details = _run_command(cmd, cwd=docs_root)
        except FileNotFoundError:
            failures.append(f"{' '.join(cmd)}: executable not found")
            continue

        if code == 0 and api_toc.exists():
            return

        failures.append(f"{' '.join(cmd)} failed: {details or f'exit code {code}'}")

    raise RuntimeError("; ".join(failures))


def _refresh_sparse_checkout(
    cache_dir: pathlib.Path,
    repo_url: str,
    ref: str,
    *,
    force_refresh: bool,
) -> pathlib.Path:
    if force_refresh and cache_dir.exists():
        shutil.rmtree(cache_dir)

    if (cache_dir / ".git").exists():
        _run_git(["remote", "set-url", "origin", repo_url], cwd=cache_dir)
        _run_git(["sparse-checkout", "init", "--cone"], cwd=cache_dir)
        _run_git(["sparse-checkout", "set", *SPARSE_PATHS], cwd=cache_dir)
        _run_git(["fetch", "--depth", "1", "origin", ref], cwd=cache_dir)
        _run_git(["checkout", "--force", "FETCH_HEAD"], cwd=cache_dir)
        return cache_dir

    if cache_dir.exists():
        shutil.rmtree(cache_dir)

    cache_dir.parent.mkdir(parents=True, exist_ok=True)
    _run_git(
        [
            "clone",
            "--depth",
            "1",
            "--filter=blob:none",
            "--sparse",
            repo_url,
            str(cache_dir),
        ]
    )
    _run_git(["sparse-checkout", "set", *SPARSE_PATHS], cwd=cache_dir)
    _run_git(["fetch", "--depth", "1", "origin", ref], cwd=cache_dir)
    _run_git(["checkout", "--force", "FETCH_HEAD"], cwd=cache_dir)
    return cache_dir


def _find_existing_docs_root(cache_dir: pathlib.Path) -> pathlib.Path | None:
    for candidate in _candidate_roots(cache_dir):
        if _is_valid_docs_root(candidate):
            return candidate
    return None


def _shell_export(path: pathlib.Path) -> str:
    escaped = str(path).replace("'", "'\"'\"'")
    return f"export TREE_DATAGRID_DOCS_ROOT='{escaped}'"


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Ensure TreeDataGrid DocFX articles and API YAML are available locally. "
            "Uses local docs when present, otherwise fetches required paths with sparse checkout "
            "and generates API metadata when needed."
        )
    )
    parser.add_argument(
        "--docs-root",
        help="Path to a repo root (or docfx directory) that should contain DocFX docs.",
    )
    parser.add_argument(
        "--cache-dir",
        help="Cache directory for fetched docs (default: $CODEX_HOME/cache/treedatagrid-docfx).",
    )
    parser.add_argument(
        "--repo-url",
        default=os.environ.get("TREE_DATAGRID_DOCS_REPO_URL", DEFAULT_REPO_URL),
        help="Git repository URL used when fetch is required.",
    )
    parser.add_argument(
        "--ref",
        default=os.environ.get("TREE_DATAGRID_DOCS_REF", DEFAULT_REF),
        help="Git ref used for fetch (branch, tag, or commit).",
    )
    parser.add_argument(
        "--no-fetch",
        action="store_true",
        help="Fail instead of fetching when docs are missing.",
    )
    parser.add_argument(
        "--refresh",
        action="store_true",
        help="Force refresh cached checkout before resolving docs.",
    )
    parser.add_argument(
        "--print-export",
        action="store_true",
        help="Print an export command for TREE_DATAGRID_DOCS_ROOT (for eval).",
    )
    return parser


def main() -> int:
    parser = _build_parser()
    args = parser.parse_args()

    cache_dir = (
        pathlib.Path(args.cache_dir).expanduser().resolve()
        if args.cache_dir
        else _default_cache_dir()
    )

    if args.docs_root:
        explicit_root = _normalize_docs_root(pathlib.Path(args.docs_root))
        if _is_valid_docs_root(explicit_root):
            if args.print_export:
                print(_shell_export(explicit_root))
            else:
                print(explicit_root)
            return 0

        if args.no_fetch:
            missing = _missing_markers(explicit_root)
            print("DocFX docs not found in --docs-root and --no-fetch is set.", file=sys.stderr)
            if missing:
                print(
                    f"Missing under {explicit_root}: {', '.join(missing)}",
                    file=sys.stderr,
                )
            if not _has_treedatagrid_signature(explicit_root):
                print(
                    f"{explicit_root} does not look like TreeDataGrid DocFX docs.",
                    file=sys.stderr,
                )
            return 1
    else:
        existing_root = _find_existing_docs_root(cache_dir)
        if existing_root and not (args.refresh and existing_root == cache_dir):
            if args.print_export:
                print(_shell_export(existing_root))
            else:
                print(existing_root)
            return 0

        if args.no_fetch:
            print("DocFX docs not found locally and --no-fetch is set.", file=sys.stderr)
            return 1

    try:
        docs_root = _refresh_sparse_checkout(
            cache_dir,
            args.repo_url,
            args.ref,
            force_refresh=args.refresh,
        )
    except Exception as exc:  # pragma: no cover - process wrapper
        print(f"Failed to fetch TreeDataGrid docs: {exc}", file=sys.stderr)
        print(
            "Set TREE_DATAGRID_DOCS_ROOT to a local repository path with docfx docs.",
            file=sys.stderr,
        )
        return 2

    if not _has_treedatagrid_signature(docs_root):
        print(
            (
                "Fetched checkout does not look like TreeDataGrid docs. "
                "Set TREE_DATAGRID_DOCS_REPO_URL to a repository that contains "
                "TreeDataGrid docfx content."
            ),
            file=sys.stderr,
        )
        return 2

    if not (docs_root / "docfx" / "api" / "toc.yml").exists():
        try:
            _generate_api_metadata(docs_root)
        except Exception as exc:  # pragma: no cover - process wrapper
            print(f"Failed to generate DocFX API metadata: {exc}", file=sys.stderr)
            return 2

    missing = _missing_markers(docs_root)
    if missing:
        print(
            f"Fetched checkout missing required files: {', '.join(missing)}",
            file=sys.stderr,
        )
        return 2

    if args.print_export:
        print(_shell_export(docs_root))
    else:
        print(docs_root)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
