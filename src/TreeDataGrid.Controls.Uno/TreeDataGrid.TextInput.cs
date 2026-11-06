using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TreeDataGridCore.Selection;
using Uno.Controls.Presentation;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    private readonly IncrementalTextSearch _textSearch = new();
    private Utf16TextInput _textInput;
    private int _textInputOperation;
    private int _textSearchStructureRevision;
    private void InitializeTextInput()
    {
        // Capability detection rejects Uno's [NotImplemented] event stubs.
        // Keep composed text on supported heads, never substitute VirtualKey A-Z.
        if (NativeTreeDataGridCapabilities.CharacterReceived)
        {
#pragma warning disable Uno0001 // The unsupported event is excluded by the capability check above.
            CharacterReceived += OnCharacterReceived;
#pragma warning restore Uno0001
        }
    }
    private void ResetTextSearch()
    {
        unchecked { ++_textInputOperation; }
        _textInput.Reset();
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
        if (_textInput.Push(e.Character, Environment.TickCount64) is { } text) OnTextInput(text);
    }

    /// <summary>Processes committed text using the current row selection and opted-in columns.</summary>
    /// <remarks>The argument is text, not a virtual key; native hosts may deliver multiple code units.</remarks>
    protected virtual void OnTextInput(string text)
    {
        var operation = unchecked(++_textInputOperation);
        if (!_loaded || EditingCell is not null || _presentation is not { } presentation) return;
        var revision = _presentationRevision;
        var structureRevision = _textSearchStructureRevision;
        var selectionModel = presentation.Selection.Model;
        if (!IsCurrentEpoch() || selectionModel is not ITreeDataGridRowSelectionModel selection) return;
        var culture = CultureInfo.CurrentCulture;
        var candidate = _textSearch.GetCandidate(text, Environment.TickCount64, culture);
        if (!IsCurrent() || candidate.Length == 0) return;
        var count = presentation.Rows.Count;
        if (!IsCurrent() || count == 0) return;
        var anchor = selection.AnchorIndex;
        if (!IsCurrent()) return;
        var selected = presentation.Rows.ModelIndexToRowIndex(anchor);
        if (!IsCurrent()) return;
        // Match Avalonia's column order: every opted-in column searches from the
        // original anchor, so a later matching column has the final selection.
        foreach (var column in presentation.NativeColumns)
        {
            var enabled = column.IsTextSearchEnabled;
            if (!IsCurrent()) return;
            if (!enabled) continue;
            var start = (int)Math.Clamp(IncrementalTextSearch.IsSingleCharacter(candidate) ? (long)selected + 1 : selected, 0, count);
            for (var step = 0; step < count; ++step)
            {
                var row = IncrementalTextSearch.WrapIndex(start, step, count);
                if (!IsCurrent()) return;
                var index = presentation.Rows.RowIndexToModelIndex(row);
                if (!IsCurrent()) return;
                var rowModel = presentation.Rows[row];
                if (!IsCurrent()) return;
                var model = rowModel.Model; // Capture Core's flyweight now.
                if (!IsCurrent()) return;
                var value = column.GetSearchText(model);
                if (!IsCurrent()) return;
                var matches = IncrementalTextSearch.Matches(value, candidate, culture);
                if (!IsCurrent()) return;
                if (!matches) continue;
                // Selectors and cancellation/edit handlers are application code.
                // Re-resolve the model path instead of applying a stale row offset.
                if (QueryCancelSelection() || !IsCurrent() || !CommitEdit() || !IsCurrent()) return;
                var currentRow = presentation.Rows.ModelIndexToRowIndex(index);
                if (!IsCurrent() || (uint)currentRow >= (uint)count) return;
                var currentModel = presentation.Rows[currentRow].Model;
                if (!IsCurrent() || !ReferenceEquals(currentModel, model)) return;
                selection.SelectedIndex = index;
                if (!IsCurrent()) return;
                _textSearch.Accept(candidate);
                BringCellIntoView(currentRow, Math.Clamp(_currentColumn, 0, Math.Max(0, presentation.NativeColumns.Count - 1)));
                if (!IsCurrent()) return;
                break;
            }
        }
        bool IsCurrentEpoch() => _loaded && operation == _textInputOperation &&
            revision == _presentationRevision && structureRevision == _textSearchStructureRevision &&
            ReferenceEquals(_presentation, presentation);
        bool IsCurrent()
        {
            if (!IsCurrentEpoch()) return false;
            var currentSelection = presentation.Selection.Model;
            // A custom source's Selection getter can itself reenter input or
            // replace the presentation. Validate again after reading it.
            return ReferenceEquals(currentSelection, selection) && IsCurrentEpoch();
        }
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
