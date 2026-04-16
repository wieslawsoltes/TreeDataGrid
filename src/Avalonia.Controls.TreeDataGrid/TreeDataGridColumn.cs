using System;
using System.Collections;
using System.Linq;
using Avalonia.Data;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using Avalonia.Media;

namespace Avalonia.Controls
{
    public abstract class TreeDataGridColumn
    {
        public object? Header { get; set; }
        public GridLength Width { get; set; } = GridLength.Auto;
        public bool? CanUserResize { get; set; }
        public bool? CanUserSortColumn { get; set; }
        public bool AllowTriStateSorting { get; set; }
        public GridLength MinWidth { get; set; } = new(30, GridUnitType.Pixel);
        public GridLength? MaxWidth { get; set; }
        public Comparison<object?>? CompareAscending { get; set; }
        public Comparison<object?>? CompareDescending { get; set; }
        public BeginEditGestures BeginEditGestures { get; set; } = BeginEditGestures.Default;

        internal abstract IColumn<object> CreateUntypedColumn();
        internal virtual void InitializeFromSample(object? sampleModel, Type? modelType)
        {
        }

        protected ColumnOptions<object> CreateCommonOptions()
        {
            return TreeDataGridColumnOptionsFactory.CreateCommonOptions<object>(this);
        }
    }

    public class TreeDataGridTextColumn : TreeDataGridColumn
    {
        private object? _sampleModel;
        private Type? _sampleModelType;

        [AssignBinding]
        public BindingBase? Binding { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsTextSearchEnabled { get; set; } = true;
        public string StringFormat { get; set; } = "{0}";
        public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.CurrentCulture;
        public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
        public TextTrimming TextTrimming { get; set; } = TextTrimming.CharacterEllipsis;
        public TextWrapping TextWrapping { get; set; } = TextWrapping.NoWrap;

        internal override IColumn<object> CreateUntypedColumn()
        {
            var accessor = Binding is not null
                ? new TreeDataGridBindingAccessor(Binding, _sampleModel, _sampleModelType)
                : throw new InvalidOperationException("TreeDataGridTextColumn requires Binding.");
            Func<object, object?> getter = accessor.Read;
            Action<object, object?>? setter = null;

            if (!IsReadOnly && accessor.CanWrite)
                setter = accessor.Write;

            var options = TreeDataGridColumnOptionsFactory.CreateTextOptions<object>(this);

            return new ReflectionTextColumn(Header, getter, setter, accessor.Links, Width, options);
        }

        internal override void InitializeFromSample(object? sampleModel, Type? modelType)
        {
            _sampleModel = sampleModel;
            _sampleModelType = modelType;
        }
    }

    public class TreeDataGridCheckBoxColumn : TreeDataGridColumn
    {
        private object? _sampleModel;
        private Type? _sampleModelType;
        private bool _isThreeState;

        [AssignBinding]
        public BindingBase? Binding { get; set; }
        public bool IsReadOnly { get; set; }
        public bool? IsThreeState { get; set; }

        internal override IColumn<object> CreateUntypedColumn()
        {
            var accessor = Binding is not null
                ? new TreeDataGridBindingAccessor(Binding, _sampleModel, _sampleModelType)
                : throw new InvalidOperationException("TreeDataGridCheckBoxColumn requires Binding.");
            Func<object, bool?> getter = accessor.ReadAsNullableBoolean;
            Action<object, bool?>? setter = null;

            if (!IsReadOnly && accessor.CanWrite)
                setter = (model, value) => accessor.Write(model, value);

            _isThreeState = IsThreeState ?? accessor.ValueType is { } valueType && Nullable.GetUnderlyingType(valueType) == typeof(bool) || accessor.SampleValueWasNull;

            var options = TreeDataGridColumnOptionsFactory.CreateCheckBoxOptions<object>(this);

            return new ReflectionCheckBoxColumn(Header, getter, setter, accessor.Links, Width, options, _isThreeState);
        }

        internal override void InitializeFromSample(object? sampleModel, Type? modelType)
        {
            _sampleModel = sampleModel;
            _sampleModelType = modelType;
            _isThreeState = false;
        }
    }

    public class TreeDataGridTemplateColumn : TreeDataGridColumn
    {
        public TreeDataGridTemplateColumn()
        {
        }

        public TreeDataGridTemplateColumn(object? header, object cellTemplateResourceKey, object? cellEditingTemplateResourceKey = null)
        {
            Header = header;
            CellTemplateResourceKey = cellTemplateResourceKey;
            CellEditingTemplateResourceKey = cellEditingTemplateResourceKey;
        }

        public IDataTemplate? CellTemplate { get; set; }
        public IDataTemplate? CellEditingTemplate { get; set; }
        public object? CellTemplateResourceKey { get; set; }
        public object? CellEditingTemplateResourceKey { get; set; }
        [AssignBinding]
        public BindingBase? TextSearchBinding { get; set; }

        internal override IColumn<object> CreateUntypedColumn()
        {
            var options = TreeDataGridColumnOptionsFactory.CreateTemplateOptions<object>(
                this,
                TreeDataGridBindingAccessor.TryCreateTextSelector<object>(TextSearchBinding));

            if (CellTemplate is not null)
                return new TemplateColumn<object>(Header, CellTemplate, CellEditingTemplate, Width, options);

            if (CellTemplateResourceKey is not null)
                return new TemplateColumn<object>(Header, CellTemplateResourceKey, CellEditingTemplateResourceKey, Width, options);

            throw new InvalidOperationException("TreeDataGridTemplateColumn requires CellTemplate or CellTemplateResourceKey.");
        }

        internal TemplateColumn<TModel> CreateTyped<TModel>() where TModel : class
        {
            var options = TreeDataGridColumnOptionsFactory.CreateTemplateOptions<TModel>(
                this,
                TreeDataGridBindingAccessor.TryCreateTextSelector<TModel>(TextSearchBinding));

            if (CellTemplate is not null)
                return new TemplateColumn<TModel>(Header, CellTemplate, CellEditingTemplate, Width, options);

            if (CellTemplateResourceKey is not null)
                return new TemplateColumn<TModel>(Header, CellTemplateResourceKey, CellEditingTemplateResourceKey, Width, options);

            throw new InvalidOperationException("TreeDataGridTemplateColumn requires CellTemplate or CellTemplateResourceKey.");
        }
    }

    public class TreeDataGridRowHeaderColumn : TreeDataGridColumn
    {
        internal override IColumn<object> CreateUntypedColumn()
        {
            var options = CreateCommonOptions();
            return new TreeDataGridRowHeaderColumnInternal<object>(Header, Width, options);
        }
    }

    public class TreeDataGridHierarchicalExpanderColumn : TreeDataGridColumn
    {
        private object? _sampleModel;
        private Type? _sampleModelType;

        [AssignBinding]
        public BindingBase? ChildrenBinding { get; set; }

        [AssignBinding]
        public BindingBase? HasChildrenBinding { get; set; }

        [AssignBinding]
        public BindingBase? IsExpandedBinding { get; set; }

        [Content]
        public TreeDataGridColumn? InnerColumn { get; set; }

        internal override IColumn<object> CreateUntypedColumn()
        {
            var childrenAccessor = ChildrenBinding is not null
                ? new TreeDataGridBindingAccessor(ChildrenBinding, _sampleModel, _sampleModelType)
                : throw new InvalidOperationException("TreeDataGridHierarchicalExpanderColumn requires ChildrenBinding.");
            var hasChildrenAccessor = HasChildrenBinding is null ? null : new TreeDataGridBindingAccessor(HasChildrenBinding, _sampleModel, _sampleModelType);
            var isExpandedAccessor = IsExpandedBinding is null ? null : new TreeDataGridBindingAccessor(IsExpandedBinding, _sampleModel, _sampleModelType);
            var inner = InnerColumn?.CreateUntypedColumn()
                ?? throw new InvalidOperationException("TreeDataGridHierarchicalExpanderColumn requires an inner column.");

            return new ObjectHierarchicalExpanderColumn(
                inner,
                model => childrenAccessor.Read(model) is IEnumerable items
                    ? items.Cast<object>()
                    : null,
                hasChildrenAccessor is null ? null : model => hasChildrenAccessor.ReadAsBoolean(model),
                isExpandedAccessor is null ? null : model => isExpandedAccessor.ReadAsBoolean(model),
                isExpandedAccessor is null || !isExpandedAccessor.CanWrite
                    ? null
                    : (model, value) => isExpandedAccessor.Write(model, value),
                hasChildrenAccessor?.Links,
                isExpandedAccessor?.Links);
        }

        internal override void InitializeFromSample(object? sampleModel, Type? modelType)
        {
            _sampleModel = sampleModel;
            _sampleModelType = modelType;
            InnerColumn?.InitializeFromSample(sampleModel, modelType);
        }
    }
}
