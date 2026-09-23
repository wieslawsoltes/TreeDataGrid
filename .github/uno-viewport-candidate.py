from pathlib import Path
import json
import sys

output = Path('artifacts/viewport-candidate')
output.mkdir(parents=True, exist_ok=True)
manifest = output / 'files.json'
changed = json.loads(manifest.read_text()) if manifest.exists() else []

def replace(name, old, new):
    path = Path(name)
    text = path.read_text()
    assert text.count(old) == 1, (name, old, text.count(old))
    path.write_text(text.replace(old, new))
    if name not in changed: changed.append(name)

def create(name, content):
    path = Path(name)
    assert not path.exists(), name
    path.write_text(content)
    changed.append(name)

if sys.argv[1] == 'tests':
    replace('samples/TreeDataGridUnoSample/ViewportMeasurementRuntimeChecks.cs',
        '            VerifyValues();\n\n            // No-change viewport updates must not hide genuine content invalidation.',
        '''            VerifyValues();

            // Move inside the already-realized first/last column boundaries.
            // Counting column callbacks detects redundant presenter passes even
            // when native Measure correctly caches each child constraint.
            grid.Scroll.ChangeView(7, null, null, true);
            await Settle();
            var horizontalMeasurements = new Dictionary<int, int>(columnMeasurements);
            var horizontalCells = grid.RowsPresenter.RealizedCells.ToArray();
            var horizontalHeaders = grid.ColumnHeadersPresenter!.RealizedHeaders.ToArray();
            grid.Scroll.ChangeView(9, null, null, true);
            await Settle();
            Check(Math.Abs(grid.Scroll.HorizontalOffset - 9) < 0.1, "The covered horizontal move did not execute.");
            var repeatedHorizontal = columnMeasurements.Sum(entry =>
                entry.Value - horizontalMeasurements.GetValueOrDefault(entry.Key));
            Check(repeatedHorizontal == 0,
                $"Covered horizontal scrolling repeated {repeatedHorizontal} cell/header column measurement callbacks.");
            Check(horizontalCells.All(cell => ReferenceEquals(cell, grid.TryGetCell(cell.ColumnIndex, cell.RowIndex))),
                "Covered horizontal scrolling replaced a retained cell.");
            Check(horizontalHeaders.All(header => ReferenceEquals(header, grid.ColumnHeadersPresenter.TryGetElement(header.ColumnIndex))),
                "Covered horizontal scrolling replaced a retained header.");
            VerifyValues();

            // Owner policies must still refresh without geometry changes.
            grid.CanUserResizeColumns = false;
            await Settle();
            Check(grid.ColumnHeadersPresenter.RealizedHeaders.All(header => !header.CanUserResize),
                "Disabling header resize was lost by viewport update coalescing.");
            grid.CanUserResizeColumns = true;
            await Settle();
            Check(grid.ColumnHeadersPresenter.RealizedHeaders.All(header => header.CanUserResize),
                "Enabling header resize was lost by viewport update coalescing.");

            // No-change viewport updates must not hide genuine content invalidation.''')
    replace('samples/TreeDataGridUnoSample/ViewportMeasurementRuntimeChecks.cs',
        '            Console.WriteLine($"UNO_RUNTIME_VIEWPORT_MEASUREMENT_PASSED: retained={unchanged.Length}; verticalOnlyMeasures={unnecessary}; columnMeasurements={unnecessaryColumnMeasures}; live content/width/horizontal changes preserved");',
        '            Console.WriteLine($"UNO_RUNTIME_VIEWPORT_MEASUREMENT_PASSED: retained={unchanged.Length}; verticalOnlyMeasures={unnecessary}; columnMeasurements={unnecessaryColumnMeasures}; coveredHorizontalCallbacks={repeatedHorizontal}; retained headers/cells, resize-policy, live content/width/horizontal changes preserved");')
elif sys.argv[1] == 'product':
    geometry = 'src/TreeDataGrid.Controls.Uno/Presentation/ColumnGeometry.cs'
    replace(geometry, '    public int Count => _ends.Length;',
        '    public int Count => _ends.Length;\n    internal int Version { get; private set; }')
    replace(geometry, '        return changed;\n',
        '        if (changed) unchecked { ++Version; }\n        return changed;\n')
    create('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridColumnarPresenterBase.Viewport.cs', '''using Windows.Foundation;

namespace Uno.Controls.Primitives;

public abstract partial class TreeDataGridColumnarPresenterBase<TItem>
{
    // The existing stable realized range is authoritative. This checks coverage,
    // not cached native measure validity, and retains no source/model references.
    internal bool CoversHorizontalViewport(double offset, double width) =>
        IsViewportCoveredByRealizedElements(new Rect(offset, 0, width, 0));
}
''')
    replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRowsPresenter.cs', '''        var columnsChanged = horizontal != _horizontalOffset || width != _viewportWidth;
        var viewport = new Rect(horizontal, vertical, width, height);
        var needsMeasure = columnsChanged || _measureViewport is not { } measured ||
            height != _measureViewportHeight || NeedsMeasureForViewportChange(measured, viewport);''', '''        var widthChanged = width != _viewportWidth;
        var columnsChanged = horizontal != _horizontalOffset || widthChanged;
        var contentWidth = Math.Min(width, Math.Max(0, Geometry.TotalWidth - horizontal));
        var cellsNeedMeasure = widthChanged;
        if (columnsChanged && !cellsNeedMeasure)
            foreach (var row in _realized.Values)
                if (row.CellsPresenter is { } cells && !cells.CoversHorizontalViewport(horizontal, contentWidth))
                {
                    cellsNeedMeasure = true;
                    break;
                }
        var viewport = new Rect(horizontal, vertical, width, height);
        var needsMeasure = cellsNeedMeasure || _measureViewport is not { } measured ||
            height != _measureViewportHeight || NeedsMeasureForViewportChange(measured, viewport);''')
    replace('src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridRowsPresenter.cs', '''        // Horizontal changes still require an explicit child measure: a row's
        // full content extent is unchanged while its visible columns change.
        if (columnsChanged)
            foreach (var row in _realized.Values) row.CellsPresenter?.InvalidateMeasure();''', '''        // Native scrolling already translates/clips existing children. A move
        // inside their stable realized range does not change their constraints.
        // New columns still need explicit child measure because the row's full
        // content extent is unchanged. Width/content/font invalidation is separate.
        if (columnsChanged)
            foreach (var row in _realized.Values)
                if (row.CellsPresenter is { } cells &&
                    (widthChanged || !cells.CoversHorizontalViewport(horizontal, contentWidth)))
                    cells.InvalidateMeasure();''')
    header = 'src/TreeDataGrid.Controls.Uno/Primitives/TreeDataGridColumnHeadersPresenter.cs'
    replace(header, '    private int _revision;', '''    private int _revision;
    private int _geometryVersion = -1;
    private TreeDataGrid? _configuredOwner;
    private Style? _configuredStyle;
    private bool _configuredResize;''')
    replace(header, '''        var revision = ++_revision;
        _presentation = presentation;
        _geometry = geometry;
        _offset = offset;
        _viewport = viewport;
        ElementFactory = Owner?.ElementFactory;
        if (revision != _revision) return;
        Items = presentation?.Columns;
        if (revision != _revision) return;
        var generation = PresenterGeneration;
        foreach (var header in RealizedHeaders)
        {
            header.SetOwner(Owner);
            if (revision != _revision || generation != PresenterGeneration) return;
            if (Owner is { } owner && !ReferenceEquals(header.Style, owner.ColumnHeaderStyle))
                header.Style = owner.ColumnHeaderStyle;
            if (revision != _revision || generation != PresenterGeneration) return;
        }
        InvalidateMeasure();''', '''        var owner = Owner;
        var factory = owner?.ElementFactory;
        var style = owner?.ColumnHeaderStyle;
        var canResize = owner?.CanUserResizeColumns == true;
        var columns = presentation?.Columns;
        var configurationChanged = !ReferenceEquals(_presentation, presentation) ||
            !ReferenceEquals(_configuredOwner, owner) || !ReferenceEquals(_configuredStyle, style) ||
            _configuredResize != canResize || !ReferenceEquals(ElementFactory, factory) ||
            !ReferenceEquals(Items, columns);
        var geometryChanged = !ReferenceEquals(_geometry, geometry) || _geometryVersion != geometry.Version;
        var contentWidth = Math.Min(viewport, Math.Max(0, geometry.TotalWidth - offset));
        var needsMeasure = configurationChanged || geometryChanged || viewport != _viewport ||
            (offset != _offset && !CoversHorizontalViewport(offset, contentWidth));
        var revision = ++_revision;
        _presentation = presentation;
        _geometry = geometry;
        _geometryVersion = geometry.Version;
        _offset = offset;
        _viewport = viewport;
        if (!ReferenceEquals(ElementFactory, factory)) ElementFactory = factory;
        if (revision != _revision) return;
        if (!ReferenceEquals(Items, columns)) Items = columns;
        if (revision != _revision) return;
        if (configurationChanged)
        {
            var generation = PresenterGeneration;
            foreach (var header in RealizedHeaders)
            {
                header.SetOwner(owner);
                if (revision != _revision || generation != PresenterGeneration) return;
                if (owner is not null && !ReferenceEquals(header.Style, style)) header.Style = style;
                if (revision != _revision || generation != PresenterGeneration) return;
            }
            _configuredOwner = owner;
            _configuredStyle = style;
            _configuredResize = canResize;
        }
        // Native model/theme/column events retain their own invalidation paths.
        // Do not dirty every header on vertical-only or covered horizontal moves.
        if (needsMeasure) InvalidateMeasure();''')
else:
    raise ValueError('Expected tests or product')
manifest.write_text(json.dumps(changed, indent=2) + '\n')
