using ImageCompare;

namespace ImageCompare.Onnx;

public static class OnnxProviders
{
    public static IReadOnlyCollection<string> ImageEmbeddingModelIds =>
        OnnxModelCatalog.ImageEmbeddings.Keys.ToArray();

    public static IReadOnlyCollection<string> QualityModelIds =>
        OnnxModelCatalog.QualityModels.Keys.ToArray();

    public static async Task<IImageEmbeddingProvider> CreateImageEmbeddingAsync(
        string modelId,
        OnnxModelStore store,
        bool allowDownload,
        Action<string>? log,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        OnnxImageEmbeddingModel model = OnnxModelCatalog.GetImageEmbedding(modelId);
        IReadOnlyDictionary<string, string> files = await store
            .EnsureFilesAsync(model, allowDownload, log, cancellationToken, progress)
            .ConfigureAwait(false);

        return new OnnxImageEmbeddingProvider(model, files);
    }

    public static async Task<IQualityScorer> CreateQualityScorerAsync(
        string modelId,
        OnnxModelStore store,
        bool allowDownload,
        Action<string>? log,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        OnnxQualityModel model = OnnxModelCatalog.GetQualityModel(modelId);
        IReadOnlyDictionary<string, string> files = await store
            .EnsureFilesAsync(model, allowDownload, log, cancellationToken, progress)
            .ConfigureAwait(false);

        return new OnnxQualityScorer(model, files);
    }
}
