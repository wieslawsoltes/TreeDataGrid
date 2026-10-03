using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

internal sealed partial class CellColumnAdapter<TModel> where TModel : class
{
    /// <summary>Owns adapter subscriptions, not the custom expander's content.</summary>
    private sealed class CustomExpanderValue : ExpanderCellValue
    {
        private readonly UI.IExpanderCellPresentation _model;
        private readonly bool _ownsModel;
        private readonly HashSet<UI.ICell> _ancestors;
        private CellValue _content = new EmptyCell();
        private object? _rawContent;
        private bool _disposed;
        private bool _parentWritable = true;
        private bool _initializing = true;
        private bool _observingModel;
        private bool _cleanupStarted;
        private bool _ownedModelReleased;
        private bool _refreshing;
        private bool _refreshAgain;
        private bool _notifyRequested;
        private bool _resetRequested;
        private bool _refreshInner;
        private bool _contentCurrent;
        private bool _contentChanged;
        private int _revision;

        public CustomExpanderValue(UI.IExpanderCellPresentation model, bool ownsModel, HashSet<UI.ICell> ancestors)
        {
            _model = model;
            _ownsModel = ownsModel;
            // Retain the path, not the temporary set mutated by recursive Adapt.
            // A later descendant change must still reject an ancestor cycle.
            _ancestors = new(ancestors, ReferenceEqualityComparer.Instance);
            Kind = CellKind.Expander;
            Exception? failure = null;
            try
            {
                if (model is INotifyPropertyChanged notifications)
                {
                    // An accessor may invoke our handler, retire us, then attach.
                    // Cleanup waits until the accessor has returned.
                    _observingModel = true;
                    notifications.PropertyChanged += ModelChanged;
                }
                if (!_disposed) RefreshContent();
            }
            catch (Exception error)
            {
                failure = error;
                _disposed = true;
                throw;
            }
            finally
            {
                _initializing = false;
                if (_disposed)
                {
                    // Adapt owns the raw model until construction succeeds.
                    // CompleteConstruction transfers/retires ownership outside
                    // Adapt's constructor-failure cleanup, avoiding double disposal.
                    try { ReleaseAll(disposeModel: false); }
                    catch (Exception cleanup) when (failure is not null)
                    { throw new AggregateException(failure, cleanup); }
                }
            }
        }

        public override UI.ICell PresentationModel => _model;
        public override CellValue Inner => _content;
        public override object? Content => _rawContent;
        internal override bool HasContent => _contentCurrent && _rawContent is UI.ICell;
        public override IRow Row => _model.Row;
        public override object? Value => _model.Value;
        public override bool CanEdit => ReadPermission(write: false);
        public override bool CanWrite => ReadPermission(write: true);
        private bool ReadPermission(bool write)
        {
            if (_disposed || !_contentCurrent || !_parentWritable) return false;
            var revision = _revision;
            var result = write ? _content.CanWrite : _model.CanEdit;
            return IsCurrent(revision) && _contentCurrent && _parentWritable && result;
        }
        public override string? DisplayText => _content.DisplayText;
        public override TextCellOptions? TextOptions => _content.TextOptions;
        public override Exception? Error => (_model as UI.IBoundCellState)?.Error ?? _content.Error;
        public override UI.BeginEditGestures EditGestures
        {
            get
            {
                if (_disposed) return UI.BeginEditGestures.None;
                var revision = _revision;
                var result = _model.EditGestures;
                return IsCurrent(revision) ? result : UI.BeginEditGestures.None;
            }
            internal set => base.EditGestures = value;
        }
        public override bool IsExpanded
        {
            get => _model.IsExpanded;
            set { ObjectDisposedException.ThrowIf(_disposed, this); _model.IsExpanded = value; }
        }
        public override bool ShowExpander
        {
            get
            {
                if (_disposed) return false;
                var revision = _revision;
                var result = _model.ShowExpander;
                return IsCurrent(revision) && result;
            }
        }
        public override void Write(object? value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_contentCurrent || !_parentWritable) throw new InvalidOperationException("The expander content has not been synchronized.");
            _content.Write(value);
        }
        internal void InvalidateWrite()
        {
            unchecked { ++_revision; }
            if (_refreshing) _refreshAgain = true;
            // Borrowed native values keep their own lifetime responsibilities.
            if (_content is CustomCellValue cell) cell.InvalidateWrite();
            else if (_content is CustomExpanderValue expander) expander.InvalidateWrite();
        }
        internal void SetParentWritable(bool value)
        {
            if (_parentWritable == value) return;
            _parentWritable = value;
            InvalidateWrite();
            SetContentWritable(value && _contentCurrent);
        }
        private void SetContentWritable(bool value)
        {
            if (_content is CustomCellValue leaf) leaf.SetParentWritable(value);
            else if (_content is CustomExpanderValue expander) expander.SetParentWritable(value);
        }
        public void RefreshAfterRetarget()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _refreshInner = true;
            RequestContent(reset: false);
        }
        private bool IsCurrent(int revision) => !_disposed && revision == _revision;
        private void RequestContent(bool reset)
        {
            InvalidateWrite();
            _contentCurrent = false;
            SetContentWritable(false);
            _notifyRequested = true;
            _resetRequested |= reset;
            _refreshAgain = true;
            if (!_initializing && !_refreshing) RefreshContent();
        }
        private void ModelChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (_disposed) return;
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(Content))
                RequestContent(string.IsNullOrEmpty(args.PropertyName));
            else if (args.PropertyName == nameof(Value) && _rawContent is not INotifyPropertyChanged && _content is CustomCellValue adapter)
                adapter.Refresh();
            else if (!_initializing) RaisePropertyChanged(args);
        }

        private void RefreshContent()
        {
            if (_disposed || _refreshing) return;
            _refreshing = true;
            Exception? failure = null;
            try
            {
                do
                {
                    _refreshAgain = false;
                    var revision = _revision;
                    var raw = _model.Content;
                    if (!IsCurrent(revision)) continue;
                    if (!ReferenceEquals(raw, _rawContent))
                    {
                        CellValue? candidate = null;
                        Exception? candidateFailure = null;
                        try
                        {
                            candidate = raw is UI.ICell cell
                                ? Adapt(cell, false, new(_ancestors, ReferenceEqualityComparer.Instance))
                                : new EmptyCell();
                            if (!IsCurrent(revision)) continue;
                            var previous = _content;
                            var previousRaw = _rawContent;
                            previous.PropertyChanged -= InnerChanged;
                            _rawContent = raw;
                            _content = candidate;
                            candidate = null; // Ownership transferred to this adapter.
                            _content.PropertyChanged += InnerChanged;
                            _contentCurrent = true;
                            _contentChanged = true;
                            // Publish the new identity before invoking old custom
                            // remove accessors. Reentrant changes are queued, and
                            // no obsolete value notification is sent afterwards.
                            DisposeAdapter(previous, previousRaw);
                        }
                        catch (Exception error) { candidateFailure = error; throw; }
                        finally
                        {
                            if (candidate is not null)
                            {
                                try { DisposeAdapter(candidate, raw); }
                                catch (Exception cleanup) when (candidateFailure is not null)
                                { throw new AggregateException(candidateFailure, cleanup); }
                            }
                        }
                    }
                    else _contentCurrent = true;
                    SetContentWritable(_parentWritable && _contentCurrent);
                    if (!IsCurrent(revision)) continue;
                    if (_refreshInner)
                    {
                        _refreshInner = false;
                        if (_content is CustomCellValue cell) cell.RefreshAfterRetarget();
                        else if (_content is CustomExpanderValue expander) expander.RefreshAfterRetarget();
                        if (!IsCurrent(revision)) continue;
                    }
                    var notify = _notifyRequested;
                    var reset = _resetRequested;
                    _notifyRequested = false;
                    _resetRequested = false;
                    if (_initializing) { _contentChanged = false; continue; }
                    if (!notify) continue;
                    var changed = _contentChanged;
                    if (changed)
                    {
                        RaisePropertyChanged(nameof(Content));
                        if (!ContinueAfterCallback(revision, reset)) continue;
                        RaisePropertyChanged(new CellContentChangedEventArgs(this));
                        if (!ContinueAfterCallback(revision, reset)) continue;
                        if (reset)
                        {
                            RaisePropertyChanged(nameof(IsExpanded));
                            if (!ContinueAfterCallback(revision, reset)) continue;
                            RaisePropertyChanged(nameof(ShowExpander));
                            if (!ContinueAfterCallback(revision, reset)) continue;
                            RaisePropertyChanged(nameof(Row));
                            if (!ContinueAfterCallback(revision, reset)) continue;
                        }
                    }
                    else
                    {
                        RaisePropertyChanged(new PropertyChangedEventArgs(reset ? null : nameof(Content)));
                        if (!ContinueAfterCallback(revision, reset)) continue;
                    }
                    _contentChanged = false;
                }
                while (!_disposed && _refreshAgain);
            }
            catch (Exception error)
            {
                failure = error;
                if (!_disposed && !_initializing && !_contentCurrent)
                {
                    // A failed candidate must not leave an editor bound to old
                    // content. HasContent is false until a later successful sync.
                    try { RaisePropertyChanged(nameof(Content)); }
                    catch (Exception notification)
                    { failure = new AggregateException(error, notification); throw failure; }
                }
                throw;
            }
            finally
            {
                _refreshing = false;
                if (_disposed && !_initializing)
                {
                    try { ReleaseAll(disposeModel: true); }
                    catch (Exception cleanup) when (failure is not null)
                    { throw new AggregateException(failure, cleanup); }
                }
            }
        }

        private bool ContinueAfterCallback(int revision, bool reset)
        {
            if (IsCurrent(revision)) return true;
            if (!_disposed)
            {
                // An interrupted reset must still publish expansion/row state
                // with the newer content, not silently lose those notifications.
                _resetRequested |= reset;
                _notifyRequested = true;
                _refreshAgain = true;
            }
            return false;
        }
        private void InnerChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (_disposed || !_contentCurrent || !ReferenceEquals(sender, _content)) return;
            if (args.PropertyName == nameof(Content))
            {
                unchecked { ++_revision; }
                if (_refreshing) _refreshAgain = true;
            }
            if (!_initializing) RaisePropertyChanged(args);
        }
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _contentCurrent = false;
            InvalidateWrite();
            if (!_initializing && !_refreshing) ReleaseAll(disposeModel: true);
        }
        internal void CompleteConstruction()
        {
            if (_disposed) ReleaseAll(disposeModel: true);
        }
        private void ReleaseAll(bool disposeModel)
        {
            List<Exception>? errors = null;
            if (!_cleanupStarted)
            {
                _cleanupStarted = true;
                if (_observingModel)
                {
                    _observingModel = false;
                    try { if (_model is INotifyPropertyChanged notifications) notifications.PropertyChanged -= ModelChanged; }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                }
                try { _content.PropertyChanged -= InnerChanged; }
                catch (Exception error) { (errors ??= new()).Add(error); }
                try { DisposeAdapter(_content, _rawContent); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            if (disposeModel && _ownsModel && !_ownedModelReleased)
            {
                _ownedModelReleased = true;
                try { (_model as IDisposable)?.Dispose(); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors is not null) throw new AggregateException(errors);
        }
        private static void DisposeAdapter(CellValue adapter, object? model)
        {
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
