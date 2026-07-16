namespace SM.HeadlessCensus;

/// <summary>구체 subject/build id를 제거해 동형 recipe를 묶는 구조 fingerprint.</summary>
public sealed record ConceptFingerprint(
    string MotifKind,
    string EnablerShape,
    string AmplifierShape,
    string PayoffShape,
    string PayoffWitness,
    string ThresholdTag,
    int Threshold,
    string DoctrineRuleId,
    string FormationProfile)
{
    public string Signature => string.Join("|", new[]
    {
        MotifKind,
        EnablerShape,
        AmplifierShape,
        PayoffShape,
        PayoffWitness,
        ThresholdTag,
        Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
        DoctrineRuleId,
        FormationProfile,
    });
}
