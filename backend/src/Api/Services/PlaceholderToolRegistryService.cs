using Api.Models;

namespace Api.Services;

public sealed class PlaceholderToolRegistryService : IToolRegistryService
{
    private static readonly IReadOnlyList<ToolDefinition> Tools =
    [
        new ToolDefinition
        {
            Name = "search_sop_chunks",
            Description = "Look up SOP chunks relevant to the user's current question."
        },
        new ToolDefinition
        {
            Name = "summarize_conversation_context",
            Description = "Condense prior turns before building the final prompt."
        }
    ];

    public IReadOnlyList<ToolDefinition> GetAvailableTools() => Tools;
}