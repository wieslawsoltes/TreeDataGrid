using System.Collections.ObjectModel;
using ReactiveUI;

namespace TreeDataGridDemo.Models
{
    internal class Person : ReactiveObject
    {
        private string? _name;
        private string? _title;
        private int _age;
        private bool _isActive;
        private bool _isExpanded;

        public string? Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string? Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public int Age
        {
            get => _age;
            set => this.RaiseAndSetIfChanged(ref _age, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => this.RaiseAndSetIfChanged(ref _isActive, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        public bool HasChildren => Children.Count > 0;

        public ObservableCollection<Person> Children { get; } = new();
    }
}
