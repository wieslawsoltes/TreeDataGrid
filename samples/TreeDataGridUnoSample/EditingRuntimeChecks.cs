using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

internal static class EditingRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate display, DataTemplate editing)
    {
        var first = new EditableItem("Original", 30);
        var items = new ObservableCollection<EditableItem>([first, new("Second", 40)]);
        using var source = new FlatTreeDataGridSource<EditableItem>(items);
        source.Columns.Add(new TextColumn<EditableItem, string>("Name", x => x.Name, (x, value) => x.Name = value, width: new(220)));
        source.Columns.Add(new TextColumn<EditableItem, int>("Age", x => x.Age, (x, value) => x.Age = value, width: new(150)));
        source.Columns.Add(new TemplateColumn<EditableItem>("Template", "Editable", width: new(220)));
        grid.CellTemplates["Editable"] = display;
        grid.CellEditingTemplates["Editable"] = editing;
        grid.Model = source;
        grid.Scroll.ChangeView(0, 0, null, true);
        await Task.Delay(200);
        Check(grid.BeginEdit(0, 0), "Text edit did not start.");
        var cell = grid.EditingCell!;
        cell.EditingText = "Cancelled";
        grid.CancelEdit();
        Check(first.Name == "Original" && first.Cancels == 1 && !cell.IsEditing, "Cancel committed buffered text or lost its transaction.");
        Check(grid.BeginEdit(0, 0), "Second text edit did not start.");
        cell.EditingText = "Committed";
        Check(grid.CommitEdit() && first.Name == "Committed" && first.Ends == 1, "Text edit did not commit to the Core model.");
        Check(grid.BeginEdit(0, 1), "Numeric edit did not start.");
        var ageCell = grid.EditingCell!;
        ageCell.EditingText = "not a number";
        Check(!grid.CommitEdit() && ageCell.HasValidationError && ageCell.IsEditing, "Conversion failure did not retain an editable validation state.");
        Check(!grid.SelectCell(1, 0), "Selection left an invalid editor.");
        ageCell.EditingText = "52";
        Check(grid.CommitEdit() && first.Age == 52 && !ageCell.HasValidationError, "A corrected value could not commit.");

        Check(grid.BeginEdit(0, 0), "Replacement edit did not start.");
        cell = grid.EditingCell!;
        cell.EditingText = "Must not reach replacement";
        var replacement = new EditableItem("Replacement", 20);
        items[0] = replacement;
        Check(!cell.IsEditing && first.Name == "Committed" && replacement.Name == "Replacement", "Rebinding leaked an old edit into the new model.");
        grid.CommitEdit();
        Check(replacement.Name == "Replacement", "Late commit changed the recycled cell's new model.");

        Check(grid.BeginEdit(0, 2), "Template edit did not start.");
        grid.UpdateLayout();
        var templateEditor = ShowcaseRuntimeChecks.Descendants(grid.EditingCell!).OfType<TextBox>().Single();
        Check(ReferenceEquals(FocusManager.GetFocusedElement(grid.XamlRoot!), templateEditor), "Template editor did not receive focus.");
        templateEditor.Text = "Template change";
        Check(replacement.Name == "Template change", "The actual template editor did not write through its two-way binding.");
        grid.CancelEdit();
        Check(replacement.Name == "Replacement", "Template cancellation did not restore IEditableObject state.");
        Check(grid.BeginEdit(0, 2), "Template edit did not restart.");
        grid.UpdateLayout();
        templateEditor = ShowcaseRuntimeChecks.Descendants(grid.EditingCell!).OfType<TextBox>().Single();
        templateEditor.Text = "Template committed";
        Check(grid.CommitEdit() && replacement.Name == "Template committed", "Template edit did not commit.");
        Check(grid.BeginEdit(0, 0), "Focus-loss edit did not start.");
        var textEditor = ShowcaseRuntimeChecks.Descendants(grid.EditingCell!).OfType<TextBox>().Single();
        Check(ReferenceEquals(FocusManager.GetFocusedElement(grid.XamlRoot!), textEditor), "Text editor did not receive focus.");
        grid.EditingCell!.EditingText = "Focus committed";
        Check(grid.Focus(FocusState.Programmatic), "Could not move focus out of the editor.");
        // Uno dispatches LostFocus asynchronously, unlike the focus-state update.
        await Task.Delay(100);
        Check(grid.EditingCell is null && replacement.Name == "Focus committed", "Leaving the editor did not commit the buffered text.");
        Check(grid.BeginEdit(0, 0), "Unload edit did not start.");
        grid.EditingCell!.EditingText = "Discard on source removal";
        grid.Model = null;
        Check(replacement.Name == "Focus committed" && grid.EditingCell is null, "Source removal retained or committed an unfinished edit.");
        grid.Model = source;
        await Task.Delay(100);
        var beginReplacement = new EditableItem("Replaced during BeginEdit", 25);
        replacement.Beginning = () => items[0] = beginReplacement;
        Check(!grid.BeginEdit(0, 0), "BeginEdit accepted a session after its model was replaced.");
        Check(grid.EditingCell is null && beginReplacement.Name == "Replaced during BeginEdit", "BeginEdit replacement retained an obsolete session.");
        grid.Model = null;
        Console.WriteLine("UNO_RUNTIME_EDITING_PASSED: text/number commit, cancel, validation retry, selection veto, row replacement, real template binding, template focus, focus-loss commit, source removal, BeginEdit reentrancy");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal sealed class EditableItem(string name, int age) : INotifyPropertyChanged, IEditableObject
    {
        private string _name = name;
        private int _age = age;
        private (string Name, int Age)? _snapshot;
        public string Name { get => _name; set { _name = value; Changed(nameof(Name)); } }
        public int Age { get => _age; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _age = value; Changed(nameof(Age)); } }
        public int Cancels { get; private set; }
        public int Ends { get; private set; }
        public Action? Beginning { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
        public void BeginEdit() { _snapshot ??= (Name, Age); Beginning?.Invoke(); }
        public void EndEdit() { ++Ends; _snapshot = null; }
        public void CancelEdit()
        {
            ++Cancels;
            if (_snapshot is { } snapshot) { Name = snapshot.Name; Age = snapshot.Age; }
            _snapshot = null;
        }
    }
}
