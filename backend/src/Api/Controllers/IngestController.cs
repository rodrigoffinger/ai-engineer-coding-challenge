using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IngestController(IIngestionService ingestionService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Post(
        [FromBody] IngestRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ingestionService.IngestAsync(
                request?.SourcePath,
                request?.ForceReingest ?? false,
                cancellationToken);

            return Ok(new IngestResponse
            {
                Accepted = true,
                Message = result.Skipped
                    ? $"Vector store already contains {result.RecordsPersisted} records. Use forceReingest=true to re-embed."
                    : $"Ingestion complete. {result.ChunksCreated} chunks embedded and persisted.",
                SourcePath = result.ResolvedSourcePath,
                ChunksCreated = result.ChunksCreated,
                RecordsPersisted = result.RecordsPersisted,
                VectorStorePath = result.VectorStorePath,
                IsPlaceholder = false
            });
        }
        catch (FileNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
