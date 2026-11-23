#!/usr/bin/env python3
"""Verify that committed historical metrics match the independently derived hosts."""
from __future__ import annotations
import argparse
import json
import math
from pathlib import Path


def verify(checkpoint: dict, derived: dict, metadata: dict) -> None:
    if derived.get('collectorSummaryReconciled') is not True:
        raise ValueError('Raw evidence has not been reconciled')
    comparison = checkpoint['controlledComparison']
    if checkpoint['testedCommit'] != derived['sourceRevisions']['candidate'] or comparison['baseline'] != derived['sourceRevisions']['baseline']:
        raise ValueError('Committed source identities differ from executed hosts')
    trees = set(derived['apiReaderTrees'].values())
    if len(trees) != 1 or comparison['apiReaderSourceTreeBothInputs'] not in trees:
        raise ValueError('Committed API reader tree does not match both inputs')
    rows = {row['operation']: row for row in comparison['medians']}
    expected = {row['operation']: row for row in derived['comparisons']}
    if len(rows) != len(comparison['medians']) or set(rows) != set(expected):
        raise ValueError('Committed workload set is incomplete or duplicated')
    for name, row in rows.items():
        timings = expected[name]['MillisecondsPerOperation']['median']
        allocation = expected[name]['AllocatedBytesPerOperation']['median']
        for label in ('baseline', 'candidate'):
            # Allow only floating-point unit-conversion roundoff, not a display
            # rounding allowance. Allocation must match exactly.
            if not math.isclose(row[label + 'Ns'], timings[label] * 1e6, rel_tol=1e-14, abs_tol=1e-10):
                raise ValueError('Incorrect committed timing: ' + name)
            if row[label + 'Bytes'] != allocation[label]:
                raise ValueError('Incorrect committed allocation: ' + name)
    passes = expected['live-int-raw']['MillisecondsPerOperation']['perPassMedians']['candidate']
    recorded = comparison['liveIntegerCandidatePassMediansNs']
    if len(recorded) != len(passes) or any(not math.isclose(x, y * 1e6, rel_tol=1e-14, abs_tol=1e-10) for x, y in zip(recorded, passes)):
        raise ValueError('Incorrect committed live-integer pass medians')
    artifact = comparison['artifact']; actual = metadata['comparison']
    if artifact['id'] != actual['id'] or artifact['bytes'] != actual['size_in_bytes'] or artifact['sha256'] != actual['digest'].removeprefix('sha256:'):
        raise ValueError('Comparison artifact metadata differs')
    if artifact['files'] != derived['extractedFileCount']:
        raise ValueError('Comparison file count differs from extracted manifest')
    recorded_failure = checkpoint['initialFailurePreserved']['comparisonArtifact']; actual_failure = metadata['initialFailure']
    if recorded_failure != {'id': actual_failure['id'], 'bytes': actual_failure['size_in_bytes'], 'sha256': actual_failure['digest'].removeprefix('sha256:')}:
        raise ValueError('Initial-failure artifact metadata differs')


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkpoint', type=Path, required=True)
    parser.add_argument('--derived', type=Path, required=True)
    parser.add_argument('--metadata', type=Path, required=True)
    args = parser.parse_args()
    verify(*(json.loads(path.read_text()) for path in (args.checkpoint, args.derived, args.metadata)))
    print('UNO_COMMITTED_EVIDENCE_RECORD_PASSED: all medians, allocations, workloads, reader/revision identities, live-integer pass medians, extracted file count and both artifact records')
    return 0


if __name__ == '__main__': raise SystemExit(main())
