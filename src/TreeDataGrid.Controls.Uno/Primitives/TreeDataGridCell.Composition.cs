using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    // Allocate native observers only for actual editors on supported heads,
    // never for the potentially thousands of display-only realized cells.
    private CompositionObserver? _compositionObserver;
    internal bool IsTextComposing => _compositionObserver?.BlocksEditKeys == true;

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_edit is null || _unrealizing) return;
        for (var current = e.OriginalSource as DependencyObject;
            current is not null && !ReferenceEquals(current, this); current = VisualTreeHelper.GetParent(current))
        {
            // Embedded grids/inner cells own their own editor and input lifetime.
            if (current is TreeDataGrid or TreeDataGridCell) return;
            if (current is TextBox editor)
            {
                ObserveComposition(editor);
                return;
            }
        }
    }

    private void ObserveComposition(TextBox? editor)
    {
        if (!NativeTreeDataGridCapabilities.TextComposition || editor is null || _edit is not { } session) return;
        (_compositionObserver ??= new(this)).Attach(editor, session, RealizationVersion);
    }

    private sealed class CompositionObserver(TreeDataGridCell owner)
    {
        private TextBox? _editor;
        private CellEditSession? _session;
        private int _realization;
        private EditorCompositionState _state;
        public bool BlocksEditKeys => IsCurrent && _state.BlocksEditKeys;
        private bool IsCurrent => _editor is not null && _session is not null &&
            ReferenceEquals(owner._edit, _session) && owner.RealizationVersion == _realization && !owner._unrealizing;

        public void Attach(TextBox editor, CellEditSession session, int realization)
        {
            if (ReferenceEquals(_editor, editor) && ReferenceEquals(_session, session) && _realization == realization) return;
            Detach();
            _editor = editor;
            _session = session;
            _realization = realization;
#pragma warning disable Uno0001 // Only subscribed after the linker-visible native capability check.
            editor.TextCompositionStarted += OnStarted;
            editor.TextCompositionEnded += OnEnded;
#pragma warning restore Uno0001
        }

        public void Detach()
        {
            var editor = _editor;
            _editor = null;
            _session = null;
            _state.Reset();
            if (editor is null) return;
#pragma warning disable Uno0001 // Symmetric removal of supported native events.
            editor.TextCompositionStarted -= OnStarted;
            editor.TextCompositionEnded -= OnEnded;
#pragma warning restore Uno0001
        }

        private void OnStarted(TextBox sender, TextCompositionStartedEventArgs args)
        {
            if (ReferenceEquals(sender, _editor) && IsCurrent) _state.Begin();
        }

        private void OnEnded(TextBox sender, TextCompositionEndedEventArgs args)
        {
            if (!ReferenceEquals(sender, _editor) || !IsCurrent) return;
            var generation = _state.End();
            // An IME's terminating Enter/Escape belongs to composition, not to
            // the grid transaction. Keep it protected through this dispatch turn.
            // The ticket also rejects an end callback queued by a retired editor.
            if (!owner.DispatcherQueue.TryEnqueue(() =>
            {
                if (!_state.Complete(generation) || !IsCurrent) return;
                // Focus loss may have arrived before TextCompositionEnded. In
                // that order, finish the normal focus-loss commit only now.
                if (owner.IsLoaded && owner.XamlRoot is { } root &&
                    !TreeDataGrid.ContainsFocus(owner, FocusManager.GetFocusedElement(root) as DependencyObject))
                    owner.CommitEdit();
            }))
                _state.Complete(generation);
        }
    }
}
