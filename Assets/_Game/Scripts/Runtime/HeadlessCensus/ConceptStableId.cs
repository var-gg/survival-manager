using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SM.HeadlessCensus;

internal static class ConceptStableId
{
    public static string Create(string prefix, params string[] parts)
    {
        var canonical = new StringBuilder(prefix).Append('|');
        foreach (var part in parts ?? Array.Empty<string>())
        {
            var value = part ?? string.Empty;
            canonical.Append(Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value)
                .Append('|');
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        var id = new StringBuilder(16);
        for (var index = 0; index < 8; index++)
        {
            id.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
        }

        return $"{prefix}-{id}";
    }
}
