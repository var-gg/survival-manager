# 챕터 비트 시트

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/chapter-beat-sheet.md`
- 관련문서:
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/narrative/narrative-pacing-formula.md`
  - `docs/02_design/narrative/dialogue-event-schema.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`

## 목적

chapter/site/node 단위의 서사 비트, 감정값, reveal, 영웅 합류 시점을 표로 고정한다. 이 문서는 node-level narrative truth의 기준이다.

## 비트 작성 규칙

- 각 node는 반드시 `type`, `beat_label`, `emotion_targets(T/R/H/C)`, `difficulty`, `unlock`을 가져야 한다.
- topology나 encounter lane 자체는 `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`를 참조하고 여기서는 반복하지 않는다.
- 모든 감정값은 `narrative-pacing-formula.md`의 수식과 일치해야 한다.

## 50노드 구조표

### Chapter 1: Ashen Gate

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_ashen_gate` | 1 | `skirmish` | 경계 돌파와 징후 확인 | 28 | 69 | 8 | 3 | 1.4 | — |
| `site_ashen_gate` | 2 | `skirmish` | 초기 교전 규칙 확립 | 33 | 63 | 10 | 3 | 1.7 | — |
| `site_ashen_gate` | 3 | `elite` | 엘리트 경비 격파 | 40 | 54 | 9 | 10 | 2.2 | — |
| `site_ashen_gate` | 4 | `boss` | 문지기 처치 | 50 | 40 | 6 | 32 | 2.9 | — |
| `site_ashen_gate` | 5 | `extract` | 첫 단서 회수 | 10 | 100 | 18 | 20 | 1.2 | — |
| `site_wolfpine_trail` | 1 | `skirmish` | 짐승길 추적 | 36 | 66 | 8 | 4 | 2.0 | — |
| `site_wolfpine_trail` | 2 | `skirmish` | 오해에서 동맹으로 | 41 | 60 | 10 | 4 | 2.3 | — |
| `site_wolfpine_trail` | 3 | `elite` | 매복 진실 파악 | 48 | 51 | 9 | 12 | 2.8 | — |
| `site_wolfpine_trail` | 4 | `boss` | 팩 우두머리 격파 | 58 | 37 | 6 | 60 | 3.5 | — |
| `site_wolfpine_trail` | 5 | `extract` | 정찰 동맹 확보 | 18 | 100 | 18 | 38 | 1.8 | `hero_rift_stalker` |

### Chapter 2: Sunken Bastion

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_sunken_bastion` | 1 | `skirmish` | 주둔지 진입 | 44 | 62 | 6 | 5 | 2.7 | — |
| `site_sunken_bastion` | 2 | `skirmish` | 호위-보호 패턴 확인 | 49 | 56 | 8 | 5 | 3.0 | — |
| `site_sunken_bastion` | 3 | `elite` | 보급 기록 탈취 | 56 | 47 | 7 | 14 | 3.5 | — |
| `site_sunken_bastion` | 4 | `boss` | 타락 지휘관 처치 | 66 | 33 | 4 | 40 | 4.2 | — |
| `site_sunken_bastion` | 5 | `extract` | 요새 배신 증거 확보 | 26 | 96 | 16 | 24 | 2.5 | `hero_bastion_penitent` |
| `site_tithe_road` | 1 | `skirmish` | 십일조 행렬 습격 | 53 | 58 | 6 | 6 | 3.3 | — |
| `site_tithe_road` | 2 | `skirmish` | 표식-처형 위협 학습 | 58 | 52 | 8 | 6 | 3.6 | — |
| `site_tithe_road` | 3 | `elite` | 정화 의식의 실체 확인 | 65 | 43 | 7 | 16 | 4.1 | — |
| `site_tithe_road` | 4 | `boss` | 심문관 격파 | 75 | 29 | 4 | 62 | 4.8 | — |
| `site_tithe_road` | 5 | `extract` | 신앙 균열이 표면화 | 35 | 92 | 16 | 40 | 3.1 | `hero_pale_executor` |

### Chapter 3: Ruined Crypts

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_ruined_crypts` | 1 | `skirmish` | 묘역 하강 | 63 | 54 | 4 | 7 | 4.0 | — |
| `site_ruined_crypts` | 2 | `skirmish` | 언데드 기억 회수 | 68 | 48 | 6 | 7 | 4.3 | — |
| `site_ruined_crypts` | 3 | `elite` | 소환 압력 대응 | 75 | 39 | 5 | 18 | 4.8 | — |
| `site_ruined_crypts` | 4 | `boss` | 묘지 수문장 처치 | 85 | 25 | 2 | 48 | 5.5 | — |
| `site_ruined_crypts` | 5 | `extract` | 유물 수호 신호 감지 | 45 | 88 | 14 | 28 | 3.8 | `hero_aegis_sentinel` |
| `site_bone_orchard` | 1 | `skirmish` | 유골 과수원 진입 | 74 | 49 | 4 | 8 | 4.6 | — |
| `site_bone_orchard` | 2 | `skirmish` | 왕국의 흡수 계획 폭로 | 79 | 43 | 6 | 8 | 4.9 | — |
| `site_bone_orchard` | 3 | `elite` | Relicborn 각성 | 86 | 34 | 5 | 20 | 5.4 | — |
| `site_bone_orchard` | 4 | `boss` | 중심 수문장 격파 | 96 | 20 | 2 | 72 | 6.1 | — |
| `site_bone_orchard` | 5 | `extract` | 중반 반전 정리 | 56 | 83 | 14 | 56 | 4.4 | `hero_echo_savant` |

### Chapter 4: Glass Forest

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_glass_forest` | 1 | `skirmish` | 유리 숲 도달 | 83 | 45 | 2 | 9 | 5.3 | — |
| `site_glass_forest` | 2 | `skirmish` | 야수족 피해 확인 | 88 | 39 | 4 | 9 | 5.6 | — |
| `site_glass_forest` | 3 | `elite` | 복합 편성 시험 | 95 | 30 | 3 | 22 | 6.1 | — |
| `site_glass_forest` | 4 | `boss` | 도관 수호자 격파 | 100 | 16 | 0 | 56 | 6.8 | — |
| `site_glass_forest` | 5 | `extract` | Relicborn 전사 합류 | 65 | 79 | 12 | 32 | 5.1 | `hero_shardblade` |
| `site_starved_menagerie` | 1 | `skirmish` | 굶주린 우리 진입 | 89 | 42 | 2 | 10 | 5.9 | — |
| `site_starved_menagerie` | 2 | `skirmish` | 지속 피해 압박 | 94 | 36 | 4 | 10 | 6.2 | — |
| `site_starved_menagerie` | 3 | `elite` | 혼성 보스 전조 | 100 | 27 | 3 | 24 | 6.7 | — |
| `site_starved_menagerie` | 4 | `boss` | 사육장 관리자 격파 | 100 | 13 | 0 | 80 | 7.4 | — |
| `site_starved_menagerie` | 5 | `extract` | Relicborn 사수 합류 | 71 | 76 | 12 | 64 | 5.7 | `hero_prism_seeker` |

### Chapter 5: Heartforge Descent

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_heartforge_gate` | 1 | `skirmish` | 심장로 진입 | 94 | 40 | 0 | 11 | 6.6 | — |
| `site_heartforge_gate` | 2 | `skirmish` | 4세력 이해관계 수렴 | 99 | 34 | 2 | 11 | 6.9 | — |
| `site_heartforge_gate` | 3 | `elite` | 결전 전 맹세 | 100 | 25 | 1 | 26 | 7.4 | — |
| `site_heartforge_gate` | 4 | `boss` | 대문 수문장 격파 | 100 | 11 | 0 | 86 | 8.1 | — |
| `site_heartforge_gate` | 5 | `extract` | 최종 정화 코드 확보 | 76 | 74 | 10 | 72 | 6.4 | `hero_mirror_cantor` |
| `site_worldscar_depths` | 1 | `skirmish` | 심부 하강 | 98 | 38 | 0 | 12 | 7.2 | — |
| `site_worldscar_depths` | 2 | `skirmish` | 핵심 진실 수용 | 100 | 32 | 2 | 12 | 7.5 | — |
| `site_worldscar_depths` | 3 | `elite` | 돌이킬 수 없는 선택 | 100 | 23 | 1 | 28 | 8.0 | — |
| `site_worldscar_depths` | 4 | `boss` | 최종 적대체 격파 | 100 | 9 | 0 | 100 | 8.7 | — |
| `site_worldscar_depths` | 5 | `extract` | 종결과 엔드게임 해금 | 80 | 72 | 10 | 92 | 7.0 | `mode_endless_cycle` |

## 대사 앵커 표

| node_ref | mandatory_event_id | optional_event_id | note |
|---|---|---|---|
| `chapter_ashen_gate/site_ashen_gate/1` | `story_event_site_intro_ashen_gate` | — | 캠페인 첫 진입 |
| `chapter_ashen_gate/site_wolfpine_trail/5` | `story_event_unlock_rift_stalker` | `story_event_beastkin_alliance` | 첫 specialist 전환 |
| `chapter_sunken_bastion/site_sunken_bastion/5` | `story_event_unlock_bastion_penitent` | — | 요새 배신 증거 |
| `chapter_sunken_bastion/site_tithe_road/5` | `story_event_unlock_pale_executor` | `story_event_faith_crack` | 신앙 균열 |
| `chapter_ruined_crypts/site_ruined_crypts/5` | `story_event_unlock_aegis_sentinel` | `story_event_relicborn_teaser` | Relicborn 첫 접촉 |
| `chapter_ruined_crypts/site_bone_orchard/3` | `story_event_relicborn_awakening` | — | Relicborn 각성 (midpoint) |
| `chapter_ruined_crypts/site_bone_orchard/5` | `story_event_midpoint_reveal` | `story_event_unlock_echo_savant` | midpoint extraction |
| `chapter_glass_forest/site_glass_forest/5` | `story_event_unlock_shardblade` | — | Relicborn 전사 합류 |
| `chapter_glass_forest/site_starved_menagerie/5` | `story_event_unlock_prism_seeker` | — | Relicborn 사수 합류 |
| `chapter_heartforge_descent/site_heartforge_gate/5` | `story_event_unlock_mirror_cantor` | `story_event_final_code` | 최종 specialist |
| `chapter_heartforge_descent/site_worldscar_depths/4` | `story_event_final_boss` | — | 최종 적대체 |
| `chapter_heartforge_descent/site_worldscar_depths/5` | `story_event_campaign_complete` | `story_event_endless_open` | 종결과 endless 해금 |

## 영웅 합류 / 해금 규칙

| hero_id | join_site | join_node | gate_type |
|---|---|---:|---|
| `hero_rift_stalker` | `site_wolfpine_trail` | 5 | extract commit |
| `hero_bastion_penitent` | `site_sunken_bastion` | 5 | extract commit |
| `hero_pale_executor` | `site_tithe_road` | 5 | extract commit |
| `hero_aegis_sentinel` | `site_ruined_crypts` | 5 | extract commit |
| `hero_echo_savant` | `site_bone_orchard` | 5 | extract commit |
| `hero_shardblade` | `site_glass_forest` | 5 | extract commit |
| `hero_prism_seeker` | `site_starved_menagerie` | 5 | extract commit |
| `hero_mirror_cantor` | `site_heartforge_gate` | 5 | extract commit |

## 작성 지침

- 모든 감정값은 `narrative-pacing-formula.md`의 수식과 일치해야 한다.
- 모든 unlock은 `story-gating-and-unlock-rules.md`와 양방향으로 확인한다.
- 한 node에 너무 많은 reveal을 몰아넣지 않는다.
