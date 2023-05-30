using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using Splat;
using Microsoft.Extensions.Configuration;
using ReactiveUI;

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

                IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json");
                IConfigurationRoot config = configBuilder.Build();
                
                TaskScheduler.UnobservedTaskException +=  (sender, eventArgs) => Console.WriteLine(eventArgs.Exception.ToString());
                RxApp.DefaultExceptionHandler = new MyCoolObservableExceptionHandler();


                AppBootstrapper.Register(Locator.CurrentMutable, Locator.Current, config);
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            AppBuilder? appBuilder = AppBuilder.Configure<App>()
                .UseReactiveUI()
                .UsePlatformDetect()
                .LogToTrace();

            return appBuilder;
        }
    }
}

public class MyCoolObservableExceptionHandler : IObserver<Exception>
{
    public void OnNext(Exception value)
    {
        if (Debugger.IsAttached) Debugger.Break();
        
        Console.WriteLine(value.ToString());

        RxApp.MainThreadScheduler.Schedule(() => { throw value; }) ;
    }

    public void OnError(Exception error)
    {
        if (Debugger.IsAttached) Debugger.Break();

        Console.WriteLine(error.ToString());

        RxApp.MainThreadScheduler.Schedule(() => { throw error; });
    }

    public void OnCompleted()
    {
        if (Debugger.IsAttached) Debugger.Break();
        RxApp.MainThreadScheduler.Schedule(() => { throw new NotImplementedException(); });
    }
}