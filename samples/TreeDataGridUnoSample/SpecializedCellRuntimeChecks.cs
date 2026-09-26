using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Automation.Peers;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native checks for scalar properties and retained cell templates.</summary>
internal static class SpecializedCellRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid, DataTemplate display, DataTemplate editing,
        ControlTemplate textTheme, ControlTemplate templateTheme)
    {
        grid.Model = null;
        var previousOptions = grid.PresentationOptions;
        grid.PresentationOptions = null;
        var items = new ObservableCollection<Item> { new("Original") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Text", x => x.Name, (x, v) => x.Name = v, width: new(150)));
        source.Columns.Add(new CheckBoxColumn<Item>("Check", x => x.Checked, (x, v) => x.Checked = v, width: new(100)));
        source.Columns.Add(new TextColumn<Item, bool?>("Bool as text", x => x.Checked, width: new(120)));
        source.Columns.Add(new TemplateColumn<Item>("Template", "PrimitiveTemplate", width: new(150)));
        grid.CellTemplates["PrimitiveTemplate"] = display;
        grid.CellEditingTemplates["PrimitiveTemplate"] = editing;
        try
        {
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(150);
            grid.UpdateLayout();
            Check(grid.TryGetCell(0, 0) is TreeDataGridTextCell && grid.TryGetCell(1, 0) is TreeDataGridCheckBoxCell &&
                grid.TryGetCell(2, 0) is TreeDataGridTextCell && grid.TryGetCell(3, 0) is TreeDataGridTemplateCell,
                "The default factory did not select specialized cells by column kind.");
            var text = (TreeDataGridTextCell)grid.TryGetCell(0, 0)!;
            var check = (TreeDataGridCheckBoxCell)grid.TryGetCell(1, 0)!;
            var readOnly = (TreeDataGridTextCell)grid.TryGetCell(2, 0)!;
            var template = (TreeDataGridTemplateCell)grid.TryGetCell(3, 0)!;
            Check(text.Value == "Original" && !check.IsReadOnly && check.IsThreeState && check.Value == false,
                "Specialized scalar values/defaults do not match their cell models.");
            Check(ReferenceEquals(template.Content, items[0]) && ReferenceEquals(template.ContentTemplate, display) &&
                ReferenceEquals(template.DataContext, template.Model), "Template-cell public content/context is incorrect.");
            text.Value = "Written through public Value";
            Check(items[0].Name == text.Value, "Text Value did not write to the Core-backed cell.");
            items[0].Name = "External update";
            Check(text.Value == items[0].Name, "External text changes did not reach public Value.");
            var rejected = false;
            try { text.Value = "Reject"; }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && text.Value == "External update", "A rejected scalar write did not restore the displayed model value.");
            text.TextAlignment = TextAlignment.Right;
            text.TextWrapping = TextWrapping.Wrap;
            text.TextTrimming = TextTrimming.None;
            var block = ShowcaseRuntimeChecks.Descendants(text).OfType<TextBlock>().Single(x => x.Name == "PART_Text");
            Check(block.TextAlignment == TextAlignment.Right && block.TextWrapping == TextWrapping.Wrap && block.TextTrimming == TextTrimming.None,
                "Text appearance properties did not update the retained native text element.");
            text.Template = textTheme;
            template.Template = templateTheme;
            grid.UpdateLayout();
            var editor = ShowcaseRuntimeChecks.Descendants(text).OfType<TextBox>().Single(x => x.Name == "PART_Edit");
            var writesBeforeEdit = items[0].NameWrites;
            Check(grid.BeginEdit(0, 0), "An Avalonia-named PART_Edit was not recognized.");
            editor.Text = "Buffered only";
            text.Value = "Buffered public Value";
            Check(editor.Text == "Buffered public Value" && items[0].Name == "External update" && items[0].NameWrites == writesBeforeEdit,
                "The template-bound Value bypassed the edit transaction or failed to update the editor.");
            grid.CancelEdit();
            Check(text.Value == "External update" && editor.Visibility == Visibility.Collapsed && items[0].NameWrites == writesBeforeEdit,
                "Cancelling a template-bound editor lost its display value or editing state.");
            Check(grid.BeginEdit(0, 0), "The retained template editor could not reopen.");
            editor.Text = "Reject";
            Check(!grid.CommitEdit() && text.HasValidationError && items[0].Name == "External update" && items[0].NameWrites == writesBeforeEdit + 1,
                "A custom template editor did not preserve a rejected edit.");
            editor.Text = "Alias committed";
            Check(grid.CommitEdit() && items[0].Name == "Alias committed" && text.Value == "Alias committed" && items[0].NameWrites == writesBeforeEdit + 2,
                "Retrying the custom template editor did not commit exactly the buffered value.");
            Check(grid.BeginEdit(0, 3), "The Avalonia-named editing content presenter was not recognized.");
            var editingContent = ShowcaseRuntimeChecks.Descendants(template).OfType<ContentPresenter>()
                .Single(x => x.Name == "PART_EditingContentPresenter");
            Check(ReferenceEquals(editingContent.Content, items[0]) && ReferenceEquals(editingContent.ContentTemplate, editing),
                "The custom editing presenter received incorrect content or template.");
            grid.CancelEdit();
            Check(editingContent.Content is null && editingContent.Visibility == Visibility.Collapsed,
                "The custom editing presenter retained its old content.");
            check.Value = true;
            Check(items[0].Checked == true && readOnly.Value == "True", "Checkbox Value did not update both checkbox and text views.");
            check.Value = null;
            Check(items[0].Checked is null, "Nullable checkbox Value lost its third state.");
            check.IsReadOnly = true;
            check.Value = true;
            Check(items[0].Checked is null && check.Value is null && new TreeDataGridCellAutomationPeer(check).IsReadOnly,
                "Checkbox read-only override was bypassed by scalar or automation access.");
            readOnly.Value = "False";
            Check(items[0].Checked is null && readOnly.Value != "False", "A read-only text view accepted writeback.");

            var content = ShowcaseRuntimeChecks.Descendants(template).OfType<ContentPresenter>().Single(x => x.Name == "PART_ContentPresenter");
            var child = ShowcaseRuntimeChecks.Descendants(content).OfType<TextBlock>().First();
            var parent = template.Parent;
            var textParent = text.Parent;
            var unloads = 0;
            var textUnloads = 0;
            template.Unloaded += (_, _) => ++unloads;
            text.Unloaded += (_, _) => ++textUnloads;
            var original = items[0];
            items[0] = new("Replacement");
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(ReferenceEquals(text, grid.TryGetCell(0, 0)) && ReferenceEquals(template, grid.TryGetCell(3, 0)),
                "Specialized cells were replaced during compatible recycling.");
            Check(ReferenceEquals(template.Parent, parent) && unloads == 0 &&
                ReferenceEquals(child, ShowcaseRuntimeChecks.Descendants(content).OfType<TextBlock>().First()),
                "Template recycling detached its control or rebuilt the retained content subtree.");
            Check(ReferenceEquals(editor, ShowcaseRuntimeChecks.Descendants(text).OfType<TextBox>().Single(x => x.Name == "PART_Edit")) &&
                ReferenceEquals(text.Parent, textParent) && textUnloads == 0,
                "Compatible recycling replaced the custom editor subtree or detached its control.");
            Check(ReferenceEquals(template.Content, items[0]) && text.Value == "Replacement" &&
                text.TextAlignment == TextAlignment.Left && !check.IsReadOnly,
                "Recycling did not reset per-realization scalar appearance/read-only state.");
            original.Name = "Obsolete";
            Check(text.Value == "Replacement", "A retired model updated its recycled specialized cell.");
            grid.Model = null;
            Check(text.Value is null && template.Content is null && template.DataContext is null,
                "Final unrealization retained specialized scalar/content references.");
            Console.WriteLine("UNO_RUNTIME_SPECIALIZED_CELLS_PASSED: factory kinds, scalar writeback, external changes, rejected writes, appearance, read-only/three-state, retained template identity, old-model cleanup");
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
            grid.CellTemplates.Remove("PrimitiveTemplate");
            grid.CellEditingTemplates.Remove("PrimitiveTemplate");
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private bool? _checked = false;
        public int NameWrites { get; private set; }
        public string Name
        {
            get => _name;
            set
            {
                ++NameWrites;
                if (value == "Reject") throw new InvalidOperationException("Rejected test value.");
                if (_name != value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); }
            }
        }
        public bool? Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; PropertyChanged?.Invoke(this, new(nameof(Checked))); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
