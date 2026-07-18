using System;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>
/// Player-visible policy observations를 sealed LLM wire의 canonical frame bytes로 변환한다.
/// </summary>
public static class SealedLlmObservationCodec
{
    public static byte[] CanonicalBytes(HeadlessPolicyObservation observation)
        => SealedLlmObservationCanonicalWriter.Write(
            observation ?? throw new ArgumentNullException(nameof(observation)));

    public static byte[] CanonicalBytes(HeadlessRosterPolicyObservation observation)
        => SealedLlmObservationCanonicalWriter.Write(
            observation ?? throw new ArgumentNullException(nameof(observation)));

    public static string Hash(HeadlessPolicyObservation observation)
        => SealedLlmCanonicalValue.Hash(CanonicalBytes(observation));

    public static string Hash(HeadlessRosterPolicyObservation observation)
        => SealedLlmCanonicalValue.Hash(CanonicalBytes(observation));
}
