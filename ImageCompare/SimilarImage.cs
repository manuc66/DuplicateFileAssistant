namespace ImageCompare;

public sealed record SimilarImage(string Path, long Length, PerceptualHash Hash);

public sealed record SimilarImageMatch(
    SimilarImage Image,
    int Distance,
    double? Similarity,
    double? Quality);

public enum SimilarImageKind
{
    Perceptual,
    Semantic
}

public sealed record SimilarImageGroup(
    IReadOnlyList<SimilarImageMatch> Images,
    int MaximumDistance,
    double? MinimumSimilarity,
    SimilarImageKind Kind,
    SimilarImage Leader,
    SimilarImage Keeper,
    double? KeeperQuality)
{
    public SimilarImage Representative => Leader;

    public int RedundantCount => Images.Count - 1;
}

public enum SimilarImageFailureStage
{
    Hash,
    Embedding,
    Quality
}

public sealed record SimilarImageFailure(string Path, string Error, SimilarImageFailureStage Stage);

public sealed record SimilarImageScanResult(
    IReadOnlyList<SimilarImageGroup> Groups,
    IReadOnlyList<SimilarImageFailure> Failures,
    int HashedCount,
    int EmbeddedCount,
    TimeSpan Elapsed)
{
    public int RedundantCount => Groups.Sum(group => group.RedundantCount);
}
