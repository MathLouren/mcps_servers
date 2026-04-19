using System.Security.Cryptography;
using System.Text.RegularExpressions;
using EprodutosAgents.Data;
using EprodutosAgents.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EprodutosAgents.Security;

public sealed partial class McpApiKeyService(EprodutosMongoContext context)
{
    private const string ApiKeyPrefix = "eprod_mcp_";
    private const int KeyIdByteCount = 12;
    private const int HashIterations = 100_000;
    private const int MaxHashIterations = 1_000_000;
    private const int SecretByteCount = 32;
    private const int SaltByteCount = 16;
    private const int HashByteCount = 32;
    private const int DefaultKeyLifetimeDays = 90;
    private const int MaxKeyLifetimeDays = 365;
    private const int MaxListedKeys = 500;

    public async Task<McpApiKeyCreatedResponse> CreateForUserAsync(
        CreateMcpApiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(request.UserId, request.Email, cancellationToken)
            ?? throw new KeyNotFoundException("Usuario nao encontrado na collection users.");

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException("Usuario sem _id valido.");
        }

        var now = DateTime.UtcNow;
        var expiresAt = NormalizeExpiresAt(request.ExpiresAt, now);
        await RevokeActiveKeysAsync(user.Id, "rotated", now, cancellationToken);

        var keyId = $"mk_{CreateTokenPart(KeyIdByteCount)}";
        var secret = CreateTokenPart(SecretByteCount);
        var salt = RandomNumberGenerator.GetBytes(SaltByteCount);
        var hash = HashSecret(secret, salt, HashIterations);

        var document = new McpApiKeyDocument
        {
            UserId = user.Id,
            UserEmail = user.Email,
            UserName = user.Name,
            Role = user.Role,
            KeyId = keyId,
            SecretHash = Convert.ToBase64String(hash),
            SecretSalt = Convert.ToBase64String(salt),
            HashIterations = HashIterations,
            Name = McpSecurityLimits.NormalizeOptionalExactText(request.Name, nameof(request.Name)) ?? "MCP",
            CreatedAt = now,
            ExpiresAt = expiresAt,
            CreatedBy = McpSecurityLimits.NormalizeOptionalExactText(
                request.CreatedBy,
                nameof(request.CreatedBy))
        };

        await context.McpApiKeys.InsertOneAsync(document, cancellationToken: cancellationToken);

        return new McpApiKeyCreatedResponse(
            $"{ApiKeyPrefix}{keyId}.{secret}",
            keyId,
            user.Id,
            user.Email,
            user.Name,
            user.Role,
            now,
            expiresAt,
            "Guarde a api_key agora. O servidor salva apenas o hash e nao consegue mostrar esta chave novamente.");
    }

    public async Task<AuthenticatedMcpUser?> ValidateAsync(
        string rawApiKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseApiKey(rawApiKey, out var keyId, out var secret))
        {
            return null;
        }

        var filter = Builders<McpApiKeyDocument>.Filter.And(
            Builders<McpApiKeyDocument>.Filter.Eq(static key => key.KeyId, keyId),
            Builders<McpApiKeyDocument>.Filter.Eq(static key => key.RevokedAt, null),
            Builders<McpApiKeyDocument>.Filter.Or(
                Builders<McpApiKeyDocument>.Filter.Eq(static key => key.ExpiresAt, null),
                Builders<McpApiKeyDocument>.Filter.Gt(static key => key.ExpiresAt, DateTime.UtcNow)));

        var keyDocument = await context.McpApiKeys.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (keyDocument is null || !VerifySecret(secret, keyDocument))
        {
            return null;
        }

        var user = await FindUserAsync(keyDocument.UserId, keyDocument.UserEmail, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            return null;
        }

        var update = Builders<McpApiKeyDocument>.Update
            .Set(static key => key.LastUsedAt, DateTime.UtcNow)
            .Set(static key => key.UserEmail, user.Email)
            .Set(static key => key.UserName, user.Name)
            .Set(static key => key.Role, user.Role);

        await context.McpApiKeys.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return new AuthenticatedMcpUser(
            user.Id,
            user.Email,
            user.Name,
            user.Role,
            keyDocument.KeyId,
            NormalizeList(user.AllowedProcesses),
            NormalizeList(user.AllowedManufacturers));
    }

    public async Task<IReadOnlyList<McpApiKeyMetadataResponse>> ListAsync(
        string? email = null,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<McpApiKeyDocument>.Filter;
        var filters = new List<FilterDefinition<McpApiKeyDocument>>();

        if (!includeRevoked)
        {
            filters.Add(builder.Eq(static key => key.RevokedAt, null));
            filters.Add(builder.Or(
                builder.Eq(static key => key.ExpiresAt, null),
                builder.Gt(static key => key.ExpiresAt, DateTime.UtcNow)));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            filters.Add(builder.Regex(static key => key.UserEmail, ExactRegex(email)));
        }

        var filter = filters.Count == 0 ? builder.Empty : builder.And(filters);
        var keys = await context.McpApiKeys
            .Find(filter)
            .SortByDescending(static key => key.CreatedAt)
            .Limit(MaxListedKeys)
            .ToListAsync(cancellationToken);

        return keys.Select(ToMetadataResponse).ToArray();
    }

    public async Task<bool> RevokeAsync(
        string keyId,
        string reason = "revoked",
        CancellationToken cancellationToken = default)
    {
        if (!McpSecurityLimits.IsValidApiKeyId(keyId))
        {
            return false;
        }

        var update = Builders<McpApiKeyDocument>.Update
            .Set(static key => key.RevokedAt, DateTime.UtcNow)
            .Set(static key => key.RevokedReason, reason);

        var result = await context.McpApiKeys.UpdateOneAsync(
            Builders<McpApiKeyDocument>.Filter.And(
                Builders<McpApiKeyDocument>.Filter.Eq(static key => key.KeyId, keyId),
                Builders<McpApiKeyDocument>.Filter.Eq(static key => key.RevokedAt, null)),
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    private async Task RevokeActiveKeysAsync(
        string userId,
        string reason,
        DateTime revokedAt,
        CancellationToken cancellationToken)
    {
        var update = Builders<McpApiKeyDocument>.Update
            .Set(static key => key.RevokedAt, revokedAt)
            .Set(static key => key.RevokedReason, reason);

        await context.McpApiKeys.UpdateManyAsync(
            Builders<McpApiKeyDocument>.Filter.And(
                Builders<McpApiKeyDocument>.Filter.Eq(static key => key.UserId, userId),
                Builders<McpApiKeyDocument>.Filter.Eq(static key => key.RevokedAt, null)),
            update,
            cancellationToken: cancellationToken);
    }

    private async Task<UserDocument?> FindUserAsync(
        string? userId,
        string? email,
        CancellationToken cancellationToken)
    {
        var builder = Builders<UserDocument>.Filter;
        var filters = new List<FilterDefinition<UserDocument>>();

        if (!string.IsNullOrWhiteSpace(userId) && ObjectId.TryParse(userId, out _))
        {
            filters.Add(builder.Eq(static user => user.Id, userId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            filters.Add(builder.Regex(static user => user.Email, ExactRegex(email)));
        }

        if (filters.Count == 0)
        {
            throw new ArgumentException("Informe user_id ou email.");
        }

        return await context.Users
            .Find(builder.Or(filters))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static McpApiKeyMetadataResponse ToMetadataResponse(McpApiKeyDocument key)
    {
        return new McpApiKeyMetadataResponse(
            key.KeyId,
            key.UserId,
            key.UserEmail,
            key.UserName,
            key.Role,
            key.CreatedAt,
            key.ExpiresAt,
            key.LastUsedAt,
            key.RevokedAt,
            key.RevokedReason);
    }

    private static bool TryParseApiKey(string rawApiKey, out string keyId, out string secret)
    {
        keyId = string.Empty;
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(rawApiKey) ||
            rawApiKey.Length > McpSecurityLimits.MaxApiKeyLength ||
            !rawApiKey.StartsWith(ApiKeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var value = rawApiKey[ApiKeyPrefix.Length..];
        var separatorIndex = value.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        keyId = value[..separatorIndex];
        secret = value[(separatorIndex + 1)..];
        return McpSecurityLimits.IsValidApiKeyId(keyId) &&
            SecretRegex().IsMatch(secret);
    }

    private static bool VerifySecret(string secret, McpApiKeyDocument keyDocument)
    {
        try
        {
            if (keyDocument.HashIterations is < HashIterations or > MaxHashIterations)
            {
                return false;
            }

            var salt = Convert.FromBase64String(keyDocument.SecretSalt);
            var expectedHash = Convert.FromBase64String(keyDocument.SecretHash);
            if (salt.Length != SaltByteCount || expectedHash.Length != HashByteCount)
            {
                return false;
            }

            var actualHash = HashSecret(secret, salt, keyDocument.HashIterations);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] HashSecret(string secret, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
    }

    private static string CreateTokenPart(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        return McpSecurityLimits.NormalizeScope(values);
    }

    private static BsonRegularExpression ExactRegex(string value)
    {
        return new BsonRegularExpression(
            $"^{Regex.Escape(McpSecurityLimits.RequireExactText(value, nameof(value)))}$",
            "i");
    }

    private static DateTime NormalizeExpiresAt(DateTime? requestedExpiresAt, DateTime now)
    {
        var maxExpiresAt = now.AddDays(MaxKeyLifetimeDays);
        if (!requestedExpiresAt.HasValue)
        {
            return now.AddDays(DefaultKeyLifetimeDays);
        }

        var expiresAt = requestedExpiresAt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(requestedExpiresAt.Value, DateTimeKind.Utc)
            : requestedExpiresAt.Value.ToUniversalTime();

        if (expiresAt <= now.AddMinutes(5))
        {
            throw new ArgumentException("expires_at deve ser uma data futura.");
        }

        if (expiresAt > maxExpiresAt)
        {
            throw new ArgumentException($"expires_at nao pode passar de {MaxKeyLifetimeDays} dias.");
        }

        return expiresAt;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretRegex();
}
