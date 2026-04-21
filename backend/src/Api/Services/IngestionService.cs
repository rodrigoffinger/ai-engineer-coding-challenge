using Api.Models;

namespace Api.Services;

public sealed class IngestionService(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<IngestionService> logger) : IIngestionService
{
    private string VectorStorePath =>
        configuration["Challenge:VectorStoreJsonPath"] ?? "Data/vector-store.json";

    public async Task<IngestionResult> IngestAsync(
        string? sourcePath,
        bool forceReingest,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolvePath(sourcePath);

        logger.LogInformation("Ingest requested — source: {SourcePath}, forceReingest: {Force}",
            resolvedPath, forceReingest);

        if (!File.Exists(resolvedPath))
        {
            logger.LogError("Source file not found: {SourcePath}", resolvedPath);
            throw new FileNotFoundException($"Source file not found: {resolvedPath}", resolvedPath);
        }

        if (!forceReingest)
        {
            var existing = await vectorStoreService.LoadAsync(cancellationToken);
            if (existing.Count > 0)
            {
                logger.LogInformation("Skipping ingest — vector store already has {Count} records", existing.Count);
                return new IngestionResult
                {
                    ResolvedSourcePath = resolvedPath,
                    VectorStorePath = VectorStorePath,
                    ChunksCreated = existing.Count,
                    RecordsPersisted = existing.Count,
                    Skipped = true
                };
            }
        }

        var sourceText = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
        var sourceName = Path.GetFileName(resolvedPath);

        logger.LogInformation("Chunking document: {SourceName} ({Bytes} bytes)", sourceName, sourceText.Length);
        var chunks = await chunkingService.ChunkAsync(sourceText, sourceName, cancellationToken);
        logger.LogInformation("Chunking complete — {ChunkCount} chunks produced", chunks.Count);

        logger.LogInformation("Requesting embeddings for {ChunkCount} chunks (model: text-embedding-3-small)", chunks.Count);
        var embeddings = await embeddingService.EmbedBatchAsync(chunks.Select(c => c.Content), cancellationToken);
        logger.LogInformation("Embeddings received — {EmbeddingCount} vectors", embeddings.Count);

        var records = chunks.Zip(embeddings, (chunk, embedding) => new VectorRecord
        {
            Source = chunk.Source,
            ChunkText = chunk.Content,
            Embedding = embedding,
            Metadata = new Dictionary<string, string>
            {
                ["section"] = chunk.SectionTitle,
                ["index"] = chunk.Index.ToString()
            }
        }).ToList();

        await vectorStoreService.SaveAsync(records, cancellationToken);
        logger.LogInformation("Ingest complete — {RecordCount} records persisted", records.Count);

        return new IngestionResult
        {
            ResolvedSourcePath = resolvedPath,
            VectorStorePath = VectorStorePath,
            ChunksCreated = chunks.Count,
            RecordsPersisted = records.Count,
            Skipped = false
        };
    }

    private string ResolvePath(string? sourcePath)
    {
        var configured = configuration["Challenge:SourceDocumentPath"]
            ?? "../../../knowledge-base/Grocery_Store_SOP.md";

        var path = string.IsNullOrWhiteSpace(sourcePath) ? configured : sourcePath;

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));
    }
}
