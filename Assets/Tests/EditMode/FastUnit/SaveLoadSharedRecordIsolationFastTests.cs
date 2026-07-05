using System;
using System.IO;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Persistence.Json;

namespace SM.Tests.EditMode;

/// <summary>
/// 세이브 로드가 프로세스 전역 공유 record(NarrativeProgressRecord.Empty 등)를 오염시키지 않음을 고정한다.
///
/// 배경(2026-07-05 clean-clone witness가 표면화): Newtonsoft 기본 ObjectCreationHandling.Auto는
/// 역직렬화 대상 필드에 이미 인스턴스가 있으면 새로 만들지 않고 그 인스턴스 안으로 populate한다.
/// SaveProfile.Narrative 초기값이 공유 static Empty를 참조하던 시절, 로드 한 번이 전역 Empty에
/// 로드된 스토리 진행(SeenEventIds/StoryFlags/PendingPresentations)을 주입했다 — 이후 생성되는
/// 모든 "빈" director가 남의 세이브 진행을 물려받는 실플레이어 결함이자, full-suite에서만 발현하는
/// 순서 의존 테스트 오염 13건의 근원. 수술: JsonSaveRepository LoadSettings=Replace + Narrative=new().
/// </summary>
[Category("FastUnit")]
public sealed class SaveLoadSharedRecordIsolationFastTests
{
    [Test]
    public void LoadProfile_WithNarrativeProgress_DoesNotMutateSharedEmptyRecord()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm_shared_record_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var repo = new JsonSaveRepository(root);
            var profile = repo.LoadOrCreate("default");
            profile.Narrative = profile.Narrative with
            {
                CurrentChapterId = "chapter_ashen_gate",
                CurrentSiteId = "site_ashen_gate",
                SeenEventIds = new[] { "story_event_site_intro_ashen_gate" },
                ResolvedEventIds = new[] { "story_event_site_intro_ashen_gate" },
                StoryFlags = new[] { "story_flag_intro_ashen_gate" },
                PendingPresentations = new[]
                {
                    new StoryPresentationRequest
                    {
                        PresentationKind = StoryPresentationKind.DialogueScene,
                        PresentationKey = "dialogue_scene_ashen_gate_intro",
                        Priority = 100,
                    },
                },
            };
            repo.Save(profile);

            var loaded = repo.LoadOrCreate("default");

            // 로드는 실제로 narrative를 복원해야 하고(테스트 공허 방지),
            Assert.That(loaded.Narrative.StoryFlags, Is.EqualTo(new[] { "story_flag_intro_ashen_gate" }));
            Assert.That(loaded.Narrative.PendingPresentations, Has.Length.EqualTo(1));
            // 복원된 인스턴스가 전역 공유 Empty의 별칭이어서는 안 된다.
            Assert.That(ReferenceEquals(loaded.Narrative, NarrativeProgressRecord.Empty), Is.False);

            // 핵심 불변: 전역 Empty는 로드 후에도 무결하다.
            Assert.That(NarrativeProgressRecord.Empty.CurrentChapterId, Is.Empty);
            Assert.That(NarrativeProgressRecord.Empty.CurrentSiteId, Is.Empty);
            Assert.That(NarrativeProgressRecord.Empty.SeenEventIds, Is.Empty);
            Assert.That(NarrativeProgressRecord.Empty.ResolvedEventIds, Is.Empty);
            Assert.That(NarrativeProgressRecord.Empty.StoryFlags, Is.Empty);
            Assert.That(NarrativeProgressRecord.Empty.UnlockedStoryHeroIds, Is.Empty);
            Assert.That(NarrativeProgressRecord.Empty.PendingPresentations, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Test]
    public void FreshProfiles_DoNotAliasSharedEmptyNarrativeRecord()
    {
        // SaveProfile 필드 초기값이 공유 static을 참조하면 populate형 역직렬화의 오염 표적이 된다.
        var a = new SM.Persistence.Abstractions.Models.SaveProfile();
        var b = new SM.Persistence.Abstractions.Models.SaveProfile();

        Assert.That(ReferenceEquals(a.Narrative, NarrativeProgressRecord.Empty), Is.False);
        Assert.That(ReferenceEquals(a.Narrative, b.Narrative), Is.False);
    }
}
