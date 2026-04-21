using OpenAI.Chat;

namespace Api.Services;

/// <summary>
/// Designated extension point for agent tool-calling.
/// Add new tools here: register the schema in GetTools() and handle execution in ExecuteAsync().
/// </summary>
public interface IToolRegistryService
{
    IReadOnlyList<ChatTool> GetTools();

    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}