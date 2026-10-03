using Microsoft.UI.Xaml;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    private int _renderVersion;

    private void RenderCurrentValue()
    {
        if (_unrealizing) return;
        var render = unchecked(++_renderVersion);
        var realization = RealizationVersion;
        var updating = _updating;
        _updating = true;
        try
        {
            var usesInner = UsesInnerCellControl;
            if (!Current()) return;
            if (!usesInner && _text is { } text && _kind == Presentation.CellKind.Text)
            {
                var value = DisplayText;
                if (!Current() || !ReferenceEquals(_text, text)) return;
                text.Text = value ?? string.Empty;
                if (!Current()) return;
            }
            if (!usesInner && _check is { } check && _kind == Presentation.CellKind.CheckBox)
            {
                var value = DisplayCheckBoxValue;
                if (!Current() || !ReferenceEquals(_check, check)) return;
                check.IsChecked = value;
                if (!Current()) return;
                var readOnly = DisplayCheckBoxIsReadOnly;
                if (!Current() || !ReferenceEquals(_check, check)) return;
                check.IsEnabled = !readOnly;
                if (!Current()) return;
            }
            if (!usesInner && _content is { } content && _kind == Presentation.CellKind.Template)
            {
                var value = _value?.Value;
                if (!Current() || !ReferenceEquals(_content, content)) return;
                content.Content = value;
                if (!Current()) return;
            }
            if (_expander is { } expander && _expanderValue is { } expanded)
            {
                var isExpanded = expanded.IsExpanded;
                if (!Current() || !ReferenceEquals(_expander, expander)) return;
                if (expander is TreeDataGridExpanderButton button) button.IsExpanded = isExpanded;
                else VisualStateManager.GoToState(expander, isExpanded ? "Expanded" : "Collapsed", false);
                if (!Current()) return;
                var show = expanded.ShowExpander;
                if (!Current() || !ReferenceEquals(_expander, expander)) return;
                expander.Opacity = show ? 1 : 0;
                if (!Current()) return;
                expander.IsHitTestVisible = show;
            }
        }
        finally { _updating = updating; }

        // A nested refresh of this same realization supersedes the older value
        // just as a new model does. This local function is not a delegate and
        // creates no per-cell closure allocation.
        bool Current() => !_unrealizing && realization == RealizationVersion && render == _renderVersion;
    }

    private void WriteCurrentCheckBoxValue(bool? next)
    {
        if (_unrealizing || _value is not { } value) return;
        var realization = RealizationVersion;
        var canWrite = value.CanWrite;
        if (canWrite && !_unrealizing && realization == RealizationVersion && ReferenceEquals(_value, value))
            value.Write(next);
    }
}
