using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeDataGridUnoShared;

/// <summary>
/// Notification-only base for the Uno build of the shared sample models.
/// The Avalonia builds retain their ReactiveObject base. This adapter does not
/// discover types, compile expressions, resolve services or initialize RxApp.
/// </summary>
public abstract class ObservableSampleModel : INotifyPropertyChanging, INotifyPropertyChanged
{
    private static readonly ConcurrentDictionary<string, Notifications> s_notifications = new();
    public event PropertyChangingEventHandler? PropertyChanging;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected TValue RaiseAndSetIfChanged<TValue>(ref TValue field, TValue value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<TValue>.Default.Equals(field, value)) return value;
        // Cache only compile-time property names, never models/subscribers.
        var notifications = s_notifications.GetOrAdd(propertyName,
            static name => new(new(name), new(name)));
        PropertyChanging?.Invoke(this, notifications.Changing);
        field = value;
        PropertyChanged?.Invoke(this, notifications.Changed);
        return value;
    }

    private sealed record Notifications(PropertyChangingEventArgs Changing, PropertyChangedEventArgs Changed);
}
