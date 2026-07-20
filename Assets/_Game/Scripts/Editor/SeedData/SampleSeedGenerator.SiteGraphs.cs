using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    internal const string AshenGateGraphId = "site_graph_ashen_gate";
    internal const string AshenGateRiskEncounterId = "site_ashen_gate_risk_skirmish_1";
    internal const string AshenGateAidStationEventId = "site_event_collapsed_aid_station";

    [MenuItem("SM/Internal/Content/Generate Ashen Gate Branching Site")]
    public static void GenerateAshenGateBranchingSite()
    {
        EnsureFolders();
        CreateAshenGateBranchingSiteContent();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("SM Ashen Gate branching graph and risk encounter generated without reseeding unrelated content.");
    }

    private static void CreateAshenGateBranchingSiteContent()
    {
        var site = GetCampaignSiteSeeds().Single(seed =>
            string.Equals(seed.SiteId, "site_ashen_gate", StringComparison.Ordinal));
        var riskSquadId = $"{AshenGateRiskEncounterId}_squad";

        CreateEnemySquad(
            riskSquadId,
            site.FactionId,
            site.Elite,
            TeamPostureTypeValue.CollapseWeakSide,
            ThreatTierValue.Tier2,
            threatCost: 2);
        CreateEncounter(
            AshenGateRiskEncounterId,
            site.SiteId,
            site.FactionId,
            EncounterKindValue.Skirmish,
            riskSquadId,
            string.Empty,
            "reward_source_elite",
            ThreatTierValue.Tier2,
            threatCost: 2,
            threatSkulls: 2,
            difficultyBand: "site_risk",
            encounterFamilyId: site.EncounterFamilyIds[0],
            answerLaneId: site.AnswerLaneId);

        CreateAsset<SiteGraphDefinition>($"{ResourcesRoot}/SiteGraphs/{AshenGateGraphId}.asset", graph =>
        {
            graph.Id = AshenGateGraphId;
            graph.SiteId = site.SiteId;
            graph.Nodes = new List<SiteGraphNodeDefinition>
            {
                // 0..4 deliberately retain the legacy battle/extract coordinates. Appended routing
                // nodes make branching additive without changing safe-lane hashes.
                Node("site_ashen_gate_skirmish_1", 1, SiteNodeKindValue.Skirmish,
                    encounterId: "site_ashen_gate_skirmish_1", next: new[] { "site_ashen_gate_skirmish_2" }, rewardSourceId: "reward_source_skirmish"),
                Node("site_ashen_gate_skirmish_2", 2, SiteNodeKindValue.Skirmish,
                    encounterId: "site_ashen_gate_skirmish_2", next: new[] { "site_ashen_gate:event:collapsed_aid_station" }, rewardSourceId: "reward_source_skirmish"),
                Node("site_ashen_gate_elite_1", 4, SiteNodeKindValue.Elite,
                    encounterId: "site_ashen_gate_elite_1", next: new[] { "site_ashen_gate_boss_1" }, rewardSourceId: "reward_source_elite"),
                Node("site_ashen_gate_boss_1", 6, SiteNodeKindValue.Boss,
                    encounterId: "site_ashen_gate_boss_1", next: new[] { "site_ashen_gate:extract" }, rewardSourceId: "reward_source_boss"),
                Node("site_ashen_gate:extract", 7, SiteNodeKindValue.Extract,
                    rewardSourceId: "reward_source_extract"),
                Node("site_ashen_gate:entry", 0, SiteNodeKindValue.Briefing,
                    next: new[] { "site_ashen_gate_skirmish_1", AshenGateRiskEncounterId }),
                Node(AshenGateRiskEncounterId, 1, SiteNodeKindValue.Skirmish,
                    laneTag: "risk", encounterId: AshenGateRiskEncounterId,
                    next: new[] { "site_ashen_gate_skirmish_2" }, echoIncome: 2, rewardSourceId: "reward_source_elite"),
                Node("site_ashen_gate:event:collapsed_aid_station", 3, SiteNodeKindValue.Event,
                    eventId: AshenGateAidStationEventId, next: new[] { "site_ashen_gate_elite_1" },
                    rewardSourceId: "reward_source_shrine_event"),
            };
        });
    }

    private static SiteGraphNodeDefinition Node(
        string nodeId,
        int rank,
        SiteNodeKindValue kind,
        string laneTag = "safe",
        string encounterId = "",
        string eventId = "",
        IReadOnlyList<string>? next = null,
        int echoIncome = 0,
        string rewardSourceId = "")
    {
        return new SiteGraphNodeDefinition
        {
            NodeId = nodeId,
            Rank = rank,
            Kind = kind,
            LaneTag = laneTag,
            EncounterId = encounterId,
            EventId = eventId,
            NextNodeIds = (next ?? Array.Empty<string>()).ToList(),
            EchoIncome = echoIncome,
            RewardSourceId = rewardSourceId,
        };
    }
}
