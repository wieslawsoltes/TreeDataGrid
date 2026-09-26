#!/usr/bin/env python3
"""Reconcile the nineteen direct portable API declarations against complete audits.

Consumes the production schema-7 reader's unchanged raw/normalized inventories;
does not normalize names itself, subtract candidates, or grant compatibility waivers.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


EXPECTED = {
    *("P:UI.Controls.Models.TreeDataGrid.ColumnOptions`1." + name for name in (
        "CanUserResizeColumn", "CanUserSortColumn", "CompareAscending", "CompareDescending")),
    *("P:UI.Controls.Models.TreeDataGrid.ColumnBase`1." + name for name in ("Header", "Tag", "SortDirection")),
    "P:UI.Controls.Models.TreeDataGrid.CheckBoxColumn`1.IsThreeState",
    *("P:UI.Controls.TreeDataGridItemsSourceView." + name for name in (
        "Count", "Inner", "Item(System.Int32)", "HasKeyIndexMapping")),
    "E:UI.Controls.TreeDataGridItemsSourceView.CollectionChanged",
    *("M:UI.Controls.TreeDataGridItemsSourceView." + name for name in (
        "GetAt(System.Int32)", "IndexOf(System.Object)", "KeyFromIndex(System.Int32)",
        "IndexFromKey(System.String)", "Dispose",
        "OnItemsSourceChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs)")),
}
POLICIES = ("schemaVersion", "mode", "namespaceMappings", "namespaceNormalization", "declaredInterfaceOrdering")


def reconcile(before: Path, after: Path) -> dict:
    documents = {phase: {name: json.loads((folder / (name + ".json")).read_text())
                         for name in ("summary", "avalonia", "uno")}
                 for phase, folder in (("before", before), ("after", after))}
    for phase, docs in documents.items():
        summary = docs["summary"]
        if summary["schemaVersion"] != 7 or summary["unresolvedBaselineTypes"] or summary["unresolvedTargetTypes"]:
            raise ValueError("Wrong audit schema or unresolved dependencies: " + phase)
        accounting = summary["scopeAccounting"]
        if not accounting["ScopeCountsAreAdditive"] or accounting["normalizationCollisions"]:
            raise ValueError("Invalid scope accounting: " + phase)
        for name in ("avalonia", "uno"):
            entries = docs[name]["Entries"]
            if docs[name]["UnresolvedTypes"] or len({e["Normalized"] for e in entries}) != len(entries):
                raise ValueError("Unresolved or ambiguous inventory: " + phase + "/" + name)
    for key in POLICIES:
        if documents["before"]["summary"][key] != documents["after"]["summary"][key]:
            raise ValueError("Audit policy changed: " + key)
    if documents["before"]["avalonia"]["Entries"] != documents["after"]["avalonia"]["Entries"]:
        raise ValueError("Complete reference declaration records changed")
    reference = {e["Normalized"]: e for e in documents["before"]["avalonia"]["Entries"]}
    targets = {phase: {e["Normalized"]: e for e in docs["uno"]["Entries"]} for phase, docs in documents.items()}
    missing = {phase: reference.keys() - entries.keys() for phase, entries in targets.items()}
    resolved = missing["before"] - missing["after"]
    if missing["after"] - missing["before"]:
        raise ValueError("Newly missing reference declarations")
    if targets["before"].keys() - targets["after"].keys():
        raise ValueError("Previously exported target declarations removed or changed")
    if targets["after"].keys() - targets["before"].keys() != resolved:
        raise ValueError("New target declarations are not exactly the resolved reference shapes")
    for shape, entry in targets["before"].items():
        if entry != targets["after"][shape]:
            raise ValueError("An existing complete raw target record changed: " + entry["Identity"])
    if {reference[shape]["Identity"] for shape in resolved} != EXPECTED or len(resolved) != 19:
        raise ValueError("Resolution does not equal the expected nineteen declaration identities")
    for phase, entries in targets.items():
        summary = documents[phase]["summary"]
        computed = {"baselineShapes": len(reference), "targetShapes": len(entries),
                    "exactNormalizedMatches": len(reference.keys() & entries.keys()),
                    "missingOrDifferent": len(missing[phase]),
                    "additionalOrDifferent": len(entries.keys() - reference.keys())}
        if any(summary[key] != value for key, value in computed.items()):
            raise ValueError("Declared summary does not match complete inventories: " + phase)
    return {
        "schemaVersion": 1, "resolvedDeclarationCount": len(resolved),
        "beforeMissing": len(missing["before"]), "afterMissing": len(missing["after"]),
        "newlyMissing": [], "removedTargetDeclarations": [], "auditPoliciesChanged": False,
        "allPreviousRawTargetRecordsUnchanged": True,
        "resolutions": [{"reference": reference[shape], "target": targets["after"][shape]} for shape in sorted(resolved)],
        "inputHashes": {phase: {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                               for path in (folder / (name + ".json") for name in ("summary", "avalonia", "uno"))}
                        for phase, folder in (("before", before), ("after", after))},
        "inputAssemblies": {phase: {name: docs[name]["Inputs"] for name in ("avalonia", "uno")}
                            for phase, docs in documents.items()},
        "completeApiParityProven": False,
        "scope": "Nineteen direct portable declarations forwarding existing Core/layout state. Not nineteen new runtime features, ABI proof or a waiver for remaining native/Core/inherited differences.",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", required=True, type=Path)
    parser.add_argument("--after", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    report = reconcile(args.before, args.after)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n")
    print("UNO_PORTABLE_FACADE_RECONCILIATION=" + json.dumps({
        "before": report["beforeMissing"], "after": report["afterMissing"],
        "resolved": report["resolvedDeclarationCount"], "newlyMissing": report["newlyMissing"]}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
