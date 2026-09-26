# Recovered portable API and JIT accessor refinement

2026-09-26 UTC. Recovered from the previously delivered local bundle ending at
`2ae4bd62c0d59831502b53a6f7d36fe558e116a2`, based on remote `93af6508`.
The four original local commits remain available in that bundle. Source, tests,
benchmarks and generator are imported unchanged; this document distinguishes
historical local validation from new remote validation.

## Portable API declarations over existing state

Nineteen native declarations forward to their existing Core/layout implementation:

* ColumnOptions<TModel>: nullable resize/sort policies and ascending/descending
  comparison delegates.
* ColumnBase<TModel>: Header, Tag and SortDirection.
* CheckBoxColumn<TModel>: read-only IsThreeState.
* TreeDataGridItemsSourceView: Count, Inner, indexer, HasKeyIndexMapping,
  CollectionChanged, GetAt, IndexOf, KeyFromIndex, IndexFromKey, Dispose, and
  protected OnItemsSourceChanged.

No new policy, collection, event, selection or disposal storage is introduced.
Both static-type event routes use the one Core event store. Key mapping delegates
the reference/Core unsupported behavior instead of advertising a new capability.
The caller continues to own its source and columns.

Twenty-nine paired-framework cases verify declared owner/member shape, policies,
delegate identity, base-reference behavior, notification order, cross-static-type
subscription removal, protected hooks, original observer exceptions and disposal.
The unchanged production reader's complete historical reconciliation is:

| Scope | Reference | Candidate | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Combined | 1,845 | 1,872 | 1,039 | 806 | 833 |
| Identical Core dependency | 590 | 590 | 590 | 0 | 0 |
| Independent UI assemblies | 1,255 | 1,282 | 449 | 806 | 833 |

`build/reconcile-uno-portable-facades.py` requires all nineteen expected full
reference shapes, unchanged policies, unchanged previous raw target records,
resolved dependencies, zero collisions and no newly missing reference entry.
These are declaration improvements, not nineteen newly created runtime features
or complete ABI/behavioral certification. Supplemental differences remain.

## Runtime-capability-based expression compilation

Intermediate expression owner links, custom-column selectors and generated
expression setters use `Compile(preferInterpretation:
!RuntimeFeature.IsDynamicCodeCompiled)`. JIT execution is selected where available;
interpretation is retained where dynamic code is not compiled. There is no global
model cache or dynamic-code requirement imposed on the fallback.

Traversal order, null-owner behavior, setter conversions, original exceptions and
retargeting stay intact. The materializer reproduces all 23 generated outputs.
Seven Uno and eight paired-framework cases check the accessor and setter behavior.
New AOT execution was not performed in the historical continuation.

## Historical performance evidence and construction cost

The original public harness ran exact revisions in ABBA process order on x64
Debian 13, .NET 10.0.12, workstation GC. It retains thirty samples per workload
per revision, raw data, checksums, subscription cleanup and library hashes.

| Warm operation | Historical baseline ns | Recovered candidate ns | Bytes/op before/after |
| --- | ---: | ---: | ---: |
| Three-link owner walk | 350.32 | 10.11 | 456 / 0 |
| Custom selector | 144.74 | 2.33 | 176 / 0 |
| Custom comparison | 286.13 | 5.65 | 352 / 0 |
| Typed notification | 763.17 | 390.15 | 456 / 0 |

These are managed micro-workloads, not native rendering, complete sorting or frame
rate. Custom-column construction rose from 0.1796625 to 0.7899031 ms, and allocation
from 10,792 to 22,544 bytes. The warm-path improvement does not erase this startup
tradeoff. These historical measurements must not be mixed with a new runner's
numbers as a controlled comparison.

## Historical execution and recovery boundary

Original local results: Core 228, Uno 903, Avalonia 536, sample state 41 and
paired-framework 212: 1,920 cases, zero failed or skipped in completed suites;
65 native suites and sequential native/Activity Monitor checks passed. The initial
aggregate attempt failed before Avalonia tests because its offline apphost pack
was unavailable. The unchanged suite subsequently passed with the matching host.
The original failure and successful recovery are preserved in the delivered archive.

The original native benchmark completed builds and four hosts but failed the
unchanged 1.10 timing/allocation budget. Its horizontal, vertical and diagonal
time ratios were 2.209, 2.731 and 3.978. It is not evidence of a whole-grid speedup.
New Windows/macOS/browser/AOT execution was not certified by those local results.

During this recovery, the historical source Git tree and bundle were reconstructed
and checked independently, and the recovered 903-case Uno suite passed again.
Code-write connector access is now available despite shell DNS being unavailable.
The recovery commit's remote results are separate from all historical results.
See the current checklist and checkpoint for publication and validation status.
