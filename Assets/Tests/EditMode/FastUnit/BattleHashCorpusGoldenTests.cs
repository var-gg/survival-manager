using System;
using System.IO;
using NUnit.Framework;
using SM.Combat.Services;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// cross-process 결정성 게이트 — 체크인된 골든 코퍼스와 이 프로세스가 재생성한
/// <see cref="BattleHashCorpus.Generate"/> 출력을 byte 비교한다. 골든은 과거의 **다른 프로세스**가
/// 생성했으므로, 매 test-batch 실행이 곧 cross-process 비교다: 프로세스마다 값이 변하는 엔트로피
/// (HashCode.Combine Marvin 시드, GetHashCode 의존 순회, 미정렬 float 집계)가 sim 도달 경로 어디에
/// 새로 생기든 그 프로세스의 코퍼스가 골든과 갈라져 실패한다.
///
/// <para>배경(엔지니어링 감사 신규발견 (a), commit f17e2f07): BuildStableSeed의 HashCode.Combine이
/// 프로세스마다 분대 스탯을 바꿔 W/L이 갈렸는데, 당시 결정론 오라클은 same-process 비교뿐이라
/// (BattleHashCorpusTests.Generate_IsDeterministic_AcrossTwoRuns) 못 잡았다 — 이 골든이 그 사각을 닫는다.
/// 6-에이전트 정적 감사조차 2번째 consumer를 놓친 사례라, 정적 리뷰가 아닌 자동 게이트가 필요하다.</para>
///
/// <para><b>의도적으로 sim 게임플레이를 바꾼 경우에만</b> 골든을 재생성해 같이 커밋한다(에디터 닫고):
/// <c>Unity -batchmode -nographics -projectPath . -executeMethod
/// SM.Editor.Determinism.BattleHashCorpusDumpEntry.WriteGolden -logFile -</c>.
/// 의도치 않은 실패 = 게임플레이 결과가 바뀌었거나 프로세스 가변 엔트로피가 생겼다는 뜻 — 원인 조사가 먼저다.</para>
/// </summary>
[Category("FastUnit")]
public sealed class BattleHashCorpusGoldenTests
{
    // BattleHashCorpusDumpEntry.GoldenRelativePath와 동일 문자열 유지(asmdef 경계로 상수 공유 불가).
    private static string GoldenPath =>
        Path.Combine(Application.dataPath, "Tests/EditMode/FastUnit/Golden/battle-hash-corpus.golden.txt");

    [Test]
    public void Corpus_MatchesCheckedInGolden_AcrossProcesses()
    {
        Assert.That(File.Exists(GoldenPath), Is.True,
            $"골든 코퍼스 파일이 없다: {GoldenPath}\n" +
            "BattleHashCorpusDumpEntry.WriteGolden(-executeMethod, 에디터 닫고 batch)으로 생성 후 커밋하라.");

        // git autocrlf 체크아웃 대비 개행 정규화 — 코퍼스 자체는 '\n'만 쓴다.
        var golden = Normalize(File.ReadAllText(GoldenPath));
        var current = Normalize(BattleHashCorpus.Generate());

        if (!string.Equals(golden, current, StringComparison.Ordinal))
        {
            var (lineNumber, goldenLine, currentLine) = FirstDivergingLine(golden, current);
            Assert.Fail(
                "hash 코퍼스가 체크인 골든과 갈라졌다 — 프로세스 가변 엔트로피 유입 또는 (의도라면) sim 게임플레이 변경.\n" +
                $"첫 발산 라인 {lineNumber}:\n  golden : {goldenLine}\n  current: {currentLine}\n" +
                "의도적 게임플레이 변경이면 BattleHashCorpusDumpEntry.WriteGolden으로 골든을 재생성해 같이 커밋. " +
                "의도가 아니면 엔트로피 원인 조사가 먼저다(analysis-engineering-audit 신규발견 (a) 참고).");
        }

        Assert.Pass();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static (int LineNumber, string GoldenLine, string CurrentLine) FirstDivergingLine(string golden, string current)
    {
        var goldenLines = golden.Split('\n');
        var currentLines = current.Split('\n');
        var max = Math.Max(goldenLines.Length, currentLines.Length);
        for (var i = 0; i < max; i++)
        {
            var goldenLine = i < goldenLines.Length ? goldenLines[i] : "(라인 없음)";
            var currentLine = i < currentLines.Length ? currentLines[i] : "(라인 없음)";
            if (!string.Equals(goldenLine, currentLine, StringComparison.Ordinal))
            {
                return (i + 1, goldenLine, currentLine);
            }
        }

        return (0, string.Empty, string.Empty);
    }
}
