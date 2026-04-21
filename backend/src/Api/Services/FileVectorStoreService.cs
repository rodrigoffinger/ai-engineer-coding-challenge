using System.Text.Json;
using Api.Models;

namespace Api.Services;

public sealed class FileVectorStoreService(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<FileVectorStoreService> logger) : IVectorStoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _artifactPath = ResolveArtifactPath(configuration, environment);
    private List<VectorRecord>? _cache;

    public async Task<IReadOnlyList<VectorRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            logger.LogDebug("Vector store cache hit — {Count} records", _cache.Count);
            return _cache;
        }

        if (!File.Exists(_artifactPath))
        {
            logger.LogInformation("Vector store file not found at {Path} — starting empty", _artifactPath);
            _cache = [];
            return _cache;
        }

        await using var stream = File.OpenRead(_artifactPath);
        _cache = await JsonSerializer.DeserializeAsync<List<VectorRecord>>(stream, JsonOptions, cancellationToken) ?? [];
        logger.LogInformation("Loaded {Count} records from {Path}", _cache.Count, _artifactPath);

        return _cache;
    }

    public async Task SaveAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        _cache = records.ToList();

        var directory = Path.GetDirectoryName(_artifactPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(_artifactPath);
        await JsonSerializer.SerializeAsync(stream, _cache, JsonOptions, cancellationToken);
        logger.LogInformation("Saved {Count} records to {Path}", _cache.Count, _artifactPath);
    }

    public async Task<IReadOnlyList<VectorSearchMatch>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadAsync(cancellationToken);

        if (records.Count == 0)
        {
            logger.LogWarning("SearchAsync called but vector store is empty — run ingestion first");
            return [];
        }

        var results = records
            .Select(r => new VectorSearchMatch
            {
                Record = r,
                Score = CosineSimilarity(queryEmbedding, r.Embedding)
            })
            .OrderByDescending(m => m.Score)
            .Take(topK)
            .ToList();

        return results;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }

    private static string ResolveArtifactPath(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Challenge:VectorStoreJsonPath"] ?? "Data/vector-store.json";

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }
}
