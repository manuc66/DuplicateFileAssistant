using System;
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
        string? configuredSearchPath = configurationRoot["searchPath"];
        string searchPath = !string.IsNullOrWhiteSpace(configuredSearchPath) ? configuredSearchPath : "~/Documents";


        string? configuredTrashFolder = configurationRoot["trashFolder"];
        string trashFolder = !string.IsNullOrWhiteSpace(configuredTrashFolder) ? configuredTrashFolder : "~/DuplicateTrash";

        services
            .RegisterConstant<IConfiguration>(configurationRoot);
        services.RegisterLazySingleton(() => new Trash(trashFolder));
        services.RegisterLazySingleton(() => new FileManagerHandler());

        Trash trash = resolver.GetService<Trash>() ?? throw new InvalidOperationException("Trash service is not registered.");
        FileManagerHandler fileManagerHandler = resolver.GetService<FileManagerHandler>() ?? throw new InvalidOperationException("FileManagerHandler service is not registered.");

        services.Register(() => new DuplicateContentInFolderViewModel(trash, searchPath, fileManagerHandler));
        services.Register(() => new DuplicateNameInFolderViewModel(trash, searchPath, fileManagerHandler));
        services.Register(() => new MainWindowViewModel(
            resolver.GetService<DuplicateContentInFolderViewModel>() ?? throw new InvalidOperationException("DuplicateContentInFolderViewModel service is not registered."),
            resolver.GetService<DuplicateNameInFolderViewModel>() ?? throw new InvalidOperationException("DuplicateNameInFolderViewModel service is not registered.")));
    }
}