using System.Text.Json;
using EntityScreening.Api.Models;

namespace EntityScreening.Api.Middleware;

public class ApiKeyAuthMiddleware
{
    public const string HeaderName = "X-API-Key";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly string _configuredApiKey;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _configuredApiKey = Environment.GetEnvironmentVariable("API_KEY")
                            ?? configuration["ApiKey"]
                            ?? string.Empty;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_configuredApiKey))
        {
            _logger.LogError("No hay API Key configurada. Defina 'ApiKey' en appsettings o la variable de entorno API_KEY.");
            await WriteError(context, StatusCodes.Status500InternalServerError, "server_misconfiguration",
                "El servidor no tiene una API Key configurada. Contacte al administrador.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            await WriteError(context, StatusCodes.Status401Unauthorized, "missing_api_key",
                $"Falta el header '{HeaderName}'. Incluya su API Key para acceder a este recurso.");
            return;
        }

        if (!ConstantTimeEquals(providedKey.ToString(), _configuredApiKey))
        {
            _logger.LogWarning("Intento de acceso con API Key inválida.");
            await WriteError(context, StatusCodes.Status401Unauthorized, "invalid_api_key",
                "La API Key proporcionada no es válida.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteError(HttpContext context, int status, string error, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var payload = new ErrorResponse(status, error, message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}
