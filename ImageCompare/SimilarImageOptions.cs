namespace ImageCompare;

public sealed class SimilarImageOptions
{
    public int MaxDistance { get; init; } = SimilarImageFinder.DefaultMaxDistance;

    /// <summary>
    /// Optional semantic pass. When set, embeddings are computed for every image
    /// and images whose cosine similarity exceeds <see cref="SemanticThreshold"/>
    /// are grouped even if their dHash differs.
    /// </summary>
    public IImageEmbeddingProvider? Embeddings { get; init; }

    public double SemanticThreshold { get; init; } = 0.80;

    /// <summary>
    /// Minimum gap between the best and second-best candidate required for a
    /// borderline semantic match. Obvious matches (above
    /// <see cref="SemanticStrongMatch"/>) bypass the margin.
    /// </summary>
    public double SemanticMargin { get; init; } = 0.05;

    /// <summary>
    /// Similarity above which a candidate is accepted without the margin test.
    /// Needed so that three or more copies of the same picture can all join the
    /// same group.
    /// </summary>
    public double SemanticStrongMatch { get; init; } = 0.94;

    /// <summary>
    /// Hamming radius of the 128-bit SimHash index used to shortlist semantic
    /// candidates. Larger values improve recall at the cost of verification work.
    /// </summary>
    public int SimHashMaxDistance { get; init; } = 30;

    /// <summary>
    /// Optional quality scorer. Only evaluated on images that belong to a group
    /// of at least two pictures, then used to choose the suggested keeper.
    /// </summary>
    public IQualityScorer? Quality { get; init; }

    /// <summary>
    /// Below this number of images, semantic candidates are compared exhaustively
    /// instead of through the SimHash index.
    /// </summary>
    public int BruteForceEmbeddingLimit { get; init; } = 5000;
}
