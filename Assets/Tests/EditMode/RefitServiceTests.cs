using System;
using System.Collections.Generic;
using NUnit.Framework;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RefitServiceTests
{
    [Test]
    public void Refit_ValidSlot_ReturnsNewAffix()
    {
        var current = new List<string> { "affix_a", "affix_b" };
        var available = new List<string> { "affix_a", "affix_b", "affix_c", "affix_d" };

        var result = RefitService.Refit(current, 0, available, seed: 42);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.OldAffixId, Is.EqualTo("affix_a"));
        Assert.That(result.NewAffixId, Is.Not.EqualTo("affix_a"), "reroll 결과는 원래 affix와 달라야 함");
        Assert.That(result.NewAffixId, Is.Not.EqualTo("affix_b"), "reroll 결과는 다른 슬롯 affix와 달라야 함");
        Assert.That(result.EchoCost, Is.EqualTo(RefitService.BaseEchoCost));
    }

    [Test]
    public void Refit_DeterministicSeed_ReturnsSameResult()
    {
        var current = new List<string> { "affix_a", "affix_b" };
        var available = new List<string> { "affix_a", "affix_b", "affix_c", "affix_d", "affix_e" };

        var result1 = RefitService.Refit(current, 0, available, seed: 123);
        var result2 = RefitService.Refit(current, 0, available, seed: 123);

        Assert.That(result1, Is.Not.Null);
        Assert.That(result2, Is.Not.Null);
        Assert.That(result1!.NewAffixId, Is.EqualTo(result2!.NewAffixId),
            "동일 seed는 동일 결과를 반환해야 함");
    }

    [Test]
    public void Refit_NegativeSlotIndex_ReturnsNull()
    {
        var current = new List<string> { "affix_a" };
        var available = new List<string> { "affix_a", "affix_b" };

        var result = RefitService.Refit(current, -1, available, seed: 1);

        Assert.That(result, Is.Null, "음수 인덱스는 null 반환");
    }

    [Test]
    public void Refit_SlotIndexOutOfRange_ReturnsNull()
    {
        var current = new List<string> { "affix_a" };
        var available = new List<string> { "affix_a", "affix_b" };

        var result = RefitService.Refit(current, 5, available, seed: 1);

        Assert.That(result, Is.Null, "범위 초과 인덱스는 null 반환");
    }

    [Test]
    public void Refit_NoCandidates_ReturnsNull()
    {
        // All available affixes are already equipped
        var current = new List<string> { "affix_a", "affix_b" };
        var available = new List<string> { "affix_a", "affix_b" };

        var result = RefitService.Refit(current, 0, available, seed: 1);

        Assert.That(result, Is.Null, "후보가 없으면 null 반환");
    }

    [Test]
    public void Refit_NullCurrentAffixIds_ReturnsNull()
    {
        var result = RefitService.Refit(null!, 0, new List<string> { "affix_a" }, seed: 1);

        Assert.That(result, Is.Null, "null 입력은 null 반환");
    }

    [Test]
    public void Refit_ExcludesAllExistingAffixes()
    {
        // current has affix_a, affix_b; available only has affix_a, affix_b, affix_c
        // Only affix_c should be a valid candidate for slot 0 (replacing affix_a)
        var current = new List<string> { "affix_a", "affix_b" };
        var available = new List<string> { "affix_a", "affix_b", "affix_c" };

        var result = RefitService.Refit(current, 0, available, seed: 1);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.NewAffixId, Is.EqualTo("affix_c"),
            "유일한 후보 affix_c만 선택 가능해야 함");
    }

    [Test]
    public void Refit_SkipsWhitespaceAvailableIds()
    {
        var current = new List<string> { "affix_a" };
        var available = new List<string> { "", " ", "affix_a", "affix_b" };

        var result = RefitService.Refit(current, 0, available, seed: 1);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.NewAffixId, Is.EqualTo("affix_b"),
            "빈 문자열과 공백은 후보에서 제외되어야 함");
    }
}
