using Api.Models;

namespace Api.Services;

public interface IToolRegistryService
{
    IReadOnlyList<ToolDefinition> GetAvailableTools();
}