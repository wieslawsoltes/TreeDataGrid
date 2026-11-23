#!/usr/bin/env python3
"""Measure raw text queries, prove regressions, and reconcile six API declarations.

Runs identical public workloads against exact source revisions in ABBA order.
The production API reader and normalization remain unchanged. A successful
collection is not the independent native-grid performance acceptance gate.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import statistics
import subprocess
import tempfile
import xml.etree.ElementTree as ET

WORKLOADS = {
    "live-int-raw": 9, "core-int-raw": 9, "live-decimal-raw": 10,
    "live-nullable-raw": 9, "core-nullable-null": -1, "live-string-raw": 11,
    "core-object-raw": 9, "live-int-formatted": 11, "core-int-formatted": 11,
    "scalar-int-control": 9,
}
FIXTURES = {
    "tests/TreeDataGrid.Parity.Tests/DeclaredDefinitionPolicyParityTests.cs": "parity",
    "tests/TreeDataGrid.Parity.Tests/UnformattedBoundTextParityTests.cs": "parity",
    "tests/TreeDataGrid.Uno.Tests/UnformattedBoundTextTests.cs": "uno",
}
PROJECTS = {
    "parity": ("tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj",
               "FullyQualifiedName~DeclaredDefinitionPolicyParityTests|FullyQualifiedName~UnformattedBoundTextParityTests", 38),
    "uno": ("tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj",
            "FullyQualifiedName~UnformattedBoundTextTests", 11),
}
EXPECTED = {"P:UI.Controls.TreeDataGridColumn." + name for name in (
    "CanUserResize", "CanUserSortColumn", "AllowTriStateSorting",
    "CompareAscending", "CompareDescending", "BeginEditGestures")}


def reconcile(before: Path, after: Path) -> dict:
    documents = {label: {name: json.loads((folder / (name + ".json")).read_text())
                        for name in ("summary", "avalonia", "uno")}
                 for label, folder in (("baseline", before), ("candidate", after))}
    for label, docs in documents.items():
        summary = docs["summary"]
        if summary["schemaVersion"] != 7 or summary["unresolvedBaselineTypes"] or summary["unresolvedTargetTypes"]:
            raise ValueError("Unresolved dependencies or wrong schema: " + label)
        scope = summary["scopeAccounting"]
        if not scope["ScopeCountsAreAdditive"] or scope["normalizationCollisions"]:
            raise ValueError("Invalid scope accounting: " + label)
        for name in ("avalonia", "uno"):
            entries = docs[name]["Entries"]
            if docs[name]["UnresolvedTypes"] or len({entry["Normalized"] for entry in entries}) != len(entries):
                raise ValueError("Unresolved or ambiguous inventory: " + label + "/" + name)
    for policy in ("schemaVersion", "mode", "namespaceMappings", "namespaceNormalization", "declaredInterfaceOrdering"):
        if documents["baseline"]["summary"][policy] != documents["candidate"]["summary"][policy]:
            raise ValueError("Changed audit policy: " + policy)
    if documents["baseline"]["avalonia"]["Entries"] != documents["candidate"]["avalonia"]["Entries"]:
        raise ValueError("Changed baseline declaration records")
    reference = {entry["Normalized"]: entry for entry in documents["baseline"]["avalonia"]["Entries"]}
    targets = {label: {entry["Normalized"]: entry for entry in docs["uno"]["Entries"]}
               for label, docs in documents.items()}
    old, new = targets["baseline"], targets["candidate"]
    for shape, entry in old.items():
        if new.get(shape) != entry:
            raise ValueError("Removed or modified existing target declaration: " + entry["Identity"])
    added = new.keys() - old.keys()
    if len(added) != 6 or added - reference.keys() or {reference[key]["Identity"] for key in added} != EXPECTED:
        raise ValueError("Added declarations are not exactly the six expected reference shapes")
    for label, entries in targets.items():
        computed = {"baselineShapes": len(reference), "targetShapes": len(entries),
                    "exactNormalizedMatches": len(reference.keys() & entries.keys()),
                    "missingOrDifferent": len(reference.keys() - entries.keys()),
                    "additionalOrDifferent": len(entries.keys() - reference.keys())}
        if any(documents[label]["summary"][key] != value for key, value in computed.items()):
            raise ValueError("Summary does not reconcile with complete declared inventories: " + label)
    return {
        "resolved": 6, "beforeMissing": len(reference.keys() - old.keys()),
        "afterMissing": len(reference.keys() - new.keys()),
        "newlyMissing": [], "allPreviousRawTargetRecordsUnchanged": True,
        "auditPoliciesChanged": False, "completeApiParityProven": False,
        "resolutions": [{"reference": reference[key], "target": new[key]} for key in sorted(added)],
        "scope": "Headless compiled declared surfaces; canonical native-target inventory is validated separately. Six declaration owners, not six new runtime features or acceptance of remaining differences.",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--candidate", default="HEAD")
    parser.add_argument("--output", type=Path, default=Path("artifacts/unformatted-text"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)

    def git(*arguments: str) -> bytes:
        return subprocess.check_output(["git", "-C", str(root), *arguments])

    def execute(name: str, command: list[str], cwd: Path, env: dict | None = None, checked: bool = True) -> int:
        with (output / (name + ".log")).open("w") as log:
            completed = subprocess.run(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=360)
        print("UNO_RAW_TEXT_STAGE=" + json.dumps({"stage": name, "exitCode": completed.returncode}), flush=True)
        if checked and completed.returncode:
            raise subprocess.CalledProcessError(completed.returncode, command)
        return completed.returncode

    revisions = {label: git("rev-parse", ref + "^{commit}").decode().strip()
                 for label, ref in (("baseline", args.baseline), ("candidate", args.candidate))}
    if len(set(revisions.values())) != 2:
        raise ValueError("Distinct source revisions are required")
    harness_path = "benchmarks/TreeDataGrid.UnformattedText/"
    files = ("TreeDataGrid.UnformattedText.csproj", "Program.cs")
    harness = {name: git("show", revisions["candidate"] + ":" + harness_path + name) for name in files}
    tests = {path: git("show", revisions["candidate"] + ":" + path) for path in FIXTURES}
    reader_trees = {label: git("rev-parse", ref + ":tools/TreeDataGrid.ApiAudit").decode().strip()
                    for label, ref in revisions.items()}
    if len(set(reader_trees.values())) != 1:
        raise ValueError("Production API reader changed between inputs")
    provenance = {
        "revisions": revisions, "processOrder": ["baseline", "candidate", "candidate", "baseline"],
        "harnessSha256": {path: hashlib.sha256(data).hexdigest() for path, data in harness.items()},
        "fixtureSha256": {path: hashlib.sha256(data).hexdigest() for path, data in tests.items()},
        "collectorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "apiReaderTrees": reader_trees,
        "runtimePolicy": "Inherited environment; no tiered-compilation or ReadyToRun override",
    }
    (output / "input.json").write_text(json.dumps(provenance, indent=2) + "\n")
    reports = {label: [] for label in revisions}
    regression = {label: {} for label in revisions}
    fingerprints = {}
    matching_environment = None
    try:
        with tempfile.TemporaryDirectory(prefix="tdg-raw-text-") as scratch_name:
            scratch = Path(scratch_name)
            worktrees = []
            hosts = {}
            try:
                for label, revision in revisions.items():
                    tree = scratch / (label + "-source")
                    git("worktree", "add", "--detach", str(tree), revision)
                    worktrees.append(tree)
                    host = scratch / (label + "-harness")
                    host.mkdir(); hosts[label] = host
                    for name, content in harness.items():
                        (host / name).write_bytes(content)
                    for name in ("global.json", "NuGet.config"):
                        (host / name).write_bytes(git("show", revisions["candidate"] + ":" + name))
                    execute(label + "-build", ["dotnet", "build", str(host / files[0]), "-c", "Release",
                        "-p:TreeDataGridSourceRoot=" + str(tree), "-p:TreeDataGridUnoTargetFrameworks=net10.0"], tree)
                    copied = []
                    try:
                        for path, data in tests.items():
                            destination = tree / path
                            if label == "baseline":
                                if destination.exists():
                                    raise ValueError("Baseline already contains new fixture: " + path)
                                destination.write_bytes(data); copied.append(destination)
                            elif destination.read_bytes() != data:
                                raise ValueError("Candidate fixture mismatch")
                        for group, (project, test_filter, expected_count) in PROJECTS.items():
                            result_path = output / (label + "-" + group)
                            code = execute(label + "-" + group + "-tests", ["dotnet", "test", project, "-c", "Release",
                                "-p:TreeDataGridUnoTargetFrameworks=net10.0", "--filter", test_filter,
                                "--logger", "trx;LogFileName=results.trx", "--results-directory", str(result_path)], tree, checked=False)
                            document = ET.parse(result_path / "results.trx")
                            counter = document.find(".//{*}Counters")
                            if counter is None:
                                raise ValueError("Missing test counters")
                            counts = {key: int(value) for key, value in counter.attrib.items()}
                            if counts["total"] != expected_count or counts["executed"] != expected_count or counts["notExecuted"]:
                                raise ValueError("Incomplete or skipped regression cases")
                            if label == "candidate" and (code != 0 or counts["failed"] or counts["passed"] != expected_count):
                                raise ValueError("Candidate regression failed")
                            if label == "baseline" and code != (1 if counts["failed"] else 0):
                                raise ValueError("Baseline exit was not a test-result exit")
                            regression[label][group] = {"exitCode": code, "counters": counts,
                                "failures": [node.attrib.get("testName") for node in document.findall(".//{*}UnitTestResult")
                                             if node.attrib.get("outcome") != "Passed"]}
                    finally:
                        for path in copied:
                            path.unlink()
                    # Both test projects have built the real reference and native
                    # runtime libraries, with complete dependency copies beside them.
                    env = dict(os.environ)
                    references = str(tree / "tests/TreeDataGrid.Parity.Tests/bin/Release/net10.0")
                    env["TREEDATAGRID_API_BASELINE_REFERENCES"] = references
                    env["TREEDATAGRID_API_TARGET_REFERENCES"] = references
                    execute(label + "-api", ["dotnet", "run", "--project", "tools/TreeDataGrid.ApiAudit", "-c", "Release", "--",
                        "src/TreeDataGrid.Avalonia/bin/Release/net8.0/TreeDataGrid.Avalonia.dll",
                        "src/TreeDataGrid.Controls.Uno/bin/Release/net10.0/TreeDataGrid.Controls.Uno.dll",
                        "src/TreeDataGrid.Core/bin/Release/net8.0/TreeDataGrid.Core.dll", str(output / (label + "-api"))], tree, env)
                if not any(value["counters"]["failed"] for value in regression["baseline"].values()):
                    raise ValueError("No executed baseline regression failure")
                api = reconcile(output / "baseline-api", output / "candidate-api")
                (output / "api-delta.json").write_text(json.dumps(api, indent=2) + "\n")
                print("UNO_DECLARATIVE_POLICY_DELTA=" + json.dumps({key: api[key] for key in (
                    "resolved", "beforeMissing", "afterMissing", "allPreviousRawTargetRecordsUnchanged")}), flush=True)
                for ordinal, label in enumerate(provenance["processOrder"]):
                    path = output / f"{ordinal:02d}-{label}.json"
                    execute(f"{ordinal:02d}-{label}", ["dotnet", str(hosts[label] / "bin/Release/net10.0/TreeDataGrid.UnformattedText.dll"),
                        revisions[label], str(path)], hosts[label])
                    report = json.loads(path.read_text())
                    if report["revision"] != revisions[label] or len(report["samples"]) != len(WORKLOADS) * 25:
                        raise ValueError("Wrong revision or incomplete sample set")
                    environment = {key: report[key] for key in ("runtime", "os", "architecture", "serverGc",
                        "tieredCompilation", "readyToRun", "count", "warmups", "sampleCount", "scope")}
                    if matching_environment is None:
                        matching_environment = environment
                    if environment != matching_environment:
                        raise ValueError("Runtime or workload policy mismatch")
                    if fingerprints.setdefault(label, report["librarySha256"]) != report["librarySha256"]:
                        raise ValueError("Library changed between measured processes")
                    for operation, length in WORKLOADS.items():
                        values = [sample for sample in report["samples"] if sample["Operation"] == operation]
                        if sorted(sample["Iteration"] for sample in values) != list(range(25)):
                            raise ValueError("Missing or duplicated iterations")
                        for sample in values:
                            if sample["Count"] != 8192 or sample["Checksum"] != 8192 * length:
                                raise ValueError("Incorrect executed work: " + operation)
                            if not math.isfinite(sample["Milliseconds"]) or sample["Milliseconds"] < 0 or sample["AllocatedBytes"] < 0:
                                raise ValueError("Invalid measurement")
                    reports[label].append(report)
                for tree in worktrees:
                    subprocess.run(["git", "-C", str(tree), "diff", "--exit-code"], check=True)
                    subprocess.run(["git", "-C", str(tree), "diff", "--cached", "--exit-code"], check=True)
                comparisons = []
                for operation in WORKLOADS:
                    entry = {"operation": operation}
                    for metric in ("Milliseconds", "AllocatedBytes"):
                        values = {label: [sample[metric] / sample["Count"] for report in runs for sample in report["samples"]
                                          if sample["Operation"] == operation] for label, runs in reports.items()}
                        medians = {label: statistics.median(data) for label, data in values.items()}
                        entry[metric + "PerOperation"] = {"samplesPerRevision": 50, "median": medians,
                            "candidateOverBaseline": medians["candidate"] / medians["baseline"] if medians["baseline"] else None,
                            "p95": {label: sorted(data)[math.ceil(.95 * len(data)) - 1] for label, data in values.items()},
                            "perPassMedians": {label: [statistics.median(sample[metric] / sample["Count"] for sample in report["samples"]
                                if sample["Operation"] == operation) for report in runs] for label, runs in reports.items()}}
                    comparisons.append(entry)
                result = {"schemaVersion": 1, **provenance, "environment": matching_environment, "librarySha256": fingerprints,
                    "regressions": regression, "apiDelta": {key: api[key] for key in ("resolved", "beforeMissing", "afterMissing")},
                    "comparisons": comparisons, "collectionSucceeded": True, "completePerformanceParityProven": False,
                    "limitations": "All samples and control workloads retained. Pooled diagnostics are not confidence intervals, full causal attribution, native layout, construction/startup, GPU completion or frame rate."}
                (output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
                print("UNO_UNFORMATTED_TEXT_COMPARISON=" + json.dumps(result), flush=True)
                return 0
            finally:
                for tree in reversed(worktrees):
                    subprocess.run(["git", "-C", str(root), "worktree", "remove", "--force", str(tree)], check=False)
    except Exception as error:
        (output / "failure.json").write_text(json.dumps({"type": type(error).__name__, "error": str(error),
            "regressions": regression}, indent=2) + "\n")
        raise


if __name__ == "__main__":
    raise SystemExit(main())
