namespace Api.Services;

public sealed class PlaceholderEmbeddingService : IEmbeddingService
{
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("TODO: implement embedding generation.");
    }
}