namespace Api.Models;

public sealed class VectorRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Source { get; init; } = string.Empty;

    public string ChunkText { get; init; } = string.Empty;

    public float[] Embedding { get; init; } = [];

    public Dictionary<string, string> Metadata { get; init; } = [];
}