using System;
using System.Collections.Generic;

namespace SM.Content.Definitions
{

    public static class RoleGlossary
    {
        private static readonly IReadOnlyDictionary<string, string> RoleFamilyByClass = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vanguard"] = "vanguard",
            ["duelist"] = "striker",
            ["ranger"] = "ranger",
            ["mystic"] = "mystic",
        };

        private static readonly IReadOnlyDictionary<string, (string Korean, string English)> RoleTagLabels = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["anchor"] = ("고정축", "Anchor"),
            ["bruiser"] = ("돌격", "Bruiser"),
            ["carry"] = ("딜핵심", "Carry"),
            ["support"] = ("지원", "Support"),
            ["frontline"] = ("전열", "Frontline"),
            ["backline"] = ("후열", "Backline"),
            ["auto"] = ("자동", "Auto"),
        };

        private static readonly IReadOnlyDictionary<string, (string Korean, string English)> RoleFamilyLabels = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["vanguard"] = ("선봉군", "Vanguard"),
            ["striker"] = ("타격군", "Striker"),
            ["ranger"] = ("사격군", "Ranger"),
            ["mystic"] = ("비전군", "Mystic"),
        };

        public static bool TryGetRoleFamilyTagForClass(string classId, out string roleFamilyTag)
        {
            return RoleFamilyByClass.TryGetValue(classId ?? string.Empty, out roleFamilyTag!);
        }

        public static string GetRoleFamilyTagOrDefault(string classId)
        {
            return TryGetRoleFamilyTagForClass(classId, out var roleFamilyTag)
                ? roleFamilyTag
                : string.IsNullOrWhiteSpace(classId)
                    ? "unknown"
                    : classId;
        }

        public static string GetLocalizedRoleTagFallback(string roleTag, string? localeCode)
        {
            if (RoleTagLabels.TryGetValue(roleTag ?? string.Empty, out var label))
            {
                return IsKorean(localeCode) ? label.Korean : label.English;
            }

            return string.IsNullOrWhiteSpace(roleTag) ? "Unknown" : roleTag;
        }

        public static string GetLocalizedRoleFamilyFallback(string roleFamilyTag, string? localeCode)
        {
            if (RoleFamilyLabels.TryGetValue(roleFamilyTag ?? string.Empty, out var label))
            {
                return IsKorean(localeCode) ? label.Korean : label.English;
            }

            return string.IsNullOrWhiteSpace(roleFamilyTag) ? "Unknown" : roleFamilyTag;
        }

        private static bool IsKorean(string? localeCode)
        {
            return string.Equals(localeCode, "ko", StringComparison.OrdinalIgnoreCase);
        }
    }
}
