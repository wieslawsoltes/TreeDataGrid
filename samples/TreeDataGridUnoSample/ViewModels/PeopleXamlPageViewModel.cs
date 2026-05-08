using System.Collections.ObjectModel;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels;

internal class PeopleXamlPageViewModel
{
    public PeopleXamlPageViewModel()
    {
        People = new ObservableCollection<Person>
        {
            new()
            {
                Name = "Eleanor Pope",
                Title = "Engineering Manager",
                Age = 32,
                IsActive = true,
                IsExpanded = true,
                Children =
                {
                    new Person
                    {
                        Name = "Marcel Gutierrez",
                        Title = "Intern",
                        Age = 19,
                        IsActive = true,
                    },
                },
            },
            new()
            {
                Name = "Jeremy Navarro",
                Title = "Director",
                Age = 47,
                IsActive = true,
                IsExpanded = true,
                Children =
                {
                    new Person
                    {
                        Name = "Jane Navarro",
                        Title = "Staff Engineer",
                        Age = 42,
                        IsActive = true,
                        IsExpanded = true,
                        Children =
                        {
                            new Person
                            {
                                Name = "Lailah Velazquez",
                                Title = "Product Designer",
                                Age = 28,
                                IsActive = false,
                            },
                        },
                    },
                },
            },
            new()
            {
                Name = "Jazmine Schroeder",
                Title = "Support Lead",
                Age = 36,
                IsActive = false,
            },
        };
    }

    public ObservableCollection<Person> People { get; }
}
