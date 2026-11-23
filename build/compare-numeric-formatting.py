#!/usr/bin/env python3
"""Compare exact numeric-display revisions with one public harness in ABBA order.

A successful collector is not the independent native parity gate. Preserve all
controls, samples, library fingerprints, p95, per-pass results and failure logs.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics
import subprocess
import tempfile

WORKLOADS = ("scalar-int-identity", "scalar-double-identity", "scalar-decimal-identity",
             "scalar-string-identity", "scalar-nullable-identity", "scalar-int-formatted",
             "scalar-custom-provider", "live-int-identity", "core-int-identity", "boxed-column-format")
HARNESS = "benchmarks/TreeDataGrid.NumericFormatting"
FILES = ("TreeDataGrid.NumericFormatting.csproj", "Program.cs")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--candidate", default="HEAD")
    parser.add_argument("--output", type=Path, default=Path("artifacts/numeric-formatting"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)

    def git(*arguments: str) -> bytes:
        return subprocess.check_output(["git", "-C", str(root), *arguments])

    revisions = {label: git("rev-parse", ref + "^{commit}").decode().strip()
                 for label, ref in (("baseline", args.baseline), ("candidate", args.candidate))}
    if revisions["baseline"] == revisions["candidate"]:
        raise ValueError("Distinct revisions required")
    harness = {name: git("show", revisions["candidate"] + ":" + HARNESS + "/" + name) for name in FILES}
    order = ("baseline", "candidate", "candidate", "baseline")
    provenance = {
        "revisions": revisions, "order": order,
        "harnessSha256": {name: hashlib.sha256(data).hexdigest() for name, data in harness.items()},
        "collectorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "method": "Exact unmodified source worktrees, identical external harness, same inherited runtime environment, ABBA process order",
    }
    (output / "input.json").write_text(json.dumps(provenance, indent=2) + "\n")
    reports: dict[str, list[dict]] = {label: [] for label in revisions}
    worktrees: list[Path] = []
    fingerprints: dict[str, str] = {}
    matching_environment = None
    matching_checksums = None
    try:
        with tempfile.TemporaryDirectory(prefix="tdg-numeric-") as temporary:
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
                    subprocess.run(["dotnet", str(hosts[label] / "bin/Release/net10.0/TreeDataGrid.NumericFormatting.dll"),
                                    revisions[label], str(path)], cwd=hosts[label],
                                   stdout=log, stderr=subprocess.STDOUT, check=True, timeout=180)
                report = json.loads(path.read_text())
                if report["revision"] != revisions[label] or len(report["samples"]) != len(WORKLOADS) * 15 or not report["cleanupPassed"]:
                    raise ValueError("Wrong revision, incomplete samples or failed cleanup")
                current_environment = {key: report[key] for key in ("runtime", "architecture", "os", "serverGc",
                    "dynamicCodeCompiled", "tieredCompilation", "readyToRun", "count", "samplesPerWorkload", "warmupBatches", "scope")}
                if matching_environment is None:
                    matching_environment = current_environment
                    matching_checksums = report["workloadChecksums"]
                if current_environment != matching_environment or report["workloadChecksums"] != matching_checksums:
                    raise ValueError("Environment or executed workload mismatch")
                if fingerprints.setdefault(label, report["librarySha256"]) != report["librarySha256"]:
                    raise ValueError("Library changed between processes")
                for operation in WORKLOADS:
                    values = [s for s in report["samples"] if s["Operation"] == operation]
                    if sorted(s["Iteration"] for s in values) != list(range(15)):
                        raise ValueError("Missing/duplicate iterations")
                    for sample in values:
                        if sample["Count"] != 8192 or sample["Checksum"] != matching_checksums[operation]:
                            raise ValueError("Incorrect executed work")
                        if not math.isfinite(sample["Milliseconds"]) or sample["Milliseconds"] < 0 or sample["AllocatedBytes"] < 0:
                            raise ValueError("Invalid measurement")
                reports[label].append(report)
                print("UNO_NUMERIC_FORMATTING_PASS=" + json.dumps({"ordinal": ordinal, "label": label, "revision": revisions[label]}), flush=True)
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
                      "limitations": ["Pooled medians/p95 are not confidence intervals or complete causal attribution.",
                                      "All unchanged controls and all measured samples remain included.",
                                      "Warm cell-model display queries, not native visual layout, construction, startup, frame rate or GPU completion."]}
            (output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
            print("UNO_NUMERIC_FORMATTING_COMPARISON=" + json.dumps(result), flush=True)
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
