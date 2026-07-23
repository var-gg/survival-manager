using System;
using System.Globalization;
using System.Text;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 캠페인 전투의 결정적 random stream을 campaign identity와 encounter node에 고정한다.
/// attempt 횟수나 loadout/equipment 상태는 의도적으로 입력에 포함하지 않는다.
/// </summary>
public static class CampaignEncounterSeed
{
    public const string HashVersion = "fnv1a32-v1";

    public static int FromCampaignIdentity(string campaignIdentity)
        => StablePositiveHash($"campaign|{campaignIdentity ?? string.Empty}");

    public static int Derive(int campaignSeed, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Encounter node id is required.", nameof(nodeId));
        }

        return StablePositiveHash(
            $"encounter|{campaignSeed.ToString(CultureInfo.InvariantCulture)}|{nodeId}");
    }

    public static BattleContextState Apply(BattleContextState context, int campaignSeed)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context with { BattleSeed = Derive(campaignSeed, context.EncounterId) };
    }

    private static int StablePositiveHash(string value)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            foreach (var item in Encoding.UTF8.GetBytes(value))
            {
                hash ^= item;
                hash *= prime;
            }

            var result = (int)(hash & 0x7fffffffu);
            return result == 0 ? 1 : result;
        }
    }
}
