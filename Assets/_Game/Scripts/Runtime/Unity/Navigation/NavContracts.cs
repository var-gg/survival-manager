namespace SM.Unity;

/// <summary>
/// 화면 전환 목적지 — 씬 이름 문자열을 대체하는 순수 enum.
/// SceneFlowController(실제 SceneManager 로드)와 RecordingNavSink(headless 테스트 기록)가
/// 같은 INavSink 계약을 구현해, 화면 전환 결정을 씬 로드 없이 검증할 수 있게 한다.
/// </summary>
public enum NavTarget
{
    Boot,
    Town,
    Atlas,
    Battle,
    Reward,
}

/// <summary>
/// 화면 전환 sink — "어디로 갈지"(NavTarget 결정)와 "어떻게 가는지"(씬 로드)를 분리하는 seam.
/// 런타임 구현은 SceneFlowController(SceneManager.LoadSceneAsync), 테스트 구현은 RecordingNavSink.
/// 전환 결정 자체는 순수 ExpeditionFlowResolver가 소유 — controller/golden 모두 같은 결정을 공유한다.
/// </summary>
public interface INavSink
{
    void Go(NavTarget target);
}
