using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

internal sealed partial class CellColumnAdapter<TModel> where TModel : class
{
    private sealed class CustomCellValue : CellValue
    {
        private readonly UI.ICell _inner;
        private readonly bool _ownsModel;
        private bool _disposed;
        private bool _parentWritable = true;
        private bool _initializing = true;
        private bool _observingInner;
        private bool _ownedModelReleased;
        private TextCellOptions? _textOptions;
        private int _textOptionsRevision;
        private int _writeRevision;

        public CustomCellValue(UI.ICell inner, bool ownsModel)
        {
            _inner = inner;
            _ownsModel = ownsModel;
            Exception? failure = null;
            try
            {
                Kind = inner switch { UI.CheckBoxCell => CellKind.CheckBox, UI.TemplateCell => CellKind.Template, _ => CellKind.Text };
                EditGestures = inner.EditGestures;
                UpdateTextOptions();
                if (!_disposed && inner is INotifyPropertyChanged notifications)
                {
                    _observingInner = true;
                    notifications.PropertyChanged += OnChanged;
                }
            }
            catch (Exception error) { failure = error; Dispose(); throw; }
            finally
            {
                _initializing = false;
                if (_disposed)
                {
                    // Event accessors must unwind before removal. Adapt owns
                    // the raw model until construction succeeds completely.
                    try { Release(disposeModel: false); }
                    catch (Exception cleanup) when (failure is not null)
                    { throw new AggregateException(failure, cleanup); }
                }
            }
        }
        public override object? Value => _inner.Value;
        public override UI.ICell PresentationModel => _inner;
        public override bool CanWrite
        {
            get
            {
                if (_disposed || !_parentWritable) return false;
                var revision = _writeRevision;
                var result = _inner is UI.CheckBoxCell check ? !check.IsReadOnly : _inner.CanEdit;
                return IsWriteCurrent(revision) && result;
            }
        }
        public override bool? IsThreeState => (_inner as UI.CheckBoxCell)?.IsThreeState;
        public override object? EditTarget => _inner is IEditableObject ? _inner : null;
        public override Exception? Error => (_inner as UI.IBoundCellState)?.Error;
        public override DataTemplate? GetCellTemplate(Microsoft.UI.Xaml.Controls.Control anchor) =>
            (_inner as UI.TemplateCell)?.GetCellTemplate(anchor);
        public override DataTemplate? GetCellEditingTemplate(Microsoft.UI.Xaml.Controls.Control anchor) =>
            (_inner as UI.TemplateCell)?.GetCellEditingTemplate?.Invoke(anchor);
        public override string? DisplayText => (_inner as UI.ITextCell)?.Text;
        public override bool CanEdit
        {
            get
            {
                if (_disposed || !_parentWritable) return false;
                var revision = _writeRevision;
                var result = _inner.CanEdit;
                return IsWriteCurrent(revision) && result;
            }
        }
        public override UI.BeginEditGestures EditGestures
        {
            get
            {
                if (_disposed) return UI.BeginEditGestures.None;
                var result = _inner.EditGestures;
                return _disposed ? UI.BeginEditGestures.None : result;
            }
            internal set => base.EditGestures = value;
        }
        public override TextCellOptions? TextOptions
        {
            get { UpdateTextOptions(); return _textOptions; }
        }
        public void Refresh() => RaisePropertyChanged(nameof(Value));
        public void RefreshAfterRetarget()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            InvalidateWrite();
            UpdateTextOptions();
        }
        private void UpdateTextOptions()
        {
            if (_disposed || _inner is not UI.ITextCell text) return;
            var revision = unchecked(++_textOptionsRevision);
            var culture = (_inner as UI.ITextCellState)?.Options?.Culture ?? CultureInfo.CurrentCulture;
            if (!IsCurrent(revision)) return;
            var alignment = text.TextAlignment;
            if (!IsCurrent(revision)) return;
            var wrapping = text.TextWrapping;
            if (!IsCurrent(revision)) return;
            var trimming = text.TextTrimming;
            if (!IsCurrent(revision)) return;
            // Consume live public cell metadata just as native built-ins do.
            // Evaluate each application getter once. A nested query may have
            // already installed newer metadata, so never overwrite its snapshot.
            if (_textOptions is { } previous && previous.TextAlignment == alignment &&
                previous.TextWrapping == wrapping && previous.TextTrimming == trimming &&
                ReferenceEquals(previous.Culture, culture)) return;
            _textOptions = new()
            {
                TextAlignment = alignment, TextWrapping = wrapping,
                TextTrimming = trimming, Culture = culture,
            };
        }
        private bool IsCurrent(int revision) => !_disposed && revision == _textOptionsRevision;
        private bool IsWriteCurrent(int revision) => !_disposed && _parentWritable && revision == _writeRevision;
        internal void InvalidateWrite() { unchecked { ++_writeRevision; } }
        internal void SetParentWritable(bool value)
        {
            if (_parentWritable == value) return;
            _parentWritable = value;
            InvalidateWrite();
        }
        public override void Write(object? value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_parentWritable) throw new InvalidOperationException("The parent expander content has not been synchronized.");
            var revision = unchecked(++_writeRevision);
            var writable = CanWrite;
            // A permission getter may retire/reuse the cell or make a newer
            // assignment. Do not act on its now-obsolete permission result.
            if (!IsWriteCurrent(revision)) return;
            if (!writable) throw new InvalidOperationException("The custom cell is read-only.");
            if (_inner is UI.CheckBoxCell check) { check.Value = (bool?)value; return; }
            if (_inner is not UI.ITextCell text) throw new NotSupportedException("The custom cell does not expose a text setter.");
            var converted = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (!IsWriteCurrent(revision)) return;
            if (value is not null && value is not string)
            {
                // Application conversion can change mutable permissions without
                // changing cell identity. Ordinary string edits avoid this second
                // query because their conversion cannot run application code.
                writable = CanWrite;
                if (!IsWriteCurrent(revision)) return;
                if (!writable) throw new InvalidOperationException("The custom cell is read-only.");
            }
            text.Text = converted;
        }
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InvalidateWrite();
            unchecked { ++_textOptionsRevision; }
            _textOptions = null;
            if (!_initializing) Release(disposeModel: true);
        }
        internal void CompleteConstruction()
        {
            if (_disposed) Release(disposeModel: true);
        }
        private void Release(bool disposeModel)
        {
            var detach = _observingInner;
            _observingInner = false;
            var dispose = disposeModel && _ownsModel && !_ownedModelReleased;
            if (dispose) _ownedModelReleased = true;
            ReleaseAdapterModel(_inner, dispose, OnChanged, detach);
        }
        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            // TextCell publishes Text as well as Value. Preserve names so an
            // external value update is not reported twice by the control.
            if (!_disposed) RaisePropertyChanged(e);
        }
    }
}
