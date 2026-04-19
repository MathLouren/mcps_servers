using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using EprodutosAgents.Configuration;
using EprodutosAgents.Data;
using EprodutosAgents.Domain;
using EprodutosAgents.Security;
using EprodutosAgents.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
const string AdminRateLimitPolicy = "mcp-admin";
const string McpRateLimitPolicy = "mcp";

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = McpSecurityLimits.MaxRequestBodyBytes;
    options.Limits.MaxRequestHeadersTotalSize = McpSecurityLimits.MaxRequestHeadersBytes;
    options.Limits.MaxRequestLineSize = McpSecurityLimits.MaxRequestLineBytes;
});

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(static serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return MongoOptions.FromConfiguration(configuration);
});

builder.Services.AddSingleton<EprodutosMongoContext>();
builder.Services.AddSingleton<EprodutosRepository>();
builder.Services.AddSingleton<McpApiKeyService>();
builder.Services.AddSingleton<McpAuditLogService>();
builder.Services.AddScoped<McpUserContext>();

builder.Services
    .AddAuthentication(McpApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, McpApiKeyAuthenticationHandler>(
        McpApiKeyAuthenticationHandler.SchemeName,
        static _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpApiKey", policy =>
    {
        policy.AddAuthenticationSchemes(McpApiKeyAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AdminRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartition(context),
            static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy(McpRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartition(context),
            static _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithTools([typeof(ProductTools), typeof(StockTools)]);

var app = builder.Build();

if (GetConfiguredAdminKey(app.Configuration) is null)
{
    app.Logger.LogWarning(
        "EPRODUTOS_MCP_ADMIN_KEY ausente ou fraca. Endpoints administrativos de API key ficarao desabilitados.");
}

await app.Services.GetRequiredService<EprodutosMongoContext>()
    .EnsureIndexesAsync(app.Lifetime.ApplicationStopping);

if (IsEnabled(app.Configuration, "EPRODUTOS_TRUST_FORWARDED_HEADERS", "Mcp:TrustForwardedHeaders"))
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async httpContext =>
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            title: "Erro interno.",
            detail: "A requisicao nao pode ser concluida.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(httpContext);
    });
});

if (IsEnabled(app.Configuration, "EPRODUTOS_MCP_REQUIRE_HTTPS", "Mcp:RequireHttps"))
{
    app.Use(async (httpContext, next) =>
    {
        if (!httpContext.Request.IsHttps)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new { message = "HTTPS obrigatorio." });
            return;
        }

        await next();
    });
}

app.Use(async (httpContext, next) =>
{
    httpContext.Response.Headers["Cache-Control"] = "no-store";
    httpContext.Response.Headers.Pragma = "no-cache";
    httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
    httpContext.Response.Headers["X-Frame-Options"] = "DENY";
    httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "eprodutos-mcp",
    transport = "streamable-http"
})).RequireRateLimiting(McpRateLimitPolicy);

var apiKeys = app.MapGroup("/api/mcp/api-keys")
    .RequireRateLimiting(AdminRateLimitPolicy);

apiKeys.MapPost("", async (
    CreateMcpApiKeyRequest request,
    HttpContext httpContext,
    IConfiguration configuration,
    McpApiKeyService apiKeyService,
    CancellationToken cancellationToken) =>
{
    if (!IsAdminRequest(httpContext, configuration))
    {
        return Results.Unauthorized();
    }

    try
    {
        var response = await apiKeyService.CreateForUserAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { message = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

apiKeys.MapGet("", async (
    string? email,
    bool include_revoked,
    HttpContext httpContext,
    IConfiguration configuration,
    McpApiKeyService apiKeyService,
    CancellationToken cancellationToken) =>
{
    if (!IsAdminRequest(httpContext, configuration))
    {
        return Results.Unauthorized();
    }

    var keys = await apiKeyService.ListAsync(email, include_revoked, cancellationToken);
    return Results.Ok(keys);
});

apiKeys.MapDelete("/{keyId}", async (
    string keyId,
    HttpContext httpContext,
    IConfiguration configuration,
    McpApiKeyService apiKeyService,
    CancellationToken cancellationToken) =>
{
    if (!IsAdminRequest(httpContext, configuration))
    {
        return Results.Unauthorized();
    }

    if (!McpSecurityLimits.IsValidApiKeyId(keyId))
    {
        return Results.BadRequest(new { message = "key_id invalido." });
    }

    var revoked = await apiKeyService.RevokeAsync(keyId, cancellationToken: cancellationToken);
    return revoked
        ? Results.Ok(new { message = "API key revogada.", key_id = keyId })
        : Results.NotFound(new { message = "API key ativa nao encontrada.", key_id = keyId });
});

app.MapMcp("/mcp")
    .RequireRateLimiting(McpRateLimitPolicy)
    .RequireAuthorization("McpApiKey");

await app.RunAsync();

static bool IsAdminRequest(HttpContext httpContext, IConfiguration configuration)
{
    var expected = GetConfiguredAdminKey(configuration);
    if (expected is null)
    {
        return false;
    }

    var provided = httpContext.Request.Headers["X-MCP-ADMIN-KEY"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(provided))
    {
        var authorization = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            provided = authorization["Bearer ".Length..].Trim();
        }
    }

    if (string.IsNullOrWhiteSpace(provided))
    {
        return false;
    }

    provided = provided.Trim();
    if (provided.Length > McpSecurityLimits.MaxAdminKeyLength)
    {
        return false;
    }

    var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
    var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
    return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
}

static string? GetConfiguredAdminKey(IConfiguration configuration)
{
    var expected = Environment.GetEnvironmentVariable("EPRODUTOS_MCP_ADMIN_KEY")
        ?? configuration["Mcp:AdminKey"];

    return McpSecurityLimits.IsStrongAdminKey(expected) ? expected!.Trim() : null;
}

static string GetRateLimitPartition(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static bool IsEnabled(IConfiguration configuration, string environmentVariable, string configurationKey)
{
    var rawValue = Environment.GetEnvironmentVariable(environmentVariable)
        ?? configuration[configurationKey];

    return bool.TryParse(rawValue, out var enabled) && enabled;
}
