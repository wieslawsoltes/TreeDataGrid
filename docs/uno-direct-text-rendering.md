# Uno direct cell text rendering

On Skia targets (desktop and WebAssembly), Uno text cells draw their text directly with
Skia instead of creating a `TextBlock` layout for every value. Profiles of scrolling
attributed most of Uno's remaining time to per-value `TextBlock` work (Unicode text
analysis, line layout and inline re-creation); Avalonia's text cell does the equivalent of
this presenter with its `TextLayout`.

## Templates are unchanged

`PART_Text` remains in the text and expander cell templates and is still the source of the
text's appearance: font family, size, weight, style and stretch, foreground (including the
selected-state visual-state setters), text alignment, trimming, wrapping, margin and
padding. When a cell's template places `PART_Text` in a `Panel`, the cell adds an internal
`TreeDataGridTextPresenter` next to it, and exactly one of the two displays the text:

- The presenter has the text block's layout (same desired size, rounding, alignment and
  therefore the same layout clip) and hosts a Skia canvas that draws the glyph run the text
  block would draw.
- While the presenter draws, `PART_Text` is collapsed and its `Text` is not assigned:
  assigning it re-creates its inlines and requests a frame. Whenever the text block itself
  displays the text, the cell assigns its `Text` exactly as before (local value, string
  instance, binding replacement and property callbacks), and it is synchronized when it is
  shown again.
- The presenter's automation peer exposes the displayed text as a text block's does
  (control type `Text`, name = text).

## Same pixels as the text block

The presenter reproduces Uno's `TextBlock`/`UnicodeText` pipeline for single-line
left-to-right text:

- the same `SKFont` and HarfBuzz font objects, obtained from Uno's font cache for the text
  block's family, (scaled) size, weight, stretch and style;
- the same shaping runs (text split at script changes), glyph positions, cluster widths and
  width without trailing whitespace;
- the same line height and baseline, `CharacterEllipsis` trimming and alignment offsets;
- the same desired-size rounding, and ink outside the line box is not cut (the canvas
  extends past the text box) unless layout clips the text block.

Everything else is rendered by `PART_Text` itself: wrapping, word ellipsis, characters
outside Latin/Latin-1/Latin Extended-A or missing from the font, tabs and line breaks,
right-to-left flow, character spacing, decorations, text selection, line stacking other
than `MaxHeight`, explicit sizes, opacity on the text block, non-solid brushes, and fonts
that are still loading.

The `direct-text` native suite compares 240 states (both themes, sizes, weights, italics,
trimming widths, alignments, padding, translucent and selected foregrounds, empty,
whitespace and fallback text) against a text block forced onto the native path, and
requires identical sizes and pixels.

Shaping does not depend on the cell width, so each font keeps a bounded cache of shaped
strings, and each shaped string keeps its last arranged glyph run; cells showing the same
value in the same column share both.

## Limitations and switch

- The canvas does not apply opacity set on ancestors of the cell (Uno's `SKCanvasElement`
  ignores inherited opacity); the default themes do not fade cell text.
- On the Windows App SDK target, and where `SKCanvasElement` is not supported, cells use
  `PART_Text` as before.

Disable direct rendering with the `Uno.Controls.TreeDataGrid.DirectTextRendering`
`AppContext` switch set to `false`, or the `TDG_DIRECT_TEXT=0` environment variable.
