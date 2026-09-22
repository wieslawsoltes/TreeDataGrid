using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using UICell = Uno.Controls.Models.TreeDataGrid.ICell;
using UITextCell = Uno.Controls.Models.TreeDataGrid.ITextCell;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

/// <summary>Adapts a public column implementation without copying its Core source.</summary>
internal sealed class CellColumnAdapter<TModel> : CellColumn where TModel : class
{
    private readonly ICellColumn<TModel> _inner;
    private bool _disposed;
    public CellColumnAdapter(IColumn model, ICellColumn<TModel> inner) : base(model)
    {
        _inner = inner;
        _inner.SetWidth(Width);
        _inner.SortDirection = model.SortDirection;
        _inner.PropertyChanged += OnInnerChanged;
    }
    public override object? Header { get => _inner.Header; set => throw new NotSupportedException("The custom column owns its header."); }
    public override bool? CanUserResize => _inner.CanUserResize;
    public override double MinimumWidth => _inner.MinActualWidth;
    public override double MaximumWidth => _inner.MaxActualWidth;
    // Match Avalonia's opt-in measurement contract. Unannotated custom
    // columns retain conservative natural measurement for Auto constraints.
    public override bool RequiresUnconstrainedWidthMeasurement =>
        (_inner as UI.IColumnMeasurementOptions)?.RequiresUnconstrainedWidthMeasurement ?? true;
    public override CellValue CreateCell(IRow row)
    {
        var cell = _inner.CreateCell((IRow<TModel>)row) ?? throw new InvalidOperationException("The column returned no cell.");
        return Adapt(cell, true, null);
    }
    // Public view-row realization transfers the actual UI model to its caller.
    // Do not create a native adapter that would need another ownership registry.
    internal override UICell CreateCellModel(IRow row) => _inner.CreateCell((IRow<TModel>)row) ??
        throw new InvalidOperationException("The column returned no cell.");
    internal static CellValue Adapt(UICell cell, bool ownsModel, HashSet<UICell>? ancestors = null)
    {
        if (cell is CellValue native) return native;
        try
        {
            if (cell is UI.IExpanderCellPresentation expander)
            {
                ancestors ??= new(ReferenceEqualityComparer.Instance);
                if (!ancestors.Add(cell)) throw new InvalidOperationException("An expander cell cannot contain itself or an ancestor.");
                try { return new CustomExpanderValue(expander, ownsModel, ancestors); }
                finally { ancestors.Remove(cell); }
            }
            return new CustomCellValue(cell, ownsModel);
        }
        catch (Exception error)
        {
            try { if (ownsModel) (cell as IDisposable)?.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    // Third-party columns select each row's actual cell model/kind. Do not
    // overwrite it with this adapter's default column kind.
    internal override void ConfigureCell(CellValue value) { }
    internal override bool SupportsRetainedCellReuse => true;
    internal override bool TryReuseCell(CellValue value, IRow row)
    {
        if (_disposed || !_inner.TryReuseCell(value.PresentationModel, (IRow<TModel>)row) || _disposed) return false;
        if (value is CustomCellValue cell) cell.RefreshAfterRetarget();
        if (value is CustomExpanderValue expander) expander.RefreshAfterRetarget();
        return true;
    }
    internal override bool RecordWidth(double width, int rowIndex = -1)
    {
        _inner.CellMeasured(width, rowIndex);
        return base.RecordWidth(width, rowIndex);
    }
    internal override bool SetActualWidth(double width)
    {
        if (Width.IsStar) _inner.CalculateStarWidth(width, Width.Value > 0 ? Width.Value : 1);
        _inner.CommitActualWidth();
        return base.SetActualWidth(width);
    }
    internal override void ModelChanged(PropertyChangedEventArgs e)
    {
        _inner.SetWidth(Width);
        _inner.SortDirection = Model.SortDirection;
        base.ModelChanged(e);
    }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inner.PropertyChanged -= OnInnerChanged;
        (_inner as IDisposable)?.Dispose();
    }
    private void OnInnerChanged(object? sender, PropertyChangedEventArgs e) => RaisePropertyChanged(e);

    private sealed class CustomCellValue : CellValue
    {
        private readonly UICell _inner;
        private readonly bool _ownsModel;
        private bool _disposed;
        public CustomCellValue(UICell inner, bool ownsModel)
        {
            _inner = inner;
            _ownsModel = ownsModel;
            Kind = inner switch { UI.CheckBoxCell => CellKind.CheckBox, UI.TemplateCell => CellKind.Template, _ => CellKind.Text };
            EditGestures = inner.EditGestures;
            UpdateTextOptions();
            if (inner is INotifyPropertyChanged notifications)
            {
                try { notifications.PropertyChanged += OnChanged; }
                catch { notifications.PropertyChanged -= OnChanged; throw; }
            }
        }
        public override object? Value => _inner.Value;
        public override UICell PresentationModel => _inner;
        public override bool CanWrite => _inner is UI.CheckBoxCell check ? !check.IsReadOnly : _inner.CanEdit;
        public override bool? IsThreeState => (_inner as UI.CheckBoxCell)?.IsThreeState;
        public override object? EditTarget => _inner is IEditableObject ? _inner : null;
        public override Exception? Error => (_inner as UI.IBoundCellState)?.Error;
        public override DataTemplate? GetCellTemplate(Microsoft.UI.Xaml.Controls.Control anchor) =>
            (_inner as UI.TemplateCell)?.GetCellTemplate(anchor);
        public override DataTemplate? GetCellEditingTemplate(Microsoft.UI.Xaml.Controls.Control anchor) =>
            (_inner as UI.TemplateCell)?.GetCellEditingTemplate?.Invoke(anchor);
        public override string? DisplayText => (_inner as UITextCell)?.Text;
        private TextCellOptions? _textOptions;
        public override TextCellOptions? TextOptions => _textOptions;
        public override bool CanEdit => _inner.CanEdit;
        public void Refresh() => RaisePropertyChanged(nameof(Value));
        public void RefreshAfterRetarget()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EditGestures = _inner.EditGestures;
            UpdateTextOptions();
        }
        private void UpdateTextOptions()
        {
            if (_inner is not UITextCell text) return;
            var culture = (_inner as UI.ITextCellState)?.Options?.Culture ?? System.Globalization.CultureInfo.CurrentCulture;
            if (_textOptions is { } previous && previous.TextAlignment == text.TextAlignment && previous.TextWrapping == text.TextWrapping &&
                previous.TextTrimming == text.TextTrimming && Equals(previous.Culture, culture)) return;
            _textOptions = new()
            {
                TextAlignment = text.TextAlignment, TextWrapping = text.TextWrapping,
                TextTrimming = text.TextTrimming, Culture = culture,
            };
        }
        public override void Write(object? value)
        {
            if (!CanWrite) throw new InvalidOperationException("The custom cell is read-only.");
            if (_inner is UI.CheckBoxCell check) { check.Value = (bool?)value; return; }
            if (_inner is not UITextCell text) throw new NotSupportedException("The custom cell does not expose a text setter.");
            text.Text = Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture);
        }
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_inner is INotifyPropertyChanged notifications) notifications.PropertyChanged -= OnChanged; }
            finally { if (_ownsModel) (_inner as IDisposable)?.Dispose(); }
        }
        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            // TextCell publishes Text as well as Value. Preserve names so an
            // external value update is not reported twice by the control.
            if (!_disposed) RaisePropertyChanged(e);
        }
    }

    private sealed class CustomExpanderValue : ExpanderCellValue
    {
        private readonly UI.IExpanderCellPresentation _model;
        private readonly bool _ownsModel;
        private CellValue _content;
        private object? _rawContent;
        private bool _disposed;

        public CustomExpanderValue(UI.IExpanderCellPresentation model, bool ownsModel, HashSet<UICell> ancestors)
        {
            _model = model;
            _ownsModel = ownsModel;
            _rawContent = model.Content;
            _content = _rawContent is UICell cell ? Adapt(cell, false, ancestors) : new EmptyCell();
            Kind = CellKind.Expander;
            EditGestures = model.EditGestures;
            try
            {
                _content.PropertyChanged += InnerChanged;
                if (model is INotifyPropertyChanged notifications) notifications.PropertyChanged += ModelChanged;
            }
            catch
            {
                _content.PropertyChanged -= InnerChanged;
                try { if (model is INotifyPropertyChanged notifications) notifications.PropertyChanged -= ModelChanged; }
                finally { DisposeAdapter(_content, _rawContent); }
                throw;
            }
        }
        public override UICell PresentationModel => _model;
        public override CellValue Inner => _content;
        public override object? Content => _rawContent;
        internal override bool HasContent => _rawContent is UICell;
        public override IRow Row => _model.Row;
        public override object? Value => _model.Value;
        public override bool CanEdit => _model.CanEdit;
        public override string? DisplayText => _content.DisplayText;
        public override TextCellOptions? TextOptions => _content.TextOptions;
        public override Exception? Error => _content.Error;
        public override bool IsExpanded { get => _model.IsExpanded; set => _model.IsExpanded = value; }
        public override bool ShowExpander => _model.ShowExpander;
        public override void Write(object? value) => _content.Write(value);
        public void RefreshAfterRetarget()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EditGestures = _model.EditGestures;
            // A custom column may retarget silently while its control is
            // unrealized. Refresh child identity even without an INPC event.
            ModelChanged(_model, new PropertyChangedEventArgs(nameof(Content)));
            if (_content is CustomCellValue cell) cell.RefreshAfterRetarget();
            if (_content is CustomExpanderValue expander) expander.RefreshAfterRetarget();
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (_disposed) return;
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(Content))
            {
                var raw = _model.Content;
                if (!ReferenceEquals(raw, _rawContent))
                {
                    var ancestors = new HashSet<UICell>(ReferenceEqualityComparer.Instance) { _model };
                    var next = raw is UICell cell ? Adapt(cell, false, ancestors) : new EmptyCell();
                    var previous = _content;
                    var previousRaw = _rawContent;
                    previous.PropertyChanged -= InnerChanged;
                    _rawContent = raw;
                    _content = next;
                    next.PropertyChanged += InnerChanged;
                    EditGestures = _model.EditGestures;
                    try
                    {
                        RaisePropertyChanged(nameof(Content));
                        if (!_disposed) RaisePropertyChanged(new CellContentChangedEventArgs(this));
                    }
                    finally { DisposeAdapter(previous, previousRaw); }
                    if (_disposed) return;
                    // An all-properties notification may also change expansion,
                    // but must not publish the content value a second time.
                    if (string.IsNullOrEmpty(args.PropertyName))
                    {
                        RaisePropertyChanged(nameof(IsExpanded));
                        if (!_disposed) RaisePropertyChanged(nameof(ShowExpander));
                        if (!_disposed) RaisePropertyChanged(nameof(Row));
                    }
                    return;
                }
            }
            if (args.PropertyName == nameof(Value) && _rawContent is not INotifyPropertyChanged && _content is CustomCellValue adapter)
                adapter.Refresh();
            else RaisePropertyChanged(args);
        }
        private void InnerChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (!_disposed && ReferenceEquals(sender, _content)) RaisePropertyChanged(args);
        }
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_model is INotifyPropertyChanged notifications) notifications.PropertyChanged -= ModelChanged;
            }
            finally
            {
                _content.PropertyChanged -= InnerChanged;
                try { DisposeAdapter(_content, _rawContent); }
                finally { if (_ownsModel) (_model as IDisposable)?.Dispose(); }
            }
        }
        private static void DisposeAdapter(CellValue adapter, object? model)
        {
            // The original expander owns/disposes its content. Only dispose
            // wrappers/subscriptions here, never its borrowed native cell.
            if (!ReferenceEquals(adapter, model)) adapter.Dispose();
        }
        private sealed class EmptyCell : CellValue
        {
            public override object? Value => null;
            public override bool CanEdit => false;
            public override void Write(object? value) => throw new InvalidOperationException("The expander has no cell content.");
        }
    }
}
