namespace SM.HeadlessPolicies;

/// <summary>elite/boss 직전의 제한된 무료 prep 결정만 노출하는 선택적 headless policy seam.</summary>
public interface IHeadlessPrepPolicy
{
    HeadlessPrepDecision DecidePrep(HeadlessPolicyObservation observation);
}
