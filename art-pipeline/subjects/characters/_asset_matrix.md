# Character image asset matrix

이 문서는 Pindoc wiki 기준 캐릭터 이미지 생성 대상을 고정한다. 실제 목록과 검사는 `art-pipeline/config/character_asset_manifest.yaml`와 `art-pipeline/scripts/audit_character_assets.py`가 source-of-truth다.

## Source of truth

- 캐릭터 목록: `pindoc://wiki-character-lore-registry-mirror`
- 대사/포트레잇 필요성: `pindoc://analysis-character-operation-layer`
- 산출물 표준: `pindoc://analysis-character-asset-matrix-dawn-priest`
- 외형 기준: 각 `wiki-character-*`의 `외모`, `P09 visual spec (atlas 인용)`, `이미지 생성 기준`

정책: **대사가 있는 Pindoc wiki 캐릭터는 모두 감정 face와 VN bust 포트레잇 생성 대상**이다. Support, Background, NPC라도 dialogue 라인 예산이 있으면 전투 최소셋으로 낮추지 않는다.

## 스타일 기준

- `style/style-anchor-common.md` + `style/style-anchor-character.md`를 기본으로 쓴다.
- 방향성은 premium mobile JRPG / gacha-banner character art다. 실사풍이 아니라 anime-leaning, painterly base, hard cel edge는 턱선/무기/천 접힘 같은 일부 경계에만 쓴다.
- P09 캡쳐는 low-poly 3D reference다. 최종 일러스트는 silhouette, color zone, outfit layout을 유지하되 더 섬세하고 화려하게 그린다.
- 모든 character/sheet 출력은 `#FF00FF` 단색 배경과 1-2px dark outline을 요구한다.
- 캐릭터 인지는 헤어 색, 눈 색, 의상 주조색, 무기/소품, stance를 동시에 고정해서 만든다. 같은 소속이어도 palette와 silhouette을 일부러 분리한다.

## 산출물 종류

| key | 구성 | 용도 |
| --- | --- | --- |
| `anchor` | `ref/characters/{id}/anchor.png` | P09 캡쳐 reference |
| `portrait_full_default` | 1024x1536, 2:3, 전신 3/4 view | wiki, recruit/card, canonical full portrait |
| `face_emotion_sheet` | 4x2 face close-up sheet, 8감정 | narrative/dialogue face variants |
| `face_combat_state_sheet` | 3x2 face close-up sheet, 6상태 | battle UI 상태 portrait |
| `bust_emotion_sheet_R` | 4x2 vertical bust sheet, 8감정, camera-right | VN/dialogue 왼쪽 배치 |
| `bust_emotion_sheet_L_outputs` | R bust crop 좌우반전 derived output | VN/dialogue 오른쪽 배치 |
| `battle_stance_sheet` | 2x2 full-body sheet, idle/attack/guard/cast | battle/banner stance |

스킬 이미지는 캐릭터 하위 산출물이 아니다. `art-pipeline/config/skill_asset_manifest.yaml`와 `art-pipeline/subjects/icons/skill/**`에서 별도 관리한다.

## 프로필

| profile | 대상 | 필요 세트 |
| --- | --- | --- |
| `story_dialogue_character` | Pindoc wiki에서 대사 라인/거점 비트/명대사가 있는 캐릭터 | anchor, full, face emotion, combat face, bust R/L, battle stance |
| `lead_story_combat` | legacy alias | `story_dialogue_character`와 동일 |
| `named_story_battle` | legacy alias | `story_dialogue_character`와 동일 |
| `battle_actor_core` | deprecated legacy alias | 더 이상 최소셋 아님. 할당되더라도 full set 요구 |

## 캐릭터 배정

현재 registry 기준 target은 28명이다. 기존 P09 runtime ID가 짧은 13명은 생성 폴더가 현재 `warden`, `guardian` 같은 runtime subject id를 쓴다. Pindoc canonical ID는 manifest의 `wiki_character_id`로 기록한다. 신규 6명은 P09 preset/capture가 없어 pending으로 남는다.

| subject id | canonical/wiki id | tier | wiki | 상태 |
| --- | --- | --- | --- | --- |
| `hero_dawn_priest` | `hero_dawn_priest` | lead | `wiki-character-hero-dawn-priest` | generated |
| `hero_pack_raider` | `hero_pack_raider` | lead | `wiki-character-hero-pack-raider` | generated |
| `hero_grave_hexer` | `hero_grave_hexer` | lead | `wiki-character-hero-grave-hexer` | generated |
| `hero_echo_savant` | `hero_echo_savant` | lead | `wiki-character-hero-echo-savant` | generated |
| `npc_lyra_sternfeld` | `npc_lyra_sternfeld` | sub-antagonist | `wiki-character-npc-lyra-sternfeld` | generated |
| `npc_grey_fang` | `npc_grey_fang` | sub-antagonist | `wiki-character-npc-grey-fang` | generated |
| `npc_black_vellum` | `npc_black_vellum` | sub-antagonist | `wiki-character-npc-black-vellum` | generated |
| `npc_silent_moon` | `npc_silent_moon` | sub-antagonist | `wiki-character-npc-silent-moon` | generated |
| `npc_baekgyu_sternheim` | `npc_baekgyu_sternheim` | NPC antagonist | `wiki-character-npc-baekgyu-sternheim` | generated |
| `warden` | `hero_iron_warden` | support | `wiki-character-hero-iron-warden` | story gap |
| `guardian` | `hero_crypt_guardian` | support | `wiki-character-hero-crypt-guardian` | story gap |
| `slayer` | `hero_oath_slayer` | support | `wiki-character-hero-oath-slayer` | story gap |
| `hunter` | `hero_longshot_hunter` | support | `wiki-character-hero-longshot-hunter` | story gap |
| `scout` | `hero_trail_scout` | support | `wiki-character-hero-trail-scout` | story gap |
| `marksman` | `hero_dread_marksman` | support | `wiki-character-hero-dread-marksman` | story gap |
| `shaman` | `hero_storm_shaman` | support | `wiki-character-hero-storm-shaman` | story gap |
| `mirror_cantor` | `hero_mirror_cantor` | support | `wiki-character-hero-mirror-cantor` | story gap |
| `bulwark` | `hero_fang_bulwark` | background | `wiki-character-hero-fang-bulwark` | story gap |
| `reaver` | `hero_grave_reaver` | background | `wiki-character-hero-grave-reaver` | story gap |
| `rift_stalker` | `hero_rift_stalker` | background | `wiki-character-hero-rift-stalker` | story gap |
| `bastion_penitent` | `hero_bastion_penitent` | background | `wiki-character-hero-bastion-penitent` | story gap |
| `pale_executor` | `hero_pale_executor` | background | `wiki-character-hero-pale-executor` | story gap |
| `hero_aegis_sentinel` | `hero_aegis_sentinel` | support | `wiki-character-hero-aegis-sentinel` | pending P09 |
| `hero_shardblade` | `hero_shardblade` | support | `wiki-character-hero-shardblade` | pending P09 |
| `hero_prism_seeker` | `hero_prism_seeker` | background | `wiki-character-hero-prism-seeker` | pending P09 |
| `hero_ember_runner` | `hero_ember_runner` | background | `wiki-character-hero-ember-runner` | pending P09 |
| `hero_iron_pelt` | `hero_iron_pelt` | background | `wiki-character-hero-iron-pelt` | missing wiki page + pending P09 |
| `npc_aldric` | `npc_aldric` | NPC antagonist | `wiki-character-npc-aldric` | pending P09 |

## 감사 명령

```powershell
python art-pipeline/scripts/audit_character_assets.py
python art-pipeline/scripts/audit_character_assets.py --show-missing
python art-pipeline/scripts/audit_character_assets.py --profile story_dialogue_character --strict
python art-pipeline/scripts/audit_skill_assets.py
python art-pipeline/scripts/audit_p09_visual_identity.py
python art-pipeline/scripts/seed_character_portrait_subjects.py
python art-pipeline/scripts/seed_character_sheet_subjects.py --profiles story_dialogue_character --skip-missing-identity
```

former `battle_actor_core` 13명의 story gap subject를 채운 뒤 생성할 때:

```powershell
python art-pipeline/scripts/run_character_asset_queue.py --phase core-story-gap --skip-existing-output
```

## P09 Studio alias 정책

`priest`, `raider`, `hexer`는 런타임 `ArchetypeId` fallback용 alias preset이다. 이 세 preset은 실제 named hero preset과 동일해야 하지만, P09 Appearance Studio 목록에는 표시하지 않는다.

Studio에서 사람이 편집하는 목록은 **P09 preset/capture가 준비된 캐릭터**만 노출한다. Pindoc registry에 있으나 P09 preset이 없는 캐릭터는 먼저 wiki/P09 visual spec/anchor를 준비한 뒤 이미지 생성 queue로 올린다.
