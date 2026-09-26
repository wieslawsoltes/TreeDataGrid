using System.ComponentModel;

namespace Uno.Controls.Presentation;

// A replaced inner model has already been realized with its new scalar value.
// Its control won't receive that model's earlier Value notification. Preserve
// the public property name while identifying the expander that owns the change,
// so nested expanders publish exactly once through the registered outer cell.
internal sealed class CellContentChangedEventArgs(CellValue owner) : PropertyChangedEventArgs(nameof(CellValue.Value))
{
    public CellValue Owner { get; } = owner;
}
