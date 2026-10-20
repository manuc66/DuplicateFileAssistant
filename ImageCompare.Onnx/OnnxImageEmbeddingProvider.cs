using ImageCompare;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageCompare.Onnx;

public sealed class OnnxImageEmbeddingProvider : IImageEmbeddingProvider
{
    private readonly InferenceSession _session;
    private readonly OnnxImageEmbeddingModel _model;
    private readonly string _inputName;
    private readonly string _outputName;

    public OnnxImageEmbeddingProvider(OnnxImageEmbeddingModel model,
        IReadOnlyDictionary<string, string> files)
    {
        _model = model;
        string graphPath = files[model.Files[0].FileName];
        _session = new InferenceSession(graphPath, CreateSessionOptions());
        _inputName = _session.InputMetadata.Keys.First();
        _outputName = model.OutputName ?? _session.OutputMetadata.Keys.First();

        if (!_session.OutputMetadata.ContainsKey(_outputName))
        {
            throw new InvalidDataException(
                $"Model '{model.Id}' does not expose the output '{_outputName}'.");
        }
    }

    public string Id => _model.Id;

    public int Dimensions => _model.Dimensions;

    public float[] Embed(string path)
    {
        float[] tensor = OnnxImagePreprocessor.Load(path, _model);

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(
            new[]
            {
                NamedOnnxValue.CreateFromTensor(
                    _inputName,
                    new DenseTensor<float>(tensor, new[] { 1, 3, _model.InputSize, _model.InputSize }))
            });

        Tensor<float> output = results
            .First(result => result.Name == _outputName)
            .AsTensor<float>();

        float[] vector = Pool(output);
        Normalize(vector);

        if (vector.Length != Dimensions)
        {
            throw new InvalidDataException(
                $"Model '{Id}' returned {vector.Length} values, expected {Dimensions}.");
        }

        return vector;
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private float[] Pool(Tensor<float> output)
    {
        int[] dimensions = output.Dimensions.ToArray();

        if (dimensions.Length <= 2)
        {
            return output.ToArray();
        }

        int tokens = dimensions[^2];
        int width = dimensions[^1];

        if (_model.Pooling == EmbeddingPooling.ClsToken || tokens == 1)
        {
            float[] cls = new float[width];

            for (int index = 0; index < width; index++)
            {
                cls[index] = output[0, 0, index];
            }

            return cls;
        }

        float[] mean = new float[width];
        int tokenCount = 0;

        for (int token = 1; token < tokens; token++)
        {
            for (int index = 0; index < width; index++)
            {
                mean[index] += output[0, token, index];
            }

            tokenCount++;
        }

        for (int index = 0; index < width; index++)
        {
            mean[index] /= Math.Max(1, tokenCount);
        }

        return mean;
    }

    private static void Normalize(Span<float> vector)
    {
        double sum = 0;

        foreach (float value in vector)
        {
            sum += (double)value * value;
        }

        if (sum <= 0)
        {
            return;
        }

        float scale = (float)(1.0 / Math.Sqrt(sum));

        for (int index = 0; index < vector.Length; index++)
        {
            vector[index] *= scale;
        }
    }

    internal static SessionOptions CreateSessionOptions()
    {
        return new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            // The finder already runs one image per worker; keep each session
            // single-threaded to avoid oversubscribing the CPU.
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1
        };
    }
}
