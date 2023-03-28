using Avalonia;
using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;
using FileCompare;
using Microsoft.Extensions.Configuration;
using Splat;

namespace DuplicateAssistant;

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
        services.RegisterLazySingleton(() => new Trash(configurationRoot["trashFolder"]));
        services.RegisterLazySingleton(() => new FileManagerHandler());
        services.Register(() => new DuplicateContentInFolderViewModel(resolver.GetService<Trash>(), configurationRoot["searchPath"], resolver.GetService<FileManagerHandler>()));
        services.Register(() => new DuplicateNameInFolderViewModel(resolver.GetService<Trash>(), configurationRoot["searchPath"], resolver.GetService<FileManagerHandler>()));
        services.Register(() => new MainWindowViewModel(
            resolver.GetService<DuplicateContentInFolderViewModel>(),
            resolver.GetService<DuplicateNameInFolderViewModel>()));
    }
}