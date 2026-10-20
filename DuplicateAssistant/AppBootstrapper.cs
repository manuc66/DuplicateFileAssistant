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
        services.RegisterLazySingleton<IDeleteConfirmation>(() => new DeleteConfirmationService());
        services.RegisterLazySingleton<IFolderPicker>(() => new FolderPickerService());

        Trash trash = resolver.GetService<Trash>() ?? throw new InvalidOperationException("Trash service is not registered.");
        FileManagerHandler fileManagerHandler = resolver.GetService<FileManagerHandler>() ?? throw new InvalidOperationException("FileManagerHandler service is not registered.");
        IDeleteConfirmation deleteConfirmation = resolver.GetService<IDeleteConfirmation>() ?? throw new InvalidOperationException("DeleteConfirmation service is not registered.");
        IFolderPicker folderPicker = resolver.GetService<IFolderPicker>() ?? throw new InvalidOperationException("FolderPicker service is not registered.");

        ScanCache scanCache = new(ScanCache.DefaultPath);

        services.Register(() => new DuplicateContentInFolderViewModel(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker, scanCache));
        services.Register(() => new DuplicateNameInFolderViewModel(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker));
        services.Register(() => new DuplicateSizeInFolderViewModel(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker));
        services.Register(() => new SimilarImageViewModel(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker, scanCache));
        services.Register(() => new MultiFolderDuplicateViewModel(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker, scanCache));
        services.Register(() => new EmptyFolderViewModel(searchPath, deleteConfirmation, folderPicker, trash));
        services.Register(() => new DeleteIfElsewhereViewModel(trash, searchPath, deleteConfirmation, folderPicker, scanCache));
        services.Register(() => new MergeToDestinationViewModel(trash, searchPath, deleteConfirmation, folderPicker));
        services.Register(() => new TrashViewModel(trash, deleteConfirmation));

        services.Register(() => new MainWindowViewModel(
            resolver.GetService<DuplicateContentInFolderViewModel>() ?? throw new InvalidOperationException("DuplicateContentInFolderViewModel service is not registered."),
            resolver.GetService<DuplicateNameInFolderViewModel>() ?? throw new InvalidOperationException("DuplicateNameInFolderViewModel service is not registered."),
            resolver.GetService<DuplicateSizeInFolderViewModel>() ?? throw new InvalidOperationException("DuplicateSizeInFolderViewModel service is not registered."),
            resolver.GetService<SimilarImageViewModel>() ?? throw new InvalidOperationException("SimilarImageViewModel service is not registered."),
            resolver.GetService<MultiFolderDuplicateViewModel>() ?? throw new InvalidOperationException("MultiFolderDuplicateViewModel service is not registered."),
            resolver.GetService<EmptyFolderViewModel>() ?? throw new InvalidOperationException("EmptyFolderViewModel service is not registered."),
            resolver.GetService<DeleteIfElsewhereViewModel>() ?? throw new InvalidOperationException("DeleteIfElsewhereViewModel service is not registered."),
            resolver.GetService<MergeToDestinationViewModel>() ?? throw new InvalidOperationException("MergeToDestinationViewModel service is not registered."),
            resolver.GetService<TrashViewModel>() ?? throw new InvalidOperationException("TrashViewModel service is not registered."),
            new FileListViewModel(trash, deleteConfirmation, folderPicker, FileListMode.EmptyFiles, searchPath),
            new FileListViewModel(trash, deleteConfirmation, folderPicker, FileListMode.BigFiles, searchPath),
            new FileListViewModel(trash, deleteConfirmation, folderPicker, FileListMode.TemporaryFiles, searchPath),
            new InvalidLinkViewModel(trash, deleteConfirmation, folderPicker, searchPath)));
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