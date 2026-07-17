namespace SM.HeadlessPolicies;

/// <summary>
/// Town roster agency를 명시적으로 opt-in하는 별도 정책 seam.
/// 기존 IHeadlessPolicy의 deployment/reward 계약과 production 6종 구현은 변경하지 않는다.
/// </summary>
public interface IHeadlessRosterPolicy
{
    HeadlessRecruitDecision DecideRecruit(HeadlessRosterPolicyObservation observation);

    HeadlessPassiveDecision DecidePassiveAllocation(HeadlessRosterPolicyObservation observation);

    HeadlessRefitDecision DecideRefit(HeadlessRosterPolicyObservation observation);
}
