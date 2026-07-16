namespace SM.HeadlessPolicies;

/// <summary>
/// H100 player policy seam. 여섯 production 구현이 같은 관측에서 교체 실행되므로 interface 다형성이 본질이다.
/// 구현에는 observation 외의 session/content/RNG service를 주입하지 않는다.
/// </summary>
public interface IHeadlessPolicy
{
    string Id { get; }

    HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation);

    HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation);
}
