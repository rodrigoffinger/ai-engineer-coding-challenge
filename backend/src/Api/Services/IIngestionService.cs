namespace Api.Services;

public interface IIngestionService
{
    Task<IngestionResult> IngestAsync(string? sourcePath, bool forceReingest, CancellationToken cancellationToken = default);
}

public sealed class IngestionResult
{
    public int ChunksCreated { get; init; }
    public int RecordsPersisted { get; init; }
    public string ResolvedSourcePath { get; init; } = string.Empty;
    public string VectorStorePath { get; init; } = string.Empty;
    public bool Skipped { get; init; }
}
