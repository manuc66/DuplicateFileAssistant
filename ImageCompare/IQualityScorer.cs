namespace ImageCompare;

/// <summary>
/// Scores the technical quality of an image. Higher is better. Used to suggest
/// which copy of a duplicate group should be kept.
/// </summary>
public interface IQualityScorer : IDisposable
{
    string Id { get; }

    double Score(string path);
}
