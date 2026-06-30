using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 프로세스 간 시드 결정성 회귀 잠금 — <see cref="GameSessionState.BuildStableSeed"/>는 데모/영입 아이템
/// 어픽스 선택(SessionInventoryItemBuilder.BuildGeneratedAffixIds의 <c>new Random(seed)</c>)과 영입 풀
/// 생성의 시드다. 한때 <c>Math.Abs(HashCode.Combine(value, salt))</c>로 구현돼 있었는데, .NET/Mono의
/// <c>HashCode.Combine</c>은 프로세스마다 Marvin 시드가 randomize되어 **같은 입력이 프로세스마다 다른 값**을
/// 냈다. 그 결과 헤드리스 캠페인이 같은 시드·분대인데도 별개 프로세스에서 분대 스탯이 달라져 W/L이 갈렸다
/// (엔지니어링 감사 신규발견 (a) — 두 test-batch 프로세스에서 victories 5 vs 4로 재현됨).
///
/// <para>이 골든은 BuildStableSeed가 입력만의 **순수 함수**(프로세스 불변 FNV)임을 알려진 상수로 못박는다.
/// HashCode.Combine으로 되돌리면 반환값이 이 상수와 거의 확실히 달라져 실패한다 — 단일 프로세스로 cross-process
/// 엔트로피 reversion을 잡는 가드다. 상수는 FNV(hash=17; hash=hash*31+ch; hash=hash*31+salt; & int.MaxValue)를
/// 손계산한 값이며, 해시 알고리즘을 의도적으로 바꾸지 않는 한 불변이다.</para>
/// </summary>
[Category("FastUnit")]
public sealed class BuildStableSeedDeterminismFastTests
{
    [TestCase("a", 0, 19344)]
    [TestCase("ab", 3, 602705)]
    [TestCase("", 0, 527)]
    public void BuildStableSeed_IsProcessStable_KnownGoldenValues(string value, int salt, int expected)
    {
        Assert.That(GameSessionState.BuildStableSeed(value, salt), Is.EqualTo(expected),
            "BuildStableSeed가 프로세스 불변 FNV 골든과 일치해야 한다 — 불일치는 process-variable 해시(HashCode.Combine 등)로의 reversion 신호.");
    }

    [Test]
    public void BuildStableSeed_IsNonNegative_AndDeterministicWithinProcess()
    {
        var first = GameSessionState.BuildStableSeed("demo-item-1", 4);
        var second = GameSessionState.BuildStableSeed("demo-item-1", 4);
        Assert.That(first, Is.EqualTo(second), "같은 입력은 같은 시드.");
        Assert.That(first, Is.GreaterThanOrEqualTo(0), "시드는 음수가 아니어야 한다(new Random 입력 안전).");
    }
}
