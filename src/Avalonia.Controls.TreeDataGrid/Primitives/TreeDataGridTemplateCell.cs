using System;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridTemplateCell : TreeDataGridCell
    {
        public static readonly DirectProperty<TreeDataGridTemplateCell, object?> ContentProperty =
            AvaloniaProperty.RegisterDirect<TreeDataGridTemplateCell, object?>(
                nameof(Content),
                x => x.Content);

        public static readonly DirectProperty<TreeDataGridTemplateCell, IDataTemplate?> ContentTemplateProperty =
            AvaloniaProperty.RegisterDirect<TreeDataGridTemplateCell, IDataTemplate?>(
                nameof(ContentTemplate),
                x => x.ContentTemplate);

        public static readonly DirectProperty<TreeDataGridTemplateCell, IDataTemplate?> EditingTemplateProperty =
            AvaloniaProperty.RegisterDirect<TreeDataGridTemplateCell, IDataTemplate?>(
                nameof(EditingTemplate),
                x => x.EditingTemplate);

        private object? _content;
        private IDataTemplate? _contentTemplate;
        private IDataTemplate? _editingTemplate;
        private ContentPresenter? _editingContentPresenter;
        private IDataTemplate? _sourceContentTemplate;

        public object? Content
        {
            get => _content;
            private set
            {
                if (SetAndRaise(ContentProperty, ref _content, value))
                    RaiseCellValueChanged();
            }
        }

        public IDataTemplate? ContentTemplate 
        { 
            get => _contentTemplate;
            set => SetAndRaise(ContentTemplateProperty, ref _contentTemplate, value);
        }

        public IDataTemplate? EditingTemplate
        {
            get => _editingTemplate;
            set => SetAndRaise(EditingTemplateProperty, ref _editingTemplate, value);
        }

        public override void Realize(
            TreeDataGridElementFactory factory,
            ITreeDataGridSelectionInteraction? selection, 
            ICell model,
            int columnIndex,
            int rowIndex)
        {
            DataContext = model;
            base.Realize(factory, selection, model, columnIndex, rowIndex);
        }

        public override void Unrealize()
        {
            DataContext = null;
            base.Unrealize();
        }

        internal void FinalizeUnrealize()
        {
            if (DataContext is null)
                Content = null;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_editingContentPresenter is not null)
                _editingContentPresenter.LostFocus -= EditingContentPresenterLostFocus;

            _editingContentPresenter = e.NameScope.Find<ContentPresenter>("PART_EditingContentPresenter");

            if (_editingContentPresenter is not null)
            {
                _editingContentPresenter.UpdateChild();

                var focus = (IInputElement?)_editingContentPresenter.GetVisualDescendants()
                    .FirstOrDefault(x => (x as IInputElement)?.Focusable == true);
                focus?.Focus();

                _editingContentPresenter.LostFocus += EditingContentPresenterLostFocus;
            }
        }

        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);

            // A detached cell can be recycled into a different template column while retaining
            // its previous ContentTemplate, so always resolve the current model's templates here.
            if (DataContext is TemplateCell cell)
            {
                SetCellTemplate(cell.GetCellTemplate(this));
                EditingTemplate = cell.GetCellEditingTemplate?.Invoke(this);
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            var cell = DataContext as TemplateCell;

            // If DataContext is null, we're unrealized. Don't clear the content template for unrealized
            // cells because this will mean that when the cell is realized again the template will need
            // to be rebuilt, slowing everything down.
            if (cell is not null)
            {
                Content = cell.Value;

                if (((ILogical)this).IsAttachedToLogicalTree)
                {
                    SetCellTemplate(cell.GetCellTemplate(this));
                    EditingTemplate = cell.GetCellEditingTemplate?.Invoke(this);
                }
            }
            else
            {
                ClearContentIfDetached();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            // Recycled cells are normally hidden and kept parented so their control subtree and
            // applied styles can be reused. Clear the content only when the cell itself has been
            // removed from both trees; ancestor detachment doesn't change either parent.
            ClearContentIfDetached();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            if (EndEditIfFocusLost())
            {
                base.OnLostFocus(e);
            }
        }

        private void EditingContentPresenterLostFocus(object? sender, RoutedEventArgs e) => EndEditIfFocusLost();

        private bool EndEditIfFocusLost()
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel &&
                topLevel?.FocusManager?.GetFocusedElement() is Control newFocus &&
                !IsDescendent(newFocus))
            {
                EndEdit();
                return true;
            }

            return false;
        }

        private bool IsDescendent(Control c)
        {
            if (this.IsVisualAncestorOf(c))
                return true;

            // If the control is not a direct visual descendent, then check to make sure it's not
            // hosted in a popup that is a descendent of the cell.
            if (TopLevel.GetTopLevel(c)?.Parent is Control host)
                return this.IsVisualAncestorOf(host);

            return false;
        }

        private void ClearContentIfDetached()
        {
            if (DataContext is null && Parent is null && this.GetVisualParent() is null)
                FinalizeUnrealize();
        }

        private void SetCellTemplate(IDataTemplate? template)
        {
            if (ReferenceEquals(_sourceContentTemplate, template))
                return;

            _sourceContentTemplate = template;
            ContentTemplate = template is IRecyclingDataTemplate recyclingTemplate ?
                new ReattachableRecyclingDataTemplate(recyclingTemplate) :
                template;
        }

        private sealed class ReattachableRecyclingDataTemplate : IRecyclingDataTemplate
        {
            private readonly IRecyclingDataTemplate _inner;
            private Control? _lastControl;

            public ReattachableRecyclingDataTemplate(IRecyclingDataTemplate inner)
            {
                _inner = inner;
            }

            public Control? Build(object? data)
            {
                return Build(data, null);
            }

            public Control? Build(object? data, Control? existing)
            {
                _lastControl = _inner.Build(data, existing ?? _lastControl);
                return _lastControl;
            }

            public bool Match(object? data)
            {
                return _inner.Match(data);
            }
        }
    }
}
