---
title: Caches
nav_order: 7
---

# Caches

Everything expensive is persisted under the platform's `LocalApplicationData` directory,
in `DuplicateFileAssistant/`: `%LOCALAPPDATA%\DuplicateFileAssistant` on Windows,
`~/.local/share/DuplicateFileAssistant` on Linux.

| Cache | File | Contents | Invalidated by |
|---|---|---|---|
| File and perceptual hashes | `scan-cache.json` | SHA-256/MD5 and dHash values | path, length, last write time, algorithm |
| Embeddings | `embeddings-<model>.bin` | one `float[]` per image | path, length, last write time; model id in the file name |
| Quality scores | `quality-<scorer>.bin` | one score per image | path, length, last write time; scorer id in the file name |
| Models | `models/<model>/...` | ONNX files and `.verified` SHA-256 markers | SHA-256 of the downloaded file |

The scan cache is keyed by path, length, last write time and algorithm. The embedding and
quality caches are keyed by path, length and last write time; the model or scorer is part
of the file name. A changed length or timestamp recomputes the value.

**Not persisted**, because they are cheap or derived: file enumeration, the SimHash index,
BK-trees, grouping and the final report. They are rebuilt from the caches on every run.

## Maintaining the caches

```bash
dotnet run --project Cli -- cache --json
dotnet run --project Cli -- cache --prune
dotnet run --project Cli -- cache --clear --apply
```

| Command | Effect |
|---|---|
| `cache` | List entries and bytes per cache. |
| `cache --prune` | Remove entries whose files no longer exist. |
| `cache --clear --apply` | Delete the computed caches. Downloaded models are kept. |
| `cache --cache-dir <dir>` | Inspect another cache directory. |

`duplicates` and `similar-images` accept `--prune-cache` to prune while scanning. To
override individual files, use `--cache-path`, `--embedding-cache` and `--quality-cache`.
`--no-cache` disables the data caches for one run; the model cache is separate and keeps
its downloaded files.

## The known trade-off

Cache entries are keyed by path, length and last write time. A file modified without
changing its size or timestamp (for example with `touch -r`) can reuse a stale value.
Deleting the caches is always safe: the next run recomputes what it needs.

The desktop app uses the same scan cache for its content-hash tabs. The CLI and the app
share the cache directory, so a folder scanned by one is faster in the other. In the
**Similar images** tab, the **Models and caches** expander shows the same totals and offers
**Refresh**, **Prune**, **Clear caches** and **Open model folder**.
