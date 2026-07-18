using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>
/// UTF-8 payload를 <c>{byteCount}:{value}|</c> frame으로 결합하고 FNV-1a 64-bit로 해시한다.
/// <see cref="ReplayHash"/>와 sealed decision trace가 같은 byte contract를 공유하는 내부 codec이다.
/// </summary>
public static class LengthPrefixedStableHash
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static void AppendPart(Stream destination, string value)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (value == null) throw new ArgumentNullException(nameof(value));

        AppendPart(destination, StrictUtf8.GetBytes(value));
    }

    public static void AppendPart(Stream destination, byte[] value)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (value == null) throw new ArgumentNullException(nameof(value));

        var lengthBytes = Encoding.ASCII.GetBytes(value.Length.ToString(CultureInfo.InvariantCulture));
        destination.Write(lengthBytes, 0, lengthBytes.Length);
        destination.WriteByte((byte)':');
        destination.Write(value, 0, value.Length);
        destination.WriteByte((byte)'|');
    }

    public static string Compute(byte[] input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var value in input)
            {
                hash ^= value;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
