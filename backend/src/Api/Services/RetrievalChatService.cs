using System.Text;
using Api.Contracts;
using OpenAI.Chat;

namespace Api.Services;

public sealed class RetrievalChatService(
    IConfiguration configuration,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IToolRegistryService toolRegistryService,
    TokenUsageTracker tokenTracker,
    ILogger<RetrievalChatService> logger) : IRetrievalChatService
{
    private const int TopKChunks = 3;

    public async Task<ChatResponse> GenerateResponseAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
        var model = configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini";

        logger.LogInformation("Chat request — conversationId: {ConversationId}, messages: {MessageCount}, useTools: {UseTools}",
            request.ConversationId, request.Messages.Count, request.UseTools);

        var chatClient = new ChatClient(model, apiKey);

        var lastUserMessage = request.Messages
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

        var retrievedMatches = Array.Empty<Models.VectorSearchMatch>();
        if (lastUserMessage is not null)
        {
            logger.LogInformation("Embedding query: \"{Query}\"", Truncate(lastUserMessage.Content, 80));
            var queryEmbedding = await embeddingService.EmbedAsync(lastUserMessage.Content, cancellationToken);
            retrievedMatches = (await vectorStoreService.SearchAsync(queryEmbedding, TopKChunks, cancellationToken))
                .ToArray();

            logger.LogInformation("Retrieved {Count} chunks — top score: {TopScore:F4}",
                retrievedMatches.Length,
                retrievedMatches.FirstOrDefault()?.Score ?? 0);

            foreach (var match in retrievedMatches)
            {
                var section = match.Record.Metadata.GetValueOrDefault("section", match.Record.Source);
                logger.LogDebug("  chunk [{Section}] score={Score:F4}", section, match.Score);
            }
        }

        var systemPrompt = BuildSystemPrompt(retrievedMatches);

        var messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };
        foreach (var dto in request.Messages)
        {
            messages.Add(dto.Role.ToLowerInvariant() switch
            {
                "assistant" => ChatMessage.CreateAssistantMessage(dto.Content),
                _ => ChatMessage.CreateUserMessage(dto.Content)
            });
        }

        var tools = request.UseTools ? toolRegistryService.GetTools() : null;
        var options = tools is { Count: > 0 } ? new ChatCompletionOptions() : null;
        if (options is not null && tools is not null)
            foreach (var tool in tools)
                options.Tools.Add(tool);

        logger.LogInformation("Calling {Model} — {MessageCount} messages in context", model, messages.Count);
        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        tokenTracker.TrackChat(completion.Value.Usage.InputTokenCount, completion.Value.Usage.OutputTokenCount);

        var invokedTools = new List<string>();

        // ── Agent orchestration ───────────────────────────────────────────────
        // Single-round tool-use: detect tool calls → execute → re-invoke model.
        // To extend to a multi-step loop, we could replace this block with a while loop
        // conditioned on FinishReason == ToolCalls. To add new tools, register
        // them in IToolRegistryService / ToolRegistryService.
        if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            logger.LogInformation("Model requested {ToolCallCount} tool call(s)", completion.Value.ToolCalls.Count);
            messages.Add(new AssistantChatMessage(completion.Value));

            foreach (var toolCall in completion.Value.ToolCalls)
            {
                logger.LogInformation("Executing tool: {ToolName} args={Args}",
                    toolCall.FunctionName, Truncate(toolCall.FunctionArguments.ToString(), 120));

                invokedTools.Add(toolCall.FunctionName);

                try
                {
                    var toolResult = await toolRegistryService.ExecuteAsync(
                        toolCall.FunctionName,
                        toolCall.FunctionArguments.ToString(),
                        cancellationToken);

                    logger.LogInformation("Tool {ToolName} returned {Bytes} bytes", toolCall.FunctionName, toolResult.Length);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolResult));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Tool execution failed: {ToolName}", toolCall.FunctionName);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, $"Error: {ex.Message}"));
                }
            }

            logger.LogInformation("Re-calling {Model} with tool results", model);
            completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            tokenTracker.TrackChat(completion.Value.Usage.InputTokenCount, completion.Value.Usage.OutputTokenCount);
        }
        // ─────────────────────────────────────────────────────────────────────

        var responseText = completion.Value.Content[0].Text;
        logger.LogInformation("Response ready — {Chars} chars, tools used: [{Tools}]",
            responseText.Length, string.Join(", ", invokedTools));

        var citations = retrievedMatches.Select(m => new CitationDto
        {
            Source = m.Record.Metadata.GetValueOrDefault("section", m.Record.Source),
            Snippet = m.Record.ChunkText.Length > 200
                ? m.Record.ChunkText[..200] + "..."
                : m.Record.ChunkText
        }).ToList();

        return new ChatResponse
        {
            ConversationId = request.ConversationId,
            AssistantMessage = responseText,
            Status = "ok",
            IsPlaceholder = false,
            ToolCalls = invokedTools,
            Citations = citations
        };
    }

    private static string BuildSystemPrompt(IReadOnlyList<Models.VectorSearchMatch> matches)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful assistant for grocery store employees.");
        sb.AppendLine("Answer questions based solely on the provided SOP context below.");
        sb.AppendLine("If the answer is not in the context, say so clearly.");
        sb.AppendLine("Be concise and specific. When referencing procedures, mention the relevant section name.");
        sb.AppendLine();

        if (matches.Count > 0)
        {
            sb.AppendLine("--- SOP CONTEXT ---");
            foreach (var match in matches)
            {
                var section = match.Record.Metadata.GetValueOrDefault("section", match.Record.Source);
                sb.AppendLine($"[{section}]");
                sb.AppendLine(match.Record.ChunkText);
                sb.AppendLine();
            }
            sb.AppendLine("--- END CONTEXT ---");
        }
        else
        {
            sb.AppendLine("No SOP context was retrieved. The vector store may be empty — ask the user to run ingestion first.");
        }

        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
