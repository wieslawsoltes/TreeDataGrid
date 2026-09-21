using System.Threading.Tasks;
using Uno.UI.Hosting;
using TreeDataGridUnoSamples;

namespace TreeDataGridUnoActivityMonitor;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        SampleRunContext.InitializeBrowser(global::Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.location.search"));
        global::Uno.Foundation.WebAssemblyRuntime.InvokeJS("globalThis.__treeDataGridSmoke = { complete: false, passed: false, error: null }; '';");
        SampleRunContext.ResultSink = (passed, error) => global::Uno.Foundation.WebAssemblyRuntime.InvokeJS(
            "globalThis.__treeDataGridSmoke = " + SampleRunContext.SerializeResult(passed, error) + "; '';");
        App.InitializeLogging();
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWebAssembly()
            .Build();
        await host.RunAsync();
    }
}
