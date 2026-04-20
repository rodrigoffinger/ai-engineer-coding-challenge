namespace Api.Contracts;

public sealed class ChatMessageDto
{
    public string Role { get; init; } = "user";

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}