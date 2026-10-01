# Uno Platform performance findings from the TreeDataGrid port

This document collects every performance problem found while bringing the Uno port of
TreeDataGrid (shared `TreeDataGrid.Core`, Uno presentation in `src/TreeDataGrid.Controls.Uno`)
towards the Avalonia version. For each one it records what was measured, where the cost
sits in TreeDataGrid and in Uno, why it is slower than Avalonia, what TreeDataGrid does about
it, and what could be changed in Uno itself, with a sketch of a fix.

Related: [demo parity report](uno-demo-parity-2026-09-30.md) ·
[direct text rendering design](uno-direct-text-rendering.md)

## Versions and method

| Component | Version |
| --- | --- |
| Uno.Sdk / Uno.WinUI (Skia) | 6.7.30 / 6.7.135 |
| SkiaSharp / HarfBuzzSharp | 3.119.2 / 8.3.1.1 |
| .NET | 10 (Skia desktop `net10.0-desktop`, WebAssembly `net10.0-browserwasm`) |
| Avalonia (reference) | 12.0.0 |

- **Workload:** `benchmarks/TreeDataGrid.Parity.Shared/ParityWorkload.cs`. It uses 10,000 rows,
  64 text columns 128 px wide and 32 px rows in an 800×480 viewport, with no headers or scroll bars.
  Both hosts run six operations and report the synchronous UI-thread time, the time until
  layout settles, and allocations. `build/run-native-parity.py` alternates AB/BA pairs. CI
  evaluates each ratio against a 1.10 budget and reports it in the job summary; since
  2026-10-01 the budget is report-only (`--report-only`) and does not fail the job.
- **Profiles:** `dotnet-trace --profile dotnet-sampled-thread-time` and `gc-verbose` on macOS.
  macOS-only samples (native accessibility) were separated out to approximate the Linux CI
  runner.
- **Uno references:** member names are from the Uno 6.7.135 Skia assemblies this build uses
  (`Uno.UI.dll`, `Uno.UI.Composition.dll`, `Uno.UI.Runtime.Skia*.dll`,
  `Uno.WinUI.Graphics2DSK.dll`), inspected with ILSpy. Search the Uno repository by type and
  member name; file names are not given because they could not be verified from binaries.

## Results

Linux CI, Uno time divided by Avalonia time (medians of the synchronous UI-thread time).
Hosted runners vary by up to 2× between runs.

| Operation | Start of work (`0cc1ca9b`) | Before direct text (`952bc9da`) | Now (last three runs) |
| --- | ---: | ---: | ---: |
| Visible-row replacement | 1.62 | 0.92 | 0.59–0.77 |
| Visible-column resizing | 1.78 | 0.75 | 0.74–1.35 |
| Sorting | 1.51 | 1.20 | 0.70–1.23 |
| Vertical scrolling | 2.74 | 3.73 | 1.28–1.78 |
| Distant diagonal scrolling | 4.23 | 4.80 | 1.86–2.58 |
| Horizontal scrolling | 2.61 | 3.44 | 2.40–2.65 |

Allocations are now 1.4–25× lower than Avalonia's. The remaining time gap is in scrolling,
and a scroll step already changes as little as it can. Across about 1,300 elements, a
vertical step moves 3 recycled rows and a horizontal step moves 15 recycled cells
(measured by comparing every element's layout slot before and after a step). What remains
is mostly the per-element cost of Uno's layout, composition and scrolling (findings 5–9).

## Summary

| # | Finding | Uno API | Effect | TreeDataGrid mitigation | Fixable in Uno |
| --- | --- | --- | --- | --- | --- |
| 1 | A `TextBlock` builds a full Unicode layout for every value | `TextBlock.MeasureOverride` → `UnicodeText..ctor` | Largest scroll cost before direct text | Direct Skia text presenter | Fast path and cache for simple text; public text API |
| 2 | A `TextBlock` re-parses in arrange when the arranged size differs from the measured size | `TextBlock.ArrangeOverride` | Every vertically centred cell laid out text twice | `TreeDataGridCellContentPanel` | Compare only the dimensions the layout depends on |
| 3 | Arrange rounds the slot position before adding the margin, using banker's rounding | `FrameworkElement.InnerArrangeCore`, `UIElement.XcpRound` | Workarounds for 1 px differences | Pre-rounded centring position | Round once; round half away from zero |
| 4 | Assigning `Text` to a collapsed `TextBlock` rebuilds inlines and requests a frame | `TextBlock.OnTextChanged` → `InvalidateInlineAndRequireRepaint` | Compositor lock contention per cell | Assign only while it displays | Defer work while not visible |
| 5 | Every visual property change drops the visual's picture and dirties the whole subtree's matrices | `Visual.OnPropertyChangedCore` → `Compositor.InvalidateRenderPartial` → `ContainerVisual.SetMatrixDirty`, `Visual.InvalidatePaint` | Main cost of moving and repainting elements | None possible | Transform-only invalidation; lazy matrices |
| 6 | Each scroll step re-anchors the content visual and schedules an arrange under a dispatcher lock | `ScrollContentPresenter.Update`, `ScrollViewer.Update`, `XamlRoot.InvalidateArrange` | About 0.4 ms per step before any TreeDataGrid work | None possible | Scroll as a render-time transform; coalesce |
| 7 | The UI thread contends with the render thread and pools for locks | `CompositionTarget.RequestNewFrame`, `NativeDispatcher.EnqueueCore`, `WeakReferencePool`, `LinearArrayPool`, `EventManager.RaiseLayoutUpdated` | 7–16% of scroll samples | Fewer invalidations | Lock-free flags; UI-thread pools |
| 8 | Visibility and opacity changes are expensive | `Visual` `"Opacity"` → `RecursiveInvalidate`; damage regions; `RaiseAutomaticPropertyChanges` | Sorting was 1.5–1.8× slower | Keep rows visible across resets | Layer alpha; rectangle damage |
| 9 | macOS accessibility is always on and updates native elements per visual | `MacOSAccessibility.IsAccessibilityEnabled`, `OnSizeOrOffsetChanged`, `uno_accessibility_update_*` | 57% of a macOS diagonal jump | None (it keeps VoiceOver correct) | Enable on demand; batch per frame |
| 10 | `VisualTreeHelper.GetChild` enumerates children for every index | `VisualTreeHelper.GetChild` | O(n²) walks; garbage collected in later frames | Walk `Panel.Children` in the benchmark host | Index the child list directly |
| 11 | `SKCanvasElement` clips to its bounds, ignores inherited opacity and throws on unsupported platforms, and there is no public text API | `SKCanvasVisual.Paint`, `SKCanvasElement..ctor`, internal `FontDetailsCache` | Needed a panel plus canvas, reflection and a HarfBuzzSharp dependency | Workarounds in the presenter | Options on `SKCanvasElement`; public font/shaping API |
| 12 | Cold JIT dominates short measurements | IL-only Uno assemblies | Early iterations 2–3× slower | Eager getter compilation | ReadyToRun packages |

## 1. `TextBlock` builds a full Unicode layout for every value

**Measured.** Before direct text, vertical scrolling was 3.73× and diagonal jumps 4.80×
slower than Avalonia. A prototype that drew cell text straight to Skia cut local vertical
scrolling from about 3.2 ms to 1.8 ms and allocations by about 6×. Profiles attributed most
remaining UI-thread time to per-value `TextBlock` work.

**Uno.** `TextBlock.MeasureOverride` calls `TextBlock.ParseText`, which constructs a new
`Microsoft.UI.Xaml.Documents.UnicodeText` for every measure. Its constructor, for even a
six-character string, does all of the following:

- builds a `StringBuilder`, runs ICU line-break analysis (`AppendBoundaries` → `ubrk_open`
  / `ubrk_next`) and ICU script detection (`uscript_getScript`) per code point, and checks
  font fallback (`SKFont.ContainsGlyph`) per code point;
- runs ICU bidi (`CreateBiDiAndSetPara`, `ubidi_getVisualRun`), and for every line
  `ubidi_setLine` and `ubidi_getLogicalMap` with pooled arrays;
- shapes each script run with a new `HarfBuzzSharp.Buffer` and builds `LinkedList<Glyph>` and
  `LinkedList<Cluster>` nodes per glyph and cluster;
- builds `_xyTable`, `_indexToCluster` and `_clustersInLogicalOrder` lists, and word
  boundaries (`GetWords`).

`UnicodeText.Draw` then allocates two dictionaries and lists per draw to group glyphs by
colour and font. Avalonia's text cell measures and draws from a `TextLayout` without most
of these steps for plain text.

**TreeDataGrid.** A direct Skia presenter
([`TreeDataGridTextPresenter`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L35),
added in `f2242215`) draws what `PART_Text` would draw for single-line left-to-right text.
It reproduces `UnicodeText`: the same `FontDetails` objects, script runs, cluster widths,
`CharacterEllipsis` search, alignment offset and `TextBlock` desired-size rounding (see
[`Shape`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L294),
[`Layout`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L423),
[`MeasureOverride`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L458)).
Shaped strings and arranged glyph runs are cached per font and shared between cells
([`ShapedText`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L314),
[`DirectTextFont.GetShapedText`](../src/TreeDataGrid.Controls.Uno/Primitives/DirectTextFont.cs#L121),
`43adafed`). The `direct-text` native suite (`samples/TreeDataGridUnoSample/DirectTextParityChecks.cs`)
requires identical pixels against the text block in 240 states.

**Fix in Uno.** Add a fast path for the most common case: one `Run`, no wrapping, no
`MaxLines`, left-to-right, a single script and font, and no tabs or line breaks. The fast
path needs no line breaking, no bidi and one shaping call, and can store glyphs in flat
arrays. Cache shaping results per `(FontDetails, string)`, since grids and lists repeat
values.

```csharp
// Sketch: inside UnicodeText construction (or a new SimpleText : IParsedText).
if (inlines.Length == 1 && textWrapping == TextWrapping.NoWrap && maxLines == 0 &&
    flowDirection == FlowDirection.LeftToRight && IsSingleScriptWithoutControls(text, font))
{
    var shaped = ShapedTextCache.GetOrAdd(font, text, static (f, t) =>
    {
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf16(t);
        buffer.GuessSegmentProperties();
        buffer.Direction = HarfBuzzSharp.Direction.LeftToRight;
        f.Font.Shape(buffer);
        return ShapedRun.FromBuffer(buffer, f.TextScale.textScaleX, t); // flat arrays
    });
    return new SimpleText(shaped, font, availableSize, textAlignment, textTrimming, out calculatedSize);
}
```

A public, supported way to draw text with Uno's fonts would also let controls like
TreeDataGrid avoid copying the engine (see finding 11).

## 2. `TextBlock` re-parses in arrange when the arranged size differs from the measured size

**Measured.** Every vertically centred text cell with fixed-height rows laid out its text
twice per value.

**Uno.** `TextBlock.ArrangeOverride` compares the arranged size, minus padding, with the
size it was measured with and re-creates `UnicodeText` when they differ:

```csharp
// Uno 6.7.135, TextBlock.ArrangeOverride
Size size = finalSize.Subtract(padding);
if (_lastParsedTextCreationValues.availableSize != size || ...alignment changed...)
    ParsedText = ParseText(size, out size2);
```

Measure usually receives an unconstrained or larger height than arrange (for example a
centred element measured with the row's height and arranged at its own height), so the
`Size` comparison fails even though a single `NoWrap` line does not depend on the height
at all.

**TreeDataGrid.** [`TreeDataGridCellContentPanel.ArrangeOverride`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellContentPanel.cs#L23)
stretches `PART_Text` so that it is arranged at the size it was measured with, and moves it
to the centred position itself (`dfc52f37`, `d44d17e6`, `7d572713`, `a33856d4`,
`952bc9da`).

**Fix in Uno.** Re-parse only when an input the layout depends on changed:

```csharp
static bool NeedsReparse(in ParseInputs last, Size available, TextWrapping wrapping,
    TextTrimming trimming, int maxLines)
{
    var widthMatters = wrapping != TextWrapping.NoWrap || trimming != TextTrimming.None ||
        textAlignment is not TextAlignment.Left and not TextAlignment.Start;
    var heightMatters = maxLines > 0 || (trimming != TextTrimming.None && wrapping != TextWrapping.NoWrap);
    return (widthMatters && last.Available.Width != available.Width) ||
           (heightMatters && last.Available.Height != available.Height);
}
```

For left-aligned, untrimmed, single-line text no dimension matters. Alignment offsets can
then be recomputed from the stored line widths without re-shaping.

## 3. Arrange rounds the slot position before adding the margin

**Measured.** Centred text was placed 1 px higher than native centring at some sizes
(for example a slot offset of 28.5 at 1× scale), which broke exact-pixel tests on Linux.

**Uno.** In `UIElement.DoArrange` the whole `finalRect` is rounded with `LayoutRound`
before `FrameworkElement.InnerArrangeCore` adds the margin and alignment offset and rounds
again. `LayoutRound` uses `XcpRound(x) => Math.Round(x)`, which rounds midpoints to even
(28.5 → 28, 29.5 → 30), so a position can be rounded twice in different directions.

**TreeDataGrid.** [`TreeDataGridCellContentPanel`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellContentPanel.cs#L60)
passes a position already rounded the way native centring rounds it (`952bc9da`).

**Fix in Uno.** Round the final visual offset once, after margins and alignment, and use
`Math.Round(x, MidpointRounding.AwayFromZero)` (or `Math.Floor(x + 0.5)`) so that midpoints
do not alternate direction with parity.

## 4. Assigning `Text` to a collapsed `TextBlock` still costs a frame request

**Measured.** After direct text, 29% of the UI thread's lock-contention time during
vertical scrolling came from `TextBlock.OnTextChanged` on the collapsed `PART_Text`.
Skipping the assignment, together with not rebuilding unchanged glyph runs (`b4219957`),
reduced local Uno medians (sort 17.7 → 12.5 ms, resize 1.82 → 1.39 ms, vertical scroll
1.02 → 0.89 ms).

**Uno.** `TextBlock.OnTextChanged` → `UpdateInlines` (clears and rebuilds `Inlines`) →
`InvalidateTextBlock` → `InvalidateInlineAndRequireRepaint` →
`Visual.Compositor.InvalidateRender(Visual)` → `CompositionTarget.RequestNewFrame()`, which
takes a lock shared with the render thread. None of this checks whether the element is
visible or in the live tree.

**TreeDataGrid.** The presenter assigns `PART_Text.Text` only while the text block itself
displays the text, exactly as before in that case
([`SetText`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L157),
[`UpdateVisibility`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L172)).

**Fix in Uno.** Defer inline rebuilding and render invalidation while the element is
collapsed or not loaded, and do the work when it becomes visible:

```csharp
protected virtual void OnTextChanged(string oldValue, string newValue)
{
    if (Visibility == Visibility.Collapsed || !IsLoaded)
    {
        _inlinesDirty = true;          // rebuild in MeasureOverride / on visibility change
        InvalidateMeasure();           // cheap flag; no compositor call
        return;
    }
    UpdateInlines(newValue);
    InvalidateTextBlock();
    ...
}
```

## 5. Every visual property change drops the picture and dirties the subtree

**Measured.** In steady-state horizontal scrolling, `Visual.InvalidatePaint` was the
largest single cost of the layout pass (29% of samples), and `ContainerVisual.SetMatrixDirty`
the largest part of `ScrollViewer.ChangeView` (20–25%). In the distant diagonal jump, with
text shaping cached, `InvalidatePaint` is 49% of the non-accessibility samples. Only 3 rows
(vertical) or 15 cells (horizontal) move per step.

**Uno.** `Visual.OnPropertyChangedCore` starts every property change with
`Compositor.InvalidateRender(this)`:

```csharp
// Uno.UI.Composition 6.7.135
private void InvalidateRenderPartial(Visual visual)
{
    visual.SetMatrixDirty();     // ContainerVisual: recursive over all descendants
    visual.InvalidatePaint();    // sk_refcnt_safe_unref(_picture) + invalidate ancestors' children pictures
    visual.CompositionTarget?.RequestNewFrame();
}
```

This covers transform-only properties such as `Offset`, `ArrangeOffset` (set by
`UIElement.OnArrangeVisual` whenever an element is arranged at a new position) and
`AnchorPoint` (the scroll offset). Those properties do not change what the visual draws,
but they discard its recorded `SKPicture`, so it must be re-recorded (for text, the glyph
run is rebuilt), and they walk and flag every descendant.

**TreeDataGrid.** Only the recycled elements are moved (layout slots are absolute; see
[`TreeDataGridCellsPresenter.ArrangeElement`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCellsPresenter.cs#L508)),
and a direct-text canvas is invalidated only when its glyph run actually changes
([`TextCanvas.SetFrame`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L587)).
The remaining cost cannot be avoided from a control.

**Fix in Uno.** Separate transform invalidation from content invalidation, and make the
total matrix lazy:

```csharp
private protected override void OnPropertyChangedCore(string? propertyName, bool isSubPropertyChange)
{
    switch (propertyName)
    {
        case "Offset": case "ArrangeOffset": case "AnchorPoint":
        case "TransformMatrix": case "Scale": case "RotationAngle": case "CenterPoint":
            // The content is unchanged: keep this visual's picture, only the parent's
            // composed children picture is stale.
            _transformVersion++;                      // descendants compare versions lazily
            InvalidateParentChildrenPicture(includeSelf: false);
            CompositionTarget?.RequestNewFrame();
            return;
    }
    Compositor.InvalidateRender(this);
    ...
}

// TotalMatrix: recompute when (own version, parent's composed version) changed, instead of
// ContainerVisual.SetMatrixDirty walking the subtree on every change.
internal Matrix4x4 TotalMatrix =>
    _cachedParentVersion == Parent?.ComposedVersion && _cachedOwnVersion == _transformVersion
        ? _totalMatrix : RecomputeTotalMatrix();
```

## 6. Each scroll step re-anchors the content and schedules an arrange

**Measured.** With timers around the host's calls, `ScrollViewer.ChangeView` averaged about
0.4 ms per step on macOS and the following layout pass about 0.7 ms. Avalonia's whole
horizontal step is 0.23–0.5 ms. TreeDataGrid code is under 5% of `ChangeView` samples.

**Uno.** `ScrollViewer.ChangeView` → `ScrollContentPresenter.Set` →
`ScrollContentPresenter.Update` sets `visual.AnchorPoint` and `visual.Scale` on the content
visual. With finding 5, that dirties every descendant's matrix and invalidates paint.
`Updated` → `ScrollViewer.OnPresenterScrolled` → `ScrollViewer.Update` →
`UIElement.InvalidateArrange` → `XamlRoot.InvalidateArrange` → `NativeDispatcher.Enqueue`,
which takes the dispatcher lock (16% of `ChangeView` samples were lock contention). It then
raises effective-viewport changes to the subtree (`ViewportInfo.GetRelativeTo`,
`IFrameworkElement_EffectiveViewport.OnParentViewportChanged`).

**TreeDataGrid.** The grid reacts to `ViewChanged` with one viewport update and realizes only
what enters the viewport ([`TreeDataGrid.UpdateViewport`](../src/TreeDataGrid.Controls.Uno/TreeDataGrid.cs#L288)).

**Fix in Uno.** Treat the scroll offset as a render-time translation of the content (as a
compositor-side transform), which with finding 5 no longer dirties descendants. Coalesce
arrange requests into a flag checked by the next layout pass instead of enqueuing a
dispatcher item per change:

```csharp
// ScrollViewer.Update: the extent and viewport did not change, only the offset did.
if (!extentOrViewportChanged)
{
    RaiseViewChanged(isIntermediate);
    InvalidateEffectiveViewport();   // a flag consumed by the next layout pass
    return;                           // no InvalidateArrange, no dispatcher enqueue
}
```

## 7. The UI thread contends for locks with the render thread and pools

**Measured.** `Monitor.Enter_Slowpath` was 7–16% of UI-thread scroll samples. The callers
were:

- `CompositionTarget.RequestNewFrame` (from `Compositor.InvalidateRender`, `Visual.set_IsArrangePending`,
  `TextBlock.OnTextChanged`);
- `NativeDispatcher.EnqueueCore` (from `XamlRoot.InvalidateArrange`);
- `WeakReferencePool.RentFromPool` (from `DependencyObjectStore..ctor`, when
  `VisualStateManager` and `UIElement` stores are created lazily);
- `LinearArrayPool<T>.Bucket.TryPop` (from `DependencyPropertyDetailsCollection.TryGetPropertyDetails`
  in `DependencyObjectStore.SetValue`);
- `ConditionalWeakTable` enumeration in `EventManager.RaiseLayoutUpdated`;
- `SerialDisposable.set_Disposable` (from `BindingExpression.ApplyBinding` for template
  bindings).

Rendering runs on a separate thread, so the first invalidation of a frame can start a render
while the UI thread continues to change the tree.

**TreeDataGrid.** Fewer invalidations (findings 4 and 5) and no per-cell bindings in the
text template reduce how often these locks are taken.

**Fix in Uno.**
- `RequestNewFrame`: set an `Interlocked.Exchange` flag and signal the render loop only on
  the 0→1 transition, instead of locking on every call.
- Pools used only on the UI thread (`WeakReferencePool`, `LinearArrayPool` buckets for
  property details) can be `[ThreadStatic]` or unsynchronized.
- `EventManager.RaiseLayoutUpdated`: keep a plain list of subscribers maintained on the UI
  thread, rather than enumerating a `ConditionalWeakTable` under its lock each layout pass.

## 8. Visibility and opacity changes are expensive

**Measured.** Sorting (a source `Reset`) collapsed and re-showed every visible row, and the
compositor's damage tracking made sorting 1.5–1.8× slower than Avalonia. Keeping the rows
visible until layout rebinds them (`b03e0c06`) brought sorting to 0.70–1.23×.

**Uno.**
- `Visual.OnPropertyChangedCore("Opacity")` calls `RecursiveInvalidate`, which drops the
  picture of every descendant.
- Hiding a visual damages its last rendered region (`Visual.DamageLastRenderedRegion` →
  `ICompositionTarget.AddDamage`). The damage accumulates as an `SKPath` combined with
  `SKPath.Op`, and clip paths are built per visual (`Visual.GetTotalClipPath`,
  `BorderVisual.GetPrePaintingClipping`, `SKPath.Rewind`).
- `UIElement.OnVisibilityChangedPartial` calls
  `AutomationPeer.RaiseAutomaticPropertyChanges`, which evaluates `IsEnabledCore`,
  `IsOffscreenCore` (global bounds), `GetNameCore` and `GetItemStatusCore` for the element.

**TreeDataGrid.** [`TreeDataGridRowsPresenter`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRowsPresenter.cs#L39)
defers hiding rows on `Reset`/`Replace` (`_deferResetVisibility`, used at line 433). Cell
state overlays change `Opacity` only on leaf `Border`s, which have no descendants.

**Fix in Uno.**
- Apply opacity as layer alpha at composition time (`SaveLayerAlpha` around the subtree
  picture) instead of re-recording every descendant.
- Track damage as a short list of rectangles, merged when they overlap, and fall back to one
  bounding rectangle beyond a small count, instead of path boolean operations.
- Evaluate automatic automation properties only when a peer has listeners (see finding 9).

## 9. macOS accessibility is always on

**Measured.** 57% of the UI-thread samples of a distant diagonal jump on macOS were
`NativeUno.uno_accessibility_update_value`, plus `uno_accessibility_update_label`,
`MacOSAccessibility.OnSizeOrOffsetChanged` and `AutomationPeer.IsOffscreen` →
`UIElement.GetGlobalBoundsWithOptions` (17% of horizontal-scroll layout). The Linux CI
runner has no accessibility tree, which is one reason macOS ratios were worse.

**Uno.**
- `MacOSAccessibility.IsAccessibilityEnabled` returns true whenever a window handle exists.
- `AccessibilityRouter.OnVisualOffsetOrSizeChanged` → `MacOSAccessibility.OnSizeOrOffsetChanged`
  calls `uno_accessibility_update_frame` and `uno_accessibility_update_visibility` for every
  moved or resized visual, synchronously on the UI thread.
- `AutomationPeer.RaisePropertyChangedEvent` for `ValueProperty` calls
  `uno_accessibility_update_value`, a P/Invoke with a string marshalled per call.

**TreeDataGrid.** The cell peer raises value changes only when a realized cell's value
actually changes
([`TreeDataGridCellAutomationPeer.NotifyValueChanged`](../src/TreeDataGrid.Controls.Uno/Automation/Peers/TreeDataGridCellAutomationPeer.cs#L254)).
This is required so that the native mirror of a recycled cell does not keep the old value,
and the text block path paid the same cost.

**Fix in Uno.**
- Build and update the native accessibility tree only while an assistive client is connected.
  On macOS that means `NSWorkspace.shared.isVoiceOverEnabled`, or the first
  `accessibilityChildren` request from an AX client.
- Mark changed peers dirty and flush frames, visibility and values once per rendered frame,
  in one native call with a batch.
- Compute frames lazily when the AX client asks, instead of on every offset change.

```csharp
// Sketch: MacOSAccessibility
protected override void OnSizeOrOffsetChanged(Visual visual)
{
    if (!IsAccessibilityEnabled || !_clientConnected) return;
    _dirty.Add(((ContainerVisual)visual).Handle);   // flushed in OnFrameRendered
}
```

## 10. `VisualTreeHelper.GetChild` enumerates children for every index

**Measured.** In the Uno benchmark host, walking cells with `GetChildrenCount`/`GetChild`
produced tens of MB of garbage per run
(`Enumerable+IEnumerableWhereIterator<UIElement>`, `MaterializableList<T>.Enumerator`), the
largest allocation source in the whole run. The resulting collections can suspend the UI
thread inside later measured intervals; GC suspension appeared in `UpdateLayout` samples.

**Uno.**

```csharp
// Uno 6.7.135, VisualTreeHelper
public static DependencyObject GetChild(DependencyObject reference, int childIndex) =>
    (from c in (reference as UIElement)?.GetChildren() where !(c is ElementStub) select c)
        .ElementAtOrDefault(childIndex);
public static int GetChildrenCount(DependencyObject reference) =>
    (reference as UIElement)?.GetChildren().Count(c => !(c is ElementStub)) ?? 0;
```

Each call allocates an iterator and scans from the first child, so a full traversal is
O(n²) per parent.

**TreeDataGrid.** The benchmark host's verification walks `Panel.Children` and
`Border.Child` directly (`benchmarks/TreeDataGrid.Parity.Uno/App.xaml.cs`, `ContainsText`,
`b4219957`).

**Fix in Uno.** Index the materialized child list and skip `ElementStub`s without LINQ. Keep
a stub count so that the common case (no stubs) is a direct index:

```csharp
public static DependencyObject? GetChild(DependencyObject reference, int childIndex)
{
    if (reference is not UIElement element) return null;
    var children = element.GetChildren();          // MaterializableList<UIElement>
    if (element.ElementStubCount == 0)
        return (uint)childIndex < (uint)children.Count ? children[childIndex] : null;
    for (int i = 0, visible = 0; i < children.Count; i++)
        if (children[i] is not ElementStub && visible++ == childIndex) return children[i];
    return null;
}
```

## 11. `SKCanvasElement` and text APIs are not enough for a replacement text renderer

Drawing text outside `TextBlock` needed several workarounds.

| Limitation | Uno | TreeDataGrid workaround | Suggested change |
| --- | --- | --- | --- |
| The canvas always clips to its size with an antialiased clip, but a `TextBlock` does not clip its ink unless layout clips it | `SKCanvasVisual.Paint` → `ClipRect(new SKRect(0,0,Size.X,Size.Y), Intersect, antialias: true)` | The presenter is a `Panel` with the text block's exact layout (so Uno applies the same layout clip), hosting a canvas 4 DIPs larger on every side ([`Bleed`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridTextPresenter.cs#L43)) | `SKCanvasElement.ClipToBounds` (default true) |
| Inherited opacity is not applied: the render callback receives only the canvas, not `PaintingSession.Opacity` | `SKCanvasVisual.Paint` ignores `session.Opacity` | Opacity on the text block makes it ineligible; opacity on ancestors is a documented limitation | Apply `session.Opacity` with `SaveLayerAlpha`, or pass it to `RenderOverride` |
| The constructor throws when the platform is not supported, so the element cannot appear in shared XAML | `SKCanvasElement..ctor` → `PlatformNotSupportedException` | Created in code only when `SKCanvasElement.IsSupportedOnCurrentPlatform()` ([`AttachTextPresenter`](../src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridCell.TextPresentation.cs#L15)) | A no-op fallback instead of throwing |
| No public API for the fonts and shaping `TextBlock` uses | `Microsoft.UI.Xaml.Documents.TextFormatting.FontDetailsCache.GetFont` and `FontDetails` are internal | Reflection with `DynamicDependency` for trimming ([`DirectTextFont`](../src/TreeDataGrid.Controls.Uno/Primitives/DirectTextFont.cs#L41)); a direct `HarfBuzzSharp` package reference matching Uno's version ([csproj](../src/TreeDataGrid.Controls.Uno/TreeDataGrid.Controls.Uno.csproj#L18)) | A public `TextFormatter`/`FontDetails` surface (see below) |
| Fonts load asynchronously in the browser; a control cannot observe completion | `FontDetailsCache` returns a fallback plus an internal `Task<FontDetails>` | Falls back to the text block until the font is loaded, and retries on the next text change | Expose the load task or a `FontLoaded` event |

A public text API would let controls draw text exactly as `TextBlock` does, without
reflection or a copy of its algorithms:

```csharp
namespace Uno.UI.Text;                                  // proposed

public sealed class TextFormatter
{
    public static TextFormatter Get(FontFamily? family, double size, FontWeight weight,
        FontStretch stretch, FontStyle style, bool textScaleEnabled);   // same cache as TextBlock
    public Task Loaded { get; }
    public FormattedLine FormatLine(string text, double availableWidth,
        TextTrimming trimming, TextAlignment alignment);                  // UnicodeText rules
    public float LineHeight { get; }
}

public readonly struct FormattedLine
{
    public Size DesiredSize { get; }        // TextBlock rounding applied by the caller's element
    public void Draw(SKCanvas canvas, SKPoint origin, Color foreground, float opacity);
}
```

## 12. Cold JIT dominates short measurements

**Measured.** The parity workload measures 25 iterations after 5 warm-up iterations per
operation. In 200-iteration runs, Uno's vertical scroll median fell from about 2.0 ms to
0.7 ms after about 50 iterations, and Avalonia's from about 3.0 ms to 0.48 ms. The early
iterations mostly run tier-0 code. Uno's scroll path executes much more framework code per
step, so it is more exposed to tier-0 cost. Both frameworks ship IL-only assemblies (checked
for ReadyToRun headers).

**TreeDataGrid.** Value getters are compiled when a column presentation is created, as
Avalonia's `ColumnBase` does, instead of compiling an expression tree on each column's first
realization while scrolling
([`CellColumn.cs`](../src/TreeDataGrid.Controls.Uno/Presentation/CellColumn.cs#L136),
`45021a75`, `5d9984cc`).

**Fix in Uno.** Publish ReadyToRun (or composite R2R) builds of `Uno.UI`,
`Uno.UI.Composition` and the Skia runtime packages for desktop targets. Apps can also opt in
with `<PublishReadyToRun>`, but that does not help `dotnet run` or tests.

## Other Uno issues found during the port

These are not performance problems, but each needed a workaround in the port.

| Issue | Uno | Workaround |
| --- | --- | --- |
| `VisualStateManager.GoToState` only finds groups declared on the template root | `VisualStateManager.GoToState` | State groups moved to the template roots in [`Generic.xaml`](../src/TreeDataGrid.Controls.Uno/Themes/Generic.xaml#L310) |
| Trimmed browser builds cannot bind to non-public types | Binding accessor generation handles public `[Bindable]` types only | Public `[Bindable]` sample models (`samples/TreeDataGridUnoSample/SharedModelMetadata.cs`, `NativeTextAssignmentSource`) |
| `Pivot` renders stacked and throws on Skia desktop | `Pivot` | The demo uses `TabView` |
| `Application.Exit` is not implemented on WebAssembly (build error `Uno0001`) | `Application.Exit` | `#if !__WASM__` |
| `AppWindow.Resize` takes physical pixels | `AppWindow.Resize` | Scale by `XamlRoot.RasterizationScale` |

## What to take from this

- The TreeDataGrid code itself, including realization, recycling, bindings and text shaping,
  is now a small share of scroll samples, and it allocates much less than Avalonia.
- Most of the remaining gap to Avalonia comes from three Uno changes: transform-only
  invalidation with lazy matrices (finding 5), scrolling without per-step dispatcher and
  subtree work (finding 6), and on-demand, batched native accessibility on macOS
  (finding 9).
- Findings 1, 2 and 4 help every app with many `TextBlock`s (lists, grids, logs), not
  only TreeDataGrid, and would let TreeDataGrid drop its direct text presenter in favour of
  the platform text block.
