using System;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>
/// One canonical decision request. Replay and live sources use the seam key and canonical bytes; the exactly-one
/// concrete observation is a capture-only aid that lets <see cref="SyntheticStandInSource"/> mirror a reference
/// policy without parsing the wire payload.
/// </summary>
public sealed record SealedLlmDecisionRequest
{
    private SealedLlmDecisionRequest(
        SealedDecisionSeamKey seamKey,
        byte[] requestCanonicalBytes,
        HeadlessPolicyObservation policyObservation,
        HeadlessRosterPolicyObservation rosterObservation)
    {
        SeamKey = seamKey ?? throw new ArgumentNullException(nameof(seamKey));
        if (string.IsNullOrWhiteSpace(seamKey.SeamType))
        {
            throw new ArgumentException("SeamType must be stable non-empty text.", nameof(seamKey));
        }

        if (seamKey.DecisionIndex < 0 || seamKey.Ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seamKey), "Seam key indices must be non-negative.");
        }

        RequestCanonicalBytes = CloneBytes(
            requestCanonicalBytes ?? throw new ArgumentNullException(nameof(requestCanonicalBytes)));
        PolicyObservation = policyObservation;
        RosterObservation = rosterObservation;
        if ((PolicyObservation == null) == (RosterObservation == null))
        {
            throw new ArgumentException("Exactly one concrete reference observation is required.");
        }
    }

    public SealedDecisionSeamKey SeamKey { get; }
    public byte[] RequestCanonicalBytes { get; }
    public HeadlessPolicyObservation PolicyObservation { get; }
    public HeadlessRosterPolicyObservation RosterObservation { get; }

    public static SealedLlmDecisionRequest ForPolicy(
        SealedDecisionSeamKey seamKey,
        byte[] requestCanonicalBytes,
        HeadlessPolicyObservation observation)
        => new(
            seamKey,
            requestCanonicalBytes,
            observation ?? throw new ArgumentNullException(nameof(observation)),
            null);

    public static SealedLlmDecisionRequest ForRoster(
        SealedDecisionSeamKey seamKey,
        byte[] requestCanonicalBytes,
        HeadlessRosterPolicyObservation observation)
        => new(
            seamKey,
            requestCanonicalBytes,
            null,
            observation ?? throw new ArgumentNullException(nameof(observation)));

    private static byte[] CloneBytes(byte[] value)
        => (byte[])value.Clone();
}
