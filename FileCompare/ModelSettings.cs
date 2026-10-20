using System.Text.Json.Serialization;

namespace FileCompare;

/// <summary>
/// Where ONNX models are stored. The default is the application cache; a user can
/// point it at a pre-provisioned folder or another disk.
/// </summary>
public sealed record ModelSettings(
    [property: JsonPropertyName("modelsFolder")] string ModelsFolder);
