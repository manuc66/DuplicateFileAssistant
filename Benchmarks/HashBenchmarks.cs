using BenchmarkDotNet.Attributes;
using DuplicateAssistant.Business.FileHashing;

namespace Benchmarks;

[MemoryDiagnoser]
public class HashBenchmarks
{
    private string _file = null!;
    private string _temporaryDirectory = null!;
    private readonly Md5FileHashing _md5 = new();
    private readonly Blake3FileHashing _blake3 = new();
    private readonly XXHashFileHashing _xxHash = new();

    [GlobalSetup]
    public void Setup()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "DuplicateFileAssistant.Benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _file = Path.Combine(_temporaryDirectory, "file.bin");

        byte[] content = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(content);
        File.WriteAllBytes(_file, content);
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
    public byte[] Md5() => _md5.Hash(_file);

    [Benchmark]
    public byte[] Blake3() => _blake3.Hash(_file);

    [Benchmark]
    public byte[] XXHash() => _xxHash.Hash(_file);
}
