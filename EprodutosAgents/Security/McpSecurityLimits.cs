using System.Text;
using System.Text.RegularExpressions;

namespace EprodutosAgents.Security;

public static partial class McpSecurityLimits
{
    public const int MaxRequestBodyBytes = 1_048_576;
    public const int MaxRequestHeadersBytes = 16_384;
    public const int MaxRequestLineBytes = 4_096;

    public const int MaxApiKeyLength = 128;
    public const int MaxAdminKeyLength = 256;
    public const int MinAdminKeyLength = 32;
    public const int MaxKeyIdLength = 32;

    public const int MaxExactFilterLength = 120;
    public const int MaxSearchTermLength = 120;
    public const int MinSearchTermLength = 2;
    public const int MaxScopeItems = 100;
    public const int MaxProductJoinPns = 2_000;
    public const int MaxPage = 10_000;
    public const int MaxPageSize = 100;

    public const int MaxAuditTextLength = 512;
    public const int MaxMarkdownCellLength = 500;

    public static string? NormalizeOptionalExactText(string? value, string parameterName)
    {
        return NormalizeOptionalText(value, parameterName, MaxExactFilterLength, minLength: 1);
    }

    public static string? NormalizeOptionalSearchText(string? value, string parameterName)
    {
        return NormalizeOptionalText(value, parameterName, MaxSearchTermLength, MinSearchTermLength);
    }

    public static string RequireExactText(string? value, string parameterName)
    {
        return NormalizeOptionalExactText(value, parameterName)
            ?? throw new ArgumentException("Informe um valor valido.", parameterName);
    }

    public static int NormalizePage(int page)
    {
        return Math.Clamp(page, 1, MaxPage);
    }

    public static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public static IReadOnlyList<string> NormalizeScope(IReadOnlyList<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => NormalizeInlineText(value, MaxExactFilterLength))
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxScopeItems)
            .ToArray() ?? [];
    }

    public static string SanitizeAuditText(string? value)
    {
        return NormalizeInlineText(value, MaxAuditTextLength);
    }

    public static string SanitizeMarkdownCell(string value)
    {
        return NormalizeInlineText(value, MaxMarkdownCellLength)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    public static bool IsValidApiKeyId(string? keyId)
    {
        return !string.IsNullOrWhiteSpace(keyId) &&
            keyId.Length <= MaxKeyIdLength &&
            ApiKeyIdRegex().IsMatch(keyId);
    }

    public static bool IsStrongAdminKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length is >= MinAdminKeyLength and <= MaxAdminKeyLength &&
            !WeakAdminKeyRegex().IsMatch(trimmed);
    }

    private static string? NormalizeOptionalText(
        string? value,
        string parameterName,
        int maxLength,
        int minLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"Informe no maximo {maxLength} caracteres.",
                parameterName);
        }

        var normalized = NormalizeInlineText(value, maxLength);
        if (normalized.Length < minLength)
        {
            throw new ArgumentException(
                $"Informe ao menos {minLength} caracteres.",
                parameterName);
        }

        return normalized;
    }

    private static string NormalizeInlineText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsControl(character))
            {
                if (character is '\r' or '\n' or '\t')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(character);
            if (builder.Length >= maxLength)
            {
                break;
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"^mk_[A-Za-z0-9_-]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyIdRegex();

    [GeneratedRegex(@"^(troque-este-valor|dev-admin-key|gere-uma-chave-forte|changeme|change-me|password|admin)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeakAdminKeyRegex();
}
