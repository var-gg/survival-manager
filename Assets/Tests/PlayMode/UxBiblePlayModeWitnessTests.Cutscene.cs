using System.Collections;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Unity;
using SM.Unity.Narrative;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SM.Tests.PlayMode;

public sealed partial class UxBiblePlayModeWitnessTests
{
    /// <summary>
    /// 컷씬(대화 씬) 실화면 위트니스.
    ///
    /// 이 표면은 <b>신규 프로필 첫 실행에서만</b> 저절로 나온다. 그래서 시각 검토를 하려면
    /// 매번 세이브를 비워야 했고, 그 실행은 컷씬 도달 전에 다른 이유로 죽어서
    /// <b>2026-07-31 까지 한 번도 실화면을 못 봤다.</b> 스토리 표면만 겨냥한 도달 경로를 둔다.
    ///
    /// 발화는 세션이, 표시는 씬이 한다(<see cref="StorySceneFlowBridge"/>). 여기서는 그 계약을
    /// 그대로 타고 — moment 를 흘리고 대화 씬 루트가 뜨기를 기다린 뒤 캡쳐한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Cutscene_DialogueScene_IsReachableAndRendersAuthoredLine()
    {
        yield return EnterOfflineTownFromBoot();

        var bridge = Object.FindFirstObjectByType<StorySceneFlowBridge>();
        Assert.That(bridge, Is.Not.Null, "Town 씬은 스토리 표시 브리지를 노출해야 한다.");

        var root = GameSessionRoot.Instance!;
        var session = root.SessionState;

        // 이미 본 연출은 다시 큐잉되지 않는다 — 그래서 저장된 프로필에서는 초반 moment 를
        // 아무리 흘려도 컷씬이 뜨지 않는다. 시각 위트니스의 목적은 <b>표면을 보는 것</b>이므로
        // 진행도만 비우고 같은 발화 경로를 그대로 탄다(연출 규칙 자체는 우회하지 않는다).
        var directorField = typeof(GameSessionState).GetProperty(
            nameof(GameSessionState.StoryDirector),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.That(directorField, Is.Not.Null, "StoryDirector 접근 경로가 바뀌었다.");
        var progressProperty = typeof(SM.Meta.StoryDirectorService).GetProperty(
            nameof(SM.Meta.StoryDirectorService.Progress),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.That(progressProperty, Is.Not.Null, "StoryDirector.Progress 접근 경로가 바뀌었다.");
        progressProperty!.SetValue(session.StoryDirector, SM.Meta.NarrativeProgressRecord.Empty);

        // 초반 moment 를 순서대로 흘려보며 처음으로 큐가 늘어나는 지점을 쓴다.
        var queued = false;
        foreach (var moment in new[]
                 {
                     NarrativeMoment.BootLoaded,
                     NarrativeMoment.TownEntered,
                     NarrativeMoment.ExpeditionSelected,
                 })
        {
            var before = session.NarrativeProgress.PendingPresentations.Length;
            bridge!.Advance(moment, StoryMomentContext.Empty);
            if (session.NarrativeProgress.PendingPresentations.Length > before || bridge.IsBusy)
            {
                queued = true;
                break;
            }
        }

        Assert.That(
            queued,
            Is.True,
            "초반 moment 중 하나는 연출을 큐에 올려야 한다 — 아무것도 안 올라오면 컷씬 표면이 죽어 있다는 뜻이다.");

        VisualElement? sceneRoot = null;
        for (var frame = 0; frame < 240 && sceneRoot == null; frame++)
        {
            sceneRoot = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .Select(document => document.rootVisualElement?.Q<VisualElement>("dialogue-scene-root"))
                .FirstOrDefault(candidate => candidate != null && IsEffectivelyVisible(candidate));
            yield return null;
        }

        Assert.That(sceneRoot, Is.Not.Null, "대화 씬 루트가 화면에 떠야 한다.");
        yield return WaitFrames(2);

        // 대사는 타자기로 한 글자씩 찍힌다 — 뜨자마자 캡쳐하면 빈 줄이 나온다.
        // (실제로 첫 시도에서 글자 0개로 실패했고, 예전 수동 캡쳐에는 "변" 한 글자만 찍혀 있었다.)
        var line = Require<Label>(sceneRoot!, "dialogue-scene-line");
        for (var frame = 0; frame < 300 && string.IsNullOrEmpty(line.text); frame++)
        {
            yield return null;
        }

        Assert.That(line.text, Is.Not.Empty, "대사 줄이 비어 있으면 안 된다.");
        Assert.That(line.text, Does.Not.StartWith("content."), "대사에 raw 콘텐츠 키가 나오면 안 된다.");
        Assert.That(line.text, Does.Not.StartWith("ui."), "대사에 raw UI 키가 나오면 안 된다.");

        // 스킵 크롬은 2026-07-31 까지 UXML 에 영문으로 박혀 있었다. 한국어 1차 계약을 여기서 든다.
        var skipButton = Require<Button>(sceneRoot!, "dialogue-scene-skip-all-button");
        Assert.That(skipButton.text, Does.Not.Contain("Skip"), "스킵 버튼이 영문이면 안 된다.");

        AssertNoRedText(sceneRoot!, "Dialogue Scene");
        yield return Capture("cutscene_dialogue");
        _packet?.RecordPass($"Cutscene dialogue scene rendered an authored line: {line.text}");
    }
}
