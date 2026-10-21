using ImageCompare;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageCompare.Onnx;

public sealed class OnnxQualityScorer : IQualityScorer
{
    private readonly InferenceSession _session;
    private readonly OnnxQualityModel _model;
    private readonly string _inputName;
    private readonly string _outputName;

    public OnnxQualityScorer(OnnxQualityModel model, IReadOnlyDictionary<string, string> files)
    {
        _model = model;
        string graphPath = files[model.Files[0].FileName];
        _session = new InferenceSession(graphPath, OnnxImageEmbeddingProvider.CreateSessionOptions());
        _inputName = _session.InputMetadata.Keys.First();
        _outputName = model.OutputName ?? _session.OutputMetadata.Keys.First();

        if (!_session.OutputMetadata.ContainsKey(_outputName))
        {
            throw new InvalidDataException(
                $"Model '{model.Id}' does not expose the output '{_outputName}'.");
        }
    }

    public string Id => _model.Id;

    public double Score(string path)
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

        int[] dimensions = output.Dimensions.ToArray();

        return dimensions.Length switch
        {
            1 => output[0],
            2 => output[0, 0],
            _ => output.ToArray()[0]
        };
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
