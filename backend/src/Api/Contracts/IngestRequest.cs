namespace Api.Contracts;

public sealed class IngestRequest
{
    public string SourcePath { get; init; } = string.Empty;

    public bool ForceReingest { get; init; }
}