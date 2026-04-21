using System.Text.Json;
using OpenAI.Chat;

namespace Api.Services;

public sealed class ToolRegistryService(
    IVectorStoreService vectorStoreService,
    IEmbeddingService embeddingService,
    ILogger<ToolRegistryService> logger) : IToolRegistryService
{
    private static readonly IReadOnlyList<ChatTool> Tools =
    [
        ChatTool.CreateFunctionTool(
            "search_sop",
            "Search the grocery store SOP document for policy or procedure details. Returns the most relevant text chunks and their section sources.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "The search query describing what information you are looking for."
                },
                "top_k": {
                  "type": "integer",
                  "description": "Number of results to return. Defaults to 3.",
                  "default": 3
                }
              },
              "required": ["query"]
            }
            """)),

        ChatTool.CreateFunctionTool(
            "get_store_hours",
            "Returns the store's operating hours for each day of the week. Use this when asked about opening times, closing times, or store schedule.",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {}
            }
            """))
    ];

    public IReadOnlyList<ChatTool> GetTools() => Tools;

    public async Task<string> ExecuteAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Executing tool: {ToolName}", toolName);

        return toolName switch
        {
            "search_sop" => await ExecuteSearchSopAsync(argumentsJson, cancellationToken),
            "get_store_hours" => ExecuteGetStoreHours(),
            _ => LogAndReturnUnknown(toolName)
        };
    }

    private async Task<string> ExecuteSearchSopAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        var query = doc.RootElement.GetProperty("query").GetString() ?? string.Empty;
        var topK = doc.RootElement.TryGetProperty("top_k", out var topKProp) ? topKProp.GetInt32() : 3;

        logger.LogInformation("search_sop — query: \"{Query}\", topK: {TopK}", query, topK);

        var queryEmbedding = await embeddingService.EmbedAsync(query, cancellationToken);
        var matches = await vectorStoreService.SearchAsync(queryEmbedding, topK, cancellationToken);

        logger.LogInformation("search_sop — returning {Count} matches", matches.Count);

        var results = matches.Select(m => new
        {
            section = m.Record.Metadata.GetValueOrDefault("section", m.Record.Source),
            snippet = m.Record.ChunkText.Length > 500
                ? m.Record.ChunkText[..500] + "..."
                : m.Record.ChunkText,
            score = Math.Round(m.Score, 4)
        });

        return JsonSerializer.Serialize(results);
    }

    private string ExecuteGetStoreHours()
    {
        // Data is hardcoded intentionally: in production this would come from a database or
        // config service, not from the SOP document. The point of this tool is to demonstrate
        // a different tool pattern from search_sop — returning structured data from a typed
        // source rather than unstructured text via vector retrieval. Hardcoding stands in for
        // that external datasource; swap the body here when a real source is available.
        logger.LogInformation("get_store_hours — returning hardcoded schedule from SOP §2.1");

        var hours = new[]
        {
            new { day = "Monday – Friday", open = "6:00 AM", close = "11:00 PM" },
            new { day = "Saturday",        open = "7:00 AM", close = "11:00 PM" },
            new { day = "Sunday",          open = "7:00 AM", close = "10:00 PM" },
            new { day = "Major Holidays",  open = "As posted", close = "As posted" }
        };

        return JsonSerializer.Serialize(hours);
    }

    private string LogAndReturnUnknown(string toolName)
    {
        logger.LogWarning("Unknown tool requested: {ToolName}", toolName);
        return $"Unknown tool: {toolName}";
    }
}
