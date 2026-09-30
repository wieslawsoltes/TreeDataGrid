#!/usr/bin/env python3
"""Reject declared API regressions using complete schema-7 compiled inventories.

Existing cross-framework differences remain visible. This is preservation of the
previous exported contract, not full API, supplemental-metadata or behavioral parity.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re

POLICIES = ("schemaVersion", "mode", "namespaceMappings", "namespaceNormalization", "declaredInterfaceOrdering")
FIELDS = ("Assembly", "Kind", "Raw", "Normalized", "Identity", "DeclaringType", "MetadataName")
# Reference declarations added after explicit review. Only additions from shared
# TreeDataGrid.Core are accepted; any removal or change still requires a new baseline.
REVIEWED_REFERENCE_ADDITIONS = frozenset({
    # Source filtering moved from the Avalonia sources into the shared Core sources.
    "public System.Boolean TreeDataGridCore.FlatTreeDataGridSource<TModel>.IsFiltered { get; }",
    "public System.Boolean TreeDataGridCore.HierarchicalTreeDataGridSource<TModel>.IsFiltered { get; }",
    "public void TreeDataGridCore.FlatTreeDataGridSource<TModel>.Filter(System.Func<TModel, System.Boolean>? predicate)",
    "public void TreeDataGridCore.FlatTreeDataGridSource<TModel>.RefreshFilter()",
    "public void TreeDataGridCore.HierarchicalTreeDataGridSource<TModel>.Filter(System.Func<TModel, System.Boolean>? predicate)",
    "public void TreeDataGridCore.HierarchicalTreeDataGridSource<TModel>.RefreshFilter()",
})


def read_json(path: Path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON field: " + key)
            result[key] = value
        return result
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique)


def resolved(value, context):
    if not isinstance(value, list) or any(not isinstance(item, str) for item in value) or value:
        raise ValueError("Missing, malformed or unresolved dependencies: " + context)


def load_audit(directory: Path) -> dict:
    summary = read_json(directory / "summary.json")
    if type(summary["schemaVersion"]) is not int or summary["schemaVersion"] != 7:
        raise ValueError("Unsupported audit schema")
    for key in POLICIES:
        if key not in summary:
            raise ValueError("Missing audit policy: " + key)
    scope = summary["scopeAccounting"]
    if type(scope["normalizationCollisions"]) is not int or scope["normalizationCollisions"] != 0 or scope["ScopeCountsAreAdditive"] is not True:
        raise ValueError("Ambiguous or nonadditive scope accounting")
    for key in ("unresolvedBaselineTypes", "unresolvedTargetTypes"):
        resolved(summary[key], str(directory) + "/" + key)
    result = {"summary": summary, "hashes": {}, "inputs": {}}
    for name in ("avalonia", "uno"):
        path = directory / (name + ".json")
        document = read_json(path)
        resolved(document["UnresolvedTypes"], str(path))
        if not isinstance(document["Inputs"], list) or not document["Inputs"]:
            raise ValueError("Missing assembly provenance: " + str(path))
        names = set()
        for entry in document["Inputs"]:
            if not isinstance(entry["Name"], str) or not entry["Name"] or entry["Name"] in names:
                raise ValueError("Missing/duplicate input assembly name")
            names.add(entry["Name"])
            if not isinstance(entry["Sha256"], str) or re.fullmatch(r"[0-9a-fA-F]{64}", entry["Sha256"]) is None:
                raise ValueError("Invalid input assembly SHA256")
        if not isinstance(document["Entries"], list):
            raise ValueError("Missing declaration list")
        entries = {}
        for entry in document["Entries"]:
            if any(not isinstance(entry.get(key), str) or not entry[key] for key in FIELDS):
                raise ValueError("Malformed declaration record")
            if entry["Assembly"] not in names or entry["Normalized"] in entries:
                raise ValueError("Unknown declaring assembly or duplicate normalized declaration")
            entries[entry["Normalized"]] = entry
        result[name] = entries
        result["inputs"][name] = document["Inputs"]
        result["hashes"][name] = hashlib.sha256(path.read_bytes()).hexdigest()
    left, right = result["avalonia"].keys(), result["uno"].keys()
    counts = {"baselineShapes": len(left), "targetShapes": len(right),
              "exactNormalizedMatches": len(left & right), "missingOrDifferent": len(left - right),
              "additionalOrDifferent": len(right - left)}
    if any(type(summary[key]) is not int or summary[key] != value for key, value in counts.items()):
        raise ValueError("Summary differs from complete inventories")
    return result


def compare(before: dict, after: dict) -> dict:
    for key in POLICIES:
        if before["summary"][key] != after["summary"][key]:
            raise ValueError("Changed audit policy requires explicit baseline review: " + key)
    if before["avalonia"] != after["avalonia"]:
        old_reference, new_reference = before["avalonia"], after["avalonia"]
        added = new_reference.keys() - old_reference.keys()
        if (old_reference.keys() - new_reference.keys() or
                any(old_reference[key] != new_reference[key] for key in old_reference.keys() & new_reference.keys()) or
                not added <= REVIEWED_REFERENCE_ADDITIONS or
                any(new_reference[key]["Assembly"] != "TreeDataGrid.Core" for key in added)):
            raise ValueError("Reference declarations changed; explicit baseline review required")
    old, new, reference = before["uno"], after["uno"], before["avalonia"]
    removed = sorted(old.keys() - new.keys())
    rewritten = sorted(shape for shape in old.keys() & new.keys() if old[shape] != new[shape])
    lost = sorted((reference.keys() & old.keys()) - new.keys())
    return {
        "schemaVersion": 1,
        "declaredRegressionGatePassed": not removed and not rewritten and not lost,
        "removedOrChangedTargetShapes": removed, "rewrittenExistingRecords": rewritten,
        "lostReferenceMatches": lost,
        "newReferenceMatches": sorted((reference.keys() & new.keys()) - old.keys()),
        "addedTargetShapes": sorted(new.keys() - old.keys()),
        "remainingReferenceDifferences": len(reference.keys() - new.keys()),
        "provenance": {"before": {key: before[key] for key in ("hashes", "inputs")},
                       "after": {key: after[key] for key in ("hashes", "inputs")}},
        "completeApiParityProven": False, "behavioralParityProven": False,
        "supplementalMetadataWaived": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, required=True)
    parser.add_argument("--after", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = compare(load_audit(args.before), load_audit(args.after))
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        result = {"declaredRegressionGatePassed": False, "error": f"{type(error).__name__}: {error}"}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("UNO_DECLARED_REGRESSION_GATE=" + json.dumps(result), flush=True)
    return 0 if result["declaredRegressionGatePassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
