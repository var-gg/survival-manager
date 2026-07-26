using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>sealed LLM Editor codec files가 공유하는 scalar framing과 canonical ordering.</summary>
internal static class SealedLlmCanonicalValue
{
    private static readonly IComparer<byte[]> CanonicalByteComparer = new ByteArrayComparer();

    public static void Append(Stream destination, string value, string path)
    {
        if (value == null)
        {
            throw new ArgumentException($"{path} cannot be null.", path);
        }

        LengthPrefixedStableHash.AppendPart(destination, value);
    }

    public static void Append(Stream destination, byte[] value, string path)
    {
        if (value == null)
        {
            throw new ArgumentException($"{path} cannot be null.", path);
        }

        LengthPrefixedStableHash.AppendPart(destination, value);
    }

    public static void AppendBoolean(Stream destination, bool value)
        => LengthPrefixedStableHash.AppendPart(destination, value ? "true" : "false");

    public static void AppendInteger(Stream destination, int value)
        => LengthPrefixedStableHash.AppendPart(destination, Invariant(value));

    public static void AppendRoundTrip(Stream destination, float value, string path)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentException($"{path} must be finite.", path);
        }

        LengthPrefixedStableHash.AppendPart(destination, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static void AppendRoundTrip(Stream destination, double value, string path)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException($"{path} must be finite.", path);
        }

        LengthPrefixedStableHash.AppendPart(destination, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static void AppendEnum<TEnum>(Stream destination, TEnum value, string path)
        where TEnum : struct, Enum
    {
        var name = Enum.GetName(typeof(TEnum), value);
        if (name == null)
        {
            throw new ArgumentOutOfRangeException(path, value, $"{path} must be a defined enum value.");
        }

        LengthPrefixedStableHash.AppendPart(destination, name);
    }

    public static string EnumName<TEnum>(TEnum value, string path)
        where TEnum : struct, Enum
    {
        var name = Enum.GetName(typeof(TEnum), value);
        return name
               ?? throw new ArgumentOutOfRangeException(path, value, $"{path} must be a defined enum value.");
    }

    public static void AppendSortedStrings(
        Stream destination,
        IReadOnlyList<string> values,
        string path)
    {
        if (values == null)
        {
            throw new ArgumentException($"{path} is required.", path);
        }

        var ordered = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            ordered[index] = values[index]
                             ?? throw new ArgumentException($"{path}[{index}] cannot be null.", path);
        }

        Array.Sort(ordered, StringComparer.Ordinal);
        AppendInteger(destination, ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            Append(destination, ordered[index], $"{path}[{index}]");
        }
    }

    public static void AppendSortedObjects<T>(
        Stream destination,
        IReadOnlyList<T> values,
        string path,
        Func<T, byte[]> canonicalize)
        where T : class
    {
        if (values == null)
        {
            throw new ArgumentException($"{path} is required.", path);
        }

        if (canonicalize == null)
        {
            throw new ArgumentNullException(nameof(canonicalize));
        }

        var ordered = new byte[values.Count][];
        for (var index = 0; index < values.Count; index++)
        {
            var item = values[index]
                       ?? throw new ArgumentException($"{path}[{index}] cannot be null.", path);
            ordered[index] = canonicalize(item);
        }

        Array.Sort(ordered, CanonicalByteComparer);
        AppendInteger(destination, ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            Append(destination, ordered[index], $"{path}[{index}]");
        }
    }

    public static void AppendOptional<T>(
        Stream destination,
        T value,
        string path,
        Func<T, byte[]> canonicalize)
        where T : class
    {
        AppendBoolean(destination, value != null);
        if (value != null)
        {
            Append(destination, canonicalize(value), path);
        }
    }

    public static string Invariant(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static string Hash(byte[] canonicalBytes)
        => LengthPrefixedStableHash.Compute(
            canonicalBytes ?? throw new ArgumentNullException(nameof(canonicalBytes)));

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            var commonLength = Math.Min(left.Length, right.Length);
            for (var index = 0; index < commonLength; index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0) return comparison;
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}
