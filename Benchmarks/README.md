# Benchmarks

Run all benchmarks:

```bash
dotnet run -c Release --project Benchmarks
```

Run one benchmark class:

```bash
dotnet run -c Release --project Benchmarks -- --filter '*HashBenchmarks*'
```

Results are written to `BenchmarkDotNet.Artifacts/`.
