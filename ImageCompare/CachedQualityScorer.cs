namespace ImageCompare;

/// <summary>
/// Adds a persistent cache in front of any <see cref="IQualityScorer"/>.
/// </summary>
public sealed class CachedQualityScorer : IQualityScorer
{
    private readonly IQualityScorer _inner;
    private readonly QualityCache _cache;

    public CachedQualityScorer(IQualityScorer inner, QualityCache cache)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);

        if (!string.Equals(inner.Id, cache.ScorerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The cache was created for '{cache.ScorerId}' but the scorer is '{inner.Id}'.",
                nameof(cache));
        }

        _inner = inner;
        _cache = cache;
    }

    public string Id => _inner.Id;

    public double Score(string path)
    {
        return _cache.GetOrCompute(path, _inner.Score);
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}
