namespace ImageCompare;

/// <summary>
/// Produces a fixed-size, L2-normalized embedding for an image. Implementations
/// are expected to be thread-safe: the finder calls them from several workers.
/// </summary>
public interface IImageEmbeddingProvider : IDisposable
{
    string Id { get; }

    int Dimensions { get; }

    float[] Embed(string path);
}
