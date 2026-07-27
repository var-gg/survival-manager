using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta;
using SM.Meta.Model;

namespace SM.Unity;

/// <summary>
/// RuntimeCombatContentLookup의 소비측 추상화.
/// GameSessionState, ContentTextResolver 등 핵심 로직은 이 인터페이스에만 의존한다.
/// production 구현(RuntimeCombatContentLookup)은 기본적으로 Resources runtime path만 사용하고,
/// editor recovery fallback은 명시적으로 opt-in한 진단 경로에서만 허용한다.
/// 테스트에서는 FakeCombatContentLookup으로 교체하여 Unity asset-pipeline 호출을 피한다.
/// </summary>
public interface ICombatContentLookup : ISessionContentLookup
{
    IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions { get; }

    // ── Single-definition lookup ──

    bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype);
    bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item);
    bool TryGetRaceDefinition(string raceId, out RaceDefinition race);
    bool TryGetClassDefinition(string classId, out ClassDefinition @class);
    bool TryGetCharacterDefinition(string characterId, out CharacterDefinition character);
    bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment);
    bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill);
    bool TryGetAffixDefinition(string affixId, out AffixDefinition affix);
    bool TryGetPassiveBoardDefinition(string boardId, out PassiveBoardDefinition board);
    bool TryGetPassiveNodeDefinition(string nodeId, out PassiveNodeDefinition node);
    bool TryGetTeamTacticDefinition(string teamTacticId, out TeamTacticDefinition teamTactic);
    bool TryGetSynergyDefinition(string synergyId, out SynergyDefinition synergy);
    bool TryGetRoleInstructionDefinition(string roleInstructionId, out RoleInstructionDefinition roleInstruction);
    bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter);
    bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site);
    bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter);
    bool TryGetSiteEventChoiceIconId(string eventId, string choiceId, out string iconId);

    // ── Trait ──

    bool TryGetTraitEntry(string archetypeId, string traitId, out TraitEntry trait);

    // ── Ordered collections ──

    IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters();
}
