using EntityScreening.Api.Models;
using EntityScreening.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EntityScreening.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private const int MinQueryLength = 2;
    private const int MaxQueryLength = 200;

    private readonly IScraperResolver _resolver;
    private readonly ILogger<SearchController> _logger;

    public SearchController(IScraperResolver resolver, ILogger<SearchController> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Search(
        [FromQuery] string? name,
        [FromQuery] string source = "ofac",
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("El parámetro 'name' es obligatorio.");
        }
        else
        {
            var trimmed = name.Trim();
            if (trimmed.Length < MinQueryLength)
            {
                errors.Add($"El parámetro 'name' debe tener al menos {MinQueryLength} caracteres.");
            }
            if (trimmed.Length > MaxQueryLength)
            {
                errors.Add($"El parámetro 'name' no puede exceder {MaxQueryLength} caracteres.");
            }
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            errors.Add("El parámetro 'source' no puede estar vacío.");
        }

        if (errors.Count > 0)
        {
            return BadRequest(new ErrorResponse(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "La solicitud contiene parámetros inválidos.",
                errors));
        }

        IScraperService scraper;
        try
        {
            scraper = _resolver.Resolve(source);
        }
        catch (SourceNotFoundException ex)
        {
            return BadRequest(new ErrorResponse(
                StatusCodes.Status400BadRequest,
                "source_not_found",
                ex.Message,
                ex.AvailableSources.ToList()));
        }

        _logger.LogInformation("Buscando '{Name}' en fuente '{Source}'", name, scraper.SourceKey);
        var result = await scraper.SearchAsync(name!.Trim(), cancellationToken);
        return Ok(result);
    }
}
