using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>sealed wire와 observation이 공유하는 strict length-prefixed frame reader.</summary>
internal sealed class SealedCanonicalFrameReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly byte[] _bytes;
    private readonly string _scope;
    private int _offset;

    public SealedCanonicalFrameReader(byte[] bytes, string scope)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public void RequireSchema(string expected)
    {
        var actual = ReadString("schema");
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new FormatException($"{_scope}.schema expected '{expected}' but found '{actual}'.");
        }
    }

    public byte[] ReadBytes(string field)
    {
        if (_offset >= _bytes.Length)
        {
            throw new FormatException($"{_scope}.{field} frame is missing.");
        }

        var lengthStart = _offset;
        long length = 0;
        var digitCount = 0;
        while (_offset < _bytes.Length && _bytes[_offset] != (byte)':')
        {
            var value = _bytes[_offset];
            if (value < (byte)'0' || value > (byte)'9')
            {
                throw new FormatException($"{_scope}.{field} frame length is not ASCII decimal.");
            }

            if (digitCount == 0
                && value == (byte)'0'
                && _offset + 1 < _bytes.Length
                && _bytes[_offset + 1] != (byte)':')
            {
                throw new FormatException($"{_scope}.{field} frame length has a leading zero.");
            }

            checked
            {
                length = (length * 10) + (value - (byte)'0');
            }

            digitCount++;
            _offset++;
        }

        if (digitCount == 0 || _offset >= _bytes.Length || _bytes[_offset] != (byte)':')
        {
            throw new FormatException(
                $"{_scope}.{field} frame length delimiter is missing at {lengthStart}.");
        }

        _offset++;
        if (length > int.MaxValue || length > _bytes.Length - _offset - 1L)
        {
            throw new FormatException($"{_scope}.{field} frame payload length exceeds remaining bytes.");
        }

        var result = new byte[(int)length];
        Buffer.BlockCopy(_bytes, _offset, result, 0, result.Length);
        _offset += result.Length;
        if (_offset >= _bytes.Length || _bytes[_offset] != (byte)'|')
        {
            throw new FormatException($"{_scope}.{field} frame terminator is missing.");
        }

        _offset++;
        return result;
    }

    public string ReadString(string field)
    {
        try
        {
            return StrictUtf8.GetString(ReadBytes(field));
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException($"{_scope}.{field} is not strict UTF-8.", exception);
        }
    }

    public int ReadInteger(string field)
    {
        var value = ReadString(field);
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            || !string.Equals(value, result.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new FormatException($"{_scope}.{field} is not a canonical integer.");
        }

        return result;
    }

    public int ReadCount(string field)
    {
        var result = ReadInteger(field);
        if (result < 0 || result > _bytes.Length)
        {
            throw new FormatException($"{_scope}.{field} is not a bounded non-negative count.");
        }

        return result;
    }

    public bool ReadBoolean(string field)
    {
        var value = ReadString(field);
        return value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new FormatException($"{_scope}.{field} is not a canonical boolean."),
        };
    }

    public float ReadSingle(string field)
    {
        var value = ReadString(field);
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            || float.IsNaN(result)
            || float.IsInfinity(result)
            || !string.Equals(value, result.ToString("R", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new FormatException($"{_scope}.{field} is not a canonical finite single.");
        }

        return result;
    }

    public double ReadDouble(string field)
    {
        var value = ReadString(field);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            || double.IsNaN(result)
            || double.IsInfinity(result)
            || !string.Equals(value, result.ToString("R", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new FormatException($"{_scope}.{field} is not a canonical finite double.");
        }

        return result;
    }

    public IReadOnlyList<string> ReadStrings(string field)
    {
        var count = ReadCount($"{field}.count");
        var result = new string[count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = ReadString($"{field}[{index}]");
        }

        return result;
    }

    public void RequireEnd()
    {
        if (_offset != _bytes.Length)
        {
            throw new FormatException(
                $"{_scope} canonical frame has {_bytes.Length - _offset} trailing bytes.");
        }
    }
}
