using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

public sealed partial class TreeDataGridPresentation<TModel> where TModel : class
{
    private bool _synchronizingColumns;
    private bool _columnsAgain;

    // Operation-local inputs only. The authoritative ordered definitions remain
    // in Core; no second mutable source or selection model is introduced.
    private readonly record struct ColumnInput(IColumn<TModel> Model, string? Key, bool Visible);

    private void SynchronizeColumns()
    {
        unchecked { ++_cellOperationVersion; }
        if (_disposed) return;
        if (_synchronizingColumns) { _columnsAgain = true; return; }
        var active = _active;
        _synchronizingColumns = true;
        try
        {
            do
            {
                _columnsAgain = false;
                SynchronizeColumnsCore(active);
            }
            while (_columnsAgain && !_disposed && _active == active);
        }
        finally { _synchronizingColumns = false; _columnsAgain = false; }
    }

    private void SynchronizeColumnsCore(bool active)
    {
        var version = _cellOperationVersion;
        var models = _model.Columns.ToArray();
        var inputs = new ColumnInput[models.Length];
        for (var i = 0; i < inputs.Length; ++i)
            inputs[i] = new(models[i], models[i].PresentationKey, models[i].IsVisible);
        if (!Current()) return;
        var desired = new HashSet<IColumn>(models, ReferenceEqualityComparer.Instance);

        // Snapshot each owner list before invoking an application's accessor.
        // It may dispose the entire presentation and clear these dictionaries.
        foreach (var removed in _observed.Keys.Where(x => !desired.Contains(x)).ToArray())
        {
            var observation = _observed[removed];
            _observed.Remove(removed);
            observation.UpdateSubscription();
            if (!Current()) return;
        }
        foreach (var column in models)
        {
            if (_observed.ContainsKey(column)) continue;
            var observation = new ColumnObservation(this, column);
            _observed.Add(column, observation);
            observation.UpdateSubscription();
            if (!Current()) return;
        }

        var staged = new Dictionary<IColumn, (CellColumn View, string? Key)>(ReferenceEqualityComparer.Instance);
        var retired = new List<CellColumn>();
        Exception? failure = null;
        try
        {
            foreach (var input in inputs)
            {
                if (_views.TryGetValue(input.Model, out var current) && current.Key == input.Key) continue;
                var view = input.Model.Accept(this) ?? throw new InvalidOperationException("A column factory returned null.");
                // Capture the key BEFORE application code. An old-key factory
                // must never be labelled with a newer key written by its callback.
                // Staging takes ownership even if that callback has retired us.
                staged.Add(input.Model, (view, input.Key));
                if (!Current()) return;
            }
            if (!MatchesInputs()) return;
            ClearPool();
            if (!Current() || !MatchesInputs()) return;

            // No application callbacks occur while transferring the view owners.
            // CellColumn's inherited PropertyChanged event is not a custom accessor.
            foreach (var column in _views.Keys.ToArray())
                if (!desired.Contains(column) || staged.ContainsKey(column))
                {
                    var view = _views[column].View;
                    retired.Add(view);
                    if (_active) view.PropertyChanged -= OnViewChanged;
                    _views.Remove(column);
                }
            foreach (var replacement in staged)
            {
                _views.Add(replacement.Key, replacement.Value);
                if (_active) replacement.Value.View.PropertyChanged += OnViewChanged;
            }
            staged.Clear(); // Ownership now belongs to the presentation.

            var next = new List<CellColumn>(inputs.Length);
            foreach (var input in inputs)
                if (input.Visible) next.Add(_views[input.Model].View);
            List<Exception>? publicationErrors = null;
            try
            {
                _selection.ColumnsChanged(next);
                if (Current()) _visible.Synchronize(next);
            }
            catch (Exception error) { (publicationErrors ??= new()).Add(error); }
            // Preserve the independent layout notification on a throwing list
            // observer, but never announce a projection superseded by reentry.
            try { if (Current()) ColumnsChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception error) { (publicationErrors ??= new()).Add(error); }
            ThrowCleanupErrors(publicationErrors);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            List<Exception>? errors = null;
            // Retired committed views and uncommitted factory values have exactly
            // one owner, including a throwing callback or reentrant Dispose.
            foreach (var replacement in staged.Values)
                try { replacement.View.Dispose(); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            foreach (var view in retired)
                try { view.Dispose(); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            if (errors is not null)
            {
                if (failure is not null) errors.Insert(0, failure);
                ThrowCleanupErrors(errors);
            }
        }

        bool Current()
        {
            if (_disposed || _active != active) return false;
            if (version == _cellOperationVersion && !_columnsAgain) return true;
            _columnsAgain = true;
            return false;
        }
        bool MatchesInputs()
        {
            // Resume constructs while source notifications are detached. Verify
            // that callback-time mutations did not evade the operation revision.
            var columns = _model.Columns;
            var matches = columns.Count == inputs.Length;
            for (var i = 0; matches && i < inputs.Length; ++i)
            {
                var input = inputs[i];
                matches = ReferenceEquals(columns[i], input.Model) &&
                    input.Model.PresentationKey == input.Key && input.Model.IsVisible == input.Visible;
            }
            if (!matches) _columnsAgain = true;
            return matches && Current();
        }
    }

    private sealed class ColumnObservation
    {
        private readonly TreeDataGridPresentation<TModel> _owner;
        private readonly IColumn _column;
        private readonly PropertyChangedEventHandler _handler;
        private bool _attached;
        private bool _changing;

        internal ColumnObservation(TreeDataGridPresentation<TModel> owner, IColumn column)
        {
            _owner = owner;
            _column = column;
            _handler = OnChanged;
        }
        private bool Desired => _owner._active && !_owner._disposed &&
            _owner._observed.TryGetValue(_column, out var current) && ReferenceEquals(current, this);
        private void OnChanged(object? sender, PropertyChangedEventArgs args)
        {
            // Ignore a captured multicast callback from a removed registration,
            // even if exactly the same definition has since been added again.
            if (_attached && Desired) _owner.OnColumnChanged(_column, args);
        }
        internal void UpdateSubscription()
        {
            if (_changing) return;
            _changing = true;
            try
            {
                while (_attached != Desired)
                {
                    var attach = Desired;
                    _attached = attach;
                    if (!attach) { _column.PropertyChanged -= _handler; continue; }
                    try { _column.PropertyChanged += _handler; }
                    catch (Exception error)
                    {
                        _attached = false;
                        try { _column.PropertyChanged -= _handler; }
                        catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                        ExceptionDispatchInfo.Capture(error).Throw();
                        throw;
                    }
                    // Disposal/suspension can occur before an add accessor has
                    // attached. Re-evaluate only after it has fully unwound.
                }
            }
            finally { _changing = false; }
        }
    }
}
