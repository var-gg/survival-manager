# 대사 / 이벤트 스키마

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/dialogue-event-schema.md`
- 관련문서:
  - `docs/02_design/narrative/chapter-beat-sheet.md`
  - `docs/03_architecture/narrative-code-architecture.md`
  - `docs/02_design/meta/story-gating-and-unlock-rules.md`

## 목적

스토리 이벤트와 대사 시퀀스의 ID 규칙, trigger schema, once policy, presentation grade를 고정한다. 이 문서는 narrative event authoring의 source of truth다.

## 명명 규칙

- **스토리 이벤트**: `story_event_{purpose}` — 예: `story_event_site_intro_ashen_gate`, `story_event_unlock_rift_stalker`
- **대사 시퀀스**: `dialogue_seq_{context}` — 예: `dialogue_seq_ashen_gate_intro`
- **스토리 플래그**: `story_flag_{state}` — 예: `story_flag_relicborn_revealed`
- ID는 chapter/site/node 혹은 hero/faction/reveal 목적이 읽히도록 구성한다.

## 이벤트 스키마 표

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_site_intro_ashen_gate` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_ashen_gate` | `SetFlag:story_flag_intro_seen` | `story_card_ashen_gate_intro` |
| `story_event_unlock_rift_stalker` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_wolfpine_trail` | `UnlockHero:hero_rift_stalker`, `SetFlag:story_flag_rift_stalker_joined` | `toast_unlock_rift_stalker` |
| `story_event_unlock_bastion_penitent` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_sunken_bastion` | `UnlockHero:hero_bastion_penitent`, `SetFlag:story_flag_bastion_penitent_joined` | `toast_unlock_bastion_penitent` |
| `story_event_unlock_pale_executor` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_tithe_road` | `UnlockHero:hero_pale_executor`, `SetFlag:story_flag_pale_executor_joined` | `toast_unlock_pale_executor` |
| `story_event_relicborn_awakening` | `BattleResolved` | 500 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard`, `NodeIs:3` | `SetFlag:story_flag_relicborn_awakened` | `dialogue_overlay_relicborn_awakening` |
| `story_event_midpoint_reveal` | `ExtractCommitted` | 600 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard` | `SetFlag:story_flag_midpoint_revealed` | `story_card_midpoint_reveal` |
| `story_event_unlock_echo_savant` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard` | `UnlockHero:hero_echo_savant`, `SetFlag:story_flag_echo_savant_joined` | `toast_unlock_echo_savant` |
| `story_event_final_boss` | `BattleResolved` | 700 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths`, `NodeIs:4` | `SetFlag:story_flag_final_boss_defeated` | `story_card_final_boss` |
| `story_event_campaign_complete` | `ExtractCommitted` | 900 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths` | `SetFlag:story_flag_campaign_complete`, `SetFlag:story_flag_endless_open` | `story_card_campaign_complete` |

## 대사 시퀀스 표

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_ashen_gate_intro` | 0 | `hero_dawn_priest` | `loc.story.ashen_gate.intro.0` | `grim` | 원정 명분 |
| `dialogue_seq_ashen_gate_intro` | 1 | `hero_pack_raider` | `loc.story.ashen_gate.intro.1` | `skeptical` | 인간 불신 |
| `dialogue_seq_relicborn_awakening` | 0 | `hero_grave_hexer` | `loc.story.relicborn.awaken.0` | `shock` | 각성 감지 |
| `dialogue_seq_relicborn_awakening` | 1 | `hero_echo_savant` | `loc.story.relicborn.awaken.1` | `solemn` | 수문장 자기소개 |

## JSON 예시

```json
{
  "id": "story_event_unlock_rift_stalker",
  "moment": "ExtractCommitted",
  "priority": 300,
  "oncePolicy": "OncePerProfile",
  "conditions": [
    { "kind": "ChapterIs", "a": "chapter_ashen_gate" },
    { "kind": "SiteIs", "a": "site_wolfpine_trail" }
  ],
  "effects": [
    { "kind": "UnlockHero", "a": "hero_rift_stalker" },
    { "kind": "SetFlag", "a": "story_flag_rift_stalker_joined" },
    { "kind": "EnqueuePresentation", "a": "toast_unlock_rift_stalker" }
  ],
  "presentationKey": "toast_unlock_rift_stalker"
}
```

## presentation grade 규칙

| kind | 사용 시점 | payload | skip 정책 |
|---|---|---|---|
| `toast-banner` | codex unlock, clue, hero unlock 반응 | 제목 1개 + 본문 1~2줄 + icon | 즉시 skip 허용 |
| `dialogue-overlay` | pre/post battle bark, town 반응, 짧은 갈등 대사 | portrait 0~2개 + 2~8줄 | line skip/전체 skip 허용 |
| `story-card` | chapter/site intro/outro, ending | full-screen still + 제목 + 본문 1~3문단 | card 단위 skip 허용 |

금지: full cutscene, camera rail, branching dialogue, lip sync, timeline animation.

## 작성 지침

- 한 event는 한 목적만 가져야 한다.
- condition/effect는 data-driven enum + payload 형태를 우선한다.
- event ID와 node beat는 반드시 역참조 가능해야 한다.
- priority가 높을수록 먼저 재생된다. 같은 moment에 여러 event가 걸리면 priority 내림차순으로 evaluate한다.
