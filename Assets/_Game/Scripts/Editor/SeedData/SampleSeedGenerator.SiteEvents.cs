using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    [MenuItem("SM/Internal/Content/Generate Site Events")]
    public static void GenerateSiteEvents()
    {
        EnsureFolders();
        CreateSiteEventContent();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("SM site events generated without reseeding campaign sites.");
    }

    private static void CreateSiteEventContent()
    {
        CreateSiteEvent(
            "site_event_collapsed_aid_station",
            "site_ashen_gate",
            "영입·보급·메아리 중 하나가 즉시 바뀝니다.\n무너진 구호소 안에서 아직 살아 있는 인기척이 들립니다.",
            "One of recruitment, supplies, or Echo changes immediately.\nSomeone is still alive inside the collapsed aid station.",
            Choice("rescue", "의무병을 영입 후보로 확보한다", "Secure a medic recruit offer",
                Outcome(OutcomeKind.GrantRecruitOffer, "priest", "trait_frail")),
            Choice("take_supplies", "응급 붕대와 갑옷을 얻는다", "Take field dressings and armor",
                Outcome(OutcomeKind.GrantConsumable, "consumable_field_dressing", amount: 2),
                Outcome(OutcomeKind.GrantItem, "item_layered_armor")),
            Choice("burn_it", "메아리 6을 얻고 구호소를 태운다", "Gain 6 Echo and burn the station",
                Outcome(OutcomeKind.GrantEcho, amount: 6)));

        CreateSiteEvent(
            "site_event_road_of_corpses",
            "site_wolfpine_trail",
            "활을 얻고 매복 정예로 우회하거나, 그대로 전진합니다.\n시체가 길을 따라 화살표처럼 놓여 있습니다.",
            "Take a bow and route into an elite ambush, or advance normally.\nCorpses point down the road like an arrow.",
            Choice("recover_scout", "사냥꾼 활을 회수하고 매복 정예로 향한다", "Recover a hunter bow and route to the elite ambush",
                Outcome(OutcomeKind.GrantItem, "item_hunter_bow"),
                Outcome(OutcomeKind.RouteToNode, "node_wolfpine_ambush_elite")),
            Choice("move_on", "아무 변화 없이 표준 경로로 전진한다", "Advance on the standard route",
                Outcome(OutcomeKind.RouteToNode, "node_wolfpine_standard")));

        CreateSiteEvent(
            "site_event_deserter_quartermaster",
            "*",
            "물자·메아리·철수 보너스 또는 전상 위험이 즉시 바뀝니다.\n탈영병 보급관이 장부를 감춘 채 거래를 제안합니다.",
            "Supplies, Echo, extract bonus, or wound risk changes immediately.\nA deserter quartermaster offers a deal while hiding the ledger.",
            Choice("trade", "보급품과 소모품을 받고 철수 보너스 3을 확보한다", "Trade for supplies, a consumable, and 3 extract bonus",
                Outcome(OutcomeKind.GrantItem, "item_lucky_charm"),
                Outcome(OutcomeKind.GrantConsumable, "consumable_scout_reroll"),
                Outcome(OutcomeKind.ExtractBonus, amount: 3)),
            Choice("claim_bounty", "메아리 8을 현상금으로 받는다", "Claim an 8 Echo bounty",
                Outcome(OutcomeKind.GrantEcho, amount: 8)),
            Choice("seize", "장비 둘을 빼앗고 전열 한 명이 전상당한다", "Seize two items and wound one frontliner",
                Outcome(OutcomeKind.GrantItem, "item_iron_sword"),
                Outcome(OutcomeKind.GrantItem, "item_layered_armor"),
                Outcome(OutcomeKind.InflictWound, targetRule: OutcomeTargetRule.LowestDeployIndexFrontline)));

        CreateSiteEvent(
            "site_event_reliquary_cart",
            "site_sunken_bastion",
            "철수 메아리·우두머리 경로·장비와 전상 중 하나가 바뀝니다.\n침수된 성물 수레가 봉인된 채 길을 막고 있습니다.",
            "Extract Echo, boss route, or item-and-wound state changes.\nA sealed reliquary cart blocks the flooded passage.",
            Choice("salvage", "철수 보너스 10을 얻고 변형 우두머리로 향한다", "Gain 10 extract bonus and route to the boss variant",
                Outcome(OutcomeKind.ExtractBonus, amount: 10),
                Outcome(OutcomeKind.RouteToNode, "node_sunken_boss_variant")),
            Choice("leave", "표준 우두머리 경로로 향한다", "Route to the standard boss",
                Outcome(OutcomeKind.RouteToNode, "node_sunken_boss_standard")),
            Choice("break_seal", "성물을 얻고 전열 한 명이 전상당한다", "Take the relic and wound one frontliner",
                Outcome(OutcomeKind.GrantItem, "item_cantor_focus"),
                Outcome(OutcomeKind.InflictWound, targetRule: OutcomeTargetRule.LowestDeployIndexFrontline)));

        CreateSiteEvent(
            "site_event_hostage_standoff",
            "*",
            "추가 전투 경로 또는 메아리 10 지불 경로를 선택합니다.\n인질을 사이에 둔 대치가 길목을 막고 있습니다.",
            "Choose a bonus-fight route or pay 10 Echo for the easier deep route.\nA hostage standoff blocks the passage.",
            Choice("assault", "추가 전투로 돌입한다", "Route into the bonus fight",
                Outcome(OutcomeKind.RouteToNode, "node_hostage_bonus_fight")),
            Choice("ransom", "메아리 10을 지불하고 쉬운 심층 경로로 간다", "Pay 10 Echo and take the easier deep route",
                Outcome(OutcomeKind.GrantEcho, amount: -10),
                Outcome(OutcomeKind.RouteToNode, "node_hostage_easy_deep")),
            Choice("ignore", "아무 변화 없이 떠난다", "Leave without changing anything"));

        var memorialId = "site_event_memorial";
        CreateSiteEvent(
            memorialId,
            "*",
            "메아리 5·경험치 25·전상 치료 중 하나를 즉시 얻습니다.\n{siteId}의 오래된 추모 흔적이 발길을 붙듭니다.",
            "Gain 5 Echo, 25 experience, or cure one wound immediately.\nAn old memorial at {siteId} stops the company.",
            Choice("offer_echo", "메아리 5를 얻는다", "Gain 5 Echo",
                Outcome(OutcomeKind.GrantEcho, amount: 5)),
            Choice("study_names", "배치 서수가 가장 낮은 영웅이 경험치 25를 얻는다", "The lowest deployed hero gains 25 experience",
                Outcome(OutcomeKind.GrantExp, amount: 25, targetRule: OutcomeTargetRule.LowestDeployIndex)),
            Choice("lay_to_rest", "전상 한 명을 치료한다", "Cure one wounded hero",
                Outcome(OutcomeKind.CureWound)));

        UpsertStringEntry(ContentLocalizationTables.Campaign, $"{ContentLocalizationTables.BuildSiteEventSetupKey(memorialId)}.site_ashen_gate",
            "메아리 5·경험치 25·전상 치료 중 하나를 즉시 얻습니다.\n불탄 예배당의 이름들이 재 속에서 드러납니다.",
            "Gain 5 Echo, 25 experience, or cure one wound immediately.\nNames emerge from the ash of a burned chapel.");
        UpsertStringEntry(ContentLocalizationTables.Campaign, $"{ContentLocalizationTables.BuildSiteEventSetupKey(memorialId)}.site_wolfpine_trail",
            "메아리 5·경험치 25·전상 치료 중 하나를 즉시 얻습니다.\n늑대소나무 길의 돌무덤마다 같은 표식이 새겨져 있습니다.",
            "Gain 5 Echo, 25 experience, or cure one wound immediately.\nEvery cairn on Wolfpine Trail bears the same mark.");
        UpsertStringEntry(ContentLocalizationTables.Campaign, $"{ContentLocalizationTables.BuildSiteEventSetupKey(memorialId)}.site_sunken_bastion",
            "메아리 5·경험치 25·전상 치료 중 하나를 즉시 얻습니다.\n물에 젖은 수비대 명부가 아직 읽힙니다.",
            "Gain 5 Echo, 25 experience, or cure one wound immediately.\nThe drowned garrison register is still legible.");
    }

    private static void CreateSiteEvent(
        string id,
        string siteId,
        string koSetup,
        string enSetup,
        params SiteEventChoiceSeed[] choices)
    {
        CreateAsset<SiteEventDefinition>($"{ResourcesRoot}/SiteEvents/{id}.asset", asset =>
        {
            asset.Id = id;
            asset.SiteId = siteId;
            asset.SetupKey = id == "site_event_memorial"
                ? $"{ContentLocalizationTables.BuildSiteEventSetupKey(id)}.{{siteId}}"
                : ContentLocalizationTables.BuildSiteEventSetupKey(id);
            asset.Choices = new List<SiteEventChoiceDefinition>();
            foreach (var choice in choices)
            {
                var labelKey = ContentLocalizationTables.BuildSiteEventChoiceLabelKey(id, choice.Id);
                asset.Choices.Add(new SiteEventChoiceDefinition
                {
                    Id = choice.Id,
                    LabelKey = labelKey,
                    Outcomes = new List<SiteEventOutcomeDefinition>(choice.Outcomes),
                });
                UpsertStringEntry(ContentLocalizationTables.Campaign, labelKey, choice.KoLabel, choice.EnLabel);
            }

            UpsertStringEntry(ContentLocalizationTables.Campaign, asset.SetupKey, koSetup, enSetup);
        });
    }

    private static SiteEventChoiceSeed Choice(string id, string koLabel, string enLabel, params SiteEventOutcomeDefinition[] outcomes) =>
        new(id, koLabel, enLabel, outcomes);

    private static SiteEventOutcomeDefinition Outcome(
        OutcomeKind kind,
        string payloadId = "",
        string auxiliaryId = "",
        int amount = 0,
        OutcomeTargetRule targetRule = OutcomeTargetRule.None) =>
        new()
        {
            Kind = kind,
            PayloadId = payloadId,
            AuxiliaryId = auxiliaryId,
            Amount = amount,
            TargetRule = targetRule,
        };

    private sealed record SiteEventChoiceSeed(
        string Id,
        string KoLabel,
        string EnLabel,
        IReadOnlyList<SiteEventOutcomeDefinition> Outcomes);
}
