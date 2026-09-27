namespace Uno.Controls.Presentation;

/// <summary>One editor's native composition and end-of-dispatch boundary.</summary>
internal struct EditorCompositionState
{
    private int _generation;
    private bool _active;
    private bool _settling;
    public bool BlocksEditKeys => _active || _settling;

    public void Begin()
    {
        unchecked { ++_generation; }
        _active = true;
        _settling = false;
    }

    public int End()
    {
        _active = false;
        _settling = true;
        return unchecked(++_generation);
    }

    public bool Complete(int generation)
    {
        if (!_settling || generation != _generation) return false;
        _settling = false;
        return true;
    }

    public void Reset()
    {
        unchecked { ++_generation; }
        _active = _settling = false;
    }
}
