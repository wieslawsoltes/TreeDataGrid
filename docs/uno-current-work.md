# Current Uno completion checklist

Updated 2026-09-21. This supersedes the earlier uncommitted/unrun checkpoint.
The port is **not yet certified for complete API, functional or performance parity**.

## Branch and architecture

PR #26 uses `codex/uno-core-port`, based on master `3ca47316` at the last verified
checkpoint. Work is committed directly to that branch; no merge or package release
is implied. `TreeDataGrid.Controls.Uno` references the actual `TreeDataGrid.Core`
project shared with Avalonia. Model, hierarchy, selection and source ownership
remain in Core; native presentation, binding, layout and input remain in the view.

## Implemented and exercised in this continuation

- Retained row slots and their cell/template trees survive replacement, sorting
  and scrolling. The native recycling suite covers 1,000-column virtualization.
- Visible column insertion, removal, single replacement and reordering publish
  precise notifications instead of resetting unrelated native cells. Selection
  mappings are published before observers receive the committed column change.
- Variable-height grid bring-into-view converges against measured geometry and
  the updated scroll extent, rather than repeating a stale estimate. The native
  row-sizing suite passes, including the last row, wrapping, dynamic row height,
  anchor preservation and fixed/automatic height switching.
- Custom reuse and template-measure callbacks check their realization generation
  before using a possibly retired presentation or column collection. Native
  custom-reuse and expander-factory reentrancy suites pass.
- Clearing a row-level factory correctly restores the effective grid factory even
  when a later TemplateBinding update delivers null. The complete element-factory
  suite passes, including nested factory assignment and legacy factory support.
- Focus validation uses native Tab traversal with XamlRoot content as the search
  root, checking forward/reverse order and two-axis focused-container retention.
  Programmatic native traversal is not physical keyboard event-delivery coverage.
- The Windows workflow restores the project graph before passing a native target
  framework to Build, preserving Core's net8.0 target. Windows App SDK builds use
  Visual Studio MSBuild separately from the three-OS desktop test matrix.
- Browser screenshot/exit/RTL fixture operations are separated from desktop-only
  APIs. Unsupported browser fixture operations report explicit limitations, not
  successful tests. Browser screenshots need a browser automation driver.
- Live-region capability discovery uses a statically known type/method signature
  on Uno rather than constructing a type-name string dynamically. Native desktop
  compilation passes; the browser trimming/package job remains a separate gate.

## Reproducible validation

`build/validate-uno-linux.py` runs four unit suites, both desktop builds, sequential
showcase checks, Activity Monitor checks, all isolated native suites, and compiled
API inventory/self-diff checks. It preserves every exit code and runs independent
checks after another check fails. The aggregate exits nonzero unless all required
checks succeeded; timeouts and absent success markers are failures.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop \
  python3 build/validate-uno-linux.py
```

The `Uno validation report` workflow runs the committed checkout with read-only
repository permissions. It does not patch sources, create Git objects, update
branches, or publish packages. Temporary repair/archive workflows are removed.
Reports include the tested revision, toolchain information, TRX files, per-suite
native logs, render captures where requested, and API inventories.

Focused native suites can be reproduced after building the desktop sample:

```sh
python3 build/run-uno-native-suites.py \
  --suite element-factory --suite custom-reuse --suite expander-factory
```

## Evidence checkpoint

Completed validation run `35659339571`, job `106530604875`, tested the reviewed
source changes integrated into this branch. Its `uno-candidate-validation`
artifact is `10666595985`. The run recorded **969 passed unit tests, zero failed
and zero skipped**: Core 210, Uno 210, Avalonia 520 and Uno sample state 29.
Both desktop sample builds had zero warnings/errors; all Activity Monitor
CPU/Memory/Energy/Disk/Network demo and lifetime checks passed.

**24 of 28** independently hosted native suites passed. The sequential workload
passed showcase, Wikipedia, Files/Find, recycling, selection, selection interaction,
focus, editing, cell lifecycle, presentation options, column compatibility and
fluent source extensions, then failed at declarative null-owner recovery. Its
later sequential assertions are unrun, even when their isolated suite passes.
The aggregate correctly remains failed rather than masking incomplete validation.

The three-OS desktop solution/unit-test matrix also passed for Windows, Linux and
macOS at `698d458e`. These are desktop checks, not certification of the separate
Windows App SDK or browser package-consumer jobs. Those jobs must be checked at
the current head after platform-specific corrections. Later run artifacts take
precedence over this dated checkpoint.

## Remaining native failures

1. Declarative hierarchy: replacing a null intermediate binding owner does not
   restore expansion correctly. The fixture requires synchronous Core observation,
   not an arbitrary delay or a weakened assertion.
2. Appearance: retained text does not refresh its theme resource after the tested
   custom-foreground/Light/Dark sequence. Font, border and parent-retention checks
   before that assertion pass; later RTL assertions are not thereby validated.
3. Generic presenter: standalone built-in rows do not retain the requested distant
   variable-height row after `BringIntoView`. The grid-level row-sizing suite is
   separate and passes; it must not mask this standalone contract.
4. Cache resizing: shrinking and regrowing the viewport cache loses one of 21
   retained rows and its cell. The other 20 preserve instance/parent identity.

These failures remain hard CI gates. A failed suite's later assertions are unrun,
not accepted as passing. Native selection and editing fixtures are not substitutes
for complete real-device pointer, keyboard, drag/drop, accessibility and scaling
validation across all heads.

## API and performance acceptance

`tools/TreeDataGrid.ApiAudit` reads compiled PE metadata without executing the UI
assemblies. It emits raw and explicitly namespace-normalized signatures, constants,
generic constraints, inheritance/interfaces and input hashes. Both dependency
sets resolve fully; an identical-assembly strict self-diff reports no differences.

At the checkpoint, the audit inventories 1,748 baseline and 1,525 target shapes:
823 exact normalized matches, 925 missing-or-different and 702 additional-or-different.
These are **not a percentage of implemented features**. Only control namespace
prefixes are normalized. Relocated Core contracts, framework-specific parameter
and property types, actual omissions, inheritance and accessibility differences
require explicit classification and compatibility tests. The report intentionally
keeps `completeApiParityProven` false.

No controlled same-hardware Uno-versus-Avalonia timing comparison has been
completed. Passing allocation budgets, retained model/template identity checks and
bounded realization tests establish individual invariants, not equal overall
performance. The shared sort-clear allocation regression remains covered.
Committed Unicode input on Skia, framework-specific routed/compiled-binding forms,
advanced binding boundaries, physical input and native accessibility remain
explicit acceptance work. Do not replace Unicode input with Latin VirtualKey
mapping, or modify the Uno framework repository as part of this branch.
