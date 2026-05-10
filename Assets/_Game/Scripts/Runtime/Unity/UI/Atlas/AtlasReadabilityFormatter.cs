using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Unity.UI.Atlas;

public static class AtlasReadabilityFormatter
{
    public static string FormatRegionTitle(string title)
    {
        return title switch
        {
            "Wolfpine Sigil Graybox" => "이리솔 숲길 각인 그레이박스",
            _ => HumanizeToken(title),
        };
    }

    public static string FormatNodeLabel(string label)
    {
        return label switch
        {
            "North Spur" => "북쪽 능선",
            "Fang Skirmish" => "송곳니 접전",
            "Moss Shelf" => "이끼 턱",
            "West Sigil Anchor" => "서쪽 각인석",
            "Broken Trail" => "부서진 길",
            "Buried Oath" => "묻힌 맹세",
            "Ridge Ambush" => "능선 매복",
            "Dry Wash" => "마른 물길",
            "Relic Cache" => "유물 보관함",
            "Glass Elk Elite" => "유리뿔 정예",
            "Center Sigil Anchor" => "중앙 각인석",
            "Fen Edge" => "늪 가장자리",
            "East Sigil Anchor" => "동쪽 각인석",
            "Cairn Step" => "돌무덤 계단",
            "Ashen Gate Boss" => "잿빛 관문 우두머리",
            "Old Road" => "옛길",
            "Watch Stone" => "감시석",
            "Dawn Extract" => "새벽 탈출로",
            "South Spur" => "남쪽 능선",
            _ => HumanizeToken(label),
        };
    }

    public static string FormatCharacterName(string characterId, string fallback)
    {
        return characterId switch
        {
            "hero_dawn_priest" => "단린 / Dawn Priest",
            "hero_pack_raider" => "이빨바람 / Pack Raider",
            "hero_echo_savant" => "공한 / Echo Savant",
            "hero_grave_hexer" => "묵향 / Grave Hexer",
            "hero_iron_warden" => "철위 / Iron Warden",
            "hero_trail_scout" => "숲살이 / Trail Scout",
            "hero_ash_cartographer" => "숲살이 / Trail Scout",
            _ => HumanizeToken(fallback),
        };
    }

    public static string FormatSigilName(string displayName)
    {
        return displayName switch
        {
            "Beast Spoils" => "야수 전리 각인",
            "Flank Pressure" => "측면 압박 각인",
            "Ruin Scholar" => "폐허 학자 각인",
            "Elite Relic" => "정예 유물 각인",
            "Marked Boss" => "표식 우두머리 각인",
            "Forest Guide" => "숲길 안내 각인",
            _ => HumanizeToken(displayName),
        };
    }

    public static string FormatSigilCardLabel(AtlasSigilDefinition definition)
    {
        var displayName = FormatSigilName(definition.DisplayName);
        var category = definition.SigilCategory switch
        {
            AtlasModifierCategory.RewardBias => "보상 군집",
            AtlasModifierCategory.ThreatPressure => "위험 통로",
            AtlasModifierCategory.AffinityBoost => "정찰 부채꼴",
            _ => FormatModifierCategory(definition.SigilCategory),
        };
        var variant = definition.FootprintProfileId switch
        {
            "RewardBias.Cluster.Dense" => "좁고 강함",
            "RewardBias.Cluster.Wide" => "넓고 약함",
            "ThreatPressure.Lane.Hard" => "짧고 강함",
            "ThreatPressure.Lane.Long" => "길고 약함",
            "AffinityBoost.ScoutArc.Deep" => "깊게 읽음",
            "AffinityBoost.ScoutArc.Wide" => "넓게 읽음",
            _ => "기본형",
        };
        return $"{displayName} — {category} ({variant})";
    }

    public static string FormatSigilTradeoffSummary(AtlasSigilDefinition definition)
    {
        return definition.FootprintProfileId switch
        {
            "RewardBias.Cluster.Dense" => "좁은 보상 pocket / 강한 밀도",
            "RewardBias.Cluster.Wide" => "넓은 보상 pocket / 약한 밀도",
            "ThreatPressure.Lane.Hard" => "짧은 위험 통로 / 강한 압박",
            "ThreatPressure.Lane.Long" => "긴 위험 통로 / 약한 압박",
            "AffinityBoost.ScoutArc.Deep" => "깊은 정찰 부채꼴 / 좁은 판독",
            "AffinityBoost.ScoutArc.Wide" => "넓은 정찰 부채꼴 / 얕은 판독",
            _ => $"{FormatModifierCategory(definition.SigilCategory)} trade-off",
        };
    }

    public static string FormatModifierCategory(AtlasModifierCategory category)
    {
        return category switch
        {
            AtlasModifierCategory.RewardBias => "보상 가중",
            AtlasModifierCategory.ThreatPressure => "위협 압력",
            AtlasModifierCategory.AffinityBoost => "인연 보정",
            _ => category.ToString(),
        };
    }

    public static string FormatModifierChipLabel(AtlasModifierCategory category, int percent)
    {
        var prefix = category switch
        {
            AtlasModifierCategory.RewardBias => "보상",
            AtlasModifierCategory.ThreatPressure => "위험",
            AtlasModifierCategory.AffinityBoost => "인연",
            _ => FormatModifierCategory(category),
        };
        return $"{prefix} +{percent.ToString(CultureInfo.InvariantCulture)}%";
    }

    public static string FormatModifierLabel(string label)
    {
        return label switch
        {
            "Beast trophy reward" => "야수 전리 보상",
            "Flank threat" => "측면 위협",
            "Ruin affinity" => "폐허 인연",
            "Relic reward" => "유물 보상",
            "Marked pressure" => "표식 압박",
            "Forest affinity" => "숲길 인연",
            _ => HumanizeToken(label),
        };
    }

    public static string FormatEnemyPreview(string value)
    {
        return value switch
        {
            "wolf scout trace" => "늑대 정찰 흔적",
            "beast skirmish / weakside dive" => "야수 접전 / 약측 다이브",
            "low pressure path" => "낮은 압박 경로",
            "anchor slot" => "각인석 슬롯",
            "beast patrol" => "야수 순찰",
            "event clue / oath memory" => "사건 단서 / 맹세 기억",
            "beast skirmish / flank pressure" => "야수 접전 / 측면 압박",
            "safe scout line" => "안전 정찰선",
            "reward cache / no combat" => "보상 보관함 / 전투 없음",
            "elite beast / guard break" => "정예 야수 / 전열 붕괴",
            "swarm signs" => "무리 흔적",
            "ruin ward" => "폐허 수호",
            "boss / marked cleave pressure" => "우두머리 / 표식 광역 압박",
            "standard patrol" => "일반 순찰",
            "distant warning" => "원거리 경고",
            "extract after boss" => "우두머리 후 탈출",
            _ => HumanizeToken(value),
        };
    }

    public static string FormatRewardFamily(string value)
    {
        return value switch
        {
            "fang" => "송곳니",
            "beast trophy" => "야수 전리품",
            "herb" => "약초",
            "sigil" => "각인석",
            "ore" => "광석",
            "bond token" => "인연 토큰",
            "standard cache" => "일반 보관함",
            "relic metal" => "유물 금속",
            "elite relic" => "정예 유물",
            "boss dossier" => "우두머리 기록",
            "coin" => "주화",
            "scout report" => "정찰 보고서",
            "region pin" => "지역 표식",
            "relic" => "유물",
            _ => HumanizeToken(value),
        };
    }

    public static string FormatRewardFamilyShort(string value)
    {
        var normalized = (value ?? string.Empty).ToLowerInvariant();
        if (Contains(normalized, "beast") || Contains(normalized, "fang"))
        {
            return "야수";
        }

        if (Contains(normalized, "relic") || Contains(normalized, "ore"))
        {
            return "유물";
        }

        if (Contains(normalized, "herb"))
        {
            return "약초";
        }

        if (Contains(normalized, "oath") || Contains(normalized, "bond"))
        {
            return "맹세";
        }

        if (Contains(normalized, "boss") || Contains(normalized, "dossier"))
        {
            return "기록";
        }

        if (Contains(normalized, "scout") || Contains(normalized, "region"))
        {
            return "정찰";
        }

        if (Contains(normalized, "sigil"))
        {
            return "각인";
        }

        return "보상";
    }

    public static string FormatAnswerLane(string lane)
    {
        return lane switch
        {
            "peel_anti_dive" => "측면 차단·다이브 방어",
            "guard_anchor" => "전열 고정·보호",
            "break_formation" => "진형 붕괴",
            "cleanse_mark" => "표식 해제·정화",
            "anti_mark_cleanse" => "표식 해제·정화",
            "route_read" => "경로 판독",
            _ when string.IsNullOrWhiteSpace(lane) => "유연 대응",
            _ => HumanizeToken(lane),
        };
    }

    public static string FormatRole(string role)
    {
        return role switch
        {
            "support cleanse" => "지원·정화",
            "bruiser anti-dive" => "근접 압박·다이브 방어",
            "carry route reader" => "주력 딜러·경로 판독",
            "control breaker" => "제어·진형 붕괴",
            "guard anchor" => "전열 고정",
            "scout pathing" => "정찰·경로 설계",
            _ => HumanizeToken(role),
        };
    }

    public static string FormatRecommendationReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "유연 선택";
        }

        return string.Join(", ", reason.Split(',')
            .Select(part => FormatReasonToken(part.Trim()))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.Ordinal));
    }

    public static string BuildJudgementLine(AtlasRegionNode node, AtlasNodeModifierStack stack)
    {
        var risk = ResolveDifficultyLabel(node, stack);
        var reward = stack.RewardBiasPercent > 0
            ? $"보상 +{stack.RewardBiasPercent.ToString(CultureInfo.InvariantCulture)}%"
            : "기본 보상";
        var answer = FormatAnswerLane(node.AnswerLane);
        return $"{risk} / {reward} / {answer} 권장";
    }

    public static string BuildRewardPreview(AtlasRegionNode node, AtlasNodeModifierStack stack)
    {
        var family = FormatRewardFamily(node.RewardFamily);
        return stack.RewardBiasPercent > 0
            ? $"{family} 계열 보상에 보상 가중 +{stack.RewardBiasPercent.ToString(CultureInfo.InvariantCulture)}%"
            : $"{family} 계열 기본 보상";
    }

    public static AtlasHexBadgeViewState BuildTypeBadge(AtlasNodeKind kind)
    {
        var full = FormatNodeKind(kind);
        var label = kind switch
        {
            AtlasNodeKind.Skirmish => "전투",
            AtlasNodeKind.Elite => "정예",
            AtlasNodeKind.Boss => "우두",
            AtlasNodeKind.Extract => "탈출",
            AtlasNodeKind.Reward => "보상",
            AtlasNodeKind.Event => "사건",
            AtlasNodeKind.SigilAnchor => "각인",
            AtlasNodeKind.Cache => "보관",
            AtlasNodeKind.ScoutVantage => "정찰",
            AtlasNodeKind.Echo => "메아",
            _ => "일반",
        };
        return new AtlasHexBadgeViewState(label, $"노드 타입: {full}", "atlas-chip--type");
    }

    public static AtlasHexBadgeViewState BuildRewardBadge(string rewardFamily)
    {
        var label = FormatRewardFamilyShort(rewardFamily);
        return new AtlasHexBadgeViewState(label, $"보상 계열: {FormatRewardFamily(rewardFamily)}", "atlas-chip--reward-family");
    }

    public static AtlasHexBadgeViewState BuildDifficultyBadge(AtlasRegionNode node, AtlasNodeModifierStack? stack)
    {
        var label = ResolveDifficultyLabel(node, stack);
        var cssClass = label switch
        {
            "고위험" => "atlas-chip--difficulty-high",
            "위험" => "atlas-chip--difficulty-risk",
            _ => "atlas-chip--difficulty-safe",
        };
        return new AtlasHexBadgeViewState(label, $"난이도: {label}", cssClass);
    }

    public static string FormatBoundaryNote()
    {
        return "각인은 노드의 보상 가중·위협 압력·인연 보정만 바꾸며, 전투 보상 이름표를 직접 만들지 않습니다.";
    }

    public static string FormatSpineStageLabel(int stageIndex)
    {
        return stageIndex switch
        {
            1 => "진입",
            2 => "교전",
            3 => "단서",
            4 => "보스",
            5 => "추출",
            _ => $"Stage {stageIndex.ToString(CultureInfo.InvariantCulture)}",
        };
    }

    public static string BuildCandidateSummary(AtlasRegionNode node, AtlasNodeModifierStack? stack)
    {
        var reward = (stack?.RewardBiasPercent ?? 0) > 0 ? $"보상 +{stack!.RewardBiasPercent.ToString(CultureInfo.InvariantCulture)}%" : "기본 보상";
        var threat = (stack?.ThreatPressurePercent ?? 0) > 0 ? $"위험 +{stack!.ThreatPressurePercent.ToString(CultureInfo.InvariantCulture)}%" : "위험 낮음";
        return $"{FormatNodeKind(node.Kind)} / {reward} / {threat}";
    }

    public static string BuildPlacementSummary(
        IReadOnlyList<AtlasPlacedSigil> placements,
        IReadOnlyList<AtlasSigilDefinition> sigilPool,
        IReadOnlyList<SigilAnchorSlot> anchorSlots,
        AtlasNodeModifierStack? selectedStack)
    {
        var anchorsById = anchorSlots.ToDictionary(slot => slot.AnchorId, StringComparer.Ordinal);
        var placed = placements
            .OrderBy(placement => placement.SigilId, StringComparer.Ordinal)
            .ThenBy(placement => placement.AnchorId, StringComparer.Ordinal)
            .Select(placement =>
            {
                var sigil = sigilPool.FirstOrDefault(candidate => string.Equals(candidate.SigilId, placement.SigilId, StringComparison.Ordinal));
                var anchor = anchorsById.TryGetValue(placement.AnchorId, out var slot) ? FormatAnchorRole(slot) : FormatCoordinate(placement.AnchorHex);
                return $"{FormatSigilName(sigil?.DisplayName ?? placement.SigilId)}@{anchor}";
            });
        var affecting = selectedStack == null || selectedStack.Influences.Count == 0
            ? "없음"
            : string.Join(", ", selectedStack.Influences
                .OrderBy(influence => influence.SigilId, StringComparer.Ordinal)
                .Select(influence => FormatSigilName(influence.DisplayName))
                .Distinct(StringComparer.Ordinal));
        return $"각인 배치 {placements.Count.ToString(CultureInfo.InvariantCulture)}/2: {string.Join(" | ", placed)}. 선택 노드 영향: {affecting}.";
    }

    public static string FormatAnchorRole(SigilAnchorSlot slot)
    {
        var role = slot.AnchorRole switch
        {
            "Approach" => "진입 앵커",
            "Pressure" => "압박 앵커",
            "Evidence" => "단서 앵커",
            _ => HumanizeToken(slot.AnchorRole),
        };
        return $"{role}/{slot.StageBand}";
    }

    public static string FormatCoordinate(AtlasHexCoordinate hex)
    {
        return $"q={hex.Q.ToString(CultureInfo.InvariantCulture)},r={hex.R.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string ResolveDifficultyLabel(AtlasRegionNode node, AtlasNodeModifierStack? stack)
    {
        var threat = stack?.ThreatPressurePercent ?? 0;
        if (threat >= 32 || node.Kind == AtlasNodeKind.Boss)
        {
            return "고위험";
        }

        return threat >= 15 || node.Kind == AtlasNodeKind.Elite ? "위험" : "안전";
    }

    private static string FormatNodeKind(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Skirmish => "전투",
            AtlasNodeKind.Elite => "정예",
            AtlasNodeKind.Boss => "우두머리",
            AtlasNodeKind.Extract => "탈출",
            AtlasNodeKind.Reward => "보상",
            AtlasNodeKind.Event => "사건",
            AtlasNodeKind.SigilAnchor => "각인 슬롯",
            AtlasNodeKind.Cache => "보관함",
            AtlasNodeKind.ScoutVantage => "정찰 지점",
            AtlasNodeKind.Echo => "메아리",
            _ => "일반",
        };
    }

    private static string FormatReasonToken(string token)
    {
        return token switch
        {
            "answer lane" => "해답 축 일치",
            "sigil affinity" => "각인 인연 보정",
            "boss anchor" => "우두머리 고정 역할",
            "flex pick" => "유연 선택",
            "beast" => "야수",
            "fang" => "송곳니",
            "trail" => "숲길",
            "oath" => "맹세",
            "herb" => "약초",
            "bond" => "인연",
            "relic" => "유물",
            "scout" => "정찰",
            "glass" => "유리",
            "ruin" => "폐허",
            "boss" => "우두머리",
            "gate" => "관문",
            "elite" => "정예",
            "road" => "길",
            "region" => "지역",
            _ => HumanizeToken(token),
        };
    }

    private static string HumanizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('_', ' ').Trim();
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, StringComparison.Ordinal) >= 0;
    }
}
