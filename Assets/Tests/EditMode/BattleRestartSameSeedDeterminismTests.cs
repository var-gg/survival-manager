using System.Globalization;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Editor.SeedData;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 같은 시드 재시작 결정성 골든 — 씬 RestartSameSeed가 소비하는 세션 합성 단일 소스
/// (<see cref="GameSessionState.TryComposeBattleState"/>)가 첫 전투(TryBuildSelectedBattleState)와
/// byte-identical한 step 스트림을 재생산함을 잠근다.
///
/// 배경(2026-07 준비도 감사): RestartSameSeed가 씬에서 BattleFactory를 직접 호출하는 2nd battle-truth라
/// 보스 overlay bootstrap·status rule fallback이 빠졌다 — 같은 시드 재시작이 첫 전투와 다른 전투가 되는
/// 2nd-consumer 결정성 drift 계열(FinalUnits id 비교, BuildStableSeed와 동형). 합성 경로를 세션으로
/// 통일한 뒤, 이 골든이 "재시작 = 같은 전투" 계약을 행동으로 고정한다(구조 lint는
/// BuildBoundaryGuardFastTests.BattleComposition_SceneRestartStaysOnSessionSingleSource).
///
/// 실 적군 콘텐츠가 필요하므로 RuntimeCombatContentLookup을 쓴다(BatchOnly) —
/// HeadlessBattleSimulationTests와 동일 셋업. 스트림 직렬화는 BattleDeterminismBaselineTests의
/// canonical 투영 사본(asmdef 경계로 헬퍼 공유 불가 — BattleHashCorpusGoldenTests 전례).
/// </summary>
[Category("BatchOnly")]
public sealed class BattleRestartSameSeedDeterminismTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(BattleRestartSameSeedDeterminismTests));
    }

    [Test]
    public void RestartComposition_ReplaysByteIdenticalStream_AgainstFirstBattle()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile()); // 기본 분대(헤더 배치 포함) 시드
        session.BeginNewExpedition();

        // 첫 전투 — 씬 RunBattle과 동일한 세션 단일 소스 진입점.
        Assert.That(
            session.TryBuildSelectedBattleState(out var firstState, out var encounter, out var allySnapshot, out var error),
            Is.True, error);
        var first = SerializeRun(new BattleSimulator(firstState, BattleSimulator.DefaultMaxSteps));
        Assert.That(first.StepCount, Is.GreaterThan(0), "첫 전투가 실제 tick을 돌리지 않았다 — 골든이 공허하다.");

        // 재시작 — 씬 RestartSameSeed와 동일한 데이터 흐름: RunBattle이 캐시한 allySnapshot/encounter를
        // 그대로 세션에 재합성 요청한다(스냅샷 재빌드 없음 = 같은 시드, 같은 로드아웃).
        Assert.That(
            session.TryComposeBattleState(allySnapshot, encounter, out var restartState, out error),
            Is.True, error);
        var restart = SerializeRun(new BattleSimulator(restartState, BattleSimulator.DefaultMaxSteps));

        Assert.That(restart.Stream, Is.EqualTo(first.Stream),
            "같은 시드 재시작이 첫 전투와 다른 step 스트림을 생산 — 합성 경로 이격(2nd battle-truth) 또는 프로세스 가변 엔트로피.");
        Assert.That(restart.Winner, Is.EqualTo(first.Winner), "같은 시드 재시작의 승자가 첫 전투와 다르다.");

        // 재시작의 재시작 — 재합성 자체가 반복 호출에도 안정적임을 함께 고정한다.
        Assert.That(
            session.TryComposeBattleState(allySnapshot, encounter, out var secondRestartState, out error),
            Is.True, error);
        var secondRestart = SerializeRun(new BattleSimulator(secondRestartState, BattleSimulator.DefaultMaxSteps));
        Assert.That(secondRestart.Stream, Is.EqualTo(first.Stream),
            "재시작 2회차가 1회차/첫 전투와 갈라졌다 — 재합성 경로에 상태 잔류(비멱등)가 있다.");
    }

    // ── 이하 canonical gameplay-truth 투영: BattleDeterminismBaselineTests.SerializeRun 사본 ──

    private static RunResult SerializeRun(BattleSimulator simulator)
    {
        var sb = new StringBuilder();
        AppendStep(sb, simulator.CurrentStep);
        var steps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 20000)
        {
            AppendStep(sb, simulator.Step());
            steps++;
        }

        return new RunResult(sb.ToString(), steps, simulator.Winner);
    }

    private static void AppendStep(StringBuilder sb, BattleSimulationStep step)
    {
        sb.Append('S').Append(step.StepIndex)
          .Append('|').Append(step.IsFinished ? 'F' : '.')
          .Append(step.Winner?.ToString() ?? "-")
          .Append('|');
        foreach (var unit in step.Units)
        {
            sb.Append(unit.Id).Append(':')
              .Append(F(unit.Position.X)).Append(',')
              .Append(F(unit.Position.Y)).Append(',')
              .Append(F(unit.CurrentHealth)).Append(',')
              .Append(F(unit.CurrentEnergy)).Append(',')
              .Append((int)unit.ActionState).Append(',')
              .Append(unit.IsAlive ? '1' : '0').Append(';');
        }

        sb.Append('|');
        foreach (var battleEvent in step.Events)
        {
            sb.Append((int)battleEvent.LogCode).Append(':')
              .Append(battleEvent.ActorId.Value).Append(':')
              .Append(battleEvent.TargetId?.Value ?? "-").Append(':')
              .Append(F(battleEvent.Value)).Append(':')
              .Append((int)battleEvent.EventKind).Append(';');
        }

        sb.Append('\n');
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private readonly struct RunResult
    {
        public RunResult(string stream, int stepCount, TeamSide? winner)
        {
            Stream = stream;
            StepCount = stepCount;
            Winner = winner;
        }

        public string Stream { get; }

        public int StepCount { get; }

        public TeamSide? Winner { get; }
    }
}
