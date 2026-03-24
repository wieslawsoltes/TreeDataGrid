using System;
using Avalonia.Data;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    internal interface IModelExpanderState<TModel>
    {
        bool TryReadModelIsExpanded(TModel model, out bool isExpanded);

        IObservable<BindingValue<bool>>? CreateModelIsExpandedObservable(TModel model);
    }
}
