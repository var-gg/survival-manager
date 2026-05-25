---
name: ui-design-review-2026-05-26
default_scope:
  areas: [art-pipeline, information-architecture, mechanisms, flows]
  include_superseded: false
  exclude_templates: true
  exclude_types: [Task]
description: 현재 구현된 UI surface 8장 screenshot을 mockup 시안과 함께 ChatGPT Pro에 전달, (1) 게임 구성·기획과 정합 검증, (2) 디자인 결격(가독성/IA/chrome/asset/hygiene) 진단, (3) 구체 개선 방향 patch 받기.
output_format: _output_format.md
---

# Scenario: ui-design-review-2026-05-26

UX Bible visual QA wave-1~24b까지 누적해서 8 surface 평균 90.14% (사용자 명시 in-game modeling 제외 scope) 도달. 이 시점에서 GPT Pro 시각으로 외부 검수받아 다음 wave 방향 잡기.

## 조립 규칙 (호출자용 메타)

- `default_scope.areas` 4 area dump (Task 제외). 사용자 `--slugs` 우선
- placeholder 치환:
  - `{{focus_block}}` — `--extra "..."` 시 `[FOCUS]\n{value}\n`, 없으면 빈 문자열
  - `{{bundle_summary}}` — bundle artifact 개수·area 분포 한 줄
  - `{{output_format_block}}` — `_output_format.md` 그대로

## Prompt body

```text
[CONTEXT]
이 채팅은 survival-manager (Unity prototype) 게임의 UI design 외부 검수다.

repo: https://github.com/var-gg/survival-manager (main, public)

프로젝트 성격:
- Unity 6.4 LTS, 캐릭터 수집/성장 RPG + 자동전투 + 짧은 호흡 + 인간형 적군 only
- 서브컬쳐 감성 흡수. 단일 캠페인 RPG 아님 — 캐릭터 voice/관계망/세력 갈등이 narrative core
- 인디 1인 개발 ($10 가격대), 음성 70-110 line, cutscene 12-15, illust commission 0, P09+lilToon+MagicaCloth2 stack
- UI Toolkit (UXML/USS) 기반, ArtBible 9-slice sprite library wire

UI 작업 현황 (wave-1~24b 누적):
- 8 surface: Town Service Hub, Character Sheet, Tactical Setup, Inventory Compare, Recruit Detail, Atlas Enemy Intel, Battle HUD Shell, Reward Result
- baseline 평균 81.75 → 현재 90.14% (in-game modeling 제외 scope)
- 적용: dev chrome cleanup → ArtBible divider/tab/scroll wire → modal corner ornament PNG upgrade → 9개 surface-specific dusk backdrop → modal-frame__texture wire → backdrop-dim 약화 → 모든 panel inner sub-block navy→sepia 일괄 통합 → Inventory cell ornate frame
- 회피: Atlas board에 정적 painterly image 박기 (wave-24a) — `task-atlas-3d-environment-v1` 정공법(3D forest + ley-line shader, boardgame 타일 인상 회피)과 충돌, revert(wave-24b)
- in-game modeling 제외 영역: (1) Battle HUD battlefield art, (2) Atlas board content area

[ATTACHED IMAGE REFS]

가장 압축된 비교: contact sheet (8 surface mockup ↔ current 한 장):
![contact_sheet](https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/comparison_contact_sheet.png)

8 surface 개별 current (고해상도, 검수 detail용):
- Town Service Hub: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/town_hub.png
- Character Sheet: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/character_sheet.png
- Tactical Setup: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/tactical_setup.png
- Inventory Compare: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/inventory_compare.png
- Recruit Detail: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/recruit_detail.png
- Atlas Enemy Intel: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/atlas_enemy_intel.png
- Battle HUD Shell: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/battle_authored.png
- Reward Result: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/visual-qa-handoff-2026-05-26/reward_result.png

8 surface 시안 (mockup, baseline):
- Town: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_town_service_hub_v0.png
- Character: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_character_sheet_class_detail_v0.png
- Tactical: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_squad_builder_v0.png
- Inventory: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_inventory_equipment_compare_v0.png
- Recruit: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_recruit_candidate_choice_v0.png
- Atlas: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_atlas_overworld_map_v0.png
- Battle: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_battle_stage_hud_v0.png
- Reward: https://raw.githubusercontent.com/var-gg/survival-manager/main/Screenshots/mockups/ui_ux_bible_reward_result_v0.png

repo 안 핵심 UI 코드 경로 (browse 가능):
- USS theme tokens: Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss
- RuntimePanelTheme atom: Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss
- ArtBible sprite library: Assets/_Game/UI/Foundation/Sprites/ArtBible/
- 8 surface USS/UXML: Assets/_Game/UI/Screens/{Town,Battle,Reward,Atlas}/, Assets/_Game/UI/Panels/{TownSquadBuilder,RecruitPack,InventoryTab,TownCharacterSheet,SkillCompendium,EquipmentRefit,PassiveBoard,PermanentAugment,SettingsGlobal,TownRosterGrid,TacticalWorkshop}/

첨부 wiki dump:
- {{bundle_summary}}
- area: art-pipeline (UX Bible mockup briefs, P09 visual baseline), information-architecture (atlas system 결정, hex topology, UI surface IA), mechanisms (atlas 3D environment, runtime UI behavior), flows (user flow)
- 각 artifact `# {slug} ({title})` H1 + manifest YAML

{{focus_block}}

[TASK]
첨부된 wiki 기획 + 8 surface 시안 + 8 surface current screenshot을 정독·시각 분석한 뒤,
**3개 축**으로 진단·개선 patch를 산출하라:

## 축 A — 게임 구성과의 정합

각 surface가 wiki 기획(atlas sigil topology, hex micro-beat, character archetype, expedition planner, recruit slot, reward card 등)을 정확히 반영하는지.

발견 시:
- 어느 wiki slug와 충돌/누락인지 명시
- 우선순위 P0(시스템 결정 위배) / P1(중요 기획 누락) / P2(미세 drift)

## 축 B — UI 디자인 결격

mockup ↔ current 시각 비교 + 단독 UI 평가 5축으로:
1. **IA / layout parity** — 영역 분할, 정보 위계, 의사결정 흐름
2. **chrome / material parity** — dark/gold sepia mood, ornate frame, gold trim
3. **content density / readability** — text 가독성, 정보 그룹화, 여백
4. **asset / art parity** — illustration, icon, portrait, ornament 활용
5. **production hygiene** — 디버그 잔재, 텍스트 clipping, raw key

surface별 단점 + before/after 개선 방향. 가능한 한 구체 css/uxml level patch (USS class 이름까지 명시).

## 축 C — 90% UI scope 다음 wave 권장

현재 평균 90.14%로 사용자 정의 임계 도달했지만, surface별로는 Town 88, Char 89, Tactical 89, Atlas chrome 90 등 단독 90 미달 surface가 있다.
다음 wave에서 어디에 투자하면 최고 ROI인가:
- USS-only chrome polish 영역 (외부 의존 0)
- game-image-gen으로 만들 art asset (외부 ChatGPT Playwright 자동화)
- 코드 변경이 필요한 wire-up (Presenter / UXML / 3D scene)

[CONSTRAINTS]
- in-game modeling 영역 — Battle HUD battlefield art, Atlas board content area (3D forest + ley-line shader) — UI scope 밖이므로 그 영역 art 제안은 제외 (대신 그 위 UI chrome 자체는 평가 OK)
- 인디 1인 개발 scope (illust commission 0, 외부 자산 발주 불가)
- runtime SoT (asmdef 경계, persistence schema, archetype/augment 시스템)는 보존 — UI/USS/UXML/Presenter 변경만 제안
- 새 panel/screen 신설 금지 — 기존 8 surface 안에서 patch
- ArtBible sprite library 안에서 우선 활용 (이미 import된 corner ornament, panel frame, icon slot frame, button gold/dark, divider, scroll, ornament 등)

[DELIVERABLE]
surface별로 발견된 issue가 있을 때만 출력 (issue 없는 surface는 완전 생략, 빈 헤더 금지).

### {Surface Name}

**축 A — 기획 정합** (issue 있을 때만)
- conflict / missing: wiki slug + 구체 위치
- 우선순위: P0/P1/P2

**축 B — 디자인 결격** (issue 있을 때만)
- 5축 중 어느 축: layout/chrome/density/asset/hygiene
- before: (current screenshot의 구체 위치 + 현 상태)
- after: 구체 patch — USS class/property 또는 UXML element level
- reason: 1 line

**축 C — 90 ROI 위치** (해당될 때만)
- 가장 효율적인 다음 wave hit
- 외부 의존 (USS only / game-image-gen / 코드)

응답 마지막에 **요약 표** 1개:

| surface | 축A | 축B | 축C P0 hit | 외부의존 |
|---|---|---|---|---|
| ... | O/- | O/- | (한 줄) | USS-only / asset / code |

(O=patch 제시 / - =해당 surface 이슈 없음)

그리고 **wave-25 권장 first hit 1개**를 마지막에 단독 명시 — "이거 하나부터 해라" 선택 + 이유 2~3줄.

{{output_format_block}}
```

## 호출 예시

```text
/gpt-pro-submit ui-design-review-2026-05-26
→ 4 area dump (art-pipeline + information-architecture + mechanisms + flows, Task 제외) + 17 image URL + repo URL

/gpt-pro-submit ui-design-review-2026-05-26 --extra="Atlas chrome 영역 우선"
→ default scope + focus_block

/gpt-pro-submit ui-design-review-2026-05-26 --slugs=decision-atlas-regional-sigil,task-atlas-3d-environment-v1,data-ui-ux-bible-batch1-briefs
→ 3개 핵심 artifact만 dump
```
