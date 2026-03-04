#!/usr/bin/env python3
"""Find DocFX API UID entries in docfx/api YAML files."""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from dataclasses import dataclass

UID_PATTERN = re.compile(r"^\s*(?:-\s*)?uid:\s*(.+?)\s*$")
NAME_PATTERN = re.compile(r"^\s*name:\s*(.+?)\s*$")
FULL_NAME_PATTERN = re.compile(r"^\s*fullName:\s*(.+?)\s*$")
TYPE_PATTERN = re.compile(r"^\s*type:\s*(.+?)\s*$")


@dataclass(frozen=True)
class ApiEntry:
    uid: str
    name: str
    full_name: str
    kind: str
    file_path: pathlib.Path
    line: int


def _strip_quotes(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def _default_api_dir() -> pathlib.Path:
    script_path = pathlib.Path(__file__).resolve()
    # Expected layout: <repo>/skills/<skill>/scripts/find_docfx_api.py
    if len(script_path.parents) >= 4:
        repo_root = script_path.parents[3]
        return repo_root / "docfx" / "api"
    return pathlib.Path.cwd() / "docfx" / "api"


def _resolve_api_dir(api_dir_arg: str | None) -> pathlib.Path:
    if api_dir_arg:
        return pathlib.Path(api_dir_arg).expanduser().resolve()

    default_dir = _default_api_dir()
    if default_dir.exists():
        return default_dir

    cwd_dir = pathlib.Path.cwd() / "docfx" / "api"
    if cwd_dir.exists():
        return cwd_dir.resolve()

    return default_dir


def _parse_entries(api_file: pathlib.Path) -> list[ApiEntry]:
    text = api_file.read_text(encoding="utf-8", errors="ignore")
    lines = text.splitlines()
    entries: list[ApiEntry] = []

    for idx, line in enumerate(lines):
        uid_match = UID_PATTERN.match(line)
        if not uid_match:
            continue

        uid = _strip_quotes(uid_match.group(1))
        name = ""
        full_name = ""
        kind = ""

        stop = min(len(lines), idx + 400)
        for j in range(idx + 1, stop):
            next_line = lines[j]
            if UID_PATTERN.match(next_line):
                break
            if not name:
                name_match = NAME_PATTERN.match(next_line)
                if name_match:
                    name = _strip_quotes(name_match.group(1))
            if not full_name:
                full_name_match = FULL_NAME_PATTERN.match(next_line)
                if full_name_match:
                    full_name = _strip_quotes(full_name_match.group(1))
            if not kind:
                type_match = TYPE_PATTERN.match(next_line)
                if type_match:
                    kind = _strip_quotes(type_match.group(1))

        entries.append(
            ApiEntry(
                uid=uid,
                name=name,
                full_name=full_name,
                kind=kind,
                file_path=api_file,
                line=idx + 1,
            )
        )

    return entries


def _score(entry: ApiEntry, query_cf: str) -> int:
    uid_cf = entry.uid.casefold()
    name_cf = entry.name.casefold()
    full_name_cf = entry.full_name.casefold()

    if uid_cf == query_cf:
        return 0
    if full_name_cf == query_cf or name_cf == query_cf:
        return 1
    if uid_cf.endswith(query_cf):
        return 2
    if query_cf in uid_cf:
        return 3
    if query_cf in full_name_cf:
        return 4
    if query_cf in name_cf:
        return 5
    return 999


def _to_display_path(path: pathlib.Path) -> str:
    try:
        return str(path.relative_to(pathlib.Path.cwd()))
    except ValueError:
        return str(path)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Find API UIDs in docfx/api YAML files (exact or fuzzy)."
    )
    parser.add_argument("query", help="UID, type name, or substring to search for.")
    parser.add_argument(
        "--api-dir",
        help="Path to the docfx API directory. Defaults to repo-local docfx/api.",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=20,
        help="Maximum number of matches to print (default: 20).",
    )
    parser.add_argument(
        "--exact",
        action="store_true",
        help="Match only exact UID values (case-insensitive).",
    )
    parser.add_argument(
        "--include-toc",
        action="store_true",
        help="Include docfx/api/toc.yml in search results (excluded by default).",
    )
    return parser


def main() -> int:
    parser = _build_parser()
    args = parser.parse_args()

    api_dir = _resolve_api_dir(args.api_dir)
    if not api_dir.exists():
        print(f"API directory not found: {api_dir}", file=sys.stderr)
        return 2

    api_files = sorted(api_dir.glob("*.yml"))
    if not args.include_toc:
        api_files = [path for path in api_files if path.name != "toc.yml"]
    if not api_files:
        print(f"No API YAML files found in: {api_dir}", file=sys.stderr)
        return 2

    entries: list[ApiEntry] = []
    for api_file in api_files:
        entries.extend(_parse_entries(api_file))

    query_cf = args.query.casefold()
    if args.exact:
        scored = [
            (entry, 0) for entry in entries if entry.uid.casefold() == query_cf
        ]
    else:
        scored: list[tuple[ApiEntry, int]] = []
        for entry in entries:
            score = _score(entry, query_cf)
            if score < 999:
                scored.append((entry, score))

    if any(entry.kind for entry, _ in scored):
        scored = [(entry, score) for entry, score in scored if entry.kind]

    # De-duplicate by UID and file; keep the best-ranked occurrence.
    best_by_uid_and_file: dict[tuple[str, pathlib.Path], tuple[ApiEntry, int]] = {}
    for entry, score in scored:
        key = (entry.uid, entry.file_path)
        previous = best_by_uid_and_file.get(key)
        if previous is None or (score, entry.line) < (previous[1], previous[0].line):
            best_by_uid_and_file[key] = (entry, score)

    expected_file_name = f"{args.query.replace('`', '-')}.yml"

    def _sort_key(entry: ApiEntry) -> tuple[int, int, int, str, int]:
        rank = 0 if args.exact else _score(entry, query_cf)
        file_priority = 0 if entry.file_path.name == expected_file_name else 1
        return (rank, file_priority, len(entry.uid), entry.uid.casefold(), entry.line)

    deduped = sorted(
        (entry for entry, _ in best_by_uid_and_file.values()),
        key=_sort_key,
    )

    if not deduped:
        print(
            f"No API matches found for '{args.query}' in {_to_display_path(api_dir)}.",
            file=sys.stderr,
        )
        return 1

    limit = max(1, args.limit)
    shown = deduped[:limit]

    print(f"Query: {args.query}")
    print(f"API dir: {_to_display_path(api_dir)}")
    print(f"Matches: {len(deduped)} (showing {len(shown)})")
    print("")

    for index, entry in enumerate(shown, start=1):
        print(f"{index}. uid: {entry.uid}")
        if entry.kind:
            print(f"   type: {entry.kind}")
        if entry.name:
            print(f"   name: {entry.name}")
        if entry.full_name:
            print(f"   fullName: {entry.full_name}")
        print(f"   file: {_to_display_path(entry.file_path)}:{entry.line}")
        print("")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
