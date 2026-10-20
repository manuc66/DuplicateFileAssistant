---
title: Development
nav_order: 8
---

# Development

## Layout

| Project | Role |
|---|---|
| `DuplicateAssistant` | The Avalonia desktop app, its view models and business services. |
| `Cli` | The command-line front end (JSON output, dry runs). |
| `FileCompare` | Hashing, content comparison, scanning, grouping, trash, caches, audit log. |
| `ImageCompare` | Perceptual hashing, embedding and quality-scorer seams, the similar-image finder. |
| `ImageCompare.Onnx` | ONNX Runtime adapters and the model catalogue. |
| `FileCompare.Tests` | NUnit tests for the engine, CLI, caches and trash. |
| `Benchmarks` | BenchmarkDotNet projects for the comparison and hashing paths. |
| `Playground` | Scratch console project. |

The single-file utilities that predate the app (`FindDuplicate`, `FindDupByHash`,
`EliminateRenamed`, `DeleteIfAtOtherPlace`, `MergeToADirectory`, `FlattenDirectory`) are
still in the solution. They are independent console front ends; new work goes through
`FileCompare`.

## Build, test, format

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format FindDuplicate.sln --verify-no-changes --no-restore
```

CI runs on Ubuntu, Windows and macOS with the .NET 10 SDK, verifies formatting on Ubuntu,
and uploads coverage. The publish job is gated on the tests and runs only for pushes to
`main`; it produces self-contained archives for `win-x64`, `linux-x64` and `osx-arm64` as
workflow artifacts.

Warnings are errors (`Directory.Build.props` sets `TreatWarningsAsErrors`), and nullable
reference types are enabled. `global.json` pins .NET 10 and refuses prereleases.

## Guidelines

- Destructive operations are dry runs by default in the CLI, and confirmed in the app.
- A new cache belongs in `CachePaths`, with a key that includes everything that can change
  the value.
- A new image model is a catalogue entry (URL, SHA-256, preprocessing, output tensor,
  pooling) in `ImageCompare.Onnx`, not a branch in the finder.
- The trash manifest and the audit log are formats, not implementation details. See
  [Safety and trash](safety.md); tests for the failure paths live in `FileCompare.Tests`.

## License

GPL-3.0. The project was created in 2022 and is developed in the open on
[GitHub](https://github.com/manuc66/DuplicateFileAssistant).
