using Avalonia;
using FileCompare;
using Microsoft.Extensions.Configuration;
using Splat;

namespace DuplicateAssitant;

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