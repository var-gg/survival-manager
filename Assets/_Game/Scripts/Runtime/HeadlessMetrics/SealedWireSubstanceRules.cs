using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>BT5/BT10 wire text의 sentinel, substance, exact content-id mention 규칙.</summary>
public static class SealedWireSubstanceRules
{
    private static readonly HashSet<string> Sentinels = new(StringComparer.Ordinal)
    {
        string.Empty,
        "none",
        "n/a",
        "na",
        "null",
        "nil",
        "unknown",
        "-",
        "todo",
        "tbd",
        "llm",
        "synthetic-stand-in",
        "synthetic-stand-in:paired-reference",
        "mirror the injected scripted reference policy",
        "follow the next scripted reference decision",
        "synthetic stand-in has no inferred desire",
        "paired scripted-policy witness only",
        "repeat the injected scripted reference",
    };

    public static string Normalize(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character);
        }

        return result.ToString().ToLowerInvariant();
    }

    public static bool Substantive(string value)
    {
        var normalized = Normalize(value);
        return normalized.Length > 0 && !Sentinels.Contains(normalized);
    }

    /// <summary>
    /// raw ordinal substring와 ASCII <c>[a-z0-9_]</c> 경계를 함께 적용한다.
    /// 대소문자 변형, fuzzy match, normalization은 의도적으로 허용하지 않는다.
    /// </summary>
    public static IReadOnlyList<string> Mentions(string text, IEnumerable<string> ids)
    {
        if (text == null)
        {
            return Array.Empty<string>();
        }

        return (ids ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrEmpty(id) && ContainsBoundedOrdinal(text, id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ContainsBoundedOrdinal(string text, string id)
    {
        var searchStart = 0;
        while (searchStart <= text.Length - id.Length)
        {
            var index = text.IndexOf(id, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var leftBoundary = index == 0 || !IsAsciiWord(text[index - 1]);
            var rightIndex = index + id.Length;
            var rightBoundary = rightIndex == text.Length || !IsAsciiWord(text[rightIndex]);
            if (leftBoundary && rightBoundary)
            {
                return true;
            }

            searchStart = index + 1;
        }

        return false;
    }

    private static bool IsAsciiWord(char value)
        => value is >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
}
