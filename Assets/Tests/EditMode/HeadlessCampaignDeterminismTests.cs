using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Tests.EditMode.Playthrough;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 헤드리스 실 캠페인 **결정성** 게이트 — <see cref="HeadlessRealCampaignSimulationTests"/>가 "실제로 싸웠나"를
/// 증명한다면, 이 골든은 "같은 입력이면 같은 결과인가"를 증명한다. 같은 프로세스 안에서 두 개의 독립
/// <see cref="GameSessionState"/>(같은 ProfileId·같은 콘텐츠)로 캠페인을 끝까지 구동해, 전투 노드 W/L·step
/// 시퀀스와 최종 영웅 진행(레벨/EXP)·dossier가 **byte-identical** 인지 단언한다.
///
/// <para>배경(엔지니어링 감사 follow-up — analysis-engineering-audit-headless-divergence-fallback-deadcode-2026-06):
/// "같은 시드·분대인데 런간 W/L이 변동한다"는 신규발견 (a)의 회귀 게이트다. 감사 #2가 전투 시드를 RunId GUID에서
/// 콘텐츠 좌표 SHA256으로 결정화한 뒤(<see cref="SeedDeterminismFastTests"/>), 6-에이전트 정적 감사가 게임플레이에
/// 도달하는 프로세스 가변 엔트로피가 0건임을 증명했다 — 즉 캠페인 결과는 결정적이어야 한다. 이 테스트가 그
/// 불변식을 잠근다. 동일-프로세스 2회로 가변 static state·컬렉션 순회 순서·정렬 누락을 포착한다(프로세스 간
/// FP 결정성은 <see cref="BattleDeterminismBaselineTests"/>의 same-seed step-stream 골든이 별도로 받친다).</para>
///
/// <para>BatchOnly: <see cref="RuntimeCombatContentLookup"/> 실 Resources 콘텐츠가 필요하다
/// (<see cref="HeadlessRealCampaignSimulationTests"/>와 동일 setup).</para>
/// </summary>
[Category("BatchOnly")]
public sealed class HeadlessCampaignDeterminismTests
{
    private const int InstanceIdWitnessSeed = 1701;

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(HeadlessCampaignDeterminismTests));
    }

    [Test]
    public void TwoIndependentCampaignRuns_AreByteIdentical_SameProfileSeed()
    {
        var first = RunHeadlessCampaign();
        var second = RunHeadlessCampaign();

        // 진단 로그 먼저 — 단언 실패 시에도 두 런의 전체 시퀀스가 콘솔에 남아 어느 노드에서 갈렸는지 보인다.
        TestContext.WriteLine($"[run A] {first.OutcomeSignature}");
        TestContext.WriteLine($"[run B] {second.OutcomeSignature}");
        TestContext.WriteLine($"[run A] progression={first.ProgressionSignature}");
        TestContext.WriteLine($"[run B] progression={second.ProgressionSignature}");

        // (1) 전투 노드 W/L·step 시퀀스가 완전히 동일 — "victories 5 vs 4" 류 런간 변동의 직접 회귀 차단.
        Assert.That(second.OutcomeSignature, Is.EqualTo(first.OutcomeSignature),
            "같은 ProfileId·콘텐츠로 두 번 구동한 캠페인의 전투 W/L·step 시퀀스가 갈렸다 — sim 비결정.");

        // (2) 캠페인 종료 형태(완주/패배 사이트·클리어 사이트)도 동일.
        Assert.That(second.TerminationSignature, Is.EqualTo(first.TerminationSignature),
            "캠페인 종료 형태(StoryCleared/DefeatedSite/ClearedSites)가 런간 갈렸다.");

        // (3) 정산 후처리 결과(영웅 레벨/EXP·dossier)도 동일 — 전투 결과가 같으면 진행도 같아야 한다.
        Assert.That(second.ProgressionSignature, Is.EqualTo(first.ProgressionSignature),
            "전투 후 영웅 진행(레벨/EXP)·dossier가 런간 갈렸다.");

        // (4) 게이트가 의미를 가지려면 실제로 싸웠어야 한다(빈 시퀀스 동일은 통과로 치지 않는다).
        Assert.That(first.BattleCount, Is.GreaterThan(0),
            "전투 노드가 0건 — 결정성 단언이 공허하지 않도록 최소 1전투를 요구.");
    }

    [Test]
    public void TwoFreshColdStartCampaigns_MintByteIdenticalIds_AndRefitAffix()
    {
        var legacyDefault = new SaveProfile();
        Assert.That(legacyDefault.HeroInstanceCounter, Is.Zero,
            "레거시 save에 필드가 없을 때 hero counter 기본값은 0이어야 한다.");
        Assert.That(legacyDefault.ItemInstanceCounter, Is.Zero,
            "레거시 save에 필드가 없을 때 item counter 기본값은 0이어야 한다.");

        // 별도 프로세스 실행이 아닌, 같은 seed/settings의 fresh in-process cold-start session 2개를 쓰는 proxy다.
        var first = RunInstanceIdWitnessCampaign();
        var second = RunInstanceIdWitnessCampaign();
        TestContext.WriteLine(
            $"hero_ids=[{string.Join(",", first.MintedHeroIds)}] "
            + $"item_ids=[{string.Join(",", first.MintedItemIds)}] "
            + $"refit={first.RefitSignature.Replace('\n', '|')} "
            + $"counters=hero:{first.HeroCounter},item:{first.ItemCounter}");

        AssertUtf8Equal(first.CampaignSignature, second.CampaignSignature,
            "같은 cold-start campaign 진행이 두 fresh session 사이에서 갈렸다.");
        AssertUtf8Equal(string.Join("\n", first.MintedHeroIds), string.Join("\n", second.MintedHeroIds),
            "생성된 hero id가 두 fresh session 사이에서 byte-identical하지 않다.");
        AssertUtf8Equal(string.Join("\n", first.MintedItemIds), string.Join("\n", second.MintedItemIds),
            "생성된 item-instance id가 두 fresh session 사이에서 byte-identical하지 않다.");
        AssertUtf8Equal(first.RefitSignature, second.RefitSignature,
            "결정적 item-instance id를 소비한 refit 결과 affix가 byte-identical하지 않다.");
        Assert.That(second.HeroCounter, Is.EqualTo(first.HeroCounter));
        Assert.That(second.ItemCounter, Is.EqualTo(first.ItemCounter));

        Assert.That(first.MintedHeroIds.Count, Is.EqualTo(2),
            "dismiss 전후로 hero id 두 개를 발급해 counter 비재사용을 증명해야 한다.");
        Assert.That(first.MintedItemIds, Is.Not.Empty,
            "production reward가 최소 한 개의 item id를 발급해야 한다.");
        foreach (var heroId in first.MintedHeroIds)
        {
            Assert.That(heroId, Does.StartWith("hero-c"));
            AssertSafeDecimalSequence(heroId, "hero-c");
        }

        foreach (var itemId in first.MintedItemIds)
        {
            AssertSafeDecimalSequence(itemId, "-i");
        }
    }

    private static CampaignRunSnapshot RunHeadlessCampaign()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile { ProfileId = "headless_determinism" }); // 기본 분대 시드(hero-1..N)
        session.SetCurrentScene(SceneNames.Town);

        var runner = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink(),
            PlaythroughBattleResolution.Simulate);
        var result = runner.Run();

        var outcomes = result.SiteObservations
            .SelectMany(site => site.BattleOutcomes ?? (IReadOnlyList<PlaythroughBattleOutcome>)Array.Empty<PlaythroughBattleOutcome>())
            .ToList();

        var outcomeSignature = string.Join(
            " | ",
            outcomes.Select(outcome => $"{outcome.NodeId}={(outcome.Victory ? "W" : "L")}({outcome.StepCount})"));

        var terminationSignature =
            $"cleared={result.StoryCleared};defeated={result.DefeatedSiteId ?? "(none)"};" +
            $"sites=[{string.Join(",", result.ClearedSiteIds)}]";

        // 영웅 진행을 HeroId ordinal 정렬해 직렬화 — 직렬화 순서가 결과를 흔들지 않도록(서명 자체는 결정적이어야).
        var progressionSignature = string.Join(
            ";",
            session.Profile.HeroProgressions
                .OrderBy(record => record.HeroId, StringComparer.Ordinal)
                .Select(record => $"{record.HeroId}:L{record.Level}/X{record.Experience}"))
            + " || dossier=" + session.Profile.Dossier.Count;

        return new CampaignRunSnapshot(outcomeSignature, terminationSignature, progressionSignature, outcomes.Count);
    }

    private static InstanceIdWitnessSnapshot RunInstanceIdWitnessCampaign()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = H100SessionDriver.CreateSession(
            lookup,
            $"headless_instance_id_determinism_s{InstanceIdWitnessSeed}");

        Assert.That(session.Profile.HeroInstanceCounter, Is.EqualTo(session.Profile.Heroes.Count),
            "cold-start demo heroes가 legacy/default counter floor를 초기화해야 한다.");
        Assert.That(session.Profile.ItemInstanceCounter, Is.EqualTo(session.Profile.Inventory.Count),
            "cold-start demo items가 legacy/default counter floor를 초기화해야 한다.");

        var initialHeroIds = session.Profile.Heroes
            .Select(hero => hero.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        var ownedArchetypeIds = session.Profile.Heroes
            .Select(hero => hero.ArchetypeId)
            .ToHashSet(StringComparer.Ordinal);
        var offer = session.RecruitOffers
            .Select((candidate, index) => new { Candidate = candidate, Index = index })
            .FirstOrDefault(candidate => !ownedArchetypeIds.Contains(candidate.Candidate.UnitBlueprintId));
        Assert.That(offer, Is.Not.Null, "cold-start H100 roster surface에 non-duplicate recruit가 필요하다.");

        session.Profile.Currencies.Gold = Math.Max(session.Profile.Currencies.Gold, offer!.Candidate.Metadata.GoldCost);
        var recruit = session.Recruit(offer.Index);
        Assert.That(recruit.IsSuccess, Is.True, recruit.Error);
        var firstMintedHero = session.Profile.Heroes.Single(hero => !initialHeroIds.Contains(hero.HeroId));
        var firstHeroCounter = session.Profile.HeroInstanceCounter;
        Assert.That(
            firstMintedHero.HeroId,
            Is.EqualTo($"hero-c{firstHeroCounter.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

        var dismiss = session.DismissHero(firstMintedHero.HeroId);
        Assert.That(dismiss.IsSuccess, Is.True, dismiss.Error);
        var heroIdsBeforeReplacement = session.Profile.Heroes
            .Select(hero => hero.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        var replacement = session.GrantHeroDirect(offer.Candidate.UnitBlueprintId);
        Assert.That(replacement.IsSuccess, Is.True, replacement.Error);
        var replacementHero = session.Profile.Heroes.Single(hero => !heroIdsBeforeReplacement.Contains(hero.HeroId));
        Assert.That(session.Profile.HeroInstanceCounter, Is.EqualTo(firstHeroCounter + 1L),
            "dismiss는 persisted hero counter를 감소시키거나 sequence를 재사용하면 안 된다.");
        Assert.That(replacementHero.HeroId, Is.Not.EqualTo(firstMintedHero.HeroId));

        var initialItemIds = session.Profile.Inventory
            .Select(item => item.ItemInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        session.PrepareQuickBattleSmoke();
        session.SetLastBattleResult(true, "instance-id-witness");
        var itemReward = session.PendingRewardChoices
            .Select((choice, index) => new { Choice = choice, Index = index })
            .FirstOrDefault(candidate => candidate.Choice.Kind == RewardChoiceKind.Item);
        Assert.That(itemReward, Is.Not.Null, "quick-battle production reward surface에 item 선택지가 필요하다.");
        Assert.That(session.ApplyRewardChoice(itemReward!.Index), Is.True);
        session.ReturnToTownAfterReward();

        var mintedItems = session.Profile.Inventory
            .Where(item => !initialItemIds.Contains(item.ItemInstanceId))
            .ToList();
        Assert.That(mintedItems, Is.Not.Empty,
            "quick-battle item reward가 SessionInventoryItemBuilder production mint path를 밟아야 한다.");

        session.Profile.Currencies.Echo = 10_000;
        var refitItemId = string.Empty;
        var refitSlotIndex = -1;
        var beforeAffix = string.Empty;
        var afterAffix = string.Empty;
        foreach (var item in mintedItems.OrderBy(item => item.ItemInstanceId, StringComparer.Ordinal))
        {
            for (var slotIndex = 0; slotIndex < item.AffixIds.Count; slotIndex++)
            {
                var candidateBefore = item.AffixIds[slotIndex];
                var refit = session.RefitItem(item.ItemInstanceId, slotIndex);
                if (!refit.IsSuccess)
                {
                    continue;
                }

                refitItemId = item.ItemInstanceId;
                refitSlotIndex = slotIndex;
                beforeAffix = candidateBefore;
                afterAffix = item.AffixIds[slotIndex];
                break;
            }

            if (refitSlotIndex >= 0)
            {
                break;
            }
        }

        Assert.That(refitSlotIndex, Is.GreaterThanOrEqualTo(0),
            "minted production item 중 RefitItem이 성공하는 affix가 필요하다.");
        Assert.That(afterAffix, Is.Not.EqualTo(beforeAffix),
            "witness는 실제로 refit된 결과 affix를 비교해야 한다.");

        var campaign = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink(),
            PlaythroughBattleResolution.AutoResolve).Run();
        Assert.That(campaign.SiteObservations, Is.Not.Empty);

        var campaignSignature = string.Join(
            "\n",
            campaign.SiteObservations.Select(site =>
                $"{site.ChapterId}|{site.SiteId}|{site.ChosenRewardKind}|{site.ChosenRewardIndex}|{site.RewardLedgerDelta}"));
        var refitSignature = $"{refitItemId}\n{refitSlotIndex}\n{afterAffix}";
        return new InstanceIdWitnessSnapshot(
            new[] { firstMintedHero.HeroId, replacementHero.HeroId },
            mintedItems.Select(item => item.ItemInstanceId).ToArray(),
            refitSignature,
            campaignSignature,
            session.Profile.HeroInstanceCounter,
            session.Profile.ItemInstanceCounter);
    }

    private static void AssertUtf8Equal(string expected, string actual, string message)
    {
        Assert.That(Encoding.UTF8.GetBytes(actual), Is.EqualTo(Encoding.UTF8.GetBytes(expected)), message);
    }

    private static void AssertSafeDecimalSequence(string id, string marker)
    {
        var markerIndex = id.LastIndexOf(marker, StringComparison.Ordinal);
        Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0), $"id marker가 없다: {id}");
        var sequence = id[(markerIndex + marker.Length)..];
        Assert.That(sequence, Is.Not.Empty, $"id sequence가 비었다: {id}");
        Assert.That(sequence.All(character => character >= '0' && character <= '9'), Is.True,
            $"id sequence는 예약 문자 없이 ASCII decimal digit만 사용해야 한다: {id}");
    }

    private readonly record struct CampaignRunSnapshot(
        string OutcomeSignature,
        string TerminationSignature,
        string ProgressionSignature,
        int BattleCount);

    private readonly record struct InstanceIdWitnessSnapshot(
        IReadOnlyList<string> MintedHeroIds,
        IReadOnlyList<string> MintedItemIds,
        string RefitSignature,
        string CampaignSignature,
        long HeroCounter,
        long ItemCounter);
}
