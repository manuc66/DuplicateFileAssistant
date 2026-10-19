using System;
using System.IO;
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
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string? configuredSearchPath = configurationRoot["searchPath"];
        string defaultSearchPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(defaultSearchPath))
        {
            defaultSearchPath = userProfile;
        }

        string searchPath = ResolvePath(configuredSearchPath, defaultSearchPath);

        string? configuredTrashFolder = configurationRoot["trashFolder"];
        string trashFolder = ResolvePath(configuredTrashFolder, Path.Combine(userProfile, "DuplicateTrash"));

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

    private static string ResolvePath(string? configuredPath, string fallbackPath)
    {
        string path = string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath;

        try
        {
            return PathUtilities.ExpandPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathUtilities.ExpandPath(fallbackPath);
        }
    }
}