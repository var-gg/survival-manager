using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

/// <summary>
/// concept intent의 formation 술어를 실제 배치(hero→anchor)와 실 역할 구성으로 정직하게 판정한다.
/// census FormationFeatureClassifier의 정적 eligibility proxy geometry를 canonical 1-per-role squad에서
/// byte-동일하게 재현하고, 역할 편향(예: 궁수 3) squad는 실 역할 다중도로 일반화한다.
/// full conjunction(" and ")과 profile 절을 모두 평가한다. 정책 asmdef는 SM.HeadlessCensus를 참조하지 않으므로
/// 이 계산은 정책 로컬이며, census 권위와의 canonical parity는 intent-track harness 재실행으로 경험 검증한다
/// (역할 편향 squad의 divergence는 policy_gap 잔여로 정직히 드러난다).
/// </summary>
internal static class PolicyFormationEvaluator
{
    private const string FortifiedLine = "fortified_line";
    private const string ForwardSpear = "forward_spear";
    private const string BaitedGap = "baited_gap";
    private const string ScreenedBackline = "screened_backline";
    private const string OpenSkirmish = "open_skirmish";

    private enum FormationRole
    {
        Tank,
        Damage,
        Ranged,
        Healer,
        Other,
    }

    internal readonly struct FormationFeatures
    {
        public FormationFeatures(
            int frontlineCount,
            int protectedSlotCount,
            double flankRearExposureScore,
            double backlineAccessibility)
        {
            FrontlineCount = frontlineCount;
            ProtectedSlotCount = protectedSlotCount;
            FlankRearExposureScore = flankRearExposureScore;
            BacklineAccessibility = backlineAccessibility;
        }

        public int FrontlineCount { get; }
        public int ProtectedSlotCount { get; }
        public double FlankRearExposureScore { get; }
        public double BacklineAccessibility { get; }
    }

    public static bool IsFormationPredicate(string predicate)
        => !string.IsNullOrEmpty(predicate) && predicate.StartsWith("formation.", StringComparison.Ordinal);

    public static bool Satisfies(
        string predicate,
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<HeadlessPlacement> placements)
        => Satisfies(predicate, Classify(heroes, placements));

    internal static bool Satisfies(string predicate, FormationFeatures features)
    {
        var clauses = ParseClauses(predicate);
        if (clauses.Count == 0)
        {
            return false;
        }

        foreach (var clause in clauses)
        {
            if (clause.Profile != null)
            {
                if (!string.Equals(ClassifyProfile(features), clause.Profile, StringComparison.Ordinal))
                {
                    return false;
                }

                continue;
            }

            var actual = clause.Key switch
            {
                "formation.frontline_count" => features.FrontlineCount,
                "formation.protected_slot_count" => features.ProtectedSlotCount,
                "formation.flank_rear_exposure_score" => features.FlankRearExposureScore,
                "formation.backline_accessibility" => features.BacklineAccessibility,
                _ => double.NaN,
            };
            if (double.IsNaN(actual) || !Compare(actual, clause.Operator, clause.Expected))
            {
                return false;
            }
        }

        return true;
    }

    internal static FormationFeatures Classify(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<HeadlessPlacement> placements)
    {
        var roleByHero = new Dictionary<string, FormationRole>(StringComparer.Ordinal);
        foreach (var hero in heroes ?? Array.Empty<HeadlessHeroObservation>())
        {
            if (hero != null && !string.IsNullOrWhiteSpace(hero.HeroId))
            {
                roleByHero[hero.HeroId] = RoleOf(hero.ClassId);
            }
        }

        var deployed = (placements ?? Array.Empty<HeadlessPlacement>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.HeroId))
            .ToArray();
        var occupied = deployed.Select(value => value.Anchor).ToHashSet();
        var frontlineCount = deployed.Count(value => value.Anchor.IsFrontRow());
        var protectedSlots = deployed.Count(value =>
            value.Anchor.IsBackRow() && occupied.Contains(ToFrontAnchor(value.Anchor)));

        var exposure = 0d;
        foreach (var placement in deployed)
        {
            var weight = RoleExposureWeight(Role(roleByHero, placement.HeroId));
            exposure += AdjacentSameRow(placement.Anchor).Count(adjacent => !occupied.Contains(adjacent)) * weight;
            if (placement.Anchor.IsBackRow() && !occupied.Contains(ToFrontAnchor(placement.Anchor)))
            {
                exposure += weight * 2d;
            }
        }

        var accessibilityScore = 0d;
        var accessibilityCount = 0;
        foreach (var placement in deployed)
        {
            var role = Role(roleByHero, placement.HeroId);
            if (role != FormationRole.Ranged && role != FormationRole.Healer)
            {
                continue;
            }

            accessibilityCount++;
            if (placement.Anchor.IsFrontRow())
            {
                accessibilityScore += 1d;
                continue;
            }

            var front = ToFrontAnchor(placement.Anchor);
            if (!occupied.Contains(front))
            {
                accessibilityScore += 1d;
                continue;
            }

            if (AdjacentSameRow(front).Any(adjacent => adjacent.IsFrontRow() && !occupied.Contains(adjacent)))
            {
                accessibilityScore += 0.5d;
            }
        }

        var backlineAccessibility = accessibilityCount == 0 ? 0d : accessibilityScore / accessibilityCount;
        return new FormationFeatures(frontlineCount, protectedSlots, Round(exposure), Round(backlineAccessibility));
    }

    internal static string ClassifyProfile(FormationFeatures features)
    {
        if (features.ProtectedSlotCount >= 2 && features.BacklineAccessibility <= 0.75d)
        {
            return FortifiedLine;
        }

        if (features.FrontlineCount >= 3 && features.BacklineAccessibility >= 0.75d)
        {
            return ForwardSpear;
        }

        if (features.FlankRearExposureScore >= 4d)
        {
            return BaitedGap;
        }

        if (features.ProtectedSlotCount >= 1 && features.FrontlineCount >= 2)
        {
            return ScreenedBackline;
        }

        return OpenSkirmish;
    }

    private static FormationRole Role(IReadOnlyDictionary<string, FormationRole> roleByHero, string heroId)
        => roleByHero.TryGetValue(heroId, out var role) ? role : FormationRole.Other;

    private static FormationRole RoleOf(string classId)
        => classId switch
        {
            "vanguard" => FormationRole.Tank,
            "duelist" => FormationRole.Damage,
            "ranger" => FormationRole.Ranged,
            "mystic" => FormationRole.Healer,
            _ => FormationRole.Other,
        };

    private static double RoleExposureWeight(FormationRole role)
        => role switch
        {
            FormationRole.Tank => 0.5d,
            FormationRole.Damage => 0.75d,
            FormationRole.Ranged => 1d,
            FormationRole.Healer => 1.25d,
            _ => 1d,
        };

    private static IEnumerable<DeploymentAnchorId> AdjacentSameRow(DeploymentAnchorId anchor)
    {
        var rowOffset = anchor.IsFrontRow() ? 0 : 3;
        var lane = (int)anchor - rowOffset;
        if (lane > 0)
        {
            yield return (DeploymentAnchorId)(rowOffset + lane - 1);
        }

        if (lane < 2)
        {
            yield return (DeploymentAnchorId)(rowOffset + lane + 1);
        }
    }

    private static DeploymentAnchorId ToFrontAnchor(DeploymentAnchorId backAnchor)
        => backAnchor switch
        {
            DeploymentAnchorId.BackTop => DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.BackCenter => DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.BackBottom => DeploymentAnchorId.FrontBottom,
            _ => backAnchor,
        };

    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static bool Compare(double actual, string op, double expected)
        => op switch
        {
            ">=" => actual >= expected,
            "<=" => actual <= expected,
            _ => false,
        };

    private static IReadOnlyList<Clause> ParseClauses(string predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            return Array.Empty<Clause>();
        }

        var parts = predicate.Split(new[] { " and " }, StringSplitOptions.None);
        var clauses = new List<Clause>(parts.Length);
        foreach (var raw in parts)
        {
            var value = raw.Trim();
            const string profilePrefix = "formation.profile=";
            if (value.StartsWith(profilePrefix, StringComparison.Ordinal))
            {
                clauses.Add(Clause.ForProfile(value.Substring(profilePrefix.Length)));
                continue;
            }

            var op = value.Contains(">=", StringComparison.Ordinal) ? ">="
                : value.Contains("<=", StringComparison.Ordinal) ? "<="
                : null;
            if (op == null)
            {
                return Array.Empty<Clause>();
            }

            var index = value.IndexOf(op, StringComparison.Ordinal);
            var key = value.Substring(0, index).Trim();
            var expectedText = value.Substring(index + op.Length).Trim();
            if (!double.TryParse(expectedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
            {
                return Array.Empty<Clause>();
            }

            clauses.Add(Clause.ForComparison(key, op, expected));
        }

        return clauses;
    }

    private readonly struct Clause
    {
        private Clause(string profile, string key, string op, double expected)
        {
            Profile = profile;
            Key = key;
            Operator = op;
            Expected = expected;
        }

        public string Profile { get; }
        public string Key { get; }
        public string Operator { get; }
        public double Expected { get; }

        public static Clause ForProfile(string profile) => new(profile, null, null, 0d);

        public static Clause ForComparison(string key, string op, double expected) => new(null, key, op, expected);
    }
}
