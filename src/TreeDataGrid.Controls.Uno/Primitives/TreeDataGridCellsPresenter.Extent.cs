using System;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCellsPresenter
{
    internal override double? CommittedExtentWidth
    {
        get
        {
            var presenter = Presenter;
            var columns = Items;
            if (presenter is null || columns is null || !ReferenceEquals(columns, presenter.Columns))
                return null;
            var geometry = presenter.Geometry;
            // A structural mutation may precede the next geometry commit.
            // Do not use the old extent for a different column cardinality.
            return geometry.Count == columns.Count ? geometry.TotalWidth : null;
        }
    }
}
