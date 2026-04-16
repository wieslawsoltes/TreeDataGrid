    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.UI.Xaml;
    using Uno.UI;
    using Uno.Resizetizer;

    namespace TreeDataGridUnoSample;

    public partial class App : Application
    {
        private readonly bool _exitAfterLaunching;
        private Window? _window;

        public App()
        {
            _exitAfterLaunching = Environment.GetCommandLineArgs().Any(a => a == "--exit");
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new Window
            {
                Content = new MainPage(),
            };
#if DEBUG
            _window.UseStudio();
#endif
            _window.SetWindowIcon();
            _window.Activate();

            if (_exitAfterLaunching)
                _ = ExitSoonAsync();
        }

        private async Task ExitSoonAsync()
        {
            await Task.Delay(1000);
            Exit();
        }

        public static void InitializeLogging()
        {
#if DEBUG
            var factory = LoggerFactory.Create(builder =>
            {
#if __WASM__
                builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
                builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());
                builder.AddConsole();
#else
                builder.AddConsole();
#endif
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("Uno", LogLevel.Warning);
                builder.AddFilter("Windows", LogLevel.Warning);
                builder.AddFilter("Microsoft", LogLevel.Warning);
            });

            global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
            global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
        }
    }
