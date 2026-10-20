using System.Globalization;
using System.Security.Cryptography;
using FileCompare;

namespace ImageCompare.Onnx;

/// <summary>
/// Downloads model files on first use into a local cache, verifies their
/// SHA-256 and keeps a small marker so subsequent launches do not re-hash
/// hundreds of megabytes.
/// </summary>
public sealed class OnnxModelStore
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OnnxModelStore(string? rootDirectory = null, HttpClient? httpClient = null)
    {
        RootDirectory = Path.GetFullPath(rootDirectory ?? DefaultRootDirectory);
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public string RootDirectory { get; }

    public static string DefaultRootDirectory => CachePaths.ModelsDirectory;

    public Task<IReadOnlyDictionary<string, string>> EnsureFilesAsync(
        OnnxImageEmbeddingModel model,
        bool allowDownload,
        Action<string>? log,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        return EnsureFilesAsync(model.Id, model.DisplayName, model.Files, allowDownload, log,
            cancellationToken, progress);
    }

    public Task<IReadOnlyDictionary<string, string>> EnsureFilesAsync(
        OnnxQualityModel model,
        bool allowDownload,
        Action<string>? log,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        return EnsureFilesAsync(model.Id, model.DisplayName, model.Files, allowDownload, log,
            cancellationToken, progress);
    }

    private async Task<IReadOnlyDictionary<string, string>> EnsureFilesAsync(
        string modelId,
        string displayName,
        IReadOnlyList<OnnxModelFile> modelFiles,
        bool allowDownload,
        Action<string>? log,
        CancellationToken cancellationToken,
        IProgress<double>? progress)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string directory = Path.Combine(RootDirectory, modelId);
            Directory.CreateDirectory(directory);

            Dictionary<string, string> paths = new(StringComparer.OrdinalIgnoreCase);

            foreach (OnnxModelFile modelFile in modelFiles)
            {
                string path = Path.Combine(directory, modelFile.FileName);
                paths[modelFile.FileName] = path;

                if (await IsVerifiedAsync(path, modelFile, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                if (!allowDownload)
                {
                    throw new FileNotFoundException(
                        $"Model file '{modelFile.FileName}' is missing from '{directory}'. " +
                        $"Download it from {modelFile.Url} or allow downloads.",
                        path);
                }

                await DownloadAsync(modelFile, path, displayName, log, progress, cancellationToken)
                    .ConfigureAwait(false);
            }

            return paths;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DownloadAsync(OnnxModelFile modelFile, string path, string displayName,
        Action<string>? log, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        string temporaryPath = path + ".download";

        log?.Invoke(
            $"Downloading {displayName} ({FormatSize(modelFile.Size)}) from {modelFile.Url}...");
        progress?.Report(0);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(modelFile.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? modelFile.Size;
            long copied = 0;
            byte[] buffer = new byte[81920];

            await using (Stream source = await response.Content
                             .ReadAsStreamAsync(cancellationToken)
                             .ConfigureAwait(false))
            await using (FileStream target = File.Create(temporaryPath))
            {
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                    copied += read;

                    if (total > 0)
                    {
                        progress?.Report(Math.Min(99.9, copied * 100.0 / total));
                    }
                }
            }

            progress?.Report(100);

            FileInfo downloaded = new(temporaryPath);

            if (downloaded.Length != modelFile.Size)
            {
                throw new InvalidDataException(
                    $"Downloaded {modelFile.FileName} has {downloaded.Length} bytes, expected {modelFile.Size}.");
            }

            string hash = await ComputeSha256Async(temporaryPath, cancellationToken).ConfigureAwait(false);

            if (!hash.Equals(modelFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SHA-256 mismatch for {modelFile.FileName}: got {hash}, expected {modelFile.Sha256}.");
            }

            File.Move(temporaryPath, path, overwrite: true);
            await WriteVerificationMarkerAsync(path, modelFile.Sha256, cancellationToken)
                .ConfigureAwait(false);

            log?.Invoke($"Model file ready: {path}");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static async Task<bool> IsVerifiedAsync(string path, OnnxModelFile modelFile,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != modelFile.Size)
        {
            return false;
        }

        string markerPath = path + ".verified";

        if (File.Exists(markerPath))
        {
            string marker = await File.ReadAllTextAsync(markerPath, cancellationToken)
                .ConfigureAwait(false);

            if (marker.Trim().Equals(modelFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        string hash = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);

        if (!hash.Equals(modelFile.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await WriteVerificationMarkerAsync(path, hash, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task WriteVerificationMarkerAsync(string path, string hash,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path + ".verified", hash, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatSize(long bytes)
    {
        return (bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("DuplicateFileAssistant/1.0");
        return client;
    }
}
