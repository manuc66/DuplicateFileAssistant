namespace ImageCompare.Onnx;

public enum ImageResizeMode
{
    Stretch,
    ShortestSideCenterCrop
}

public enum EmbeddingPooling
{
    ClsToken,
    MeanTokens
}

public sealed record OnnxModelFile(string FileName, string Url, string Sha256, long Size);

public sealed record OnnxImageEmbeddingModel(
    string Id,
    string DisplayName,
    string License,
    string? OutputName,
    int InputSize,
    ImageResizeMode ResizeMode,
    float[] Mean,
    float[] StandardDeviation,
    EmbeddingPooling Pooling,
    int Dimensions,
    IReadOnlyList<OnnxModelFile> Files);

public sealed record OnnxQualityModel(
    string Id,
    string DisplayName,
    string License,
    string? OutputName,
    int InputSize,
    float[] Mean,
    float[] StandardDeviation,
    IReadOnlyList<OnnxModelFile> Files);
