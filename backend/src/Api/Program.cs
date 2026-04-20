using Api.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<IChunkingService, PlaceholderChunkingService>();
builder.Services.AddSingleton<IEmbeddingService, PlaceholderEmbeddingService>();
builder.Services.AddSingleton<IVectorStoreService, FileVectorStoreService>();
builder.Services.AddSingleton<IToolRegistryService, PlaceholderToolRegistryService>();
builder.Services.AddSingleton<IRetrievalChatService, PlaceholderRetrievalChatService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("LocalFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
