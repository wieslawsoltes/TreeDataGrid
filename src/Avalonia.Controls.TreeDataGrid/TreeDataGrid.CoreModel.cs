using System;
using Avalonia.Controls.Presentation;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls
{
    public partial class TreeDataGrid
    {
        /// <summary>The primary, framework-neutral source. The control owns view state, never this model.</summary>
        public static readonly StyledProperty<Core.ITreeDataGridSource?> ModelProperty =
            AvaloniaProperty.Register<TreeDataGrid, Core.ITreeDataGridSource?>(nameof(Model));
        public static readonly StyledProperty<ITreeDataGridPresentationOptions?> PresentationOptionsProperty =
            AvaloniaProperty.Register<TreeDataGrid, ITreeDataGridPresentationOptions?>(nameof(PresentationOptions));
        public static readonly DirectProperty<TreeDataGrid, TreeDataGridPresentation?> PresentationProperty =
            AvaloniaProperty.RegisterDirect<TreeDataGrid, TreeDataGridPresentation?>(nameof(Presentation), x => x.Presentation);
        private TreeDataGridPresentation? _presentation;
        public Core.ITreeDataGridSource? Model
        {
            get => GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }
        public ITreeDataGridPresentationOptions? PresentationOptions
        {
            get => GetValue(PresentationOptionsProperty);
            set => SetValue(PresentationOptionsProperty, value);
        }
        public TreeDataGridPresentation? Presentation => _presentation;
        private void UpdateCorePresentation()
        {
            if (Model is null)
            {
                UpdateActiveSource();
                return;
            }
            var oldSource = _source;
            _source = null;
            _explicitSource = null;
            if (oldSource is not null) RaisePropertyChanged(SourceProperty, oldSource, null);
            SetPresentation(TreeDataGridPresentation.Create(Model, PresentationOptions));
            ApplySelectionMode();
        }
        private void ReleaseCorePresentation()
        {
            if (Model is not null) _presentation?.Suspend();
        }
        private void SetPresentation(TreeDataGridPresentation? presentation)
        {
            UnsubscribeSourceEvents();
            var old = _presentation;
            if (old is not null) old.NativeSelectionChanged -= OnNativeSelectionChanged;
            _presentation = presentation;
            if (presentation is not null) presentation.NativeSelectionChanged += OnNativeSelectionChanged;
            Columns = presentation?.Columns;
            Rows = presentation?.Rows;
            SelectionInteraction = presentation?.SelectionInteraction;
            SubscribeSourceEvents();
            SubscribeSelectionModel();
            RaisePropertyChanged(PresentationProperty, old, presentation);
            old?.Dispose();
        }
    }
}
