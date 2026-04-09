# 스토리 게이팅 및 해금 규칙

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/meta/story-gating-and-unlock-rules.md`
- 관련문서:
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`
  - `docs/02_design/narrative/dialogue-event-schema.md`
  - `docs/03_architecture/narrative-code-architecture.md`

## 목적

story unlock, hero join, endless gate, fail-safe, save field를 메타 관점에서 고정한다. 이 문서는 narrative와 progression이 만나는 지점의 source of truth다.

## 게이팅 원칙

- v1은 선형 트랙이므로 hard gate는 최소화한다.
- chapter/site 진입은 직전 site의 extract commit만 요구한다.
- hero unlock은 story event의 effect로 처리하며, reward gate와 별도로 관리한다.
- endless mode는 최종 extract commit으로만 해금된다.

## 해금 규칙 표

| gate_id | source_event | target_unlock | fail_safe | ui_surface | save_field |
|---|---|---|---|---|---|
| `gate_unlock_rift_stalker` | `story_event_unlock_rift_stalker` | `hero_rift_stalker` | 다음 town 진입 시 재평가 | `toast_unlock_rift_stalker` | `UnlockedStoryHeroIds` |
| `gate_unlock_bastion_penitent` | `story_event_unlock_bastion_penitent` | `hero_bastion_penitent` | 다음 town 진입 시 재평가 | `toast_unlock_bastion_penitent` | `UnlockedStoryHeroIds` |
| `gate_unlock_pale_executor` | `story_event_unlock_pale_executor` | `hero_pale_executor` | 다음 town 진입 시 재평가 | `toast_unlock_pale_executor` | `UnlockedStoryHeroIds` |
| `gate_unlock_aegis_sentinel` | `story_event_unlock_aegis_sentinel` | `hero_aegis_sentinel` | 다음 town 진입 시 재평가 | `toast_unlock_aegis_sentinel` | `UnlockedStoryHeroIds` |
| `gate_unlock_echo_savant` | `story_event_unlock_echo_savant` | `hero_echo_savant` | 다음 town 진입 시 재평가 | `toast_unlock_echo_savant` | `UnlockedStoryHeroIds` |
| `gate_unlock_shardblade` | `story_event_unlock_shardblade` | `hero_shardblade` | 다음 town 진입 시 재평가 | `toast_unlock_shardblade` | `UnlockedStoryHeroIds` |
| `gate_unlock_prism_seeker` | `story_event_unlock_prism_seeker` | `hero_prism_seeker` | 다음 town 진입 시 재평가 | `toast_unlock_prism_seeker` | `UnlockedStoryHeroIds` |
| `gate_unlock_mirror_cantor` | `story_event_unlock_mirror_cantor` | `hero_mirror_cantor` | 다음 town 진입 시 재평가 | `toast_unlock_mirror_cantor` | `UnlockedStoryHeroIds` |
| `gate_unlock_endless_cycle` | `story_event_campaign_complete` | `mode_endless_cycle` | credits 종료 후 재평가 | `story_card_endless_open` | `StoryFlags.story_flag_endless_open` |

## fail-safe 표

| failure_case | canonical_resolution | owner_service |
|---|---|---|
| unlock toast 재생 전 종료 | save에는 unlock이 이미 남아 있어야 한다. presentation은 다음 scene에서 재시도 | `StoryDirectorService` |
| story-card skip | presentation만 스킵되고 flag/effect는 유지된다 | `StoryPresentationRunner` |
| extract 중 crash | extract commit은 atomic으로 save에 기록. narrative effect는 다음 boot에서 재평가 | `StoryDirectorService` |
| chapter 진입 조건 미충족 | 직전 site extract가 save에 있으면 진입 허용. 없으면 town에서 대기 | meta flow |

## UI 표면화 규칙

| unlock_type | ui_surface | timing | note |
|---|---|---|---|
| hero unlock | `toast-banner` | extract commit 직후 | truth(save)는 toast 이전에 이미 기록됨 |
| story flag | 없음 (내부 상태) | event evaluation 시 | UI에 직접 노출하지 않음 |
| codex entry | `toast-banner` | battle resolve 또는 extract | 수집 요소 |
| endless unlock | `story-card` | campaign complete extract 직후 | 엔드게임 진입 축하 |

UI는 truth를 만들지 않고 truth를 보여주기만 해야 한다.

## save field 연계

| gate_category | save_field | type | migration_default |
|---|---|---|---|
| hero unlock | `NarrativeProgressRecord.UnlockedStoryHeroIds` | `List<string>` | empty list |
| story flag | `NarrativeProgressRecord.StoryFlags` | `Dictionary<string, bool>` | empty map |
| seen events | `NarrativeProgressRecord.SeenEventIds` | `List<string>` | empty list |
| resolved events | `NarrativeProgressRecord.ResolvedEventIds` | `List<string>` | empty list |
| endless state | `NarrativeProgressRecord.EndlessCycle` | `EndlessCycleStateRecord` | null (not started) |
| pending presentation | `NarrativeProgressRecord.PendingPresentations` | `List<StoryPresentationRequest>` | empty list |

## 작성 지침

- 이 문서는 narrative text를 길게 서술하지 않는다.
- gate 조건은 `dialogue-event-schema.md`의 event ID와 정확히 대응해야 한다.
- fail-safe가 없는 unlock 규칙은 허용하지 않는다.
