#!/usr/bin/env python3
"""Measure the same public binding-instantiation harness against exact revisions in ABBA order.

This is a diagnostic collector, not an API/native performance acceptance waiver.
All measurements, including construction costs and slow samples, are retained.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import statistics
import subprocess
import tempfile


WORKLOADS = {
    "descriptor-create": (4096, 17),
    "descriptor-observe": (4096, 17),
    "descriptor-one-time": (4096, 17),
    "custom-cell-create": (4096, 17),
    "direct-constructor": (4096, 17),
    "transient-descriptor": (4096, 17),
    "changed-reader": (4096, 17),
    "changed-link": (4096, 17),
    "changed-fallback": (4096, 17),
}

HARNESS = "benchmarks/TreeDataGrid.BindingPlanExecution"
FILES = ("TreeDataGrid.BindingPlanExecution.csproj", "Program.cs")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--candidate", default="HEAD")
    parser.add_argument("--output", type=Path, default=Path("artifacts/binding-plan-comparison"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)

    def git(*arguments: str) -> bytes:
        return subprocess.check_output(["git", "-C", str(root), *arguments])

    revisions = {name: git("rev-parse", f"{value}^{{commit}}").decode().strip()
                 for name, value in (("baseline", args.baseline), ("candidate", args.candidate))}
    if revisions["baseline"] == revisions["candidate"]:
        raise ValueError("Baseline and candidate must be distinct commits")
    harness = {name: git("show", revisions["candidate"] + ":" + HARNESS + "/" + name) for name in FILES}
    hashes = {name: hashlib.sha256(content).hexdigest() for name, content in harness.items()}
    order = ["baseline", "candidate", "candidate", "baseline"]
    provenance = {
        "schemaVersion": 1, "revisions": revisions, "order": order,
        "harnessSourceRevision": revisions["candidate"], "harnessSha256": hashes,
        "collectorSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "machine": platform.platform(), "processor": platform.processor(),
        "method": "Two exact unmodified source revisions, identical harness outside each worktree, ABBA process order; tiering and ReadyToRun disabled for measured processes to avoid mid-run compilation tiers.",
        "completePerformanceParityProven": False,
    }
    (output / "input.json").write_text(json.dumps(provenance, indent=2) + "\n")
    runtime_env = dict(os.environ, DOTNET_TieredCompilation="0", DOTNET_ReadyToRun="0")
    reports: dict[str, list[dict]] = {label: [] for label in revisions}
    worktrees: list[Path] = []
    matched_environment = None
    library_hashes: dict[str, str] = {}
    try:
        with tempfile.TemporaryDirectory(prefix="tdg-binding-plan-") as scratch_name:
            scratch = Path(scratch_name)
            hosts: dict[str, Path] = {}
            binaries: dict[str, Path] = {}
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
                    subprocess.run([
                        "dotnet", "build", str(host / FILES[0]), "-c", "Release",
                        "-p:TreeDataGridSourceRoot=" + str(tree),
                        "-p:TreeDataGridUnoTargetFrameworks=net10.0",
                    ], cwd=tree, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=240)
                target = subprocess.check_output([
                    "dotnet", "msbuild", str(host / FILES[0]), "-nologo", "-getProperty:TargetPath",
                    "-p:Configuration=Release", "-p:TreeDataGridSourceRoot=" + str(tree),
                    "-p:TreeDataGridUnoTargetFrameworks=net10.0",
                ], cwd=tree, text=True, timeout=60).strip()
                binary = Path(target).resolve()
                if not binary.is_relative_to(host) or not binary.is_file():
                    raise ValueError("MSBuild returned an invalid harness target: " + target)
                binaries[label] = binary
            for ordinal, label in enumerate(order):
                path = output / f"{ordinal:02d}-{label}.json"
                with (output / f"{ordinal:02d}-{label}.log").open("w") as log:
                    subprocess.run([
                        "dotnet", str(binaries[label]),
                        revisions[label], str(path),
                    ], cwd=hosts[label], env=runtime_env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=120)
                report = json.loads(path.read_text())
                if report["revision"] != revisions[label] or len(report["samples"]) != 15 * len(WORKLOADS):
                    raise ValueError("Wrong revision or incomplete sample set")
                environment = {key: report[key] for key in (
                    "runtime", "architecture", "os", "serverGc", "dynamicCodeCompiled",
                    "samplesPerWorkload", "warmupBatches", "scope", "tieredCompilation", "readyToRun")}
                if matched_environment is None:
                    matched_environment = environment
                if environment != matched_environment:
                    raise ValueError("Runtime, workload or collection policy changed")
                fingerprint = library_hashes.setdefault(label, report["librarySha256"])
                if fingerprint != report["librarySha256"]:
                    raise ValueError("Library bytes changed between measured processes")
                for name, (count, checksum) in WORKLOADS.items():
                    samples = [s for s in report["samples"] if s["Operation"] == name]
                    if sorted(s["Iteration"] for s in samples) != list(range(15)):
                        raise ValueError("Missing, duplicated or unexpected iterations")
                    for sample in samples:
                        if sample["Count"] != count or sample["Checksum"] != count * checksum:
                            raise ValueError("Incorrect work/checksum")
                        if not math.isfinite(sample["Milliseconds"]) or sample["Milliseconds"] < 0 or sample["AllocatedBytes"] < 0:
                            raise ValueError("Invalid measurement")
                reports[label].append(report)
                print("UNO_BINDING_PLAN_PASS=" + json.dumps({"ordinal": ordinal, "label": label, "revision": revisions[label]}), flush=True)
            for tree in worktrees:
                subprocess.run(["git", "-C", str(tree), "diff", "--exit-code"], check=True)
                subprocess.run(["git", "-C", str(tree), "diff", "--cached", "--exit-code"], check=True)
            comparisons = []
            for operation in WORKLOADS:
                entry: dict = {"operation": operation}
                for metric in ("Milliseconds", "AllocatedBytes"):
                    values = {label: [s[metric] / s["Count"] for report in runs for s in report["samples"]
                                      if s["Operation"] == operation] for label, runs in reports.items()}
                    medians = {label: statistics.median(samples) for label, samples in values.items()}
                    entry[metric + "PerOperation"] = {
                        "samplesPerRevision": 30, "median": medians,
                        "candidateOverBaseline": medians["candidate"] / medians["baseline"] if medians["baseline"] else None,
                        "p95": {label: sorted(samples)[math.ceil(0.95 * len(samples)) - 1] for label, samples in values.items()},
                        "perPassMedians": {label: [statistics.median(s[metric] / s["Count"] for s in report["samples"]
                                              if s["Operation"] == operation) for report in runs] for label, runs in reports.items()},
                    }
                comparisons.append(entry)
            result = {**provenance, "environment": matched_environment, "librarySha256": library_hashes,
                      "comparisons": comparisons, "collectionSucceeded": True,
                      "limitations": ["Pooled diagnostic medians/p95; not confidence intervals or proof of causal significance.",
                                      "Construction cost remains separately visible; no workload or sample is discarded.",
                                      "Not native layout, whole-grid sorting, frame rate, startup or GPU completion acceptance."]}
            (output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
            print("UNO_BINDING_PLAN_COMPARISON=" + json.dumps(result), flush=True)
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
