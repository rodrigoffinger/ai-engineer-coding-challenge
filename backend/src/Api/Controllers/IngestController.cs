using Api.Contracts;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IngestController(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<IngestController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Post(
        [FromBody] IngestRequest? request,
        CancellationToken cancellationToken)
    {
        var configuredSourcePath = configuration["Challenge:SourceDocumentPath"]
            ?? "../../../../knowledge-base/Grocery_Store_SOP.md";
        var vectorStorePath = configuration["Challenge:VectorStoreJsonPath"] ?? "Data/vector-store.json";

        var sourcePath = string.IsNullOrWhiteSpace(request?.SourcePath)
            ? configuredSourcePath
            : request.SourcePath;

        if (!Path.IsPathRooted(sourcePath))
            sourcePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, sourcePath));

        logger.LogInformation("Ingest requested — source: {SourcePath}, forceReingest: {Force}",
            sourcePath, request?.ForceReingest ?? false);

        if (!System.IO.File.Exists(sourcePath))
        {
            logger.LogError("Source file not found: {SourcePath}", sourcePath);
            return BadRequest(new { error = $"Source file not found: {sourcePath}" });
        }

        if (!(request?.ForceReingest ?? false))
        {
            var existing = await vectorStoreService.LoadAsync(cancellationToken);
            if (existing.Count > 0)
            {
                logger.LogInformation("Skipping ingest — vector store already has {Count} records", existing.Count);
                return Ok(new IngestResponse
                {
                    Accepted = true,
                    Message = $"Vector store already contains {existing.Count} records. Use forceReingest=true to re-embed.",
                    SourcePath = sourcePath,
                    ChunksCreated = existing.Count,
                    RecordsPersisted = existing.Count,
                    VectorStorePath = vectorStorePath,
                    IsPlaceholder = false
                });
            }
        }

        var sourceText = await System.IO.File.ReadAllTextAsync(sourcePath, cancellationToken);
        var sourceName = Path.GetFileName(sourcePath);

        logger.LogInformation("Chunking document: {SourceName} ({Bytes} bytes)", sourceName, sourceText.Length);
        var chunks = await chunkingService.ChunkAsync(sourceText, sourceName, cancellationToken);
        logger.LogInformation("Chunking complete — {ChunkCount} chunks produced", chunks.Count);

        logger.LogInformation("Requesting embeddings for {ChunkCount} chunks (model: text-embedding-3-small)", chunks.Count);
        var embeddings = await embeddingService.EmbedBatchAsync(
            chunks.Select(c => c.Content),
            cancellationToken);
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
        logger.LogInformation("Ingest complete — {RecordCount} records persisted to {VectorStorePath}",
            records.Count, vectorStorePath);

        return Ok(new IngestResponse
        {
            Accepted = true,
            Message = $"Ingestion complete. {chunks.Count} chunks embedded and persisted.",
            SourcePath = sourcePath,
            ChunksCreated = chunks.Count,
            RecordsPersisted = records.Count,
            VectorStorePath = vectorStorePath,
            IsPlaceholder = false
        });
    }
}
