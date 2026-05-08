using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeDataGridDemo.Models;

internal class Person : INotifyPropertyChanged
{
    private string? _name;
    private string? _title;
    private int _age;
    private bool _isActive;
    private bool _isExpanded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Name
    {
        get => _name;
        set => SetAndRaise(ref _name, value);
    }

    public string? Title
    {
        get => _title;
        set => SetAndRaise(ref _title, value);
    }

    public int Age
    {
        get => _age;
        set => SetAndRaise(ref _age, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetAndRaise(ref _isActive, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetAndRaise(ref _isExpanded, value);
    }

    public bool HasChildren => Children.Count > 0;

    public ObservableCollection<Person> Children { get; } = new();

    private void SetAndRaise<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChildren)));
    }
}
