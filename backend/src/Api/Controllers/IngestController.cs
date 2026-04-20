using Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IngestController(IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public ActionResult<IngestResponse> Post([FromBody] IngestRequest? request)
    {
        var configuredSourcePath = configuration["Challenge:SourceDocumentPath"] ?? "../../../../knowledge-base/Grocery_Store_SOP.md";
        var vectorStorePath = configuration["Challenge:VectorStoreJsonPath"] ?? "Data/vector-store.json";

        return Ok(new IngestResponse
        {
            Accepted = true,
            Message = "TODO: implement SOP loading, chunking, embedding, and optional vector-store persistence. The scaffold currently reports the configured paths only.",
            SourcePath = string.IsNullOrWhiteSpace(request?.SourcePath) ? configuredSourcePath : request!.SourcePath,
            ChunksCreated = 0,
            RecordsPersisted = 0,
            VectorStorePath = vectorStorePath,
            IsPlaceholder = true
        });
    }
}