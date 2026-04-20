namespace Api.Contracts;

public sealed class ChatRequest
{
    public string ConversationId { get; init; } = Guid.NewGuid().ToString("N");

    public List<ChatMessageDto> Messages { get; init; } = [];

    public bool UseTools { get; init; } = true;
}