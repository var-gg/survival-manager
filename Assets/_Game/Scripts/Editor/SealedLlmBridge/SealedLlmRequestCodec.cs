using System;
using System.IO;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>Prompt manifest hash, observation bytes, history prefix hash를 sealed request preimage로 묶는다.</summary>
public static class SealedLlmRequestCodec
{
    public static byte[] RequestCanonicalBytes(
        LlmPromptManifestV1 manifest,
        byte[] observationCanonicalBytes,
        string historyPrefixHash)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (observationCanonicalBytes == null) throw new ArgumentNullException(nameof(observationCanonicalBytes));
        if (historyPrefixHash == null) throw new ArgumentNullException(nameof(historyPrefixHash));

        using var payload = new MemoryStream();
        SealedLlmCanonicalValue.Append(payload, "SealedLlmRequestV1", "schema");
        SealedLlmCanonicalValue.Append(payload, manifest.PromptSchemaHash, nameof(manifest.PromptSchemaHash));
        SealedLlmCanonicalValue.Append(
            payload,
            observationCanonicalBytes,
            nameof(observationCanonicalBytes));
        SealedLlmCanonicalValue.Append(payload, historyPrefixHash, nameof(historyPrefixHash));
        return payload.ToArray();
    }

    public static string Hash(
        LlmPromptManifestV1 manifest,
        byte[] observationCanonicalBytes,
        string historyPrefixHash)
        => SealedLlmCanonicalValue.Hash(
            RequestCanonicalBytes(manifest, observationCanonicalBytes, historyPrefixHash));
}
