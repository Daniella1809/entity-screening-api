using System.Text.Json;
using System.Threading.RateLimiting;
using EntityScreening.Api.Middleware;
using EntityScreening.Api.Models;
using EntityScreening.Api.Services;
using EntityScreening.Api.Services.Scrapers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Entity Screening API",
        Version = "v1",
        Description = "REST API que hace web scraping de listas de alto riesgo (fuente principal: OFAC) " +
                      "para identificar entidades por nombre. Incluye autenticación por API Key y rate limiting."
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Ingrese su API Key en el header " + ApiKeyAuthMiddleware.HeaderName
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpClient(nameof(OfacScraper), client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
});

builder.Services.AddScoped<IScraperService, OfacScraper>();
builder.Services.AddScoped<IScraperResolver, ScraperResolver>();

const int PermitLimitPerMinute = 20;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.Request.Headers[ApiKeyAuthMiddleware.HeaderName].FirstOrDefault()
                           ?? context.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        var payload = new ErrorResponse(
            StatusCodes.Status429TooManyRequests,
            "rate_limit_exceeded",
            $"Se superó el límite de {PermitLimitPerMinute} solicitudes por minuto. Intente nuevamente en unos momentos.");

        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            cancellationToken);
    };
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Entity Screening API v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow }))
   .WithName("HealthCheck");

app.MapControllers();

app.Run();

public partial class Program { }
