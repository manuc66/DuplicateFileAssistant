using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using FileCompare;

namespace Benchmarks;

[MemoryDiagnoser]
public class DuplicateScanBenchmarks
{
    private string _temporaryDirectory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "DuplicateFileAssistant.Benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);

        byte[] content = RandomNumberGenerator.GetBytes(64 * 1024);

        for (int i = 0; i < 100; i++)
        {
            File.WriteAllBytes(Path.Combine(_temporaryDirectory, $"unique-{i}.bin"), RandomNumberGenerator.GetBytes(64 * 1024));
        }

        for (int i = 0; i < 25; i++)
        {
            File.WriteAllBytes(Path.Combine(_temporaryDirectory, $"duplicate-a-{i}.bin"), content);
            File.WriteAllBytes(Path.Combine(_temporaryDirectory, $"duplicate-b-{i}.bin"), content);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Benchmark]
    public int ScanForDuplicates()
    {
        Dictionary<string, HashSet<DuplicateFile>> result = DupProvider.FindDuplicateByHash(
            _temporaryDirectory,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            HashFile,
            CancellationToken.None);

        return result.Count;
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
