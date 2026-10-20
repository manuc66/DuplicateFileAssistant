---
title: Similar images
nav_order: 5
---

# Similar images

`similar-images` finds near-identical pictures: the same photo saved at another
resolution, another compression level, or with a small crop or edit. It is available in the
desktop app (**Similar images** tab) and as a CLI command. The CLI command is **report-only**
and never deletes a file, even with `--apply`; the desktop tab uses the normal confirmed
trash flow.

```bash
dotnet run --project Cli -- similar-images --path ~/Pictures --json
```

## Desktop tab

The **Similar images** tab applies the same finder. The default pass is dHash; two optional
passes refine it:

- **Detect crops and edits (DINOv2)** enables the semantic pass. The model is downloaded on
  first use unless **Allow model download** is unchecked.
- **Suggest a keeper by quality** enables quality scoring. The default is the model-free
  heuristic; **Use DBCNN quality model** selects the trained model instead.
- The numeric field next to the semantic checkbox is the cosine threshold (0.80 by default).
- The progress bar reports model downloads, then the scan itself.
- **Dry run** is checked by default. **Delete non-keepers** moves the other copies to the
  trash after confirmation.
- Each group shows a thumbnail of the suggested keeper, decoded at a small size.
- Each file row shows its dHash distance, cosine similarity and quality score when those
  values are available.
- Selecting a file shows it side by side with the keeper in a larger preview. The
  **Zoom** button opens a full-screen viewer with zoom in/out, actual size and F11. Drag
  the image to pan when zoomed.
- In the viewer, the **Overlay** slider blends the two pictures (0 = first, 1 = second),
  and **Difference** shows a normalized absolute-difference heatmap. The heatmap is
  meaningful for the same framing (resize, recompression); a crop or rotation is not
  aligned, so it appears as a large difference.

Perceptual matches are not byte-identical, so the **Hardlink** action is disabled for image
groups in the desktop tab.

The **Models and caches** expander at the bottom of the tab shows the size of the hash,
embedding, quality and model caches. It can refresh the summary, prune entries for deleted
files, clear the computed caches (downloaded models are kept), and open the model folder.

**Stop** cancels the scan or an in-progress model download. A partially downloaded model is
removed; the next scan starts the download again.

## The perceptual pass

Every image gets a 64-bit **difference hash** (dHash):

1. The image is decoded at a reduced scale.
2. Transparency is composited on white.
3. EXIF orientation is applied, so a portrait photo stored sideways is compared upright.
4. A 9x8 grayscale grid is compared horizontally, one bit per neighbouring pair.

The distance between two hashes is the Hamming distance. A distance of 0 means the grids
are identical; 64 means every comparison differs.

- `--threshold <bits>` defaults to **8** over 64 bits.
- Images must also have a compatible aspect ratio (**8% tolerance**) to be grouped.
- `--min-size <bytes>` defaults to **10 KiB**, which keeps icons and thumbnails out.
- `--extensions <ext,…>` defaults to `jpg,jpeg,png,gif,bmp,webp,ico`.

Formats are the ones SkiaSharp decodes. **HEIC, AVIF and camera RAW are not decoded**; a
library of iPhone originals needs a conversion step first.

Grouping is **leader-based**: a picture can join an existing group, but a group is never
merged into another group through a member. This avoids the chaining failure where A looks
like B and B looks like C, so A, B and C are reported as one group even though A and C are
unrelated.

## The semantic pass

dHash cannot see that a crop or a colour edit is the same scene. `--semantic` adds a
DINOv2-small embedding pass:

```bash
dotnet run --project Cli -- similar-images --path ~/Pictures --semantic --json
```

Embeddings are compared with cosine similarity, and grouping stays leader-based:

- `--semantic-threshold <0..1>` defaults to **0.80**.
- `--semantic-margin <0..1>` defaults to **0.05**. A borderline match must be clearly
  better than the second-best candidate. Obvious matches (≥ 0.94) bypass the margin, so
  three or more copies of the same picture can still join the same group.
- `--embedding <id>` selects the model; `--semantic` is shorthand for
  `--embedding dinov2-small`.

The threshold is tuned for photographs. Libraries of very uniform illustrations or
renders may need a higher value.

## Suggested keeper

`--keeper` scores the technical quality of every image in a group without a model
(Laplacian sharpness and resolution). `--keeper-model dbcnn` uses DBCNN (Apache-2.0,
~60 MB) instead.

```bash
dotnet run --project Cli -- similar-images --path ~/Pictures --keeper --json
dotnet run --project Cli -- similar-images --path ~/Pictures --keeper-model dbcnn --json
```

The keeper is chosen among copies within **5% of the resolution of the largest member**, so
a 640 px thumbnail can never be suggested over the 4K original even if its quality score is
higher.

Quality scoring only runs on images that already belong to a group. The result is a
**suggestion**: no file is moved or deleted. Distances and similarities in the report are
measured against the group **leader**, not against the keeper; the keeper can be another
member when it scores better.

## Models and downloads

Models are downloaded on first use to
`%LOCALAPPDATA%/DuplicateFileAssistant/models` (on Linux and macOS, the equivalent
`LocalApplicationData` directory), verified with SHA-256, and reused offline. In the
desktop app, **Settings → Models** changes that folder.

| Id | Role | Licence | Size |
|---|---|---|---|
| `dinov2-small` | Embeddings for the semantic pass | Apache-2.0 | downloaded on demand |
| `dbcnn` | Quality scoring for the keeper | Apache-2.0 | ~60 MB |

- `--model-dir <dir>` puts the models somewhere else.
- `--no-download` fails instead of downloading a missing model.
- Model files are not part of the repository; they come from Hugging Face.

The core `ImageCompare` project only knows `IImageEmbeddingProvider` and `IQualityScorer`
and has no ONNX dependency. The ONNX adapter lives in `ImageCompare.Onnx`. Another model
is a catalogue entry (URL, SHA-256, preprocessing, output tensor, pooling) or another
provider, not a change to the finder.

## Caches

Hashes are cached in `scan-cache.json`; embeddings go to `embeddings-<model>.bin` and
quality scores to `quality-<scorer>.bin`, next to it.

- `--no-cache` disables the data caches for the run.
- `--cache-path`, `--embedding-cache` and `--quality-cache` override individual files.
- `--prune-cache` drops entries whose files no longer exist while scanning.

The full list, including how to inspect and clear them, is on [Caches](caches.md).

## Performance

Measured on CPU, from the README:

- **dHash**: ~500 images/s on 3.8 MP JPEGs with 8 workers.
- **DINOv2-small**: ~100 ms/image single-threaded; 133 images in ~7 s with 8 workers. An
  embedding cache makes later runs nearly instant.
- **DBCNN**: ~100 ms/image, only for images inside groups.

Up to **5,000 images**, semantic candidates are compared exhaustively. Above that, the
finder uses an in-memory 128-bit SimHash index, which is approximate for borderline crops.

The CLI pulls SkiaSharp (~11 MB) and ONNX Runtime (~15 MB) per runtime identifier, plus the
models downloaded on demand. The desktop app references the same projects and exposes the
feature in the **Similar images** tab.

## Limits

- **CLI report-only**: there is no `--apply`, and passing it is an error. The desktop tab can
  move non-keepers to the trash, with a confirmation.
- Not a classifier: dHash groups by visual structure, not by "the same subject". Landscape
  photos of the same beach can match; two portraits can match. The semantic pass narrows
  this, it does not remove the need to look.
- A group's distances are relative to its leader. A chain of loose matches cannot merge
  unrelated images, but a very low threshold can still split a real group.
- The keeper is a suggestion about technical quality, not about which copy you want to
  keep.
