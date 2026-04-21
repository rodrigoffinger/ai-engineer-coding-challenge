using System.Net.Sockets;
using Api.Services;
using Serilog;
using Serilog.Sinks.Udp.TextFormatters;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext:l} — {Message:lj}{NewLine}{Exception}")
    .WriteTo.Udp("127.0.0.1", 9998, AddressFamily.InterNetwork, new Log4jTextFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting Grocery Store SOP Assistant API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173"];

    builder.Services.AddControllers();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LocalFrontend", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    builder.Services.AddSingleton<TokenUsageTracker>();
    builder.Services.AddSingleton<IIngestionService, IngestionService>();
    builder.Services.AddSingleton<IChunkingService, MarkdownChunkingService>();
    builder.Services.AddSingleton<IEmbeddingService, OpenAiEmbeddingService>();
    builder.Services.AddSingleton<IVectorStoreService, FileVectorStoreService>();
    builder.Services.AddSingleton<IToolRegistryService, ToolRegistryService>();
    builder.Services.AddSingleton<IRetrievalChatService, RetrievalChatService>();

    var app = builder.Build();

    app.UseHttpsRedirection();
    app.UseCors("LocalFrontend");
    app.UseAuthorization();
    app.MapControllers();

    app.Run();

    app.Services.GetRequiredService<TokenUsageTracker>().LogSessionSummary();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
