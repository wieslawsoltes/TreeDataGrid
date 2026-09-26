using Microsoft.UI.Xaml.Data;

namespace Uno.Controls.Presentation;

internal sealed partial class NativeColumnBinding
{
#if !WINDOWS
    private ElementNameSubject? _namedSource;
    private Binding? _namedBinding;
    private bool _namedSourceSubscribed;
#endif

    private void InitializeNamedSource()
    {
#if !WINDOWS
        // Local generated names carry their own source identity. Outer-scope
        // names still require a visual namescope and are left to native binding.
        _namedSource = (_binding.ElementName as ElementNameSubject ??
            (_binding.ElementName is null ? _binding.Source as ElementNameSubject : null)) is { IsLoadTimeBound: false } subject
            ? subject : null;
#endif
    }

    private bool HasNamedSource
    {
        get
        {
#if !WINDOWS
            return _namedSource is not null;
#else
            return false;
#endif
        }
    }

    private Binding ResolveNamedSource()
    {
#if !WINDOWS
        if (_namedSource is { } subject)
        {
            if (!_namedSourceSubscribed)
            {
                subject.ElementInstanceChanged += OnNamedSourceChanged;
                _namedSourceSubscribed = true;
            }
            var source = subject.ElementInstance;
            if (_namedBinding is null || !ReferenceEquals(_namedBinding.Source, source))
            {
                var binding = CopyBinding(_binding);
                binding.ElementName = null;
                binding.RelativeSource = null;
                binding.Source = source;
                _namedBinding = binding;
            }
            // Do not hand the subject to the native expression: its late-name
            // event subscription cannot be removed through the Binding contract.
            return _namedBinding;
        }
#endif
        return _binding;
    }

    private bool IsNamedSourceCurrent(Binding binding)
    {
#if !WINDOWS
        return _namedSource is null || ReferenceEquals(_namedSource.ElementInstance, binding.Source);
#else
        return true;
#endif
    }

    private void DetachNamedSource()
    {
#if !WINDOWS
        if (_namedSourceSubscribed)
        {
            _namedSource!.ElementInstanceChanged -= OnNamedSourceChanged;
            _namedSourceSubscribed = false;
        }
        _namedBinding = null;
#endif
    }

#if !WINDOWS
    private void OnNamedSourceChanged(object sender, object? source)
    {
        if (!_disposed && _model is { } model) Retarget(model);
    }
#endif
}
