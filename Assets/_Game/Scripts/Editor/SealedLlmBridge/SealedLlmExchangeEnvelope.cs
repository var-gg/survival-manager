using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

public sealed record SealedLlmDeploymentActionSpaceV1(
    IReadOnlyList<string> AvailableHeroIds,
    IReadOnlyList<string> AvailableAnchorIds,
    int DeployCapacity);

public sealed record SealedLlmExchangeRequestV1(
    string SchemaVersion,
    SealedDecisionSeamKey SeamKey,
    string RequestCanonicalHash,
    IReadOnlyList<string> LegalActionKeys,
    SealedLlmDeploymentActionSpaceV1 DeploymentActionSpace,
    string PromptFile,
    int AttemptLimit)
{
    public const string CurrentSchemaVersion = "SealedLlmExchangeRequestV1";
}

public sealed record SealedLlmExchangeResponseV1(
    string SchemaVersion,
    SealedDecisionSeamKey SeamKey,
    string RequestCanonicalHash,
    string AgentKind,
    string RawResponseJson)
{
    public const string CurrentSchemaVersion = "SealedLlmExchangeResponseV1";
}

public sealed record SealedLlmExchangeRejectV1(
    string SchemaVersion,
    SealedDecisionSeamKey SeamKey,
    string RequestCanonicalHash,
    string ReasonKind,
    string ErrorText,
    int NextAttempt)
{
    public const string CurrentSchemaVersion = "SealedLlmExchangeRejectV1";
}

/// <summary>Strict versioned exchange envelopes plus immutable UTF-8-no-BOM atomic file I/O.</summary>
public static class SealedLlmExchangeEnvelope
{
    public const string StrictParseReason = "strict_parse";
    public const string ActionDecodeReason = "action_decode";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static string CanonicalJson(SealedLlmExchangeRequestV1 value)
    {
        Validate(value);
        return HeadlessMetricJson.Serialize(value);
    }

    public static string CanonicalJson(SealedLlmExchangeResponseV1 value)
    {
        Validate(value);
        return HeadlessMetricJson.Serialize(value);
    }

    public static string CanonicalJson(SealedLlmExchangeRejectV1 value)
    {
        Validate(value);
        return HeadlessMetricJson.Serialize(value);
    }

    public static SealedLlmExchangeRequestV1 ParseRequest(string json)
    {
        var value = HeadlessMetricJson.Deserialize<SealedLlmExchangeRequestV1>(json);
        Validate(value);
        return value;
    }

    public static SealedLlmExchangeResponseV1 ParseResponse(string json)
    {
        var value = HeadlessMetricJson.Deserialize<SealedLlmExchangeResponseV1>(json);
        Validate(value);
        return value;
    }

    public static SealedLlmExchangeRejectV1 ParseReject(string json)
    {
        var value = HeadlessMetricJson.Deserialize<SealedLlmExchangeRejectV1>(json);
        Validate(value);
        return value;
    }

    public static SealedLlmExchangeRequestV1 ReadRequest(string path)
        => ParseRequest(ReadFinalFile(path));

    public static SealedLlmExchangeResponseV1 ReadResponse(string path)
        => ParseResponse(ReadFinalFile(path));

    public static SealedLlmExchangeRejectV1 ReadReject(string path)
        => ParseReject(ReadFinalFile(path));

    public static void WriteRequest(string path, SealedLlmExchangeRequestV1 value)
        => WriteAtomic(path, CanonicalJson(value) + "\n");

    public static void WriteResponse(string path, SealedLlmExchangeResponseV1 value)
        => WriteAtomic(path, CanonicalJson(value) + "\n");

    public static void WriteReject(string path, SealedLlmExchangeRejectV1 value)
        => WriteAtomic(path, CanonicalJson(value) + "\n");

    public static void WritePrompt(string path, string prompt)
        => WriteAtomic(path, prompt ?? throw new ArgumentNullException(nameof(prompt)));

    public static void WriteAtomic(string path, string content)
    {
        var finalPath = RequireFinalPath(path);
        var directory = Path.GetDirectoryName(finalPath)
                        ?? throw new ArgumentException("Exchange path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = finalPath + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(content ?? throw new ArgumentNullException(nameof(content)));
            }

            File.Move(temporaryPath, finalPath);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static string ReadFinalFile(string path)
        => File.ReadAllText(RequireFinalPath(path), Utf8WithoutBom);

    private static string RequireFinalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Exchange path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (fullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Exchange readers and writers ignore temporary files.", nameof(path));
        }

        return fullPath;
    }

    private static void Validate(SealedLlmExchangeRequestV1 value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireSchema(value.SchemaVersion, SealedLlmExchangeRequestV1.CurrentSchemaVersion);
        ValidateSeam(value.SeamKey);
        RequireText(value.RequestCanonicalHash, nameof(value.RequestCanonicalHash));
        if (value.LegalActionKeys == null)
        {
            throw new ArgumentException("LegalActionKeys must be materialized.", nameof(value));
        }

        if (value.LegalActionKeys.Count == 0 && value.DeploymentActionSpace == null
            && !string.Equals(value.SeamKey.SeamType, SealedLlmSeamTypes.RunReport, StringComparison.Ordinal))
        {
            throw new ArgumentException("A decision request must expose legal keys or a deployment action space.", nameof(value));
        }

        if (value.DeploymentActionSpace != null)
        {
            if (!string.Equals(value.SeamKey.SeamType, SealedLlmSeamTypes.Deployment, StringComparison.Ordinal)
                && !string.Equals(value.SeamKey.SeamType, SealedLlmSeamTypes.Prep, StringComparison.Ordinal))
            {
                throw new ArgumentException("DeploymentActionSpace is only valid for deployment or prep requests.", nameof(value));
            }

            if (value.DeploymentActionSpace.AvailableHeroIds == null
                || value.DeploymentActionSpace.AvailableAnchorIds == null
                || value.DeploymentActionSpace.DeployCapacity <= 0)
            {
                throw new ArgumentException("DeploymentActionSpace is incomplete.", nameof(value));
            }
        }

        RequireText(value.PromptFile, nameof(value.PromptFile));
        if (!string.Equals(Path.GetFileName(value.PromptFile), value.PromptFile, StringComparison.Ordinal))
        {
            throw new ArgumentException("PromptFile must be a leaf filename, not a path.", nameof(value));
        }

        if (value.AttemptLimit is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "AttemptLimit must be in [1,3].");
        }
    }

    private static void Validate(SealedLlmExchangeResponseV1 value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireSchema(value.SchemaVersion, SealedLlmExchangeResponseV1.CurrentSchemaVersion);
        ValidateSeam(value.SeamKey);
        RequireText(value.RequestCanonicalHash, nameof(value.RequestCanonicalHash));
        RequireText(value.AgentKind, nameof(value.AgentKind));
        if (value.RawResponseJson == null)
        {
            throw new ArgumentException("RawResponseJson must be a non-null string.", nameof(value));
        }
    }

    private static void Validate(SealedLlmExchangeRejectV1 value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireSchema(value.SchemaVersion, SealedLlmExchangeRejectV1.CurrentSchemaVersion);
        ValidateSeam(value.SeamKey);
        RequireText(value.RequestCanonicalHash, nameof(value.RequestCanonicalHash));
        if (!string.Equals(value.ReasonKind, StrictParseReason, StringComparison.Ordinal)
            && !string.Equals(value.ReasonKind, ActionDecodeReason, StringComparison.Ordinal))
        {
            throw new ArgumentException("Reject ReasonKind is not recognized.", nameof(value));
        }

        RequireText(value.ErrorText, nameof(value.ErrorText));
        if (value.NextAttempt is < 2 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "NextAttempt must be in [2,3].");
        }
    }

    private static void RequireSchema(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new FormatException($"Envelope schema must equal '{expected}'.");
        }
    }

    private static void ValidateSeam(SealedDecisionSeamKey seamKey)
    {
        if (seamKey == null
            || seamKey.DecisionIndex < 0
            || seamKey.Ordinal < 0
            || string.IsNullOrWhiteSpace(seamKey.SeamType))
        {
            throw new ArgumentException("Envelope SeamKey is invalid.", nameof(seamKey));
        }
    }

    private static void RequireText(string value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{path} is required.", path);
        }
    }
}
