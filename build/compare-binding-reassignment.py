#!/usr/bin/env python3
"""Compare exact binding implementations with one public harness in ABBA order.

All measured workloads and failures are retained. Collection success is not the
independent native performance gate, nor proof of statistical significance.
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

WORKLOADS = ("stable", "same-read", "same-write", "same-links", "same-all",
             "same-all-observed", "same-all-text-cell", "changed-read", "replaced-links",
             "mutated-links", "transient", "direct")
HARNESS = "benchmarks/TreeDataGrid.BindingReassignment"
FILES = ("TreeDataGrid.BindingReassignment.csproj", "Program.cs")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--candidate", default="HEAD")
    parser.add_argument("--output", type=Path, default=Path("artifacts/binding-reassignment"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)

    def git(*arguments: str) -> bytes:
        return subprocess.check_output(["git", "-C", str(root), *arguments])

    revisions = {label: git("rev-parse", ref + "^{commit}").decode().strip()
                 for label, ref in (("baseline", args.baseline), ("candidate", args.candidate))}
    if revisions["baseline"] == revisions["candidate"]:
        raise ValueError("Distinct revisions are required")
    harness = {name: git("show", revisions["candidate"] + ":" + HARNESS + "/" + name) for name in FILES}
    order = ("baseline", "candidate", "candidate", "baseline")
    provenance = {
        "revisions": revisions, "order": order,
        "harnessSha256": {name: hashlib.sha256(data).hexdigest() for name, data in harness.items()},
        "collectorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "method": "Exact source worktrees, identical external public harness, ABBA process order, tiering and ReadyToRun disabled for both measured revisions",
    }
    (output / "input.json").write_text(json.dumps(provenance, indent=2) + "\n")
    environment = dict(os.environ, DOTNET_TieredCompilation="0", DOTNET_ReadyToRun="0")
    reports: dict[str, list[dict]] = {label: [] for label in revisions}
    worktrees: list[Path] = []
    fingerprints: dict[str, str] = {}
    matching_environment = None
    try:
        with tempfile.TemporaryDirectory(prefix="tdg-reassignment-") as temporary:
            scratch = Path(temporary)
            hosts: dict[str, Path] = {}
            for label, revision in revisions.items():
                tree = scratch / (label + "-source")
                git("worktree", "add", "--detach", str(tree), revision)
                worktrees.append(tree)
                host = scratch / (label + "-harness")
                host.mkdir()
                hosts[label] = host
                for name, content in harness.items():
                    (host / name).write_bytes(content)
                for name in ("global.json", "NuGet.config"):
                    (host / name).write_bytes(git("show", revisions["candidate"] + ":" + name))
                with (output / (label + "-build.log")).open("w") as log:
                    subprocess.run(["dotnet", "build", str(host / FILES[0]), "-c", "Release",
                                    "-p:TreeDataGridSourceRoot=" + str(tree),
                                    "-p:TreeDataGridUnoTargetFrameworks=net10.0"],
                                   cwd=tree, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=300)
            for ordinal, label in enumerate(order):
                path = output / f"{ordinal:02d}-{label}.json"
                with (output / f"{ordinal:02d}-{label}.log").open("w") as log:
                    subprocess.run(["dotnet", str(hosts[label] / "bin/Release/net10.0/TreeDataGrid.BindingReassignment.dll"),
                                    revisions[label], str(path)], cwd=hosts[label], env=environment,
                                   stdout=log, stderr=subprocess.STDOUT, check=True, timeout=180)
                report = json.loads(path.read_text())
                if report["revision"] != revisions[label] or len(report["samples"]) != len(WORKLOADS) * 15:
                    raise ValueError("Wrong revision or incomplete samples")
                current_environment = {key: report[key] for key in ("runtime", "architecture", "os", "serverGc",
                    "dynamicCodeCompiled", "tieredCompilation", "readyToRun", "count", "samplesPerWorkload", "warmupBatches", "scope")}
                if matching_environment is None:
                    matching_environment = current_environment
                if current_environment != matching_environment:
                    raise ValueError("Environment/workload mismatch")
                if fingerprints.setdefault(label, report["librarySha256"]) != report["librarySha256"]:
                    raise ValueError("Library changed between measured processes")
                for operation in WORKLOADS:
                    values = [s for s in report["samples"] if s["Operation"] == operation]
                    if sorted(s["Iteration"] for s in values) != list(range(15)):
                        raise ValueError("Missing or duplicated iterations")
                    for sample in values:
                        if sample["Count"] != 4096 or sample["Checksum"] != 4096 * 17:
                            raise ValueError("Incorrect executed work")
                        if not math.isfinite(sample["Milliseconds"]) or sample["Milliseconds"] < 0 or sample["AllocatedBytes"] < 0:
                            raise ValueError("Invalid measured value")
                reports[label].append(report)
                print("UNO_REASSIGNMENT_PASS=" + json.dumps({"ordinal": ordinal, "label": label, "revision": revisions[label]}), flush=True)
            for tree in worktrees:
                subprocess.run(["git", "-C", str(tree), "diff", "--exit-code"], check=True)
                subprocess.run(["git", "-C", str(tree), "diff", "--cached", "--exit-code"], check=True)
            comparisons = []
            for operation in WORKLOADS:
                comparison: dict = {"operation": operation}
                for metric in ("Milliseconds", "AllocatedBytes"):
                    data = {label: [s[metric] / s["Count"] for report in runs for s in report["samples"]
                                    if s["Operation"] == operation] for label, runs in reports.items()}
                    medians = {label: statistics.median(values) for label, values in data.items()}
                    comparison[metric + "PerOperation"] = {
                        "samplesPerRevision": 30, "median": medians,
                        "candidateOverBaseline": medians["candidate"] / medians["baseline"] if medians["baseline"] else None,
                        "p95": {label: sorted(values)[math.ceil(.95 * len(values)) - 1] for label, values in data.items()},
                        "perPassMedians": {label: [statistics.median(s[metric] / s["Count"] for s in report["samples"]
                                                      if s["Operation"] == operation) for report in runs] for label, runs in reports.items()},
                    }
                comparisons.append(comparison)
            result = {"schemaVersion": 1, **provenance, "environment": matching_environment,
                      "librarySha256": fingerprints, "comparisons": comparisons, "collectionSucceeded": True,
                      "completePerformanceParityProven": False,
                      "limitations": ["Pooled diagnostics are not confidence intervals or full causal attribution.",
                                      "Includes unchanged, genuinely changed and transient cases; no measured workload discarded.",
                                      "Not native control layout, full-grid sorting, frame rate, startup or GPU completion."]}
            (output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
            print("UNO_REASSIGNMENT_COMPARISON=" + json.dumps(result), flush=True)
        return 0
    except Exception as error:
        (output / "failure.json").write_text(json.dumps({"type": type(error).__name__, "error": str(error)}, indent=2) + "\n")
        raise
    finally:
        for tree in reversed(worktrees):
            subprocess.run(["git", "-C", str(root), "worktree", "remove", "--force", str(tree)], check=False)
        subprocess.run(["git", "-C", str(root), "worktree", "prune"], check=False)


if __name__ == "__main__":
    raise SystemExit(main())
