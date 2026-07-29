using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SM.Editor.Tools;

/// <summary>
/// character-lighting A/B PNG를 검증하고 단일 machine-readable 결과 파일로 기록한다.
/// </summary>
internal static class BattleCaptureResultFile
{
    internal const string ResultPath = "Captures/battle_ab_charlight_result.json";

    internal static void Clear()
    {
        if (File.Exists(ResultPath))
        {
            File.Delete(ResultPath);
        }
    }

    internal static bool TryWriteSuccess(
        string offPath,
        string onPath,
        float minimumLuminance,
        int stepIndex,
        int aliveUnits,
        int totalUnits,
        out BattleCapturePngMetrics metrics,
        out string error)
    {
        metrics = default;
        if (!TryReadPng(offPath, out var offBytes, out var offLuminance, out var offError))
        {
            error = $"lights-off PNG validation failed. {offError}";
            return false;
        }

        if (!TryReadPng(onPath, out var onBytes, out var onLuminance, out var onError))
        {
            error = $"lights-on PNG validation failed. {onError}";
            return false;
        }

        var bytesDiffer = !File.ReadAllBytes(offPath).SequenceEqual(File.ReadAllBytes(onPath));
        metrics = new BattleCapturePngMetrics(
            offBytes,
            onBytes,
            offLuminance,
            onLuminance,
            bytesDiffer);
        if (!bytesDiffer)
        {
            error = "lights-off and lights-on PNG files are byte-identical.";
            return false;
        }

        if (offLuminance < minimumLuminance || onLuminance < minimumLuminance)
        {
            error =
                $"one or both frames are nearly black. offLuminance={offLuminance:0.000000}, " +
                $"onLuminance={onLuminance:0.000000}.";
            return false;
        }

        Write(new CaptureResultRecord
        {
            status = "success",
            step_index = stepIndex,
            alive_units = aliveUnits,
            total_units = totalUnits,
            off_bytes = offBytes,
            on_bytes = onBytes,
            off_luminance = offLuminance,
            on_luminance = onLuminance,
            bytes_differ = bytesDiffer,
        });
        error = string.Empty;
        return true;
    }

    internal static void WriteFailure(
        string reason,
        string offPath = "",
        string onPath = "",
        int stepIndex = -1,
        int aliveUnits = 0,
        int totalUnits = 0)
    {
        var result = new CaptureResultRecord
        {
            status = "failed",
            reason = reason,
            step_index = stepIndex,
            alive_units = aliveUnits,
            total_units = totalUnits,
        };

        if (TryReadPng(offPath, out var offBytes, out var offLuminance, out _))
        {
            result.off_bytes = offBytes;
            result.off_luminance = offLuminance;
        }

        if (TryReadPng(onPath, out var onBytes, out var onLuminance, out _))
        {
            result.on_bytes = onBytes;
            result.on_luminance = onLuminance;
        }

        if (result.off_bytes > 0 && result.on_bytes > 0)
        {
            result.bytes_differ = !File.ReadAllBytes(offPath).SequenceEqual(File.ReadAllBytes(onPath));
        }

        Write(result);
    }

    private static bool TryReadPng(
        string path,
        out long byteCount,
        out float luminance,
        out string error)
    {
        byteCount = 0;
        luminance = 0f;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = $"File does not exist: {path}";
            return false;
        }

        var bytes = File.ReadAllBytes(path);
        byteCount = bytes.LongLength;
        if (byteCount == 0)
        {
            error = $"File is empty: {path}";
            return false;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (!texture.LoadImage(bytes, markNonReadable: false))
            {
                error = $"Unity could not decode the PNG: {path}";
                return false;
            }

            luminance = BattleSceneCaptureTool.MeanLuminance(texture);
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Write(CaptureResultRecord result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? "Captures");
        File.WriteAllText(ResultPath, JsonUtility.ToJson(result, prettyPrint: true));
    }

    [Serializable]
    private sealed class CaptureResultRecord
    {
        public string status = string.Empty;
        public string reason = string.Empty;
        public int step_index = -1;
        public int alive_units;
        public int total_units;
        public long off_bytes;
        public long on_bytes;
        public float off_luminance;
        public float on_luminance;
        public bool bytes_differ;
    }
}

internal readonly struct BattleCapturePngMetrics
{
    internal BattleCapturePngMetrics(
        long offBytes,
        long onBytes,
        float offLuminance,
        float onLuminance,
        bool bytesDiffer)
    {
        OffBytes = offBytes;
        OnBytes = onBytes;
        OffLuminance = offLuminance;
        OnLuminance = onLuminance;
        BytesDiffer = bytesDiffer;
    }

    internal long OffBytes { get; }
    internal long OnBytes { get; }
    internal float OffLuminance { get; }
    internal float OnLuminance { get; }
    internal bool BytesDiffer { get; }
}
