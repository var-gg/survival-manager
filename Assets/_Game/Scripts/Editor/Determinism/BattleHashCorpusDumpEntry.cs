using System;
using System.IO;
using SM.Combat.Services;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Determinism
{
    /// <summary>
    /// <c>-executeMethod</c> 진입점: <see cref="BattleHashCorpus"/>를 backend별 파일로 덤프한다
    /// (ADR-0029 Phase 0 cross-platform 골든 오라클). 배치 호출 예:
    /// <code>
    /// Unity -batchmode -nographics -projectPath . \
    ///   -executeMethod SM.Editor.Determinism.BattleHashCorpusDumpEntry.Run \
    ///   -hashCorpusOut TestResults/hash-corpus-editor-mono.txt -logFile ... -quit
    /// </code>
    /// 출력 경로는 <c>-hashCorpusOut &lt;path&gt;</c>로 override(기본 <c>TestResults/hash-corpus.txt</c>).
    /// 순수 코퍼스 생성은 <see cref="BattleHashCorpus"/>(SM.Combat)가 담당 — 여기는 IO/실행 wrapper다.
    /// 열린 GUI 에디터와 batchmode는 프로젝트 락이 충돌하므로, CI 또는 에디터를 닫은 세션에서 호출한다.
    /// </summary>
    public static class BattleHashCorpusDumpEntry
    {
        private const string DefaultOutPath = "TestResults/hash-corpus.txt";
        private const string OutArg = "-hashCorpusOut";

        public static void Run()
        {
            try
            {
                var outPath = ResolveOutPath();
                var corpus = BattleHashCorpus.Generate();
                var fullPath = Path.IsPathRooted(outPath)
                    ? outPath
                    : Path.Combine(Directory.GetCurrentDirectory(), outPath);

                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, corpus);
                Debug.Log($"[BattleHashCorpusDump] wrote {corpus.Length} chars to {fullPath} " +
                          $"(seeds={string.Join(",", BattleHashCorpus.DefaultSeeds)})");

                Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleHashCorpusDump] failed: {ex}");
                Exit(1);
            }
        }

        // BattleHashCorpusGoldenTests가 비교하는 체크인 골든 경로(테스트 쪽과 동일 문자열 유지).
        private const string GoldenRelativePath = "Tests/EditMode/FastUnit/Golden/battle-hash-corpus.golden.txt";

        /// <summary>
        /// 체크인 골든 코퍼스 재생성 진입점 — cross-process 결정성 게이트(BattleHashCorpusGoldenTests)의
        /// 기준선을 쓴다. <b>의도적으로 sim 게임플레이를 바꾼 커밋에서만</b> 실행해 골든을 갱신·같이 커밋한다:
        /// <code>
        /// Unity -batchmode -nographics -projectPath . \
        ///   -executeMethod SM.Editor.Determinism.BattleHashCorpusDumpEntry.WriteGolden -logFile ...
        /// </code>
        /// </summary>
        public static void WriteGolden()
        {
            try
            {
                var goldenPath = Path.Combine(Application.dataPath, GoldenRelativePath);
                var directory = Path.GetDirectoryName(goldenPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var corpus = BattleHashCorpus.Generate();
                File.WriteAllText(goldenPath, corpus);
                Debug.Log($"[BattleHashCorpusDump] golden written: {goldenPath} ({corpus.Length} chars)");
                Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleHashCorpusDump] WriteGolden failed: {ex}");
                Exit(1);
            }
        }

        private static string ResolveOutPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], OutArg, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return DefaultOutPath;
        }

        private static void Exit(int code)
        {
            // 배치 실행에서만 프로세스를 종료한다(에디터에서 직접 호출한 경우 에디터를 닫지 않음).
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
