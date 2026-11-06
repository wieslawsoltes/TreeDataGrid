using System;

namespace Uno.Controls.Presentation;

internal sealed partial class CellBinding<TModel, TValue> where TModel : class
{
    private int _writeRevision;

    internal void WriteConverted(object? value, IFormatProvider? provider)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_model is null) throw new InvalidOperationException("A suspended cell cannot write a value.");
        if (_column.Setter is null) throw new InvalidOperationException("The column is read-only.");
        if (value is null || value is TValue)
        {
            Write((TValue)value!);
            return;
        }

        var lifetime = _revision;
        var write = unchecked(++_writeRevision);
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        var converted = type.IsEnum
            ? Enum.Parse(type, value.ToString()!)
            : Convert.ChangeType(value, type, provider);
        // Conversion may invoke IConvertible, an enum input's ToString or a
        // custom format provider. No result may cross a lifetime boundary or
        // overwrite a more recent nested assignment, even to the same model.
        if (_disposed || _model is null || lifetime != _revision || write != _writeRevision) return;
        Write((TValue)converted!);
    }
}
