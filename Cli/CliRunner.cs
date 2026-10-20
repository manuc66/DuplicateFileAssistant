using System.Security.Cryptography;
using System.Text.Json;
using FileCompare;

namespace DuplicateFileAssistant.Cli;

public static class CliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args, TextWriter output, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            output.WriteLine("Commands: duplicates, empty-files, big-files, temporary, invalid-symlinks, empty-folders");
            output.WriteLine("Options: --path <dir> [--json] [--apply] [--trash <dir>] [--exclude <dir>]... [--max <n>] [--algorithm sha256|md5]");
            return args.Length == 0 ? 1 : 0;
        }

        string command = args[0].ToLowerInvariant();
        CliOptions options = CliOptions.Parse(args.Skip(1));

        if (!options.TryGetSingle("path", out string? root) || string.IsNullOrWhiteSpace(root))
        {
            output.WriteLine("Missing required option: --path <dir>");
            return 1;
        }

        bool json = options.HasFlag("json");
        bool apply = options.HasFlag("apply");
        string trashPath = options.GetSingle("trash") ?? "~/DuplicateTrash";
        string[] excludedFolders = options.GetMany("exclude").ToArray();
        FileSearchFilter filter = FileSearchFilter.FromText(0, null, string.Join(";", excludedFolders));

        try
        {
            return command switch
            {
                "duplicates" => await RunDuplicatesAsync(root, options, filter, apply, trashPath, json, output, cancellationToken),
                "empty-files" => await RunFileFinderAsync("empty-files", root, filter,
                    () => EmptyFileFinder.Find(root, filter, cancellationToken), apply, trashPath, json, output, cancellationToken),
                "big-files" => await RunFileFinderAsync("big-files", root, filter,
                    () => BigFileFinder.Find(root, options.GetInt("max", 100), filter, cancellationToken),
                    apply, trashPath, json, output, cancellationToken),
                "temporary" => await RunFileFinderAsync("temporary", root, filter,
                    () => TemporaryFileFinder.Find(root, filter, cancellationToken),
                    apply, trashPath, json, output, cancellationToken),
                "invalid-symlinks" => await RunInvalidLinksAsync(root, filter, apply, trashPath, json, output, cancellationToken),
                "empty-folders" => await RunEmptyFoldersAsync(root, filter, apply, json, output, cancellationToken),
                _ => Unknown(command, output)
            };
        }
        catch (Exception ex)
        {
            output.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunDuplicatesAsync(string root, CliOptions options, FileSearchFilter filter, bool apply,
        string trashPath, bool json, TextWriter output, CancellationToken cancellationToken)
    {
        string algorithm = options.GetSingle("algorithm")?.ToLowerInvariant() ?? "sha256";

        Dictionary<HashKey, HashSet<DuplicateFile>> groups = DupProvider.FindDuplicateByHash(
            root,
            SearchOption.AllDirectories,
            fileInfo => !fileInfo.IsLink() && !fileInfo.IsFifoFile(),
            TextWriter.Null,
            _ => { },
            path => HashFile(path, algorithm),
            cancellationToken,
            filter.MatchesPath);

        List<object> results = groups.Select(group => new
        {
            size = group.Value.First().Length,
            hash = group.Key.ToString(),
            files = group.Value.Select(file => file.Path).OrderBy(path => path, PathUtilities.PathComparer).ToArray()
        }).Cast<object>().ToList();

        int affected = groups.Sum(group => Math.Max(0, group.Value.Count - 1));

        if (apply)
        {
            Trash trash = new(trashPath);
            await DeleteFilesAsync(groups.SelectMany(group =>
                group.Value.OrderBy(file => file.Path, PathUtilities.PathComparer).Skip(1)), trash, output,
                cancellationToken);
        }

        Write(output, json, command: "duplicates", root, apply, affected, results);
        return 0;
    }

    private static async Task<int> RunFileFinderAsync(string command, string root, FileSearchFilter filter,
        Func<IReadOnlyList<DuplicateFile>> finder, bool apply, string trashPath, bool json, TextWriter output,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DuplicateFile> files = finder();
        var results = files.Select(file => new { path = file.Path, length = file.Length }).Cast<object>().ToList();

        if (apply && files.Count > 0)
        {
            Trash trash = new(trashPath);
            await DeleteFilesAsync(files, trash, output, cancellationToken);
        }

        Write(output, json, command, root, apply, files.Count, results);
        return 0;
    }

    private static async Task<int> RunInvalidLinksAsync(string root, FileSearchFilter filter, bool apply,
        string trashPath, bool json, TextWriter output, CancellationToken cancellationToken)
    {
        IReadOnlyList<InvalidSymbolicLink> links = InvalidSymbolicLinkFinder.Find(root, filter, cancellationToken);
        var results = links.Select(link => new { path = link.Path, target = link.Target }).Cast<object>().ToList();

        if (apply && links.Count > 0)
        {
            Trash trash = new(trashPath);

            foreach (InvalidSymbolicLink link in links)
            {
                await trash.DeleteSymbolicLinkAsync(link.Path, cancellationToken);
            }
        }

        Write(output, json, "invalid-symlinks", root, apply, links.Count, results);
        return 0;
    }

    private static Task<int> RunEmptyFoldersAsync(string root, FileSearchFilter filter, bool apply, bool json,
        TextWriter output, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> directories = EmptyDirectoryService.FindEmptyDirectories(
            root,
            cancellationToken,
            filter.MatchesDirectory);

        var results = directories.Select(directory => new { path = directory }).Cast<object>().ToList();

        if (apply)
        {
            EmptyDirectoryService.DeleteEmptyDirectories(directories, cancellationToken);
        }

        Write(output, json, "empty-folders", root, apply, directories.Count, results);
        return Task.FromResult(0);
    }

    private static async Task DeleteFilesAsync(IEnumerable<DuplicateFile> files, Trash trash, TextWriter output,
        CancellationToken cancellationToken)
    {
        foreach (DuplicateFile file in files)
        {
            await trash.DeleteAsync(file.Path, file.Length, file.LastWriteTimeUtc, cancellationToken);
        }
    }

    private static void Write(TextWriter output, bool json, string command, string root, bool applied, int affected,
        IReadOnlyList<object> results)
    {
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                command,
                root,
                applied,
                affected,
                results
            }, JsonOptions));
            return;
        }

        output.WriteLine($"{command}: {root} ({(applied ? "applied" : "dry-run")})");
        output.WriteLine($"Affected: {affected}");

        foreach (object result in results)
        {
            output.WriteLine(result);
        }
    }

    private static int Unknown(string command, TextWriter output)
    {
        output.WriteLine($"Unknown command: {command}");
        return 1;
    }

    private static byte[] HashFile(string path, string algorithm)
    {
        using FileStream stream = File.OpenRead(path);

        return algorithm == "md5"
            ? MD5.HashData(stream)
            : SHA256.HashData(stream);
    }
}
