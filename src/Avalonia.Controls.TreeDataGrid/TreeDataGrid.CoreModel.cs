using System;
using Avalonia.Controls.Adapters;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls
{
    public partial class TreeDataGrid
    {
        /// <summary>A neutral model. The control owns its presentation adapter, never the model.</summary>
        public static readonly StyledProperty<Core.ITreeDataGridSource?> ModelProperty =
            AvaloniaProperty.Register<TreeDataGrid, Core.ITreeDataGridSource?>(nameof(Model));
        private ITreeDataGridSource? _corePresentation;
        private bool _settingCoreSource;
        public Core.ITreeDataGridSource? Model
        {
            get => GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }
        private void UpdateCorePresentation()
        {
            ReleaseCorePresentation();
            if (Model is null)
                return;
            _corePresentation = TreeDataGridSourceAdapter.Create(Model);
            SetCoreSource(_corePresentation);
        }
        private void ReleaseCorePresentation()
        {
            if (_corePresentation is not { } presentation)
                return;
            _corePresentation = null;
            if (ReferenceEquals(Source, presentation))
                SetCoreSource(null);
            ((IDisposable)presentation).Dispose();
        }
        private void SetCoreSource(ITreeDataGridSource? source)
        {
            _settingCoreSource = true;
            try
            { Source = source; }
            finally { _settingCoreSource = false; }
        }
    }
}
