using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TreeDataGridCore.Selection;
using Uno.Controls.Presentation;
using Windows.Foundation.Metadata;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private readonly IncrementalTextSearch _textSearch = new();
    private char? _pendingSearchSurrogate;
    private int _textSearchStructureRevision;
    private void InitializeTextInput()
    {
        // Uno advertises an unimplemented CharacterReceived event on Skia.
        // Never subscribe blindly or replace composed input with VirtualKey A-Z.
        if (ApiInformation.IsEventPresent("Microsoft.UI.Xaml.UIElement", nameof(CharacterReceived)))
            CharacterReceived += OnCharacterReceived;
    }
    private void ResetTextSearch()
    {
        _pendingSearchSurrogate = null;
        _textSearch.Reset();
    }
    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (IsOwnInput(e.OriginalSource as DependencyObject)) _selectionInteraction?.OnTextInput(this, e);
    }
    internal void ProcessSelectionTextInput(CharacterReceivedRoutedEventArgs e)
    {
        if (e.Handled || EditingCell is not null || IsEditor(e.OriginalSource as DependencyObject) ||
            !IsOwnInput(e.OriginalSource as DependencyObject))
        { ResetTextSearch(); return; }
        var character = e.Character;
        if (char.IsHighSurrogate(character)) { _pendingSearchSurrogate = character; return; }
        var high = _pendingSearchSurrogate;
        _pendingSearchSurrogate = null;
        if (char.IsLowSurrogate(character))
        {
            if (high is { } first) OnTextInput(new string(new[] { first, character }));
            return;
        }
        if (!char.IsControl(character)) OnTextInput(character.ToString());
    }

    /// <summary>Processes committed text using the current row selection and opted-in columns.</summary>
    /// <remarks>The argument is text, not a virtual key; native hosts may deliver multiple code units.</remarks>
    protected virtual void OnTextInput(string text)
    {
        if (!_loaded || EditingCell is not null || _presentation is not { } presentation ||
            presentation.Selection.Model is not ITreeDataGridRowSelectionModel selection || presentation.Rows.Count == 0) return;
        var culture = CultureInfo.CurrentCulture;
        var candidate = _textSearch.GetCandidate(text, Environment.TickCount64, culture);
        if (candidate.Length == 0) return;
        var revision = _presentationRevision;
        var structureRevision = _textSearchStructureRevision;
        var selected = presentation.Rows.ModelIndexToRowIndex(selection.AnchorIndex);
        var count = presentation.Rows.Count;
        // Match Avalonia's column order: every opted-in column searches from the
        // original anchor, so a later matching column has the final selection.
        foreach (var column in presentation.NativeColumns)
        {
            var enabled = column.IsTextSearchEnabled;
            if (!IsCurrent()) return;
            if (!enabled) continue;
            var start = Math.Clamp(IncrementalTextSearch.IsSingleCharacter(candidate) ? selected + 1 : selected, 0, count);
            for (var step = 0; step < count; ++step)
            {
                var row = (start + step) % count;
                if (!IsCurrent()) return;
                var index = presentation.Rows.RowIndexToModelIndex(row);
                var model = presentation.Rows[row].Model; // Capture Core's flyweight now.
                var value = column.GetSearchText(model);
                if (!IsCurrent()) return;
                if (!IncrementalTextSearch.Matches(value, candidate, culture)) continue;
                // Selectors and cancellation/edit handlers are application code.
                // Re-resolve the model path instead of applying a stale row offset.
                if (QueryCancelSelection() || !IsCurrent() || !CommitEdit() || !IsCurrent()) return;
                var currentRow = presentation.Rows.ModelIndexToRowIndex(index);
                if (currentRow < 0 || !ReferenceEquals(presentation.Rows[currentRow].Model, model)) return;
                selection.SelectedIndex = index;
                if (!IsCurrent()) return;
                _textSearch.Accept(candidate);
                BringCellIntoView(currentRow, Math.Clamp(_currentColumn, 0, Math.Max(0, presentation.NativeColumns.Count - 1)));
                if (!IsCurrent()) return;
                break;
            }
        }
        bool IsCurrent() => _loaded && revision == _presentationRevision && structureRevision == _textSearchStructureRevision && ReferenceEquals(_presentation, presentation) &&
            ReferenceEquals(presentation.Selection.Model, selection) && presentation.Rows.Count == count;
    }

    private bool IsOwnInput(DependencyObject? source)
    {
        for (var current = source; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, this)) return true;
            if (current is TreeDataGrid) return false;
        }
        return false;
    }
}
