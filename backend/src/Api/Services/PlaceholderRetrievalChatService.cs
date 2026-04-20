using Api.Contracts;

namespace Api.Services;

public sealed class PlaceholderRetrievalChatService : IRetrievalChatService
{
    public Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var latestUserMessage = request.Messages.LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase));

        var response = new ChatResponse
        {
            ConversationId = request.ConversationId,
            Status = "not-implemented",
            IsPlaceholder = true,
            AssistantMessage = latestUserMessage is null
                ? "Chat is not implemented in this scaffold yet. Add retrieval, grounding, citations, model invocation, and optional tool use."
                : $"Chat is not implemented in this scaffold yet. The latest user message was: '{latestUserMessage.Content}'. Add retrieval, grounding, citations, model invocation, and optional tool use.",
            ToolCalls = [],
            Citations = []
        };

        return Task.FromResult(response);
    }
}