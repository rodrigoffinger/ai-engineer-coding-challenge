using OpenAI.Chat;

namespace Api.Services;

public interface IToolRegistryService
{
    IReadOnlyList<ChatTool> GetTools();

    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}