using System.ComponentModel;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Selection;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCheckBoxCell
{
    // Direct portable declaration, as in the reference specialized control. The
    // base transaction already performs native adaptation, selection, observation
    // and specialized Realize dispatch: do not add a second subscription here.
    public override void Realize(TreeDataGridElementFactory factory,
        ITreeDataGridSelectionInteraction? selection, ICell model, int columnIndex, int rowIndex) =>
        base.Realize(factory, selection, model, columnIndex, rowIndex);

    public override void Unrealize() => base.Unrealize();

    protected override void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        base.OnModelPropertyChanged(sender, e);
}
