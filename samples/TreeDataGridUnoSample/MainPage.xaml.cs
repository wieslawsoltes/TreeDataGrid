using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

public sealed partial class MainPage : Page
{
    private readonly FlatTreeDataGridSource<Country> _source;
    public MainPage()
    {
        InitializeComponent();
        _source = new(Countries.All);
        _source.Columns.Add(new TextColumn<Country, string?>("Country", x => x.Name, width: new(210)));
        _source.Columns.Add(new TextColumn<Country, string>("Region", x => x.Region, width: new(190)));
        _source.Columns.Add(new TextColumn<Country, int>("Population", x => x.Population, width: new(150)));
        _source.Columns.Add(new TextColumn<Country, int>("Area", x => x.Area, width: new(150)));
        _source.Columns.Add(new TextColumn<Country, double>("Density", x => x.PopulationDensity, width: new(150)));
        _source.Columns.Add(new TextColumn<Country, int>("GDP", x => x.GDP, width: new(150)));
        CountriesGrid.Model = _source;
    }
    public Uno.Controls.TreeDataGrid Grid => CountriesGrid;
    public void VerifyInitialRender()
    {
        if (!ReferenceEquals(CountriesGrid.Presentation?.Rows, _source.Rows))
            throw new InvalidOperationException("Uno must expose the actual Core rows.");
        if (!CountriesGrid.RowsPresenter.RealizedCells.Any(cell => cell.ActualWidth > 0 && cell.ActualHeight > 0))
            throw new InvalidOperationException("No cells were rendered.");
        if (CountriesGrid.RowsPresenter.RealizedCells.Count >= _source.Rows.Count * _source.Columns.Count)
            throw new InvalidOperationException("The sample did not virtualize its rows.");
        Console.WriteLine($"Initial realized cells: {CountriesGrid.RowsPresenter.RealizedCells.Count}");
    }
    public void VerifyScrolledRender()
    {
        if (!CountriesGrid.RowsPresenter.RealizedCells.Any(cell => cell.RowIndex > 0))
            throw new InvalidOperationException("Scrolling did not realize later rows.");
        foreach (var cell in CountriesGrid.RowsPresenter.RealizedCells)
            if (!ReferenceEquals(cell.RowModel, _source.Rows[cell.RowIndex].Model))
                throw new InvalidOperationException("A recycled cell retained an old Core row.");
        Console.WriteLine($"Scrolled realized cells: {CountriesGrid.RowsPresenter.RealizedCells.Count}");
    }
}
