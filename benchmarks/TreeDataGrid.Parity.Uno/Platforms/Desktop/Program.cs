using System;
using Uno.UI.Hosting;

namespace TreeDataGrid.Parity.Uno;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => UnoPlatformHostBuilder.Create()
        .App(() => new App()).UseX11().UseLinuxFrameBuffer().UseMacOS().UseWin32().Build().Run();
}
