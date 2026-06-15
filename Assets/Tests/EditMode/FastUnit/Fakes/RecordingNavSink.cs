using System.Collections.Generic;
using SM.Unity;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// headless 테스트용 INavSink — 실제 씬 로드 대신 요청된 NavTarget 순서를 기록한다.
/// 골든이 Town→Atlas→Battle→Reward→Town 전이 시퀀스를 씬 없이 검증할 수 있게 한다.
/// </summary>
public sealed class RecordingNavSink : INavSink
{
    public readonly List<NavTarget> Targets = new();

    public void Go(NavTarget target) => Targets.Add(target);

    public NavTarget? Last => Targets.Count == 0 ? (NavTarget?)null : Targets[Targets.Count - 1];
}
