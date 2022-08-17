using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Dialogs;
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
            try
            {

                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json");
                var config = configBuilder.Build();


                AppBootstrapper.Register(Locator.CurrentMutable, Locator.Current, config);
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
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