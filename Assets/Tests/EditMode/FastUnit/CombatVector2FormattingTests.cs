using NUnit.Framework;
using SM.Combat.Model;

namespace SM.Tests.EditMode;

/// <summary>
/// CombatVector2의 컴파일러 합성 ToString은 Normalized(자기 타입 반환 public 속성)를 출력하느라
/// 무한 재귀해 StackOverflow를 일으켰다 — NUnit 단정 실패 메시지 포맷 경로에서 에디터가 죽는 형태.
/// 명시적 ToString 오버라이드가 그 경로를 차단하는 계약을 고정한다.
/// </summary>
[Category("FastUnit")]
public sealed class CombatVector2FormattingTests
{
    [Test]
    public void ToString_TerminatesAndPrintsComponents()
    {
        Assert.That(new CombatVector2(3f, 4f).ToString(), Is.EqualTo("(3, 4)"));
        Assert.That(CombatVector2.Zero.ToString(), Is.EqualTo("(0, 0)"));
        Assert.That(new CombatVector2(0.5f, -1.25f).ToString(), Is.EqualTo("(0.5, -1.25)"));
    }

    [Test]
    public void FailedDeepEqualityAssert_ProducesMessageInsteadOfStackOverflow()
    {
        var exception = Assert.Throws<AssertionException>(
            () => Assert.That(new CombatVector2(3f, 4f), Is.EqualTo(new CombatVector2(1f, 2f))));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception.Message, Does.Contain("(1, 2)"));
        Assert.That(exception.Message, Does.Contain("(3, 4)"));
    }
}
