using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>player-visible 정보만 사용하는 deterministic campaign session 동작 집합.</summary>
internal static class H100SessionDriver
{
    private static readonly HashSet<string> FrontRowClasses = new(StringComparer.Ordinal)
    {
        "vanguard", "duelist",
    };

    public static GameSessionState CreateSession(RuntimeCombatContentLookup lookup, string profileId)
    {
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile { ProfileId = profileId });
        session.SetCurrentScene(SceneNames.Town);
        ApplyScriptedDeployment(session);
        return session;
    }

    public static void ApplyScriptedDeployment(GameSessionState session)
    {
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        var front = new Queue<DeploymentAnchorId>(session.DeploymentAnchors.Where(anchor => anchor.IsFrontRow()));
        var back = new Queue<DeploymentAnchorId>(session.DeploymentAnchors.Where(anchor => !anchor.IsFrontRow()));
        foreach (var hero in session.Profile.Heroes)
        {
            var prefersFront = FrontRowClasses.Contains(hero.ClassId);
            var primary = prefersFront ? front : back;
            var fallback = prefersFront ? back : front;
            if (primary.Count > 0)
            {
                session.AssignHeroToAnchor(primary.Dequeue(), hero.HeroId);
            }
            else if (fallback.Count > 0)
            {
                session.AssignHeroToAnchor(fallback.Dequeue(), hero.HeroId);
            }
        }
    }

    public static void AdvanceToNextUnclearedSite(GameSessionState session)
    {
        var progress = session.Profile.CampaignProgress;
        if (!progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            return;
        }

        session.TryCycleCampaignSite(+1);
        if (progress.ClearedSiteIds.Contains(session.SelectedCampaignSiteId))
        {
            session.TryCycleCampaignChapter(+1);
        }
    }

    public static string ScenarioId(BattleContextState context)
        => $"{context.ChapterId}/{context.SiteId}/{context.SiteNodeIndex.ToString(CultureInfo.InvariantCulture)}/{context.EncounterId}";

    public static int DeriveSeed(string contextHash, int salt)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            var payload = Encoding.UTF8.GetBytes($"h100|{contextHash}|{salt.ToString(CultureInfo.InvariantCulture)}");
            foreach (var value in payload)
            {
                hash ^= value;
                hash *= prime;
            }

            var result = (int)(hash & 0x7fffffffu);
            return result == 0 ? 1 : result;
        }
    }
}
