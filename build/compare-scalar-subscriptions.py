#!/usr/bin/env python3
"""Compare scalar observers without altering product inputs or native parity gates."""
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

WORKLOADS = {"raw-text-readonly": (17, 0), "raw-text-editable": (19, 1),
             "raw-checkbox-readonly": (1, 0), "raw-checkbox-editable": (1, 1),
             "typed-text-readonly": (17, 0), "typed-text-editable": (19, 1),
             "typed-checkbox-readonly": (1, 0), "typed-checkbox-editable": (1, 1),
             "constant-text": (17, 0), "constant-checkbox": (1, 0)}
HARNESS = "benchmarks/TreeDataGrid.ScalarSubscriptions"
TEST = "tests/TreeDataGrid.Uno.Tests/ScalarCellSubscriptionTests.cs"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--candidate", default="HEAD")
    parser.add_argument("--output", type=Path, default=Path("artifacts/scalar-subscriptions"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    def git(*args):
        return subprocess.check_output(["git", "-C", str(root), *args])
    revisions = {label: git("rev-parse", ref + "^{commit}").decode().strip()
                 for label, ref in (("baseline", args.baseline), ("candidate", args.candidate))}
    if revisions["baseline"] == revisions["candidate"]:
        raise ValueError("Distinct revisions required")
    harness = {name: git("show", revisions["candidate"] + ":" + HARNESS + "/" + name)
               for name in ("Program.cs", "TreeDataGrid.ScalarSubscriptions.csproj")}
    tests = git("show", revisions["candidate"] + ":" + TEST)
    evidence = {"revisions": revisions, "processOrder": ["baseline", "candidate", "candidate", "baseline"],
                "harnessSha256": {name: hashlib.sha256(content).hexdigest() for name, content in harness.items()},
                "testSha256": hashlib.sha256(tests).hexdigest(),
                "collectorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                "runtimePolicy": "Inherited defaults; no tiering or ReadyToRun override"}
    (output / "inputs.json").write_text(json.dumps(evidence, indent=2) + "\n")
    trees = {}
    reports = {label: [] for label in revisions}
    fingerprints = {}
    environment = None
    try:
        with tempfile.TemporaryDirectory(prefix="scalar-cells-") as scratch:
            scratch = Path(scratch)
            hosts = {}
            for label, revision in revisions.items():
                tree = scratch / (label + "-source")
                git("worktree", "add", "--detach", str(tree), revision)
                trees[label] = tree
                host = scratch / (label + "-harness")
                host.mkdir()
                hosts[label] = host
                for name, content in harness.items(): (host / name).write_bytes(content)
                for name in ("global.json", "NuGet.config"):
                    (host / name).write_bytes(git("show", revisions["candidate"] + ":" + name))
                with (output / (label + "-build.log")).open("w") as log:
                    subprocess.run(["dotnet", "build", str(host / "TreeDataGrid.ScalarSubscriptions.csproj"), "-c", "Release",
                                    "-p:TreeDataGridSourceRoot=" + str(tree), "-p:TreeDataGridUnoTargetFrameworks=net10.0"],
                                   cwd=tree, stdout=log, stderr=subprocess.STDOUT, timeout=300, check=True)
            # Test the same newly added assertions against the old product. Only
            # the new test file is overlaid, never a runtime/library source file.
            outcomes = {}
            for label, tree in trees.items():
                test = tree / TEST
                if label == "baseline":
                    if test.exists(): raise ValueError("Baseline already contains the new test file")
                    test.write_bytes(tests)
                folder = output / (label + "-tests")
                folder.mkdir()
                try:
                    with (output / (label + "-tests.log")).open("w") as log:
                        result = subprocess.run(["dotnet", "test", "tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj",
                            "-c", "Release", "--filter", "FullyQualifiedName~ScalarCellSubscriptionTests",
                            "--logger", "trx;LogFileName=scalar.trx", "--results-directory", str(folder)],
                            cwd=tree, stdout=log, stderr=subprocess.STDOUT, timeout=300)
                    document = ET.parse(folder / "scalar.trx")
                    counters = document.find(".//{*}Counters")
                    if counters is None: raise ValueError("No TRX counters")
                    values = {key: int(value) for key, value in counters.attrib.items()}
                    failures = [entry.attrib["testName"] for entry in document.findall(".//{*}UnitTestResult")
                                if entry.attrib.get("outcome") != "Passed"]
                    outcomes[label] = {"exitCode": result.returncode, "counters": values, "failures": failures}
                    if values["total"] != 32 or values["executed"] != 32 or values["notExecuted"] != 0:
                        raise ValueError("Incomplete focused regression execution")
                    if label == "candidate" and (result.returncode != 0 or values["passed"] != 32):
                        raise ValueError("Candidate regression failure")
                    if label == "baseline" and (result.returncode == 0 or values["failed"] == 0):
                        raise ValueError("Baseline did not reproduce a constructor defect")
                finally:
                    if label == "baseline": test.unlink(missing_ok=True)
                    (output / "regressions.json").write_text(json.dumps(outcomes, indent=2) + "\n")
            for ordinal, label in enumerate(evidence["processOrder"]):
                target = output / f"{ordinal:02d}-{label}.json"
                with (output / f"{ordinal:02d}-{label}.log").open("w") as log:
                    subprocess.run(["dotnet", str(hosts[label] / "bin/Release/net10.0/TreeDataGrid.ScalarSubscriptions.dll"),
                                    revisions[label], str(target)], cwd=hosts[label], stdout=log,
                                   stderr=subprocess.STDOUT, timeout=180, check=True)
                report = json.loads(target.read_text())
                if report["revision"] != revisions[label] or len(report["samples"]) != 250:
                    raise ValueError("Invalid revision or sample count")
                current = {key: report[key] for key in ("runtime", "os", "architecture", "serverGc", "tieredCompilation",
                                                       "readyToRun", "count", "warmups", "sampleCount", "scope")}
                if environment is None: environment = current
                if current != environment: raise ValueError("Benchmark environment changed")
                if fingerprints.setdefault(label, report["librarySha256"]) != report["librarySha256"]:
                    raise ValueError("Library bytes changed within one revision")
                for name, (value, writes) in WORKLOADS.items():
                    samples = [s for s in report["samples"] if s["Operation"] == name]
                    if sorted(s["Iteration"] for s in samples) != list(range(25)):
                        raise ValueError("Missing/duplicated iterations")
                    for sample in samples:
                        if sample["Count"] != 4096 or sample["Checksum"] != 4096 * value or sample["Writes"] != 4096 * writes:
                            raise ValueError("Work/checksum/writeback mismatch")
                        if sample["AllocatedBytes"] < 0 or not math.isfinite(sample["Milliseconds"]) or sample["Milliseconds"] < 0:
                            raise ValueError("Invalid measurement")
                reports[label].append(report)
            for tree in trees.values():
                subprocess.run(["git", "-C", str(tree), "diff", "--exit-code"], check=True)
                subprocess.run(["git", "-C", str(tree), "diff", "--cached", "--exit-code"], check=True)
            comparisons = []
            for name in WORKLOADS:
                item = {"operation": name}
                for metric in ("Milliseconds", "AllocatedBytes"):
                    data = {label: [sample[metric] / sample["Count"] for report in runs for sample in report["samples"]
                                    if sample["Operation"] == name] for label, runs in reports.items()}
                    medians = {label: statistics.median(values) for label, values in data.items()}
                    item[metric + "PerOperation"] = {"samplesPerRevision": 50, "median": medians,
                        "candidateOverBaseline": medians["candidate"] / medians["baseline"] if medians["baseline"] else None,
                        "p95": {label: sorted(values)[math.ceil(.95 * len(values)) - 1] for label, values in data.items()},
                        "perPassMedians": {label: [statistics.median(sample[metric] / sample["Count"] for sample in report["samples"]
                                                                   if sample["Operation"] == name) for report in runs]
                                           for label, runs in reports.items()}}
                comparisons.append(item)
            summary = {"schemaVersion": 1, **evidence, "environment": environment, "librarySha256": fingerprints,
                       "regressions": outcomes, "comparisons": comparisons, "collectionSucceeded": True,
                       "completeNativePerformanceParityProven": False,
                       "limitations": "Single-runner ABBA diagnostics, not statistical confidence intervals or full causal attribution. Every workload/sample retained; no native layout or frame-rate claim."}
            (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
            print("UNO_SCALAR_SUBSCRIPTION_COMPARISON=" + json.dumps(summary), flush=True)
        return 0
    except Exception as error:
        (output / "failure.json").write_text(json.dumps({"type": type(error).__name__, "message": str(error)}, indent=2) + "\n")
        raise
    finally:
        for tree in reversed(list(trees.values())):
            subprocess.run(["git", "-C", str(root), "worktree", "remove", "--force", str(tree)], check=False)
        subprocess.run(["git", "-C", str(root), "worktree", "prune"], check=False)

if __name__ == "__main__":
    raise SystemExit(main())
