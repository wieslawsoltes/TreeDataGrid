using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models

#else

namespace Avalonia.Controls.Models

#endif
{
    public class NotifyingBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool RaiseAndSetIfChanged<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                RaisePropertyChanged(propertyName);
                return true;
            }

            return false;
        }

        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void RaisePropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
    }
}
