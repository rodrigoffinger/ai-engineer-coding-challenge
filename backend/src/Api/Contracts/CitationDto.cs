namespace Api.Contracts;

public sealed class CitationDto
{
    public string Source { get; init; } = string.Empty;

    public string Snippet { get; init; } = string.Empty;

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }
}