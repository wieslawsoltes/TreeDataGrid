# Current Uno completion checklist

Updated 2026-09-25 UTC. Retained implementation **13e2ff79**.
**The audit's signature blind spot is corrected. Two performance candidates were
rejected; this continuation claims no new accepted runtime speedup or full parity.**

[Audit and experiment review](uno-signature-audit-performance-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-13e2ff79.json) ·
[Previous checklist preserved](archive/uno-current-work-before-13e2ff79.md)

## Revisions and retained changes

Starting head `07c571547fd6357317df2df6b4d3e5637fe81511`.
Audit correction `15eec42dc462c9ea358c11620e694eddd3bc5d30`.
Experimental runtime `d2ad0cb97d6a45f22cf06bf8b57c646fb2611a20`.
Comparison setup `c52cf980e9a3c78b789ee167d75dd7c5ac1a73e0`.
Candidate withdrawal and retained implementation `13e2ff7901a249393f29b8eed57dff544963afcf`.
Tested tree `14e4748469c798efd4eac4613deeaf4d7a43e007`.
CI merge `7780595e02ad455f0a2c237135fc0672d8058ca0`, with that same tree.

The runtime-library source diff against the starting head is empty after withdrawal.
Retained work consists of signature-audit correctness, four direct-framework binding
comparisons, one loaded vertical-retirement scenario and the reproducible rejected
experiment. Actual shared Core still owns source, row, hierarchy and selection
state. Earlier horizontal-visibility optimizations remain; their earlier performance
results are not recounted as new work. No merge or public release is included.
Final documentation follows completed implementation validation and changes only docs.

## Audit findings

The raw **834** is reproducible declared metadata, not a missing-feature count.
Historical all-input accounting is 1,845 baseline / 1,847 target / 1,011 exact;
590 exact matches are the identical shared Core dependency. UI-only accounting is
1,255 / 1,257 / **421 exact**, with the same 834 missing-or-different and 836
additional-or-different entries. Scopes reconcile; normalized collisions are zero.

The 834 partition is 214 changed same-identity declarations, 43 absent type identities,
316 members of absent types, 118 members not declared on matched types and 143
overload/parameter differences. Core-relocation and inherited candidates remain
explicitly unaccepted; none is subtracted or hidden. Native signature and inheritance
examples, strict-mode interpretation and remaining metadata boundaries are in the review.

A remaining false-negative defect was fixed: ordinary C# display omitted ordered
signature `modreq`/`modopt` and convention details. The supplemental reader now records
return/ref/parameter/field/accessor, array/pointer/function-pointer and nested/enclosing
generic argument sites; modifier order/duplicates, required/optional distinction,
calling conventions and unresolved modifier types. Output schema is 6. All **62 new
production PE-reader checks** pass, alongside 27 existing reader, 39 semantic,
57 normalization and 23 Python integrity checks. A local negative control with only
the new append hook disabled fails the unchanged modifier-only regression.

Raw counts remain unchanged; richer supplemental rows still count 13,465 / 15,813,
3,838 exact, 9,627 missing-or-different and 11,975 additional-or-different. Strict
self-comparison has zero declared and supplemental differences. Local strict cross-
framework audit exits 1; inventory mode exits 0 without claiming parity. Full ABI,
assembly-qualified forwarding, attribute inheritance, source lookup, native property
defaults and behavior still require explicit acceptance.

## Performance experiments and decision

The reference Avalonia presenter uses deferred same-row-container rebind, reusable
cell models, bounded native ranges and real measure-validity checks. An aggressive
Uno row-range prototype was investigated, fault-tested and rejected locally after
mixed/diagonal regressions. It was never pushed. Native measurement was not replaced
with an unsafe size-only cache.

A narrower reusable retirement-snapshot candidate was pushed and tested. Controlled
[ABBA run 36197169685](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36197169685)
compared exact baseline `15eec42d` and candidate `d2ad0cb9`: all 16 hosts completed,
100 Uno samples per workload/revision, and eight identical frame sequences. It saved
80-1,200 bytes in four workloads, but five timing medians worsened: horizontal +18.25%,
vertical +14.26%, diagonal +4.30%, resize +19.78%, sort +9.51%; replacement improved
3.33%. Complete medians, p95, settlement, reference ratios and per-pass results are
retained in artifact `10890289852`. These diagnostics are not confidence intervals.

That result did not justify the small allocation benefit. `13e2ff79` restores the
exact original presenter blob and removes the experimental helper and its twelve
new tests. No preexisting assertion was removed. Candidate checkpoint `c52cf980` had
passed 1,874 cases; the twelve withdrawn helper cases are deliberately not counted
in the final **1,862**. Four direct-framework tests and the loaded native scenario
remain useful independent regression coverage. The pinned experiment remains
reproducible, not presented as a successful optimization or rerun until favorable.

## Final implementation validation

[Functional run 36197904479](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36197904479)
passes all fifteen stages on unchanged input: **228 Core + 882 Uno + 536 Avalonia +
41 sample-state + 175 direct-framework = 1,862 cases**, zero failed/skipped;
**65/65 native suites**; sequential native smoke; both native sample builds with
zero warnings/errors; Activity Monitor checks; and the full audit/integrity suite.
The new marker `UNO_RUNTIME_VERTICAL_RETIREMENT_PASSED` appears in isolated and
sequential native logs. Report artifact `10891795146`; source artifact `10890806503`.

[Platform run 36197904535](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36197904535)
passes all six jobs: Windows/macOS/Linux builds/tests, Linux X11 native/NuGet execution,
Windows App SDK builds/package publication, and actual published trimmed-browser
consumers. Downloaded browser artifact `10891236045` verifies four passed routes:
showcase, Activity Monitor, and all fifteen browser-dispatched input stages at scales
1 and 2. The new vertical-retirement marker is present in the published showcase
console. Chromium is 143.0.7499.4. Browser artifact hash and route results were
independently inspected, not inferred from a successful build. Windows publication
is not Windows OS runtime acceptance; these browser routes are not physical-device,
universal-browser, IME or external screen-reader acceptance. Existing browser
splash-screen warnings remain. Supporting Build, dependency, reference-pack,
trimmed-binding and reproducibility workflows also passed; no retry was requested.

Local SDK 10.0.201 and public offline assets were used for real .NET tests and native
experiments. The local net8 reference pack was 8.0.31 instead of requested 8.0.25,
without changing target frameworks; CI used unmodified repository pins. Local 882 Uno
and 175 direct-framework cases passed after withdrawal. Seven retained authored
implementation/test files match CI bytes; original presenter bytes and absence of
rejected files were verified. The archived source independently reconstructs Git
tree `14e4748469c798efd4eac4613deeaf4d7a43e007`. Source, functional, browser and performance
archive hashes match their metadata; source.tar matches its manifest.

## Independent performance budget and remaining work

[Final paired run 36197904730](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36197904730)
completes both builds and all four hosts but **fails the unchanged 1.10 budget**.
Timing/allocation Uno-to-Avalonia ratios are horizontal **2.609/0.741**, vertical
**1.937/1.957**, diagonal **3.507/1.928**, replacement **1.509/2.668**, resize
**1.588/1.516**, sort **1.302/0.620**. Artifact `10890599750` retains raw evidence.
This separate runner is not combined with the controlled experiment as a before/after
claim. Synchronous UI/layout work is not GPU completion or frame rate.

Remaining gates: genuine API adaptations with compiler/behavioral proofs; native
text/measurement and source-sort performance; broader callback and hierarchy/variable-
height workloads; the prior undiagnosed intermittent allocation observation; physical
input/drag, Unicode/IME, external accessibility and cross-head lifecycle/scaling.
No assertion, Core ownership rule, rendering setting, trimming diagnostic, workload
or performance threshold was weakened. PR #26 remains draft and the full port is not
marked complete.
