using System;

namespace Avalonia.Threading
{
    public sealed class Dispatcher
    {
        public static Dispatcher UIThread { get; } = new();

        public bool CheckAccess() => true;
        public void VerifyAccess()
        {
        }

        public void Post(Action action)
        {
            action();
        }
    }
}
