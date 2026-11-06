using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Uno.Controls.Presentation;

internal sealed partial class CellColumnAdapter<TModel> where TModel : class
{
    private static void ReleaseAdapterModel(object model, bool ownsModel, PropertyChangedEventHandler handler, bool detach = true)
    {
        Exception? failure = null;
        try
        {
            if (detach && model is INotifyPropertyChanged notifications) notifications.PropertyChanged -= handler;
        }
        catch (Exception error) { failure = error; }
        try { if (ownsModel) (model as IDisposable)?.Dispose(); }
        catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
