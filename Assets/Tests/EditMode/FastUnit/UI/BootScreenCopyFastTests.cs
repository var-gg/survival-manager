using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode.FastUnit.UI;

/// <summary>
/// 시작 화면 문구가 <b>플레이어의 말</b>인지 지킨다.
///
/// 2026-07-31 이전까지 게임의 첫 화면은 이랬다.
///   제목  "시작"
///   본문  "이 빌드는 Town에서 준비하고 Expedition/Battle/Reward를 거치는 로컬 플레이 루프입니다."
///   버튼  "로컬 실행 시작"
/// 플레이어에게 빌드 구조를 설명하고 있었다.
///
/// 이 화면에서 특히 조심할 것 — <b>폴백이 곧 문구다.</b> 시작 화면은 로컬라이제이션이
/// 초기화되기 전에 그려지므로 테이블 값이 아니라 <see cref="BootScreenController"/> 에 적힌
/// 폴백 문자열이 그대로 화면에 나온다. 실제로 시드를 한국어로 바꾼 뒤에도 화면에는
/// "Start" / "Start Local Run" 이 떴다. 그래서 폴백을 "번역 실패 시 임시값"이 아니라
/// 제품 문구로 취급하고, 여기서 계약으로 건다.
/// </summary>
[Category("FastUnit")]
public sealed class BootScreenCopyFastTests
{
    private const string ControllerPath = "Assets/_Game/Scripts/Runtime/Unity/BootScreenController.cs";

    [Test]
    public void BootScreen_FallbackCopy_IsPlayerFacing_NotBuildPipelineDescription()
    {
        // 주석은 걷어낸다 — 계약은 <b>코드</b>가 무엇을 화면에 내보내는지에 대한 것이다.
        // (이 파일 자신의 주석이 옛 문구를 인용하고 있어서, 걷지 않으면 자기 설명에 걸린다.)
        var controller = StripComments(File.ReadAllText(ControllerPath));

        // 개발용 하네스 어휘가 첫 화면 폴백으로 돌아오면 여기서 잡는다.
        Assert.That(controller, Does.Not.Contain("이 빌드는"), "첫 화면이 플레이어에게 빌드 구조를 설명하면 안 된다");
        Assert.That(controller, Does.Not.Contain("로컬 실행 시작"), "'로컬 실행'은 개발자 어휘다");
        Assert.That(controller, Does.Not.Contain("로컬 루프"), "'루프'는 개발자 어휘다");
        Assert.That(controller, Does.Not.Contain("\"Start Local Run\""), "폴백은 곧 화면 문구다 — 영문 개발 라벨 금지");
        Assert.That(controller, Does.Not.Contain("\"Start\""), "폴백은 곧 화면 문구다 — 영문 개발 라벨 금지");

        // 한국어 1차. 폴백이 실제 제품 문구여야 한다.
        Assert.That(controller, Does.Contain("잿골 연대기"), "제목 폴백");
        Assert.That(controller, Does.Contain("이어서 시작"), "시작 버튼 폴백");
    }

    /// <summary>
    /// 시드와 폴백이 갈라지면 로컬라이제이션 초기화 시점에 따라 화면 문구가 바뀐다.
    /// 두 경로가 같은 문구를 들고 있는지 확인한다.
    /// </summary>
    [Test]
    public void BootScreen_SeedCopy_MatchesControllerFallbacks()
    {
        var controller = StripComments(File.ReadAllText(ControllerPath));
        var seed = File.ReadAllText("Assets/_Game/Scripts/Editor/Bootstrap/LocalizationFoundationBootstrap.cs");

        foreach (var line in new[]
                 {
                     "잿골 연대기",
                     "이어서 시작",
                     "잿문이 닫힌 뒤로, 변방에 남은 것은 잿골 하나뿐이다.",
                     "마을을 꾸리고, 분대를 짜고, 원정에 나선다.",
                     // 차단 상태(세이브 로드 실패)의 첫 줄. 이 화면은 조건부로만 뜨므로
                     // 2026-07-31 에 전용 PlayMode 도달 경로를 만들고서야 처음 봤다 —
                     // 그전에는 저장소 진단 문자열이 본문 자리에 그대로 앉아 있었다.
                     "저장된 기록을 열지 못했습니다. 게임을 다시 시작해 주세요.",
                 })
        {
            Assert.That(controller, Does.Contain(line), $"컨트롤러 폴백에 '{line}' 이 있어야 한다");
            Assert.That(seed, Does.Contain(line), $"시드에 '{line}' 이 있어야 한다 — 폴백과 갈라지면 화면 문구가 시점 따라 바뀐다");
        }
    }

    /// <summary>줄 주석을 제거한다. 문자열 리터럴 안의 <c>//</c> 는 이 파일들에 없다.</summary>
    private static string StripComments(string source)
    {
        var lines = source.Split('\n');
        var kept = new System.Collections.Generic.List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", System.StringComparison.Ordinal))
            {
                continue;
            }

            kept.Add(line);
        }

        return string.Join("\n", kept);
    }
}
