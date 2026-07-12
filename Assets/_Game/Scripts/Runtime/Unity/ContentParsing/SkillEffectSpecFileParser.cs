using System;
using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Contracts;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

/// <summary>
/// 스킬/증강의 발동형·변조형 효과 payload(TriggeredEffects, SupportModifier) 파일 파서.
/// 폴백 파서 레인은 복구 전용이지만, 이 두 payload를 못 읽으면 발동형 패시브·서포트 젬·
/// 유령 패시브 부여 스킬이 그 레인에서만 조용히 inert가 된다(레인 간 parity 갭) — 위생 봉합.
/// 기본값 규약: 미저작/구버전 asset에서 배수류(기본 1)가 0으로 추락하지 않도록
/// 필드를 "키가 있을 때만 덮어쓰는" 방식으로 채운다(2보 ExtractFloat(fallback) 함정과 동일 축).
/// </summary>
internal static class SkillEffectSpecFileParser
{
    internal static List<TriggeredEffectSpec> ParseTriggeredEffects(string[] lines, string sectionHeader)
    {
        var result = new List<TriggeredEffectSpec>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Trigger:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var spec = new TriggeredEffectSpec
            {
                Trigger = (CombatTriggerKind)ParseInt(trimmed["- Trigger:".Length..].Trim()),
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Trigger:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Op:", StringComparison.Ordinal))
                {
                    spec.Op = (TriggeredEffectOp)ParseInt(trimmed["Op:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Scope:", StringComparison.Ordinal))
                {
                    spec.Scope = (EffectScope)ParseInt(trimmed["Scope:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Magnitude:", StringComparison.Ordinal))
                {
                    spec.Magnitude = ParseFloat(trimmed["Magnitude:".Length..].Trim());
                }
                else if (trimmed.StartsWith("ThresholdRatio:", StringComparison.Ordinal))
                {
                    spec.ThresholdRatio = ParseFloat(trimmed["ThresholdRatio:".Length..].Trim());
                }
                else if (trimmed.StartsWith("StatusId:", StringComparison.Ordinal))
                {
                    spec.StatusId = trimmed["StatusId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("DurationSeconds:", StringComparison.Ordinal))
                {
                    spec.DurationSeconds = ParseFloat(trimmed["DurationSeconds:".Length..].Trim());
                }
                else if (trimmed.StartsWith("MaxStacks:", StringComparison.Ordinal))
                {
                    spec.MaxStacks = ParseInt(trimmed["MaxStacks:".Length..].Trim());
                }
            }

            result.Add(spec);
        }

        return result;
    }

    /// <summary>
    /// SupportModifier 블록 파싱. 섹션이 없으면 null(호출부가 identity 기본값 유지) —
    /// 배수 필드는 키 매칭 시에만 덮어써 identity 기본(1)이 0으로 추락하는 함정을 구조적으로 차단.
    /// 중첩 리스트(AddedStatuses/OwnerModifiers)는 스킬 파일에서 키가 유일해 섹션 단위 재사용 파서로 위임.
    /// </summary>
    internal static SupportModifierSpec? ParseSupportModifier(string[] lines, string sectionHeader)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return null;
        }

        var spec = new SupportModifierSpec();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (GetIndent(line) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith("PowerMultiplier:", StringComparison.Ordinal))
            {
                spec.PowerMultiplier = ParseFloat(trimmed["PowerMultiplier:".Length..].Trim());
            }
            else if (trimmed.StartsWith("CooldownMultiplier:", StringComparison.Ordinal))
            {
                spec.CooldownMultiplier = ParseFloat(trimmed["CooldownMultiplier:".Length..].Trim());
            }
            else if (trimmed.StartsWith("CastWindupMultiplier:", StringComparison.Ordinal))
            {
                spec.CastWindupMultiplier = ParseFloat(trimmed["CastWindupMultiplier:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RangeBonus:", StringComparison.Ordinal))
            {
                spec.RangeBonus = ParseFloat(trimmed["RangeBonus:".Length..].Trim());
            }
            else if (trimmed.StartsWith("StatusDurationMultiplier:", StringComparison.Ordinal))
            {
                spec.StatusDurationMultiplier = ParseFloat(trimmed["StatusDurationMultiplier:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ForceCanCrit:", StringComparison.Ordinal))
            {
                spec.ForceCanCrit = ParseBool(trimmed["ForceCanCrit:".Length..].Trim());
            }
            else if (trimmed.StartsWith("GrantCleanseProfileId:", StringComparison.Ordinal))
            {
                spec.GrantCleanseProfileId = trimmed["GrantCleanseProfileId:".Length..].Trim();
            }
        }

        spec.AddedStatuses = StatusFileParser.ParseStatusApplicationRules(lines, "AddedStatuses:");
        spec.OwnerModifiers = ParseModifiers(lines, "OwnerModifiers:");
        return spec;
    }
}
