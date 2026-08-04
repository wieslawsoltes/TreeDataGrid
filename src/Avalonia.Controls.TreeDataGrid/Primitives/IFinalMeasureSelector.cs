namespace Avalonia.Controls.Primitives
{
    internal interface IFinalMeasureSelector
    {
        bool NeedsFinalMeasure(Control element, int index);
    }
}
