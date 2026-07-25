using SM.Content.Definitions;
using SM.Unity;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    private static void CreateDecisionAffixes()
    {
        var affixes = new[]
        {
            new DecisionAffixText("affix_reckless_edge", "무모한 칼끝", "Reckless Edge", "물리 출력이 오르지만 방어가 내려갑니다.", "Gain physical power, but lose armor."),
            new DecisionAffixText("affix_brittle_focus", "깨지기 쉬운 초점", "Brittle Focus", "마법 출력이 오르지만 저항이 내려갑니다.", "Gain magical power, but lose resistance."),
            new DecisionAffixText("affix_overclocked", "과부하", "Overclocked", "공격 속도가 오르지만 최대 생명력이 줄어듭니다.", "Attack faster, but lose maximum health."),
            new DecisionAffixText("affix_blood_price", "피의 대가", "Blood Price", "흡혈을 얻지만 최대 생명력이 줄어듭니다.", "Gain lifesteal, but lose maximum health."),
            new DecisionAffixText("affix_lightfooted_plate", "경량 판금", "Lightfooted Plate", "이동 속도가 오르지만 방어가 내려갑니다.", "Move faster, but lose armor."),
            new DecisionAffixText("affix_burdened_reach", "무거운 조준", "Burdened Reach", "공격 사거리가 늘지만 이동 속도가 내려갑니다.", "Gain attack range, but move more slowly."),
            new DecisionAffixText("affix_reaper_spark", "사신의 불꽃", "Reaper Spark", "적을 처치하면 에너지를 얻습니다.", "Gain energy when this unit kills an enemy."),
            new DecisionAffixText("affix_last_ward", "최후의 방벽", "Last Ward", "생명력이 50% 이하로 처음 내려가면 보호막을 얻습니다.", "Gain a barrier the first time health falls to 50% or lower."),
            new DecisionAffixText("affix_executioners_edge", "처형자의 칼날", "Executioner's Edge", "물리 출력이 오르고 생명력 35% 이하 적에게 주는 피해가 25% 늘어납니다.", "Gain physical power and deal 25% more damage to enemies at 35% health or lower."),
            new DecisionAffixText("affix_desperate_focus", "절박한 집중", "Desperate Focus", "생명력이 40% 이하로 처음 내려가면 에너지를 얻습니다.", "Gain energy the first time health falls to 40% or lower."),
            new DecisionAffixText("affix_mourning_aegis", "애도의 방패", "Mourning Aegis", "아군이 쓰러지면 보호막을 얻습니다.", "Gain a barrier when another ally falls."),
            new DecisionAffixText("affix_first_light", "첫 빛", "First Light", "전투 시작 시 보호막을 얻습니다.", "Gain a barrier at battle start."),
            new DecisionAffixText("affix_war_chorus", "전쟁의 합창", "War Chorus", "전투 시작 시 모든 전투 아군이 보호막을 얻습니다.", "Grant a barrier to all allied combatants at battle start."),
            new DecisionAffixText("affix_fallen_chorus", "쓰러진 자의 합창", "Fallen Chorus", "아군이 쓰러지면 생존 아군을 회복합니다.", "Heal surviving allied combatants when another ally falls."),
        };

        foreach (var text in affixes)
        {
            CreateAsset<AffixDefinition>($"{ResourcesRoot}/Affixes/{text.Id}.asset", affix =>
            {
                affix.Id = text.Id;
                affix.NameKey = ContentLocalizationTables.BuildAffixNameKey(text.Id);
                affix.DescriptionKey = ContentLocalizationTables.BuildAffixDescriptionKey(text.Id);
                UpsertStringEntry(ContentLocalizationTables.Affixes, affix.NameKey, text.KoreanName, text.EnglishName);
                UpsertStringEntry(ContentLocalizationTables.Affixes, affix.DescriptionKey, text.KoreanDescription, text.EnglishDescription);
            });
        }
    }

    private sealed record DecisionAffixText(
        string Id,
        string KoreanName,
        string EnglishName,
        string KoreanDescription,
        string EnglishDescription);
}
