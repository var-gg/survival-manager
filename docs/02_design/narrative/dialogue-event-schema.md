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

### Chapter 1: Ashen Gate

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_site_intro_ashen_gate` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_ashen_gate` | `SetFlag:story_flag_intro_ashen_gate` | `story_card_ashen_gate_intro` |
| `story_event_boss_bark_ashen_gate` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_ashen_gate`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_ashen_gate` |
| `story_event_boss_defeat_ashen_gate` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_ashen_gate`, `NodeIs:4` | `SetFlag:story_flag_gatekeeper_defeated` | `dialogue_overlay_boss_defeat_ashen_gate` |
| `story_event_extract_ashen_gate` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_ashen_gate` | `SetFlag:story_flag_first_clue` | `story_card_extract_ashen_gate` |
| `story_event_site_intro_wolfpine_trail` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_wolfpine_trail` | `SetFlag:story_flag_intro_wolfpine_trail` | `story_card_wolfpine_trail_intro` |
| `story_event_boss_bark_wolfpine_trail` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_wolfpine_trail`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_wolfpine_trail` |
| `story_event_boss_defeat_wolfpine_trail` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_wolfpine_trail`, `NodeIs:4` | `SetFlag:story_flag_grey_fang_defeated` | `dialogue_overlay_boss_defeat_wolfpine_trail` |
| `story_event_unlock_rift_stalker` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_ashen_gate`, `SiteIs:site_wolfpine_trail` | `UnlockHero:hero_rift_stalker`, `SetFlag:story_flag_rift_stalker_joined` | `toast_unlock_rift_stalker` |

### Chapter 2: Sunken Bastion

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_site_intro_sunken_bastion` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_sunken_bastion` | `SetFlag:story_flag_intro_sunken_bastion` | `story_card_sunken_bastion_intro` |
| `story_event_boss_bark_sunken_bastion` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_sunken_bastion`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_sunken_bastion` |
| `story_event_boss_defeat_sunken_bastion` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_sunken_bastion`, `NodeIs:4` | `SetFlag:story_flag_silent_regent_defeated` | `dialogue_overlay_boss_defeat_sunken_bastion` |
| `story_event_unlock_bastion_penitent` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_sunken_bastion` | `UnlockHero:hero_bastion_penitent`, `SetFlag:story_flag_bastion_penitent_joined` | `toast_unlock_bastion_penitent` |
| `story_event_site_intro_tithe_road` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_tithe_road` | `SetFlag:story_flag_intro_tithe_road` | `story_card_tithe_road_intro` |
| `story_event_boss_bark_tithe_road` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_tithe_road`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_tithe_road` |
| `story_event_boss_defeat_tithe_road` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_tithe_road`, `NodeIs:4` | `SetFlag:story_flag_inquisitor_defeated` | `dialogue_overlay_boss_defeat_tithe_road` |
| `story_event_unlock_pale_executor` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_sunken_bastion`, `SiteIs:site_tithe_road` | `UnlockHero:hero_pale_executor`, `SetFlag:story_flag_pale_executor_joined` | `toast_unlock_pale_executor` |

### Chapter 3: Ruined Crypts

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_site_intro_ruined_crypts` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_ruined_crypts` | `SetFlag:story_flag_intro_ruined_crypts` | `story_card_ruined_crypts_intro` |
| `story_event_boss_bark_ruined_crypts` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_ruined_crypts`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_ruined_crypts` |
| `story_event_boss_defeat_ruined_crypts` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_ruined_crypts`, `NodeIs:4` | `SetFlag:story_flag_silent_archivist_defeated` | `dialogue_overlay_boss_defeat_ruined_crypts` |
| `story_event_unlock_aegis_sentinel` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_ruined_crypts` | `UnlockHero:hero_aegis_sentinel`, `SetFlag:story_flag_aegis_sentinel_joined` | `toast_unlock_aegis_sentinel` |
| `story_event_site_intro_bone_orchard` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard` | `SetFlag:story_flag_intro_bone_orchard` | `story_card_bone_orchard_intro` |
| `story_event_boss_bark_bone_orchard` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_bone_orchard` |
| `story_event_relicborn_awakening` | `BattleResolved` | 500 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard`, `NodeIs:3` | `SetFlag:story_flag_relicborn_awakened` | `dialogue_overlay_relicborn_awakening` |
| `story_event_boss_defeat_bone_orchard` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard`, `NodeIs:4` | `SetFlag:story_flag_root_watcher_defeated` | `dialogue_overlay_boss_defeat_bone_orchard` |
| `story_event_midpoint_reveal` | `ExtractCommitted` | 600 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard` | `SetFlag:story_flag_midpoint_revealed` | `story_card_midpoint_reveal` |
| `story_event_unlock_echo_savant` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_ruined_crypts`, `SiteIs:site_bone_orchard` | `UnlockHero:hero_echo_savant`, `SetFlag:story_flag_echo_savant_joined` | `toast_unlock_echo_savant` |

### Chapter 4: Glass Forest

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_site_intro_glass_forest` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_glass_forest` | `SetFlag:story_flag_intro_glass_forest` | `story_card_glass_forest_intro` |
| `story_event_boss_bark_glass_forest` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_glass_forest`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_glass_forest` |
| `story_event_boss_defeat_glass_forest` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_glass_forest`, `NodeIs:4` | `SetFlag:story_flag_prism_guardian_defeated` | `dialogue_overlay_boss_defeat_glass_forest` |
| `story_event_unlock_shardblade` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_glass_forest` | `UnlockHero:hero_shardblade`, `SetFlag:story_flag_shardblade_joined` | `toast_unlock_shardblade` |
| `story_event_site_intro_starved_menagerie` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_starved_menagerie` | `SetFlag:story_flag_intro_starved_menagerie` | `story_card_starved_menagerie_intro` |
| `story_event_boss_bark_starved_menagerie` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_starved_menagerie`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_starved_menagerie` |
| `story_event_boss_defeat_starved_menagerie` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_starved_menagerie`, `NodeIs:4` | `SetFlag:story_flag_hunger_warden_defeated` | `dialogue_overlay_boss_defeat_starved_menagerie` |
| `story_event_unlock_prism_seeker` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_glass_forest`, `SiteIs:site_starved_menagerie` | `UnlockHero:hero_prism_seeker`, `SetFlag:story_flag_prism_seeker_joined` | `toast_unlock_prism_seeker` |

### Chapter 5: Heartforge Descent

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_site_intro_heartforge_gate` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_heartforge_gate` | `SetFlag:story_flag_intro_heartforge_gate` | `story_card_heartforge_gate_intro` |
| `story_event_boss_bark_heartforge_gate` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_heartforge_gate`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_heartforge_gate` |
| `story_event_boss_defeat_heartforge_gate` | `BattleResolved` | 250 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_heartforge_gate`, `NodeIs:4` | `SetFlag:story_flag_eternal_bulwark_defeated` | `dialogue_overlay_boss_defeat_heartforge_gate` |
| `story_event_unlock_mirror_cantor` | `ExtractCommitted` | 300 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_heartforge_gate` | `UnlockHero:hero_mirror_cantor`, `SetFlag:story_flag_mirror_cantor_joined` | `toast_unlock_mirror_cantor` |
| `story_event_site_intro_worldscar_depths` | `SiteEntered` | 100 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths` | `SetFlag:story_flag_intro_worldscar_depths` | `story_card_worldscar_depths_intro` |
| `story_event_boss_bark_worldscar_depths` | `BattleStarted` | 200 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths`, `NodeIs:4` | — | `dialogue_overlay_boss_bark_worldscar_depths` |
| `story_event_final_boss` | `BattleResolved` | 700 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths`, `NodeIs:4` | `SetFlag:story_flag_final_boss_defeated` | `story_card_final_boss` |
| `story_event_campaign_complete` | `ExtractCommitted` | 900 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths` | `SetFlag:story_flag_campaign_complete`, `SetFlag:story_flag_endless_open` | `story_card_campaign_complete` |
| `story_event_endless_open` | `ExtractCommitted` | 900 | `OncePerProfile` | `ChapterIs:chapter_heartforge_descent`, `SiteIs:site_worldscar_depths`, `FlagIs:story_flag_campaign_complete` | `UnlockMode:mode_endless_cycle`, `SetFlag:story_flag_endless_unlocked` | `toast_endless_open` |

### Town Return Reactions

| story_event_id | moment | priority | once_policy | conditions | effects | presentation_key |
|---|---|---:|---|---|---|---|
| `story_event_town_return_ch1` | `TownEntered` | 150 | `OncePerProfile` | `FlagIs:story_flag_rift_stalker_joined`, `FlagNot:story_flag_town_return_ch1` | `SetFlag:story_flag_town_return_ch1` | `dialogue_overlay_town_return_ch1` |
| `story_event_town_return_ch2` | `TownEntered` | 150 | `OncePerProfile` | `FlagIs:story_flag_pale_executor_joined`, `FlagNot:story_flag_town_return_ch2` | `SetFlag:story_flag_town_return_ch2` | `dialogue_overlay_town_return_ch2` |
| `story_event_town_return_ch3` | `TownEntered` | 150 | `OncePerProfile` | `FlagIs:story_flag_echo_savant_joined`, `FlagNot:story_flag_town_return_ch3` | `SetFlag:story_flag_town_return_ch3` | `dialogue_overlay_town_return_ch3` |
| `story_event_town_return_ch4` | `TownEntered` | 150 | `OncePerProfile` | `FlagIs:story_flag_prism_seeker_joined`, `FlagNot:story_flag_town_return_ch4` | `SetFlag:story_flag_town_return_ch4` | `dialogue_overlay_town_return_ch4` |
| `story_event_town_return_ch5` | `TownEntered` | 150 | `OncePerProfile` | `FlagIs:story_flag_campaign_complete`, `FlagNot:story_flag_town_return_ch5` | `SetFlag:story_flag_town_return_ch5` | `dialogue_overlay_town_return_ch5` |

## 대사 시퀀스 표

### Chapter 1: Ashen Gate — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_ashen_gate_intro` | 0 | `hero_dawn_priest` | `loc.story.ashen_gate.intro.0` | `grim` | 원정 명분 선언 |
| `dialogue_seq_ashen_gate_intro` | 1 | `hero_pack_raider` | `loc.story.ashen_gate.intro.1` | `skeptical` | 인간 불신 표출 |
| `dialogue_seq_ashen_gate_intro` | 2 | `hero_dawn_priest` | `loc.story.ashen_gate.intro.2` | `solemn` | 재의 들판 묘사 |

### Chapter 1: Ashen Gate — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_ashen_gate` | 0 | `hero_dawn_priest` | `loc.story.ashen_gate.boss_bark.0` | `grim` | 문지기 골렘 목격 |
| `dialogue_seq_boss_bark_ashen_gate` | 1 | `hero_pack_raider` | `loc.story.ashen_gate.boss_bark.1` | `defiant` | 돌 따위에 지지 않겠다 |
| `dialogue_seq_boss_defeat_ashen_gate` | 0 | `hero_dawn_priest` | `loc.story.ashen_gate.boss_defeat.0` | `shock` | 성유물이 무기였다 |
| `dialogue_seq_boss_defeat_ashen_gate` | 1 | `hero_pack_raider` | `loc.story.ashen_gate.boss_defeat.1` | `skeptical` | 인간의 신앙 의심 |

### Chapter 1: Wolfpine Trail

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_wolfpine_trail_intro` | 0 | `hero_pack_raider` | `loc.story.wolfpine.intro.0` | `solemn` | 영역 표식 설명 |
| `dialogue_seq_wolfpine_trail_intro` | 1 | `hero_dawn_priest` | `loc.story.wolfpine.intro.1` | `grim` | 야수족 영역 진입 긴장 |
| `dialogue_seq_boss_bark_wolfpine_trail` | 0 | `hero_pack_raider` | `loc.story.wolfpine.boss_bark.0` | `bitter` | 회색 송곳니와의 재회 |
| `dialogue_seq_boss_bark_wolfpine_trail` | 1 | `hero_dawn_priest` | `loc.story.wolfpine.boss_bark.1` | `grim` | 시험을 받아들이겠다 |
| `dialogue_seq_boss_defeat_wolfpine_trail` | 0 | `hero_pack_raider` | `loc.story.wolfpine.boss_defeat.0` | `solemn` | 분노가 조작된 것일 수도 |
| `dialogue_seq_boss_defeat_wolfpine_trail` | 1 | `hero_dawn_priest` | `loc.story.wolfpine.boss_defeat.1` | `gentle` | 첫 동맹의 가능성 인정 |

### Chapter 1: Pack Raider — First Contact / Totem

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_raider_first_contact` | 0 | `hero_pack_raider` | `loc.story.raider.first_contact.0` | `defiant` | 바람 냄새로 적 수를 안다 |
| `dialogue_seq_raider_first_contact` | 1 | `hero_pack_raider` | `loc.story.raider.first_contact.1` | `skeptical` | 사제의 말은 바람에 흩어진다 |
| `dialogue_seq_raider_first_contact` | 2 | `hero_dawn_priest` | `loc.story.raider.first_contact.2` | `solemn` | 함께 걷자는 제안 |
| `dialogue_seq_raider_totem_explanation` | 0 | `hero_pack_raider` | `loc.story.raider.totem.0` | `solemn` | 토템은 기도이지 무기가 아니다 |
| `dialogue_seq_raider_totem_explanation` | 1 | `hero_pack_raider` | `loc.story.raider.totem.1` | `bitter` | 인간이 빼앗은 것의 무게 |
| `dialogue_seq_raider_totem_explanation` | 2 | `hero_dawn_priest` | `loc.story.raider.totem.2` | `shock` | 성유물과 토템이 같은 물질 |

### Chapter 1: Dawn Priest — Ashen Gate Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_priest_ashen_gate` | 0 | `hero_dawn_priest` | `loc.story.priest.ashen_gate.0` | `resolute` | 영원한 질서의 이름으로 |
| `dialogue_seq_priest_ashen_gate` | 1 | `hero_dawn_priest` | `loc.story.priest.ashen_gate.1` | `grim` | 이 문은 우리가 세운 것 |
| `dialogue_seq_priest_ashen_gate` | 2 | `hero_dawn_priest` | `loc.story.priest.ashen_gate.2` | `solemn` | 문 너머의 진실을 보겠다 |

### Chapter 2: Sunken Bastion — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_sunken_bastion_intro` | 0 | `hero_dawn_priest` | `loc.story.sunken_bastion.intro.0` | `shock` | 기울어진 요새 — 내 훈련장 |
| `dialogue_seq_sunken_bastion_intro` | 1 | `hero_pack_raider` | `loc.story.sunken_bastion.intro.1` | `skeptical` | 인간은 자기가 지은 것도 지키지 못한다 |
| `dialogue_seq_sunken_bastion_intro` | 2 | `hero_dawn_priest` | `loc.story.sunken_bastion.intro.2` | `weary` | 같은 신도끼리 싸우는 현실 |

### Chapter 2: Sunken Bastion — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_sunken_bastion` | 0 | `hero_dawn_priest` | `loc.story.sunken_bastion.boss_bark.0` | `grim` | 침묵의 집정관 대면 |
| `dialogue_seq_boss_bark_sunken_bastion` | 1 | `hero_pack_raider` | `loc.story.sunken_bastion.boss_bark.1` | `defiant` | 진실을 매장하는 자 |
| `dialogue_seq_boss_defeat_sunken_bastion` | 0 | `hero_dawn_priest` | `loc.story.sunken_bastion.boss_defeat.0` | `shock` | 성유물이 도굴품이었다 |
| `dialogue_seq_boss_defeat_sunken_bastion` | 1 | `hero_pack_raider` | `loc.story.sunken_bastion.boss_defeat.1` | `bitter` | 80년간 감춰온 약탈 |

### Chapter 2: Tithe Road — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_tithe_road_intro` | 0 | `hero_dawn_priest` | `loc.story.tithe_road.intro.0` | `grim` | 처형대와 고해소의 행렬 |
| `dialogue_seq_tithe_road_intro` | 1 | `hero_pack_raider` | `loc.story.tithe_road.intro.1` | `shock` | 자기 동족에게도 이런 짓을 |
| `dialogue_seq_tithe_road_intro` | 2 | `hero_dawn_priest` | `loc.story.tithe_road.intro.2` | `weary` | 정화라는 이름의 폭력 |

### Chapter 2: Tithe Road — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_tithe_road` | 0 | `hero_dawn_priest` | `loc.story.tithe_road.boss_bark.0` | `defiant` | 대심문관에 맞서다 |
| `dialogue_seq_boss_bark_tithe_road` | 1 | `hero_pack_raider` | `loc.story.tithe_road.boss_bark.1` | `grim` | 불꽃에 이빨을 세우겠다 |
| `dialogue_seq_boss_defeat_tithe_road` | 0 | `hero_dawn_priest` | `loc.story.tithe_road.boss_defeat.0` | `bitter` | 축복과 정화가 같은 원리 |
| `dialogue_seq_boss_defeat_tithe_road` | 1 | `hero_pack_raider` | `loc.story.tithe_road.boss_defeat.1` | `solemn` | 사제의 얼굴이 처음으로 무너졌다 |

### Chapter 2: Dawn Priest — Faith Question / Faith Crack

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_priest_faith_question` | 0 | `hero_dawn_priest` | `loc.story.priest.faith_question.0` | `weary` | 내가 섬긴 질서의 실체 |
| `dialogue_seq_priest_faith_question` | 1 | `hero_dawn_priest` | `loc.story.priest.faith_question.1` | `grim` | 기록실의 진실 앞에서 |
| `dialogue_seq_priest_faith_question` | 2 | `hero_pack_raider` | `loc.story.priest.faith_question.2` | `gentle` | 의심하는 것도 용기 |
| `dialogue_seq_priest_faith_crack` | 0 | `hero_dawn_priest` | `loc.story.priest.faith_crack.0` | `bitter` | 도굴품 위에 세운 신앙 |
| `dialogue_seq_priest_faith_crack` | 1 | `hero_dawn_priest` | `loc.story.priest.faith_crack.1` | `weary` | 사제 인장의 무게 |
| `dialogue_seq_priest_faith_crack` | 2 | `hero_pack_raider` | `loc.story.priest.faith_crack.2` | `solemn` | 무너진 믿음도 흙이 된다 |

### Chapter 2: Pack Raider — Kingdom Anger

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_raider_kingdom_anger` | 0 | `hero_pack_raider` | `loc.story.raider.kingdom_anger.0` | `defiant` | 인간의 요새가 우리 땅을 짓밟았다 |
| `dialogue_seq_raider_kingdom_anger` | 1 | `hero_pack_raider` | `loc.story.raider.kingdom_anger.1` | `bitter` | 피 냄새가 돌담에 배어 있다 |
| `dialogue_seq_raider_kingdom_anger` | 2 | `hero_dawn_priest` | `loc.story.raider.kingdom_anger.2` | `solemn` | 부정하지 않겠다 |

### Chapter 3: Ruined Crypts — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_ruined_crypts_intro` | 0 | `hero_grave_hexer` | `loc.story.ruined_crypts.intro.0` | `solemn` | 고향에 돌아왔다 |
| `dialogue_seq_ruined_crypts_intro` | 1 | `hero_grave_hexer` | `loc.story.ruined_crypts.intro.1` | `gentle` | 속삭임은 공격이 아니라 부름 |
| `dialogue_seq_ruined_crypts_intro` | 2 | `hero_dawn_priest` | `loc.story.ruined_crypts.intro.2` | `grim` | 죽음의 땅이라 들었지만 |

### Chapter 3: Ruined Crypts — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_ruined_crypts` | 0 | `hero_grave_hexer` | `loc.story.ruined_crypts.boss_bark.0` | `solemn` | 침묵의 기록관 — 가장 오래된 기억 |
| `dialogue_seq_boss_bark_ruined_crypts` | 1 | `hero_dawn_priest` | `loc.story.ruined_crypts.boss_bark.1` | `grim` | 기록을 지키는 자와 싸운다 |
| `dialogue_seq_boss_defeat_ruined_crypts` | 0 | `hero_grave_hexer` | `loc.story.ruined_crypts.boss_defeat.0` | `gentle` | 기록관이 열쇠를 넘겼다 |
| `dialogue_seq_boss_defeat_ruined_crypts` | 1 | `hero_dawn_priest` | `loc.story.ruined_crypts.boss_defeat.1` | `solemn` | 수복자로 인정받았다 |

### Chapter 3: Bone Orchard — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_bone_orchard_intro` | 0 | `hero_grave_hexer` | `loc.story.bone_orchard.intro.0` | `grim` | 기억 밀도가 묘역의 수십 배 |
| `dialogue_seq_bone_orchard_intro` | 1 | `hero_pack_raider` | `loc.story.bone_orchard.intro.1` | `defiant` | 뼈가 뿌리처럼 자란 나무 |
| `dialogue_seq_bone_orchard_intro` | 2 | `hero_dawn_priest` | `loc.story.bone_orchard.intro.2` | `solemn` | 무언가 깨어나려 한다 |

### Chapter 3: Bone Orchard — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_bone_orchard` | 0 | `hero_grave_hexer` | `loc.story.bone_orchard.boss_bark.0` | `grim` | 뿌리의 관망자 — 1800년의 수문장 |
| `dialogue_seq_boss_bark_bone_orchard` | 1 | `hero_dawn_priest` | `loc.story.bone_orchard.boss_bark.1` | `resolute` | 관망자를 설득할 수 없다면 쓰러뜨린다 |
| `dialogue_seq_boss_defeat_bone_orchard` | 0 | `hero_grave_hexer` | `loc.story.bone_orchard.boss_defeat.0` | `solemn` | 무지 때문이었다는 선언 |
| `dialogue_seq_boss_defeat_bone_orchard` | 1 | `hero_dawn_priest` | `loc.story.bone_orchard.boss_defeat.1` | `weary` | 세 세력의 피해가 한눈에 |

### Chapter 3: Relicborn Awakening

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_relicborn_awakening` | 0 | `hero_grave_hexer` | `loc.story.relicborn.awaken.0` | `shock` | 각성 감지 — 격자가 깨어난다 |
| `dialogue_seq_relicborn_awakening` | 1 | `hero_echo_savant` | `loc.story.relicborn.awaken.1` | `solemn` | 수문장 자기소개 |
| `dialogue_seq_relicborn_awakening` | 2 | `hero_dawn_priest` | `loc.story.relicborn.awaken.2` | `shock` | 봉인 그물의 수호자가 있었다 |
| `dialogue_seq_relicborn_awakening` | 3 | `hero_pack_raider` | `loc.story.relicborn.awaken.3` | `skeptical` | 또 다른 세력이라 |

### Chapter 3: Grave Hexer — Memory Introduction / Ancient Coexistence

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_hexer_memory_intro` | 0 | `hero_grave_hexer` | `loc.story.hexer.memory_intro.0` | `solemn` | 400년 전 일을 어제처럼 기억한다 |
| `dialogue_seq_hexer_memory_intro` | 1 | `hero_grave_hexer` | `loc.story.hexer.memory_intro.1` | `gentle` | 기억은 무게가 있다 |
| `dialogue_seq_hexer_memory_intro` | 2 | `hero_dawn_priest` | `loc.story.hexer.memory_intro.2` | `solemn` | 산 자보다 오래 기억하는 자 |
| `dialogue_seq_hexer_ancient_coexistence` | 0 | `hero_grave_hexer` | `loc.story.hexer.ancient_coexist.0` | `gentle` | 네 종족이 같은 물을 마셨다 |
| `dialogue_seq_hexer_ancient_coexistence` | 1 | `hero_grave_hexer` | `loc.story.hexer.ancient_coexist.1` | `bitter` | 그 기억을 지운 것이 왕국의 첫 번째 죄 |
| `dialogue_seq_hexer_ancient_coexistence` | 2 | `hero_pack_raider` | `loc.story.hexer.ancient_coexist.2` | `solemn` | 토템이 기억하고 있었다 |

### Chapter 3: Echo Savant — Awakening / Lattice Assessment

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_savant_awakening` | 0 | `hero_echo_savant` | `loc.story.savant.awakening.0` | `solemn` | 공명이 나를 깨웠다 |
| `dialogue_seq_savant_awakening` | 1 | `hero_echo_savant` | `loc.story.savant.awakening.1` | `grim` | 격자의 풍화가 임계에 가깝다 |
| `dialogue_seq_savant_awakening` | 2 | `hero_grave_hexer` | `loc.story.savant.awakening.2` | `solemn` | 기억과 공명이 같은 파장 |
| `dialogue_seq_savant_lattice_assessment` | 0 | `hero_echo_savant` | `loc.story.savant.lattice.0` | `grim` | Heartforge는 기억을 에너지로 변환한다 |
| `dialogue_seq_savant_lattice_assessment` | 1 | `hero_echo_savant` | `loc.story.savant.lattice.1` | `solemn` | 부산물은 적대감 — 제어 없이 증폭된다 |
| `dialogue_seq_savant_lattice_assessment` | 2 | `hero_dawn_priest` | `loc.story.savant.lattice.2` | `shock` | 우리 모두가 기계의 부산물에 조종당했다 |

### Chapter 3: Midpoint Reveal

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_midpoint_reveal` | 0 | `hero_echo_savant` | `loc.story.midpoint.reveal.0` | `solemn` | 조사가 아니라 순환 종결이 목적이다 |
| `dialogue_seq_midpoint_reveal` | 1 | `hero_dawn_priest` | `loc.story.midpoint.reveal.1` | `resolute` | 왕국의 죄를 인정하고 나아간다 |
| `dialogue_seq_midpoint_reveal` | 2 | `hero_pack_raider` | `loc.story.midpoint.reveal.2` | `solemn` | 씨족의 분노 너머를 보겠다 |
| `dialogue_seq_midpoint_reveal` | 3 | `hero_grave_hexer` | `loc.story.midpoint.reveal.3` | `resolute` | 기억의 정의를 위해 |

### Chapter 3: Dawn Priest — Midpoint Shock

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_priest_midpoint_shock` | 0 | `hero_dawn_priest` | `loc.story.priest.midpoint.0` | `shock` | 교리가 기계의 배기가스 위에 |
| `dialogue_seq_priest_midpoint_shock` | 1 | `hero_dawn_priest` | `loc.story.priest.midpoint.1` | `bitter` | 사제로서 무엇을 지켜왔나 |
| `dialogue_seq_priest_midpoint_shock` | 2 | `hero_pack_raider` | `loc.story.priest.midpoint.2` | `gentle` | 부서진 것에서 새 뿌리가 난다 |

### Chapter 4: Glass Forest — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_glass_forest_intro` | 0 | `hero_echo_savant` | `loc.story.glass_forest.intro.0` | `solemn` | 격자 폭발의 결정 — 전쟁의 화석 |
| `dialogue_seq_glass_forest_intro` | 1 | `hero_pack_raider` | `loc.story.glass_forest.intro.1` | `bitter` | 결정 안에 씨족 전사가 얼어 있다 |
| `dialogue_seq_glass_forest_intro` | 2 | `hero_dawn_priest` | `loc.story.glass_forest.intro.2` | `weary` | 아름다움과 파괴가 공존한다 |

### Chapter 4: Glass Forest — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_glass_forest` | 0 | `hero_echo_savant` | `loc.story.glass_forest.boss_bark.0` | `grim` | 프리즘 수호자 — 결정화된 격자 |
| `dialogue_seq_boss_bark_glass_forest` | 1 | `hero_pack_raider` | `loc.story.glass_forest.boss_bark.1` | `defiant` | 유리 속 전사들의 복수 |
| `dialogue_seq_boss_defeat_glass_forest` | 0 | `hero_echo_savant` | `loc.story.glass_forest.boss_defeat.0` | `solemn` | 도관이 열렸다 — 도난의 통로 |
| `dialogue_seq_boss_defeat_glass_forest` | 1 | `hero_dawn_priest` | `loc.story.glass_forest.boss_defeat.1` | `bitter` | 방어 장치가 훔친 파편이었다 |

### Chapter 4: Starved Menagerie — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_starved_menagerie_intro` | 0 | `hero_pack_raider` | `loc.story.starved_menagerie.intro.0` | `bitter` | 우리가 돌보던 짐승들이었다 |
| `dialogue_seq_starved_menagerie_intro` | 1 | `hero_grave_hexer` | `loc.story.starved_menagerie.intro.1` | `solemn` | 변이체의 잔류 기억 — 주인을 찾고 있다 |
| `dialogue_seq_starved_menagerie_intro` | 2 | `hero_echo_savant` | `loc.story.starved_menagerie.intro.2` | `grim` | Heartforge 오염이 자연까지 |

### Chapter 4: Starved Menagerie — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_starved_menagerie` | 0 | `hero_pack_raider` | `loc.story.starved_menagerie.boss_bark.0` | `defiant` | 기아의 관리자에게 이빨을 세운다 |
| `dialogue_seq_boss_bark_starved_menagerie` | 1 | `hero_echo_savant` | `loc.story.starved_menagerie.boss_bark.1` | `grim` | 생명력 흡수 패턴 감지 |
| `dialogue_seq_boss_defeat_starved_menagerie` | 0 | `hero_pack_raider` | `loc.story.starved_menagerie.boss_defeat.0` | `solemn` | 공명석이 드러났다 |
| `dialogue_seq_boss_defeat_starved_menagerie` | 1 | `hero_echo_savant` | `loc.story.starved_menagerie.boss_defeat.1` | `solemn` | 순환을 끊을 열쇠의 조각 |

### Chapter 4: Pack Raider — Glass Forest Grief

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_raider_glass_forest_grief` | 0 | `hero_pack_raider` | `loc.story.raider.glass_grief.0` | `bitter` | 200년 전 죽은 전사 — 장례도 치르지 못했다 |
| `dialogue_seq_raider_glass_forest_grief` | 1 | `hero_pack_raider` | `loc.story.raider.glass_grief.1` | `solemn` | 바람이 결정을 울린다 — 곡소리 같다 |
| `dialogue_seq_raider_glass_forest_grief` | 2 | `hero_grave_hexer` | `loc.story.raider.glass_grief.2` | `gentle` | 기억이 남아 있다면 장례는 아직 끝나지 않았다 |

### Chapter 4: Grave Hexer — Memory Debt

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_hexer_memory_debt` | 0 | `hero_grave_hexer` | `loc.story.hexer.memory_debt.0` | `bitter` | 기억을 지우는 것은 우리에게 가장 큰 죄 |
| `dialogue_seq_hexer_memory_debt` | 1 | `hero_grave_hexer` | `loc.story.hexer.memory_debt.1` | `solemn` | 왕국의 정화 의식이 바로 그 죄를 저질렀다 |
| `dialogue_seq_hexer_memory_debt` | 2 | `hero_dawn_priest` | `loc.story.hexer.memory_debt.2` | `weary` | 부정할 수 없다 |

### Chapter 4: Echo Savant — Resonance Adjustment

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_savant_resonance_adj` | 0 | `hero_echo_savant` | `loc.story.savant.resonance.0` | `solemn` | 토템 주파수를 격자에 맞춘다 |
| `dialogue_seq_savant_resonance_adj` | 1 | `hero_echo_savant` | `loc.story.savant.resonance.1` | `gentle` | 간섭이 사라지면 맑은 의식이 돌아온다 |
| `dialogue_seq_savant_resonance_adj` | 2 | `hero_pack_raider` | `loc.story.savant.resonance.2` | `shock` | 이것이 본래의 감각인가 |

### Chapter 5: Heartforge Gate — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_heartforge_gate_intro` | 0 | `hero_echo_savant` | `loc.story.heartforge_gate.intro.0` | `solemn` | 관문의 네 문양 — 공존 협약의 증거 |
| `dialogue_seq_heartforge_gate_intro` | 1 | `hero_dawn_priest` | `loc.story.heartforge_gate.intro.1` | `resolute` | 왕국의 죄를 인정하고 격자 복원에 동의한다 |
| `dialogue_seq_heartforge_gate_intro` | 2 | `hero_pack_raider` | `loc.story.heartforge_gate.intro.2` | `solemn` | 영역의 안정은 수복으로만 온다 |
| `dialogue_seq_heartforge_gate_intro` | 3 | `hero_grave_hexer` | `loc.story.heartforge_gate.intro.3` | `solemn` | 기억 보존과 정화가 양립하는지 확인해야 한다 |

### Chapter 5: Heartforge Gate — Boss Barks & Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_heartforge_gate` | 0 | `hero_dawn_priest` | `loc.story.heartforge_gate.boss_bark.0` | `resolute` | 영원의 대문장 — 마지막 방벽 |
| `dialogue_seq_boss_bark_heartforge_gate` | 1 | `hero_grave_hexer` | `loc.story.heartforge_gate.boss_bark.1` | `grim` | 기억을 무기로 쓰는 존재 |
| `dialogue_seq_boss_defeat_heartforge_gate` | 0 | `hero_dawn_priest` | `loc.story.heartforge_gate.boss_defeat.0` | `weary` | 가장 고통스러운 기억을 넘었다 |
| `dialogue_seq_boss_defeat_heartforge_gate` | 1 | `hero_echo_savant` | `loc.story.heartforge_gate.boss_defeat.1` | `solemn` | 정화 코드가 노출되었다 |

### Chapter 5: Worldscar Depths — Site Intro

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_worldscar_depths_intro` | 0 | `hero_echo_savant` | `loc.story.worldscar_depths.intro.0` | `solemn` | 3000년의 기억이 벽면에 투사된다 |
| `dialogue_seq_worldscar_depths_intro` | 1 | `hero_dawn_priest` | `loc.story.worldscar_depths.intro.1` | `weary` | 가한 피해와 받은 피해를 동시에 본다 |
| `dialogue_seq_worldscar_depths_intro` | 2 | `hero_pack_raider` | `loc.story.worldscar_depths.intro.2` | `solemn` | 바람의 기억도 여기 있다 |

### Chapter 5: Worldscar Depths — Final Boss & Campaign Complete

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_boss_bark_worldscar_depths` | 0 | `hero_echo_savant` | `loc.story.worldscar_depths.boss_bark.0` | `grim` | 순환의 핵 — 3000년의 적대감 응축 |
| `dialogue_seq_boss_bark_worldscar_depths` | 1 | `hero_dawn_priest` | `loc.story.worldscar_depths.boss_bark.1` | `resolute` | 이 순환을 끝낸다 |
| `dialogue_seq_boss_bark_worldscar_depths` | 2 | `hero_pack_raider` | `loc.story.worldscar_depths.boss_bark.2` | `defiant` | 이빨과 발톱을 세운다 |
| `dialogue_seq_boss_bark_worldscar_depths` | 3 | `hero_grave_hexer` | `loc.story.worldscar_depths.boss_bark.3` | `resolute` | 기억의 정의를 위해 |
| `dialogue_seq_final_boss_defeat` | 0 | `hero_dawn_priest` | `loc.story.final_boss.defeat.0` | `weary` | 순환이 멈추었다 |
| `dialogue_seq_final_boss_defeat` | 1 | `hero_echo_savant` | `loc.story.final_boss.defeat.1` | `solemn` | 격자가 복원된다 |
| `dialogue_seq_final_boss_defeat` | 2 | `hero_pack_raider` | `loc.story.final_boss.defeat.2` | `gentle` | 바람이 맑아졌다 |
| `dialogue_seq_final_boss_defeat` | 3 | `hero_grave_hexer` | `loc.story.final_boss.defeat.3` | `gentle` | 기억이 평화로운 장면으로 바뀐다 |
| `dialogue_seq_campaign_complete` | 0 | `hero_dawn_priest` | `loc.story.campaign.complete.0` | `resolute` | 부서진 신앙 위에 새 의미를 세우겠다 |
| `dialogue_seq_campaign_complete` | 1 | `hero_pack_raider` | `loc.story.campaign.complete.1` | `solemn` | 씨족에게 진실을 전하러 돌아간다 |
| `dialogue_seq_campaign_complete` | 2 | `hero_grave_hexer` | `loc.story.campaign.complete.2` | `resolute` | 복원된 기억을 모든 세력에 전한다 |
| `dialogue_seq_campaign_complete` | 3 | `hero_echo_savant` | `loc.story.campaign.complete.3` | `solemn` | 격자 수호를 이어가겠다 — 심부에 남는다 |

### Chapter 5: Dawn Priest — Final Prayer

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_priest_final_prayer` | 0 | `hero_dawn_priest` | `loc.story.priest.final_prayer.0` | `solemn` | 영원한 질서는 없었다 |
| `dialogue_seq_priest_final_prayer` | 1 | `hero_dawn_priest` | `loc.story.priest.final_prayer.1` | `resolute` | 그래도 기도할 수 있다 |
| `dialogue_seq_priest_final_prayer` | 2 | `hero_dawn_priest` | `loc.story.priest.final_prayer.2` | `gentle` | 문 너머에서 시작하는 새 질서 |

### Chapter 5: Pack Raider — Oath Swear

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_raider_oath_swear` | 0 | `hero_pack_raider` | `loc.story.raider.oath.0` | `resolute` | 이빨로 서약한다 — 순환은 끝났다 |
| `dialogue_seq_raider_oath_swear` | 1 | `hero_pack_raider` | `loc.story.raider.oath.1` | `solemn` | 바람이 다시 깨끗하다 |
| `dialogue_seq_raider_oath_swear` | 2 | `hero_pack_raider` | `loc.story.raider.oath.2` | `gentle` | 씨족에게 돌아가 진실을 전한다 |

### Chapter 5: Grave Hexer — Final Testimony

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_hexer_final_testimony` | 0 | `hero_grave_hexer` | `loc.story.hexer.final_testimony.0` | `solemn` | 400년의 기억을 증언한다 |
| `dialogue_seq_hexer_final_testimony` | 1 | `hero_grave_hexer` | `loc.story.hexer.final_testimony.1` | `gentle` | 기억은 이제 빚이 아니라 유산이다 |
| `dialogue_seq_hexer_final_testimony` | 2 | `hero_grave_hexer` | `loc.story.hexer.final_testimony.2` | `resolute` | 모든 세력에 전하겠다 — 진실을 |

### Chapter 5: Echo Savant — Seal Decision

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_savant_seal_decision` | 0 | `hero_echo_savant` | `loc.story.savant.seal.0` | `solemn` | 정화하면 기억은 남지만 에너지는 영구 상실 |
| `dialogue_seq_savant_seal_decision` | 1 | `hero_echo_savant` | `loc.story.savant.seal.1` | `resolute` | 격자를 수호하겠다 — 심부에 남는다 |
| `dialogue_seq_savant_seal_decision` | 2 | `hero_echo_savant` | `loc.story.savant.seal.2` | `gentle` | 공명은 끝나지 않는다 — 형태만 바뀐다 |

### Town Return Reactions

| dialogue_seq_id | line_index | speaker_id | text_key | emote | note |
|---|---:|---|---|---|---|
| `dialogue_seq_town_return_ch1` | 0 | `hero_dawn_priest` | `loc.story.town.return_ch1.0` | `weary` | 문 밖의 세계는 예상과 달랐다 |
| `dialogue_seq_town_return_ch1` | 1 | `hero_pack_raider` | `loc.story.town.return_ch1.1` | `skeptical` | 인간의 마을 — 이빨을 감추고 있겠다 |
| `dialogue_seq_town_return_ch2` | 0 | `hero_dawn_priest` | `loc.story.town.return_ch2.0` | `bitter` | 같은 신도가 같은 신도를 매장했다 |
| `dialogue_seq_town_return_ch2` | 1 | `hero_pack_raider` | `loc.story.town.return_ch2.1` | `solemn` | 요새의 돌담에서 피 냄새가 났다 |
| `dialogue_seq_town_return_ch3` | 0 | `hero_grave_hexer` | `loc.story.town.return_ch3.0` | `solemn` | 기억의 보관소를 열었다 |
| `dialogue_seq_town_return_ch3` | 1 | `hero_echo_savant` | `loc.story.town.return_ch3.1` | `solemn` | 격자의 풍화를 확인했다 — 시간이 없다 |
| `dialogue_seq_town_return_ch3` | 2 | `hero_dawn_priest` | `loc.story.town.return_ch3.2` | `resolute` | 조사에서 순환 종결로 목적이 바뀌었다 |
| `dialogue_seq_town_return_ch4` | 0 | `hero_pack_raider` | `loc.story.town.return_ch4.0` | `solemn` | 유리 숲의 전사들을 기억한다 |
| `dialogue_seq_town_return_ch4` | 1 | `hero_echo_savant` | `loc.story.town.return_ch4.1` | `resolute` | 공명석으로 순환 종결의 실마리를 잡았다 |
| `dialogue_seq_town_return_ch5` | 0 | `hero_dawn_priest` | `loc.story.town.return_ch5.0` | `gentle` | 순환이 끝났다 — 세계의 수습은 이제부터 |
| `dialogue_seq_town_return_ch5` | 1 | `hero_pack_raider` | `loc.story.town.return_ch5.1` | `gentle` | 바람이 달라졌다 — 깨끗하다 |
| `dialogue_seq_town_return_ch5` | 2 | `hero_grave_hexer` | `loc.story.town.return_ch5.2` | `resolute` | 기억의 유산을 전할 준비가 되었다 |

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
