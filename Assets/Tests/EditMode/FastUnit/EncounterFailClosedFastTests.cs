using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SM.Core;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace SM.Tests.EditMode;

/// <summary>
/// 인카운터 무음 강등 방어 회귀 잠금 — authored 카탈로그가 있는데 인카운터 해석이 실패하면(오타·미시드 squad/encounter)
/// 예전에는 GameSessionState가 그 error를 버리고 4인 디버그 스모크 전투로 조용히 바꿔치기한 뒤 return true 했다.
/// 그 결과 깨진 authored 보스전이 무관한 placeholder로 둔갑하고 아무도 모르게 됐다(엔지니어링 감사 폴백 섹션).
/// 이제 HasAuthoredCatalog==true면 Debug.LogError로 표면화 + return false(fail-closed)한다.
///
/// 엔지니어링 감사(analysis-engineering-audit-headless-divergence-fallback-deadcode-2026-06) 작업 순서 #4.
/// </summary>
[Category("FastUnit")]
public sealed class EncounterFailClosedFastTests
{
    [Test]
    public void AuthoredCatalog_EncounterResolutionFailure_FailsClosed_NotSilentDebugSmoke()
    {
        var lookup = EditorFreeCombatContentFixture.CreateAuthoredLookupWithDanglingEncounterSquad();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "encounter_fail_closed",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        session.BeginNewExpedition();

        // authored 카탈로그에서 encounter→squad ref가 깨졌다. 무음 디버그 스모크 강등이 아니라
        // LogError로 표면화돼야 한다(테스트가 unhandled error로 죽지 않도록 Expect로 소비).
        LogAssert.Expect(LogType.Error, new Regex("authored 인카운터 해석 실패"));

        var resolved = session.TryResolveCurrentEncounter(out _, out var error);

        Assert.That(resolved, Is.False,
            "authored 카탈로그에서 인카운터 해석이 실패하면 디버그 스모크로 강등하지 않고 실패를 반환해야 한다(fail-closed).");
        Assert.That(error, Is.Not.Empty,
            "실패 사유(error)가 채워져 호출자/로그에 전파돼야 한다.");
    }

    private static HeroInstanceRecord CreateHero(string heroId, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = heroId,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }
}
