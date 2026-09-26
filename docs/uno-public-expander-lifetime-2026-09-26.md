# Public Uno expander lifetime: constructor retirement and recovery

2026-09-26. Implementation `aa112e51a7fdb06c3090f2b6fe64b9ebc7b4c5a0`,
over `9d624ed53d47ff9d774d1b053d1ad7876c9892d1`.
[Current checklist](uno-current-work.md).
[Implementation](https://github.com/wieslawsoltes/TreeDataGrid/commit/aa112e51a7fdb06c3090f2b6fe64b9ebc7b4c5a0).

## Defects reproduced

The public `ExpanderCell<TModel>` attached a row event, then assigned subscription
handles directly from `Subscribe`. A custom row event accessor can access the
constructing cell through its delegate target. An observable can synchronously
publish values or execute other callbacks before returning its subscription.

When those callbacks disposed the cell, cleanup ran before the constructor had
received all its subscription handles. Construction then continued, leaving live
subscriptions in an already disposed cell. Disposal inside a row add accessor
could similarly attempt removal before attachment completed.

Separately, `IsExpanded` still allowed writes after disposal, including changes
to the real shared Core hierarchy. `CanEdit` and `ShowExpander` could return true
after a caller-owned getter disposed the cell during the read.

The same nineteen new tests were executed against the original runtime and the
candidate: **baseline seven passed / twelve failed; candidate nineteen passed**.
The baseline failures are actual executed regressions, not inferred assertions.

## Implementation and ownership

Only `Models/TreeDataGrid/ExpanderCell.cs` changes in the runtime library.

Construction now records attempted row-handler ownership before invoking the event
accessor. A callback retires delivery immediately, but ownership release is deferred
until the event/Subscribe call has unwound and any returned handle is captured.
Retirement stops subsequent subscription stages. A constructor finally block
performs deferred cleanup on early return or failure.

Cleanup is guarded before invoking caller code. Row unsubscription, visibility
lease, expansion lease and owned content are released independently and once.
One cleanup error preserves its exception identity; multiple errors retain release
order. Constructor failures remain first when cleanup also fails. Recursive disposal
cannot repeat ownership release.

Disposed expansion writes now throw `ObjectDisposedException`. Edit permission and
expander visibility return false without invoking borrowed getters after retirement;
live reads recheck lifetime after the getter returns. Live getter exceptions are
not suppressed. Normal Core expansion, native editing and observation still work.

The row and observable sources remain caller-owned. If a faulty `Subscribe` throws
before returning a lease, its inaccessible registration must be rolled back by the
observable itself; this patch does not claim to recover that opaque ownership.

Three private state flags were added. No object-size or performance gain is claimed.
This is synchronous/reentrant lifetime protection, not cross-thread synchronization.
Public signatures, shared Core source, native renderer and performance budgets are
unchanged.

## Coverage and real native recovery

`PublicExpanderConstructionChecks.cs` is shared between unit and sample consumers.
Its nineteen cases cover disposal before/after event and subscription attachment,
initial-value callbacks, constructor and cleanup failures, independent cleanup,
stale delivery, disposed writes into an actual Core hierarchy, predicate retirement,
getter-error recovery and independently owned cells sharing a borrowed row.

`PublicExpanderRuntimeChecks.cs` retains its previous assertions and adds recovery:
the same attached native expander control is reused with a new public model over
the exact same Core row. Old text/visibility/expansion sources cannot change the
replacement. Native edit commit writes to the replacement source; native collapse
uses the shared Core controller; final cleanup releases only owned observations.

Markers:
- `UNO_RUNTIME_PUBLIC_EXPANDER_CONSTRUCTION_PASSED` (nineteen cases).
- `UNO_RUNTIME_PUBLIC_EXPANDER_RECOVERY_PASSED`.

No registered native suite was added: these run inside existing `public-expander`.
No earlier unit assertion was changed, no test skipped, and no new paired-framework
case is counted. The unit suite increases from 951 to 970.

## Completed local validation

| Validation | Result |
| --- | --- |
| New cases on unchanged runtime | 7 passed, 12 failed |
| Same new cases on candidate | 19 passed |
| Core / Uno / Avalonia | 228 / 970 / 536 passed |
| Paired-framework / sample-state | 234 / 41 passed |
| Full .NET total | **2,009 passed; zero failed/skipped** |
| Focused native expander | Passed |
| Registered native suites | **65/65 passed** |
| Sequential native smoke | Passed |
| Production API audit and strict self-check | Completed; self-differences zero |

The API audit remains 1,845 reference / 1,888 target declarations, 1,055 exact,
790 missing-or-different and 833 additional-or-different. Dependencies resolve and
normalization collisions are zero. This is not complete API parity. Shared Core's
590 identical dependency declarations are not independent UI-port coverage.

Local input was recovered from CI source artifact `10906881911`. Its SHA256 was
independently recomputed as
`f81bbb480dd096c5e2734f608481b10996effd9391f231389f16270033ca4dda`.
Reconstructing its Git index produced the exact original tree
`a217acd1c3e33b62ec778db993308f60bb3cdcd0`.
That implementation differs from starting head `9d624ed5` only in documentation.
The published patch is based on the actual remote head, not a synthetic local commit.

Local tests used restored .NET SDK 10.0.201 / runtime 10.0.12 with an offline package
cache. Local SDK metadata selected available .NET 8.0.31 reference/apphost packs in
place of unavailable 8.0.25. This environment-only adjustment and early failed
restore attempts are preserved with their hashes/logs. Repository pins and tests
were not changed to accommodate restore. SourceLink produced warnings because the
reconstructed local checkout lacks an origin remote; no blanket zero-warning claim.

An external copy of the native runner adjusts only root/output paths for inherited
`PLATFORM=linux/amd64`; the registry, assertions, three workers and timeouts remain
unchanged. These local results are distinct from canonical GitHub CI.

## CI snapshot and remaining acceptance

The previous implementation's platform run `36242946789` has now completed all six
jobs successfully, including macOS. That does not certify this new implementation.

Current implementation runs:
[functional](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768340),
[platform](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768332),
[native performance](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768337).
At this checkpoint, functional/platform validation has not completed. Trimming,
contract reproducibility, dependency snapshot and reference-pack workflows passed.

The new independent native performance run completed both builds and all four
hosts, but failed the unchanged **1.10** gate. Time ratios (Uno/Avalonia) are 2.585
horizontal, 2.930 vertical, 3.459 diagonal, 1.802 row replacement, 1.299 column resize
and 2.018 sorting. Raw results are in artifact `10910721852`; its ZIP hash was
independently verified. These are synchronous UI/layout and settlement diagnostics,
not a controlled before/after benefit from this correctness fix.

Documentation-only publication uses `[skip ci]` to avoid superseding active product
checks under the existing concurrency rule. The implementation commit was not
skipped. No new-head green status, merge, release or full port parity is claimed.

Remaining work includes unresolved native/Core/inheritance API contracts, measured
native layout and sorting costs, variable-height/hierarchy workloads, physical input,
IME, external accessibility and cross-platform runtime acceptance.
