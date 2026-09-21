using EntityScreening.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityScreening.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SourcesController : ControllerBase
{
    private readonly IScraperResolver _resolver;

    public SourcesController(IScraperResolver resolver)
    {
        _resolver = resolver;
    }

    [HttpGet]
    public IActionResult GetSources()
    {
        return Ok(new { sources = _resolver.AvailableSources });
    }
}
