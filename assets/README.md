# Icons

`icon.svg` is the source of truth for the README, the window icon and the large
Windows icon sizes. `icon-small.svg` is a simplified variant used for the 16, 24 and
32 px frames of the `.ico`, where the document lines would be unreadable.

Regenerate the raster assets with librsvg and ImageMagick:

```bash
rsvg-convert -w 512 -h 512 assets/icon.svg -o assets/icon.png

for size in 16 24 32; do
  rsvg-convert -w "$size" -h "$size" assets/icon-small.svg -o "/tmp/icon-small-$size.png"
done
for size in 48 64 128 256; do
  rsvg-convert -w "$size" -h "$size" assets/icon.svg -o "/tmp/icon-$size.png"
done

magick /tmp/icon-small-16.png /tmp/icon-small-24.png /tmp/icon-small-32.png \
  /tmp/icon-48.png /tmp/icon-64.png /tmp/icon-128.png /tmp/icon-256.png \
  assets/icon.ico
```
