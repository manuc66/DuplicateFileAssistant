namespace ImageCompare.Onnx;

/// <summary>
/// Central registry of the ONNX models shipped with the app. Adding a model is
/// a data change: provide its files, licence, preprocessing and output metadata.
/// The core finder only sees <see cref="ImageCompare.IImageEmbeddingProvider"/>
/// and <see cref="ImageCompare.IQualityScorer"/>.
/// </summary>
public static class OnnxModelCatalog
{
    private static readonly float[] ImageNetMean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] ImageNetStandardDeviation = { 0.229f, 0.224f, 0.225f };

    public static IReadOnlyDictionary<string, OnnxImageEmbeddingModel> ImageEmbeddings { get; } =
        new Dictionary<string, OnnxImageEmbeddingModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["dinov2-small"] = new OnnxImageEmbeddingModel(
                Id: "dinov2-small",
                DisplayName: "DINOv2 small",
                License: "Apache-2.0",
                OutputName: "last_hidden_state",
                InputSize: 224,
                ResizeMode: ImageResizeMode.ShortestSideCenterCrop,
                Mean: ImageNetMean,
                StandardDeviation: ImageNetStandardDeviation,
                Pooling: EmbeddingPooling.ClsToken,
                Dimensions: 384,
                Files: new[]
                {
                    new OnnxModelFile(
                        "model.onnx",
                        "https://huggingface.co/onnx-community/dinov2-small-ONNX/resolve/main/onnx/model.onnx",
                        "6266c3cd72db6953cecdcbfeab9422a9f783d96f1a4e296ba70ffbac43b54a18",
                        88_513_997)
                })
        };

    public static IReadOnlyDictionary<string, OnnxQualityModel> QualityModels { get; } =
        new Dictionary<string, OnnxQualityModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["dbcnn"] = new OnnxQualityModel(
                Id: "dbcnn",
                DisplayName: "DBCNN",
                License: "Apache-2.0",
                OutputName: "score",
                InputSize: 224,
                Mean: ImageNetMean,
                StandardDeviation: ImageNetStandardDeviation,
                Files: new[]
                {
                    new OnnxModelFile(
                        "dbcnn_model.onnx",
                        "https://huggingface.co/86Cao/IQA-ONNX-Models/resolve/main/dbcnn_model.onnx",
                        "2a679a6264bfc188e623e9a937ad214dd761f7deb9469442a37107560557bc3f",
                        102_172),
                    new OnnxModelFile(
                        "dbcnn_model.onnx.data",
                        "https://huggingface.co/86Cao/IQA-ONNX-Models/resolve/main/dbcnn_model.onnx.data",
                        "ed3e6df9109aa30323bcd2006a098023aa57fbcb98bfe1d2767227fc71c5382f",
                        61_276_160)
                })
        };

    public static OnnxImageEmbeddingModel GetImageEmbedding(string id)
    {
        return ImageEmbeddings.TryGetValue(id, out OnnxImageEmbeddingModel? model)
            ? model
            : throw new ArgumentException(
                $"Unknown embedding model '{id}'. Available: {string.Join(", ", ImageEmbeddings.Keys)}.",
                nameof(id));
    }

    public static OnnxQualityModel GetQualityModel(string id)
    {
        return QualityModels.TryGetValue(id, out OnnxQualityModel? model)
            ? model
            : throw new ArgumentException(
                $"Unknown quality model '{id}'. Available: {string.Join(", ", QualityModels.Keys)}.",
                nameof(id));
    }
}
