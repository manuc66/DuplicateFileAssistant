using BenchmarkDotNet.Attributes;
using FileCompare;

namespace Benchmarks;

[MemoryDiagnoser]
public class FileComparisonBenchmarks
{
    private string _first = null!;
    private string _second = null!;
    private string _temporaryDirectory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "DuplicateFileAssistant.Benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _first = Path.Combine(_temporaryDirectory, "first.bin");
        _second = Path.Combine(_temporaryDirectory, "second.bin");

        byte[] content = new byte[4 * 1024 * 1024];
        Random.Shared.NextBytes(content);
        File.WriteAllBytes(_first, content);
        File.WriteAllBytes(_second, content);
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
    public bool CompareIdenticalFiles() => FastFileCompare.AreFilesEqual(new FileInfo(_first), new FileInfo(_second));
}
