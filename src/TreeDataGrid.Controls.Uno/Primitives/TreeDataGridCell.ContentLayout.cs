using Microsoft.UI.Xaml;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
    private int _contentLayoutRevision;

    private bool IsContentLayoutCurrent(int realization, int revision) =>
        !_unrealizing && realization == RealizationVersion && revision == _contentLayoutRevision;

    private bool UpdateCurrentContentKind()
    {
        if (_unrealizing) return false;
        var realization = RealizationVersion;
        var revision = unchecked(++_contentLayoutRevision);
        // This virtual getter, the style getters below, and native DP setters
        // can execute application code. Even a same-model, same-index rebind or
        // nested refresh supersedes this particular content-layout operation.
        var inner = UsesInnerCellControl;
        if (!IsContentLayoutCurrent(realization, revision)) return false;
        var editing = IsEditing;
        var kind = _kind;

        if (_expander is { } expander)
        {
            SetContentVisibility(expander, _expanderValue is null ? Visibility.Collapsed : Visibility.Visible);
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            var margin = new Thickness(_indent * 20, 0, 0, 0);
            if (expander.Margin != margin) expander.Margin = margin;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
        }
        if (_text is { } text)
        {
            SetContentVisibility(text, !inner && !editing && kind == Presentation.CellKind.Text ? Visibility.Visible : Visibility.Collapsed);
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            var alignment = DisplayTextAlignment;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            var wrapping = DisplayTextWrapping;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            var trimming = DisplayTextTrimming;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            if (text.TextAlignment != alignment) text.TextAlignment = alignment;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            if (text.TextWrapping != wrapping) text.TextWrapping = wrapping;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            if (text.TextTrimming != trimming) text.TextTrimming = trimming;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
        }
        if (_check is { } check)
        {
            SetContentVisibility(check, !inner && !editing && kind == Presentation.CellKind.CheckBox ? Visibility.Visible : Visibility.Collapsed);
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            var threeState = DisplayCheckBoxIsThreeState;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            if (check.IsThreeState != threeState) check.IsThreeState = threeState;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
        }
        if (_content is { } content)
        {
            SetContentVisibility(content, !inner && !editing && kind == Presentation.CellKind.Template ? Visibility.Visible : Visibility.Collapsed);
            if (!IsContentLayoutCurrent(realization, revision)) return false;
            if (!inner && !ReferenceEquals(content.ContentTemplate, _template)) content.ContentTemplate = _template;
            if (!IsContentLayoutCurrent(realization, revision)) return false;
        }
        if (_editorHost is { } editorHost)
        {
            SetContentVisibility(editorHost, !inner && editing && _editingTemplate is null ? Visibility.Visible : Visibility.Collapsed);
            if (!IsContentLayoutCurrent(realization, revision)) return false;
        }
        if (_editContent is { } editContent)
        {
            SetContentVisibility(editContent, !inner && editing && _editingTemplate is not null ? Visibility.Visible : Visibility.Collapsed);
            if (!IsContentLayoutCurrent(realization, revision)) return false;
        }
        return true;
    }

    private static void SetContentVisibility(UIElement element, Visibility value)
    {
        // Native enum DP setters box even when the effective value is unchanged.
        // Keep the real visibility transition, but do not repeat an equal write.
        if (element.Visibility != value) element.Visibility = value;
    }
}
