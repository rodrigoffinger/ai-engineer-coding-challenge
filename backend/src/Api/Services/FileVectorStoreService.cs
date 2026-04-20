using Api.Models;

namespace Api.Services;

public sealed class FileVectorStoreService(IConfiguration configuration, IWebHostEnvironment environment) : IVectorStoreService
{
    private readonly string _artifactPath = ResolveArtifactPath(configuration, environment);

    public Task<IReadOnlyList<VectorRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Scaffold-only placeholder: candidates can replace this with real persistence.
        return Task.FromResult<IReadOnlyList<VectorRecord>>([]);
    }

    public Task SaveAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        // The scaffold keeps the planned artifact path available without persisting data.
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchMatch>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<VectorSearchMatch>>([]);
    }

    private static string ResolveArtifactPath(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Challenge:VectorStoreJsonPath"] ?? "Data/vector-store.json";

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }
}