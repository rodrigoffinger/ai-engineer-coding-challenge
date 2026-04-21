using Api.Models;

namespace Api.Services;

public sealed class MarkdownChunkingService(ILogger<MarkdownChunkingService> logger) : IChunkingService
{
    private const int MinBodyChars = 20;

    public Task<IReadOnlyList<TextChunk>> ChunkAsync(
        string sourceText,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<TextChunk>();
        var lines = sourceText.Split('\n');
        int skipped = 0;

        string? currentTitle = null;
        var bodyLines = new List<string>();
        int chunkIndex = 0;

        void FlushChunk()
        {
            if (currentTitle is null) return;

            var body = string.Join('\n', bodyLines).Trim();
            if (body.Length < MinBodyChars)
            {
                skipped++;
                logger.LogDebug("Skipping thin chunk [{Title}] ({Chars} chars)", currentTitle, body.Length);
                return;
            }

            chunks.Add(new TextChunk
            {
                Source = sourceName,
                Index = chunkIndex++,
                SectionTitle = currentTitle,
                Content = $"{currentTitle}\n\n{body}"
            });
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("### ") || line.StartsWith("## "))
            {
                FlushChunk();
                currentTitle = line.TrimStart('#').Trim();
                bodyLines.Clear();
            }
            else
            {
                bodyLines.Add(line);
            }
        }

        FlushChunk();

        logger.LogInformation("Chunking complete — {ChunkCount} chunks kept, {Skipped} skipped (body < {Min} chars)",
            chunks.Count, skipped, MinBodyChars);

        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks);
    }
}
