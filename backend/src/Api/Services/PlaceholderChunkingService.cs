using Api.Models;

namespace Api.Services;

public sealed class PlaceholderChunkingService : IChunkingService
{
    public Task<IReadOnlyList<TextChunk>> ChunkAsync(string sourceText, string sourceName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("TODO: implement SOP chunking for ingestion.");
    }
}