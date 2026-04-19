using EprodutosAgents.Domain;
using EprodutosAgents.Security;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;

namespace EprodutosAgents.Data;

public sealed class McpAuditLogService(
    EprodutosMongoContext context,
    IHttpContextAccessor httpContextAccessor,
    ILogger<McpAuditLogService> logger)
{
    public async Task RecordToolCallAsync(
        AuthenticatedMcpUser user,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        string status,
        long durationMs,
        long? resultCount = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            var document = new McpAuditLogDocument
            {
                UserId = user.UserId,
                UserEmail = user.Email,
                UserName = user.Name,
                Role = user.Role,
                ApiKeyId = user.ApiKeyId,
                ToolName = toolName,
                Arguments = ToBsonDocument(arguments),
                Status = status,
                DurationMs = durationMs,
                ResultCount = resultCount,
                IpAddress = GetClientIpAddress(httpContext),
                UserAgent = McpSecurityLimits.SanitizeAuditText(
                    httpContext?.Request.Headers.UserAgent.FirstOrDefault()),
                ErrorMessage = McpSecurityLimits.SanitizeAuditText(errorMessage),
                CreatedAt = DateTime.UtcNow
            };

            await context.McpAuditLogs.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao gravar auditoria MCP da tool {ToolName}.", toolName);
        }
    }

    private static BsonDocument ToBsonDocument(IReadOnlyDictionary<string, object?> arguments)
    {
        var document = new BsonDocument();
        foreach (var (key, value) in arguments)
        {
            document[key] = ToBsonValue(value);
        }

        return document;
    }

    private static BsonValue ToBsonValue(object? value)
    {
        return value switch
        {
            null => BsonNull.Value,
            string text => McpSecurityLimits.SanitizeAuditText(text),
            int number => number,
            long number => number,
            double number => number,
            decimal number => Convert.ToDouble(number),
            bool boolean => boolean,
            DateTime date => date,
            _ => McpSecurityLimits.SanitizeAuditText(value.ToString())
        };
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
