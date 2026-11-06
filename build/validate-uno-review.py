#!/usr/bin/env python3
"""Join actual test, native-feature and raw API evidence without waiving parity gaps."""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

SUITES = ('core', 'uno', 'avalonia', 'sample-state', 'contract-parity')
STAGES = (*SUITES, 'native', 'native-smoke', 'activity', 'activity-smoke', 'isolated-suites',
          'avalonia-api-build', 'api-audit', 'api-self-check', 'parity-review-tests', 'parity-review',
          'browser-validator-tests', 'review-tool-tests', 'review-regressions')
MARKER = 'UNO_RUNTIME_DECLARATIVE_OWNERSHIP_REVIEW_PASSED'
PRESENTER_MARKER = 'UNO_RUNTIME_REVIEW_PRESENTER_CALLBACKS_PASSED'


def verify_trx(path: Path) -> int:
    tree = ET.parse(path)
    counters = tree.find('.//{*}ResultSummary/{*}Counters')
    if counters is None:
        raise ValueError('TRX has no execution counters')
    values = {key: int(value) for key, value in counters.attrib.items()}
    total = values.get('total', 0)
    if total <= 0 or values.get('executed') != total or values.get('passed') != total:
        raise ValueError('TRX did not execute and pass every case')
    if any(value != 0 for key, value in values.items() if key not in ('total', 'executed', 'passed')):
        raise ValueError('TRX contains nonpassing or skipped outcomes')
    results = tree.findall('.//{*}Results/{*}UnitTestResult')
    if len(results) != total or any(result.get('outcome') != 'Passed' for result in results):
        raise ValueError('TRX result records disagree with counters')
    ids = [result.get('executionId') for result in results]
    if None in ids or len(set(ids)) != total:
        raise ValueError('TRX contains missing or duplicate execution identities')
    return total


def verify_native(folder: Path, expected: list[str]) -> list[dict]:
    if not expected or len(set(expected)) != len(expected):
        raise ValueError('Native registrations are empty or duplicated')
    document = json.loads((folder / 'summary.json').read_text())
    rows = document['results']
    names = [row['suite'] for row in rows]
    if len(names) != len(expected) or set(names) != set(expected):
        raise ValueError('Native result set does not equal all current registrations')
    if document['registered'] != len(expected) or document['passed'] != len(expected):
        raise ValueError('Native summary counts do not reconcile')
    result = []
    for row in rows:
        log = (folder / row['log']).resolve()
        if not log.is_relative_to(folder.resolve()):
            raise ValueError('Native log is outside its evidence directory')
        if row['passed'] is not True or type(row['exitCode']) is not int or row['exitCode'] != 0:
            raise ValueError('Native suite did not pass')
        text = log.read_text(errors='replace')
        if f"UNO_SUITE_PASSED: {row['suite']};" not in text:
            raise ValueError('Native pass has no matching execution marker')
        result.append({'suite': row['suite'], 'log': row['log'],
                       'sha256': hashlib.sha256(log.read_bytes()).hexdigest()})
    return result


def verify_self_audit(summary: dict) -> None:
    if summary.get('schemaVersion') != 7:
        raise ValueError('Unexpected API audit schema')
    for key in ('unresolvedBaselineTypes', 'unresolvedTargetTypes'):
        if summary.get(key) != []:
            raise ValueError('Missing or unresolved self-audit dependency evidence')
    count = summary['targetShapes']
    if count <= 0 or summary['baselineShapes'] != count or summary['exactNormalizedMatches'] != count:
        raise ValueError('Self-audit declared inventory does not reconcile')
    if summary['missingOrDifferent'] != 0 or summary['additionalOrDifferent'] != 0:
        raise ValueError('Self-audit has declared differences')
    supplemental = summary['supplementalMetadata']
    if supplemental['TargetEntries'] <= 0 or not (
        supplemental['BaselineEntries'] == supplemental['TargetEntries'] == supplemental['ExactMatches']
        and supplemental['MissingOrDifferent'] == supplemental['AdditionalOrDifferent'] == 0):
        raise ValueError('Self-audit supplemental inventory does not reconcile')


def verify_preservation(report: dict, negative: dict) -> None:
    if report.get('declaredRegressionGatePassed') is not True:
        raise ValueError('The compiled declared API preservation gate did not pass')
    for key in ('removedOrChangedTargetShapes', 'rewrittenExistingRecords', 'lostReferenceMatches'):
        if report.get(key) != []:
            raise ValueError('An existing declared contract regressed')
    if negative.get('declaredRegressionGatePassed') is not False or not negative.get('lostReferenceMatches'):
        raise ValueError('The same-count removed-export control was not rejected')


def collect(root: Path, artifacts: Path) -> dict:
    outcomes = json.loads((artifacts / 'validation-outcomes.json').read_text())
    if any(type(outcomes.get(name)) is not int or outcomes[name] != 0 for name in STAGES):
        raise ValueError('A required functional or tooling stage did not pass')
    inputs: dict[str, str] = {}
    tests = {}
    for name in SUITES:
        paths = list((artifacts / (name + '-tests')).glob('*.trx'))
        if len(paths) != 1:
            raise ValueError(f'Expected one fresh {name} TRX, found {len(paths)}')
        tests[name] = verify_trx(paths[0])
        inputs[str(paths[0].relative_to(artifacts))] = hashlib.sha256(paths[0].read_bytes()).hexdigest()
    expected = re.findall(r'case "([a-z-]+)":', (root / 'samples/TreeDataGridUnoSample/App.Validation.cs').read_text())
    native = verify_native(artifacts / 'uno-native-suites', expected)
    for marker, suite in ((MARKER, 'declarative'), (PRESENTER_MARKER, 'appearance')):
        for path in (artifacts / f'uno-native-suites/{suite}/runtime.log', artifacts / 'logs/native-smoke.log'):
            if marker not in path.read_text(errors='replace'):
                raise ValueError('Review scenario did not execute in isolated and sequential modes: ' + marker)
    preservation = json.loads((artifacts / 'review-regressions/api-preservation.json').read_text())
    negative = json.loads((artifacts / 'review-regressions/removed-export-control.json').read_text())
    verify_preservation(preservation, negative)
    regression = json.loads((artifacts / 'review-regressions/summary.json').read_text())
    if regression.get('passed') is not True:
        raise ValueError('Review regression reproduction did not pass')
    for name in ('api-preservation', 'removed-export-control', 'summary'):
        path = artifacts / f'review-regressions/{name}.json'
        inputs[str(path.relative_to(artifacts))] = hashlib.sha256(path.read_bytes()).hexdigest()
    audit_path = artifacts / 'api-audit'
    documents = {}
    for name in ('avalonia', 'uno', 'classified-differences', 'summary'):
        path = audit_path / (name + '.json')
        documents[name] = json.loads(path.read_text())
        inputs[str(path.relative_to(artifacts))] = hashlib.sha256(path.read_bytes()).hexdigest()
    spec = importlib.util.spec_from_file_location('review_audit', root / 'build/audit-uno-parity.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    reviewed = module.review(documents['avalonia'], documents['uno'], documents['classified-differences'], documents['summary'])
    self_summary = json.loads((artifacts / 'api-self-check/summary.json').read_text())
    verify_self_audit(self_summary)
    if self_summary['targetShapes'] != documents['summary']['targetShapes']:
        raise ValueError('Self-check and cross-framework target inventories differ')
    return {
        'schemaVersion': 1,
        'revision': subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip(),
        'testCases': tests, 'totalDotnetCases': sum(tests.values()),
        'nativeSuites': native, 'nativeSuiteCount': len(native), 'inputSha256': inputs,
        'rawApiCounts': reviewed['rawCounts'], 'rawDifferencesPreserved': reviewed['rawDifferencesPreserved'],
        'functionalEvidenceVerified': True, 'strictSelfAuditVerified': True,
        'declaredApiPreservationVerified': True, 'removedExportControlRejected': True,
        'completeApiParityProven': False, 'completeFeatureParityProven': False,
        'completePerformanceParityProven': False,
        'boundary': 'All registered native suites and five executed test assemblies reconcile. Declared preservation rejects removal of existing exports, but does not waive unresolved cross-framework or supplemental differences. Not proof of untested features, physical input/IME, external accessibility, all platform heads, or the separate 1.10 native performance gate.'
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--artifacts', type=Path, default=Path('artifacts'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    report = collect(root, args.artifacts.resolve())
    path = args.artifacts / 'review-evidence.json'
    path.write_text(json.dumps(report, indent=2) + '\n')
    print('UNO_PR_REVIEW_EVIDENCE=' + json.dumps({key: report[key] for key in
        ('revision', 'testCases', 'totalDotnetCases', 'nativeSuiteCount', 'rawApiCounts',
         'functionalEvidenceVerified', 'strictSelfAuditVerified', 'declaredApiPreservationVerified',
         'removedExportControlRejected', 'completeFeatureParityProven')}))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
