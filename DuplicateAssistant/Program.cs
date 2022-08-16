using Avalonia;
using System;
using System.IO;
using Avalonia.ReactiveUI;
using Splat;
using Microsoft.Extensions.Configuration;

namespace DuplicateAssistant
{
    class Program
    {

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json"); 
            var config = configBuilder.Build();
			

            AppBootstrapper.Register(Locator.CurrentMutable, Locator.Current, config);
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            var appBuilder = AppBuilder.Configure<App>()
                .UseReactiveUI()
                .UsePlatformDetect()
                .LogToTrace();
         
            return appBuilder;
        }
    }
}