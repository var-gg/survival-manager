using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.Inspectors;

internal readonly record struct CombatSandboxAxisPreview(
    bool IsResolved,
    string Warning,
    string CharacterId,
    string CharacterName,
    string ArchetypeId,
    string ArchetypeName,
    string RaceId,
    string RaceName,
    string ClassId,
    string ClassName,
    string RoleInstructionId,
    string RoleName,
    string RoleFamilyName,
    DeploymentAnchorId Anchor)
{
    internal string BuildSummary()
    {
        if (!IsResolved)
        {
            return Warning;
        }

        var builder = new StringBuilder();
        AppendLine(builder, "ui.battle.axis.character", "캐릭터", "Character", CharacterName, CharacterId);
        AppendLine(builder, "ui.battle.axis.archetype", "전투 원형", "Archetype", ArchetypeName, ArchetypeId);
        AppendLine(builder, "ui.battle.axis.race", "종족", "Race", RaceName, RaceId);
        AppendLine(builder, "ui.battle.axis.class", "직업", "Class", ClassName, ClassId);
        AppendLine(builder, "ui.battle.axis.role", "역할", "Role", RoleName, RoleInstructionId);
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.role_family", "역할군", "Role Family")}: {RoleFamilyName}");
        builder.Append($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.anchor", "배치", "Anchor")}: {EditorLocalizedTextResolver.GetAnchorName(Anchor)}");
        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, string key, string ko, string en, string value, string id)
    {
        var formatted = string.IsNullOrWhiteSpace(id) ? value : $"{value} [{id}]";
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi(key, ko, en)}: {formatted}");
    }
}

internal static class CombatSandboxPreviewResolver
{
    private sealed class PreviewContext
    {
        public PreviewContext()
        {
            Lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
            Session = new GameSessionState(Lookup);
            Session.BindProfile(new SaveProfile());
        }

        public RuntimeCombatContentLookup Lookup { get; }
        public GameSessionState Session { get; }
    }

    private static PreviewContext? _context;

    public static CombatSandboxAxisPreview ResolveAlly(CombatSandboxAllySlot slot)
    {
        try
        {
            var context = GetContext();
            var hero = context.Session.Profile.Heroes.FirstOrDefault(entry => entry.HeroId == slot.HeroId);
            if (hero == null)
            {
                return Unresolved(EditorLocalizedTextResolver.Label("영웅 ID를 찾을 수 없습니다.", "Hero id could not be resolved."), slot.Anchor);
            }

            var characterId = string.IsNullOrWhiteSpace(hero.CharacterId) ? hero.ArchetypeId : hero.CharacterId;
            context.Lookup.TryGetCharacterDefinition(characterId, out var character);
            context.Lookup.TryGetArchetype(hero.ArchetypeId, out var archetype);
            var race = character?.Race ?? archetype?.Race;
            var @class = character?.Class ?? archetype?.Class;
            var roleInstructionId = ResolveRoleInstructionId(context.Lookup, slot.RoleInstructionIdOverride, character, @class?.Id ?? hero.ClassId, slot.Anchor, archetype);
            context.Lookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction);

            return new CombatSandboxAxisPreview(
                true,
                string.Empty,
                characterId,
                EditorLocalizedTextResolver.GetCharacterName(character, characterId),
                hero.ArchetypeId,
                EditorLocalizedTextResolver.GetArchetypeName(archetype, hero.ArchetypeId),
                race?.Id ?? hero.RaceId,
                EditorLocalizedTextResolver.GetRaceName(race, hero.RaceId),
                @class?.Id ?? hero.ClassId,
                EditorLocalizedTextResolver.GetClassName(@class, hero.ClassId),
                roleInstructionId,
                EditorLocalizedTextResolver.GetRoleName(roleInstruction, roleInstruction?.RoleTag ?? string.Empty, roleInstructionId),
                EditorLocalizedTextResolver.GetRoleFamilyName(@class),
                slot.Anchor);
        }
        catch (System.Exception ex)
        {
            return Unresolved(ex.Message, slot.Anchor);
        }
    }

    public static CombatSandboxAxisPreview ResolveEnemy(CombatSandboxEnemySlot slot)
    {
        try
        {
            var context = GetContext();
            var characterId = string.IsNullOrWhiteSpace(slot.CharacterId) ? slot.ArchetypeIdOverride : slot.CharacterId;
            context.Lookup.TryGetCharacterDefinition(characterId, out var character);
            var archetypeId = !string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride)
                ? slot.ArchetypeIdOverride
                : character?.DefaultArchetype != null
                    ? character.DefaultArchetype.Id
                    : characterId;
            context.Lookup.TryGetArchetype(archetypeId, out var archetype);
            var race = character?.Race ?? archetype?.Race;
            var @class = character?.Class ?? archetype?.Class;
            var roleInstructionId = ResolveRoleInstructionId(context.Lookup, slot.RoleInstructionId, character, @class?.Id ?? string.Empty, slot.Anchor, archetype);
            context.Lookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction);

            if (string.IsNullOrWhiteSpace(characterId) && archetype == null)
            {
                return Unresolved(EditorLocalizedTextResolver.Label("캐릭터 또는 전투 원형이 비어 있습니다.", "Character or archetype is empty."), slot.Anchor);
            }

            return new CombatSandboxAxisPreview(
                true,
                string.Empty,
                characterId,
                EditorLocalizedTextResolver.GetCharacterName(character, characterId),
                archetypeId,
                EditorLocalizedTextResolver.GetArchetypeName(archetype, archetypeId),
                race?.Id ?? string.Empty,
                EditorLocalizedTextResolver.GetRaceName(race, race?.Id ?? string.Empty),
                @class?.Id ?? string.Empty,
                EditorLocalizedTextResolver.GetClassName(@class, @class?.Id ?? string.Empty),
                roleInstructionId,
                EditorLocalizedTextResolver.GetRoleName(roleInstruction, roleInstruction?.RoleTag ?? slot.RoleTag, roleInstructionId),
                EditorLocalizedTextResolver.GetRoleFamilyName(@class),
                slot.Anchor);
        }
        catch (System.Exception ex)
        {
            return Unresolved(ex.Message, slot.Anchor);
        }
    }

    private static PreviewContext GetContext()
    {
        _context ??= new PreviewContext();
        return _context;
    }

    private static CombatSandboxAxisPreview Unresolved(string warning, DeploymentAnchorId anchor)
    {
        return new CombatSandboxAxisPreview(false, warning, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, anchor);
    }

    private static string ResolveRoleInstructionId(
        ICombatContentLookup lookup,
        string overrideRoleInstructionId,
        CharacterDefinition? character,
        string classId,
        DeploymentAnchorId anchor,
        UnitArchetypeDefinition? archetype)
    {
        if (!string.IsNullOrWhiteSpace(overrideRoleInstructionId))
        {
            return overrideRoleInstructionId;
        }

        if (character?.DefaultRoleInstruction != null)
        {
            return character.DefaultRoleInstruction.Id;
        }

        if (archetype != null && !string.IsNullOrWhiteSpace(archetype.Class?.Id))
        {
            classId = archetype.Class.Id;
        }

        var fallback = ResolveDefaultRoleInstructionId(classId, anchor);
        if (lookup.TryGetRoleInstructionDefinition(fallback, out _))
        {
            return fallback;
        }

        return fallback;
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }
}
