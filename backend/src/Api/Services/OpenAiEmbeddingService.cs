using OpenAI.Embeddings;

namespace Api.Services;

public sealed class OpenAiEmbeddingService(IConfiguration configuration, TokenUsageTracker tokenTracker, ILogger<OpenAiEmbeddingService> logger) : IEmbeddingService
{
    private readonly EmbeddingClient _client = CreateClient(configuration);
    private readonly string _model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Embedding single text ({Chars} chars)", text.Length);
        var result = await _client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        // Single-embedding result does not expose Usage in SDK v2; token cost is negligible (<50 tokens per query)
        return result.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        logger.LogInformation("Requesting batch embeddings — {Count} texts, model: {Model}", textList.Count, _model);

        try
        {
            var result = await _client.GenerateEmbeddingsAsync(textList, cancellationToken: cancellationToken);
            var vectors = result.Value.Select(e => e.ToFloats().ToArray()).ToList();
            logger.LogInformation("Batch embeddings received — {Count} vectors, dims: {Dims}",
                vectors.Count, vectors.FirstOrDefault()?.Length ?? 0);
            tokenTracker.TrackEmbedding(result.Value.Usage.InputTokenCount);
            return vectors;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch embedding request failed for {Count} texts", textList.Count);
            throw;
        }
    }

    private static EmbeddingClient CreateClient(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
        var model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        return new EmbeddingClient(model, apiKey);
    }
}
