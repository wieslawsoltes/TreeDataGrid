using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCell
{
#if !WINDOWS
    private TreeDataGridTextPresenter? _presenter;

    /// <summary>
    /// Adds a direct Skia text presenter next to <c>PART_Text</c>, which remains the source of
    /// the text's appearance and renders whatever the presenter cannot.
    /// </summary>
    private void AttachTextPresenter()
    {
        if (_presenter is { } old)
        {
            _presenter = null;
            old.Detach();
        }
        if (!TreeDataGridTextPresenter.IsEnabled || _text is not { Parent: Panel host } text) return;
        var presenter = new TreeDataGridTextPresenter(text);
        host.Children.Insert(host.Children.IndexOf(text) + 1, presenter);
        presenter.SetShown(text.Visibility == Visibility.Visible);
        _presenter = presenter;
    }
#endif

    private void SetDisplayText(TextBlock text, string value)
    {
        text.Text = value;
#if !WINDOWS
        if (_presenter is { } presenter && ReferenceEquals(presenter.Source, text)) presenter.SetText(value);
#endif
    }

    private void SetTextVisibility(TextBlock text, bool shown)
    {
#if !WINDOWS
        if (_presenter is { } presenter && ReferenceEquals(presenter.Source, text))
        {
            presenter.SetShown(shown);
            return;
        }
#endif
        SetContentVisibility(text, shown ? Visibility.Visible : Visibility.Collapsed);
    }
}
