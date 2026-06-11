using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Unity;

/// <summary>
/// P2 전투 하이라이트 원장 — 관전 중 흘러간 "내 그림"을 전투 종료 요약으로 되돌려준다.
/// step 스트림의 typed 채널(BattleEvent kind/logcode/value/KillPayload + contact accent — J8: note
/// 문자열 비파싱)만 집계해 아군 MVP(피해+회복+처치 가중)와 극적 순간 카운트(측면/후방/차단/구출/
/// 후열격파)를 decisive timeline에 붙인다. presentation 전용 — sim/save truth 무접촉.
/// </summary>
public sealed class BattleHighlightLedger
{
    private const float HealingMvpWeight = 1.2f;
    private const float KillMvpWeight = 15f;

    private sealed class ActorTally
    {
        public float Damage;
        public float Healing;
        public int Kills;
    }

    private readonly Dictionary<string, ActorTally> _allyTallies = new(StringComparer.Ordinal);
    private int _flankStrikes;
    private int _rearStrikes;
    private int _screenAbsorbs;
    private int _saveMoments;
    private int _backlineDiveKills;
    private bool _flushed;

    public void Reset()
    {
        _allyTallies.Clear();
        _flankStrikes = 0;
        _rearStrikes = 0;
        _screenAbsorbs = 0;
        _saveMoments = 0;
        _backlineDiveKills = 0;
        _flushed = false;
    }

    /// <summary>스텝 전환마다 1회 호출 — 아군 관점 집계.</summary>
    public void Record(BattleSimulationStep step)
    {
        var allyIds = new HashSet<string>(
            step.Units.Where(unit => unit.Side == TeamSide.Ally).Select(unit => unit.Id),
            StringComparer.Ordinal);

        foreach (var eventData in step.Events)
        {
            var isAllyActor = allyIds.Contains(eventData.ActorId.Value);
            if (eventData.EventKind == BattleEventKind.Kill)
            {
                if (isAllyActor)
                {
                    ResolveTally(eventData.ActorId.Value).Kills++;
                    if (eventData.KillPayload is { IsBacklineDiveKill: true })
                    {
                        _backlineDiveKills++;
                    }
                }

                continue;
            }

            if (!isAllyActor || eventData.Value <= 0f)
            {
                continue;
            }

            if (eventData.LogCode == BattleLogCode.ActiveSkillHeal)
            {
                ResolveTally(eventData.ActorId.Value).Healing += eventData.Value;
            }
            else if (eventData.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage)
            {
                ResolveTally(eventData.ActorId.Value).Damage += eventData.Value;
            }
        }

        if (step.CombatEventIntents == null)
        {
            return;
        }

        foreach (var intent in step.CombatEventIntents)
        {
            if (intent.Status != CombatEventIntentStatus.Contacted || intent.Contacts == null)
            {
                continue;
            }

            var isAllyAttacker = allyIds.Contains(intent.ActorId.Value);
            foreach (var contact in intent.Contacts)
            {
                if (contact.Accent == CombatContactAccent.None)
                {
                    continue;
                }

                if (isAllyAttacker && contact.Accent.HasFlag(CombatContactAccent.Rear))
                {
                    _rearStrikes++;
                }
                else if (isAllyAttacker && contact.Accent.HasFlag(CombatContactAccent.Flank))
                {
                    _flankStrikes++;
                }

                // 차단은 수비측 사실 — 아군 표적이 스크린 뒤에서 맞아준 횟수.
                if (contact.Accent.HasFlag(CombatContactAccent.Screened)
                    && contact.TargetId != null
                    && allyIds.Contains(contact.TargetId.Value.Value))
                {
                    _screenAbsorbs++;
                }

                if (isAllyAttacker && contact.Accent.HasFlag(CombatContactAccent.SaveMoment))
                {
                    _saveMoments++;
                }
            }
        }
    }

    /// <summary>
    /// 전투 종료 시 1회 — MVP 줄과 하이라이트 줄을 timeline에 붙인다(이후 호출은 no-op).
    /// </summary>
    public bool TryAppendBattleEndLines(BattleSimulationStep step, IList<string> timeline, Func<string, string> resolveName)
    {
        if (!step.IsFinished || _flushed)
        {
            return false;
        }

        _flushed = true;
        var appended = false;
        var mvp = _allyTallies
            .OrderByDescending(pair => ResolveMvpScore(pair.Value))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (mvp.Value != null && ResolveMvpScore(mvp.Value) > 0f)
        {
            timeline.Add(
                $"MVP {resolveName(mvp.Key)} | 피해 {mvp.Value.Damage:0} · 회복 {mvp.Value.Healing:0} · 처치 {mvp.Value.Kills}");
            appended = true;
        }

        var parts = new List<string>(5);
        if (_rearStrikes > 0) parts.Add($"후방 강타 {_rearStrikes}");
        if (_flankStrikes > 0) parts.Add($"측면 협격 {_flankStrikes}");
        if (_screenAbsorbs > 0) parts.Add($"전열 차단 {_screenAbsorbs}");
        if (_saveMoments > 0) parts.Add($"구출 {_saveMoments}");
        if (_backlineDiveKills > 0) parts.Add($"후열 격파 {_backlineDiveKills}");
        if (parts.Count > 0)
        {
            timeline.Add($"하이라이트 | {string.Join(" · ", parts)}");
            appended = true;
        }

        return appended;
    }

    private static float ResolveMvpScore(ActorTally tally)
    {
        return tally.Damage + (tally.Healing * HealingMvpWeight) + (tally.Kills * KillMvpWeight);
    }

    private ActorTally ResolveTally(string actorId)
    {
        if (!_allyTallies.TryGetValue(actorId, out var tally))
        {
            tally = new ActorTally();
            _allyTallies[actorId] = tally;
        }

        return tally;
    }
}
