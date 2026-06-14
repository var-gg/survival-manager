using System.Collections.Generic;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// FirstRunStatusResolver 계약 — 첫 원정 루프 완료 전(클리어 site 0 + 스토리 미클리어)을 first-run 으로 판정.
/// 신규 플레이어 온보딩 강조의 게이트. 콘텐츠 무저작(CampaignProgress 기반)이라 narrative 에셋 없이 동작한다.
/// </summary>
[Category("FastUnit")]
public sealed class FirstRunStatusResolverFastTests
{
    [Test]
    public void FreshProgress_NoClearedSites_IsFirstRun()
    {
        Assert.IsTrue(FirstRunStatusResolver.IsFirstRunActive(new CampaignProgressRecord()));
    }

    [Test]
    public void ClearedAnySite_NotFirstRun()
    {
        var progress = new CampaignProgressRecord { ClearedSiteIds = new List<string> { "site_intro" } };

        Assert.IsFalse(FirstRunStatusResolver.IsFirstRunActive(progress));
    }

    [Test]
    public void StoryCleared_NotFirstRun_EvenWithNoClearedSites()
    {
        var progress = new CampaignProgressRecord { StoryCleared = true };

        Assert.IsFalse(FirstRunStatusResolver.IsFirstRunActive(progress));
    }

    [Test]
    public void NullProgress_TreatedAsFirstRun()
    {
        Assert.IsTrue(FirstRunStatusResolver.IsFirstRunActive(null));
    }
}
