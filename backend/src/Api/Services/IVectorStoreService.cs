using Api.Models;

namespace Api.Services;

public interface IVectorStoreService
{
    Task<IReadOnlyList<VectorRecord>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchMatch>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default);
}