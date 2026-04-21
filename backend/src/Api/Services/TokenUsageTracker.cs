namespace Api.Services;

public sealed class TokenUsageTracker(ILogger<TokenUsageTracker> logger)
{
    private long _chatInput;
    private long _chatOutput;
    private long _embeddingTokens;

    public void TrackChat(int inputTokens, int outputTokens)
    {
        var totalInput  = Interlocked.Add(ref _chatInput,  inputTokens);
        var totalOutput = Interlocked.Add(ref _chatOutput, outputTokens);

        logger.LogInformation(
            "Tokens — request: {Input} in + {Output} out = {Total} | session chat total: {SessionInput} in + {SessionOutput} out = {SessionTotal}",
            inputTokens, outputTokens, inputTokens + outputTokens,
            totalInput, totalOutput, totalInput + totalOutput);
    }

    public void TrackEmbedding(int tokens)
    {
        var sessionTotal = Interlocked.Add(ref _embeddingTokens, tokens);

        logger.LogInformation(
            "Embedding tokens — request: {Tokens} | session embedding total: {SessionTotal}",
            tokens, sessionTotal);
    }

    public void LogSessionSummary()
    {
        var chatIn   = Interlocked.Read(ref _chatInput);
        var chatOut  = Interlocked.Read(ref _chatOutput);
        var embeddings = Interlocked.Read(ref _embeddingTokens);

        logger.LogInformation(
            "Session token summary — chat: {ChatIn} in + {ChatOut} out = {ChatTotal} | embeddings: {Embed} | grand total: {Grand}",
            chatIn, chatOut, chatIn + chatOut, embeddings, chatIn + chatOut + embeddings);
    }
}
