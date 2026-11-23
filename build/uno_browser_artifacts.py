#!/usr/bin/env python3
"""Fingerprint one published consumer bundle and require complete three-engine evidence."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from typing import Any

MANIFEST = 'consumer-manifest.json'
ENGINES = ('chromium', 'firefox', 'webkit')
CASES = ('showcase', 'monitor', 'showcase-input-scale-1', 'showcase-input-scale-2')


def load(path: Path) -> dict[str, Any]:
    def unique(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result = {}
        for name, value in pairs:
            if name in result: raise ValueError(f'Duplicate JSON key: {name}')
            result[name] = value
        return result
    result = json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique)
    if not isinstance(result, dict): raise ValueError(f'Expected an object: {path}')
    return result


def fingerprint(root: Path) -> dict[str, str]:
    root = root.resolve(strict=True)
    files = {}
    for path in sorted(root.rglob('*')):
        if path.is_symlink(): raise ValueError(f'Consumer bundle contains a symlink: {path}')
        if not path.is_file() or path == root / MANIFEST: continue
        with path.open('rb') as stream:
            files[path.relative_to(root).as_posix()] = hashlib.file_digest(stream, 'sha256').hexdigest()
    if not files: raise ValueError('Consumer bundle is empty.')
    for sample in ('showcase', 'monitor'):
        if not any(name in files for name in (f'{sample}/index.html', f'{sample}/wwwroot/index.html')):
            raise ValueError(f'No published {sample} entry point.')
    return files


def write_manifest(root: Path, revision: str) -> dict[str, Any]:
    if not re.fullmatch('[0-9a-f]{40}', revision): raise ValueError('Expected an exact Git revision.')
    manifest = {'schemaVersion': 1, 'revision': revision, 'files': fingerprint(root)}
    (root / MANIFEST).write_text(json.dumps(manifest, sort_keys=True, indent=2) + '\n', encoding='utf-8')
    return manifest


def verify_bundle(root: Path, revision: str) -> dict[str, Any]:
    manifest_path = root / MANIFEST
    manifest = load(manifest_path)
    if manifest.get('schemaVersion') != 1 or manifest.get('revision') != revision:
        raise ValueError('Published consumer revision/schema does not match the checked-out validation source.')
    actual = fingerprint(root)
    if manifest.get('files') != actual:
        raise ValueError('Published consumer content was added, removed or changed.')
    return {'revision': revision, 'manifestSha256': hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
            'files': len(actual)}


def verify_matrix(reports: Path, identity: dict[str, Any]) -> dict[str, Any]:
    from uno_browser_input import STAGES
    engines = []
    for engine in ENGINES:
        directory = reports / engine
        if load(directory / 'consumer-identity.json') != identity:
            raise ValueError(f'{engine} did not validate the identical published bundle.')
        summary = load(directory / 'summary.json')
        if summary.get('browserEngine') != engine or summary.get('revision') != identity['revision']:
            raise ValueError(f'{engine} revision or engine identity mismatch.')
        if summary.get('passed') is not True or not isinstance(summary.get('browserVersion'), str) or not summary['browserVersion']:
            raise ValueError(f'{engine} did not complete successfully.')
        results = summary.get('results')
        if not isinstance(results, list) or len(results) != len(CASES) or [r.get('name') for r in results] != list(CASES):
            raise ValueError(f'{engine} consumer/input case set is incomplete or reordered.')
        for result in results:
            if result.get('passed') is not True or result.get('failures') != [] or result.get('canvasCount', 0) < 1 or result.get('crossOriginIsolated') is not True:
                raise ValueError(f'{engine}/{result.get("name")} failed native browser acceptance.')
            state = result.get('result')
            if not isinstance(state, dict) or state.get('complete') is not True or state.get('passed') is not True:
                raise ValueError(f'{engine} has incomplete application assertions.')
            if result['name'].startswith('showcase-input'):
                scale = int(result['name'][-1])
                if result.get('deviceScaleFactor') != scale or result.get('inputSteps') != list(STAGES):
                    raise ValueError(f'{engine} input actions or DPI evidence are incomplete.')
        engines.append({'engine': engine, 'version': summary['browserVersion'], 'cases': len(results)})
    return {'schemaVersion': 1, 'passed': True, 'consumer': identity, 'engines': engines,
            'cases': len(ENGINES) * len(CASES), 'completePlatformParityProven': False,
            'scope': 'Identical trimmed NuGet consumers, Playwright engines, native assertions and browser-dispatched input at DPR 1/2; not physical hardware, branded Safari/Firefox, OS IME or external screen-reader acceptance.'}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('create', 'verify', 'matrix'))
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--reports', type=Path)
    args = parser.parse_args()
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    if args.command == 'create': result = write_manifest(args.bundle, revision)
    else:
        result = verify_bundle(args.bundle, revision)
        if args.command == 'matrix':
            if args.reports is None: parser.error('--reports is required for matrix.')
            result = verify_matrix(args.reports, result)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, sort_keys=True, indent=2) + '\n', encoding='utf-8')
    print('UNO_BROWSER_ARTIFACTS=' + json.dumps({key: value for key, value in result.items() if key != 'files'}), flush=True)
    return 0


if __name__ == '__main__': raise SystemExit(main())
