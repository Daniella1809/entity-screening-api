using System.Text.Json;
using EntityScreening.Api.Models;
using EntityScreening.Api.Services;

namespace EntityScreening.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SourceNotFoundException ex)
        {
            await WriteError(context, StatusCodes.Status400BadRequest, "source_not_found", ex.Message);
        }
        catch (ScrapingException ex)
        {
            _logger.LogWarning(ex, "Error de scraping en la fuente '{Source}'", ex.SourceKey);
            await WriteError(context, StatusCodes.Status502BadGateway, "source_error", ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("La solicitud fue cancelada por el cliente.");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno no controlado.");
            await WriteError(context, StatusCodes.Status500InternalServerError, "internal_error",
                "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    private static async Task WriteError(HttpContext context, int status, string error, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var payload = new ErrorResponse(status, error, message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
