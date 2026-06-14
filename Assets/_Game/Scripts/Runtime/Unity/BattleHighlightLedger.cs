using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta;

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
    // Phase 4 — beat 채널(아군 관점) 집계: 시너지 발동 / 콤보 작렬 / 증강 트리거 발동.
    private int _allySynergyActivations;
    private int _allyComboConsumes;
    private int _allyTriggeredEffects;
    private bool _flushed;

    public void Reset()
    {
        _allyTallies.Clear();
        _flankStrikes = 0;
        _rearStrikes = 0;
        _screenAbsorbs = 0;
        _saveMoments = 0;
        _backlineDiveKills = 0;
        _allySynergyActivations = 0;
        _allyComboConsumes = 0;
        _allyTriggeredEffects = 0;
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

        RecordBeats(step);

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

    // Phase 4 — beat 채널의 아군 관점 집계. 시너지/콤보/증강 발동이 종료 요약에서도 한 줄로 남는다.
    private void RecordBeats(BattleSimulationStep step)
    {
        if (step.Beats == null)
        {
            return;
        }

        foreach (var beat in step.Beats)
        {
            if (beat.Side != TeamSide.Ally)
            {
                continue;
            }

            switch (beat.Type)
            {
                case CombatBeatType.SynergyActivated:
                    _allySynergyActivations++;
                    break;
                case CombatBeatType.ComboConsumed:
                    _allyComboConsumes++;
                    break;
                case CombatBeatType.BattleStartEffect:
                case CombatBeatType.OnKillEffect:
                case CombatBeatType.HpThresholdEffect:
                case CombatBeatType.AllyDeathEffect:
                    _allyTriggeredEffects++;
                    break;
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
        var mvp = ResolveTopAlly();
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

        // Phase 4 — 시너지/콤보/증강 발현 요약("어떤 콤보가 터졌고 무엇이 발동했나"의 post-fight 답).
        var beatParts = new List<string>(3);
        if (_allySynergyActivations > 0) beatParts.Add($"시너지 발동 {_allySynergyActivations}");
        if (_allyComboConsumes > 0) beatParts.Add($"콤보 작렬 {_allyComboConsumes}");
        if (_allyTriggeredEffects > 0) beatParts.Add($"증강 발동 {_allyTriggeredEffects}");
        if (beatParts.Count > 0)
        {
            timeline.Add($"발현 | {string.Join(" · ", beatParts)}");
            appended = true;
        }

        return appended;
    }

    /// <summary>
    /// 전투 종료 후 보상 화면용 구조화 요약을 1회 산출 — 누적 집계(MVP/진형 전과/발현)를 carrier 로
    /// 투영만 한다. 표시 포맷은 SM.Meta(BattleFormationPayoffFormatter)가 소유하므로 여기선 수치/이름만
    /// 채운다. 전투 피드(<see cref="TryAppendBattleEndLines"/>)와 동일 소스라 두 surface 의 숫자가 일치한다.
    /// </summary>
    public BattleFormationPayoffSummary BuildFormationPayoff(Func<string, string> resolveName)
    {
        var mvp = ResolveTopAlly();
        var hasMvp = mvp.Value != null && ResolveMvpScore(mvp.Value) > 0f;
        var hasData = hasMvp
            || _flankStrikes > 0 || _rearStrikes > 0 || _screenAbsorbs > 0
            || _saveMoments > 0 || _backlineDiveKills > 0
            || _allySynergyActivations > 0 || _allyComboConsumes > 0 || _allyTriggeredEffects > 0;

        return new BattleFormationPayoffSummary(
            HasData: hasData,
            MvpUnitName: hasMvp ? resolveName(mvp.Key) : "",
            MvpDamage: hasMvp ? mvp.Value.Damage : 0f,
            MvpHealing: hasMvp ? mvp.Value.Healing : 0f,
            MvpKills: hasMvp ? mvp.Value.Kills : 0,
            FlankStrikes: _flankStrikes,
            RearStrikes: _rearStrikes,
            ScreenAbsorbs: _screenAbsorbs,
            SaveMoments: _saveMoments,
            BacklineDiveKills: _backlineDiveKills,
            SynergyActivations: _allySynergyActivations,
            ComboConsumes: _allyComboConsumes,
            TriggeredEffects: _allyTriggeredEffects);
    }

    private static float ResolveMvpScore(ActorTally tally)
    {
        return tally.Damage + (tally.Healing * HealingMvpWeight) + (tally.Kills * KillMvpWeight);
    }

    // MVP 후보 정렬(피해+회복가중+처치가중 내림차순, id 안정) — 종료 라인과 carrier 산출이 공유.
    private KeyValuePair<string, ActorTally> ResolveTopAlly()
    {
        return _allyTallies
            .OrderByDescending(pair => ResolveMvpScore(pair.Value))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault();
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
