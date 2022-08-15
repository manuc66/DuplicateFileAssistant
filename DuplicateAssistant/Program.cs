using Avalonia;
using System;
using System.IO;
using Avalonia.ReactiveUI;
using FileCompare;
using Splat;
using Microsoft.Extensions.Configuration;

namespace DuplicateAssitant
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

    public class AppBootstrapper
    {
        public AppBootstrapper(Application app)
        {
            
        }

        public static void Register(IMutableDependencyResolver services, IReadonlyDependencyResolver resolver,
            IConfigurationRoot configurationRoot)
        {
            services
                .RegisterConstant<IConfiguration>(configurationRoot);
            services.RegisterLazySingleton<Trash>(() => new Trash(configurationRoot["trashFolder"]));
            services.Register(() => new DuplicateInFolderViewModel(resolver.GetService<Trash>(), configurationRoot["searchPath"]));
            services.Register(() => new MainWindowViewModel(resolver.GetService<DuplicateInFolderViewModel>()));
        }
    }
}