namespace Api.Models;

public sealed class VectorSearchMatch
{
    public VectorRecord Record { get; init; } = new();

    public double Score { get; init; }
}