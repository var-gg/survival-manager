# Town mockup ↔ runtime spec 정합 audit

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-14
- 소스오브트루스: `docs/02_design/ui/town-mockup-vs-runtime-audit.md`
- 관련문서:
  - **pindoc V1 SoT (system spec)** — settled reskin 적용본:
    - `pindoc://wiki-combat-v1-index` (전투 V1 시스템 reference 인덱스)
    - `pindoc://wiki-combat-synergy-v1` (synergy 7 family + 2/4·2/3 breakpoint, 솔라룸/이리솔 부족/회상 결사)
    - `pindoc://wiki-combat-augment-v1` (augment 28 = temp 24 + **permanent 4** · 5 bucket)
    - `pindoc://wiki-combat-encounter-v1` (encounter family 8 + chapter 3/site 6)
    - `pindoc://wiki-combat-counter-topology-v1` (counter 8 lane 한국어 표시명)
    - `pindoc://wiki-combat-posture-tactic-v1` (5 posture + per-unit tactic V1 read-only)
    - `pindoc://wiki-combat-archetype-matrix-v1` (12 character matrix)
    - `pindoc://roster-expedition-combat-시스템지도` (roster 12/8/4)
  - **pindoc UI SoT** (visual + IA):
    - `pindoc://town-ui-ux-시안-갤러리-v1-gallery-town-ui-mockups-v1`
    - `pindoc://analysis-town-ui-component-system-v1`
    - `pindoc://ux-surface-catalog-v1-draft`
    - `pindoc://flow-campaign-story-scene-schedule-v1`
  - **로컬 docs/02_design/*** — V1 narrative reskin **이전** spec. 위 pindoc wiki와 충돌 시 **pindoc이 우선**. 동기화는 별도 task (`SynergyService.BuildForTeam` 코드 폴백 정리 등).

## 목적

`Assets/_Game/UI/Screens/Town/Preview/` 8 Preview surface는 pindoc V1 시안 갤러리에서
미적 baseline을 뽑아낸 컨셉 mockup이다. 본 문서는 각 mockup이 **pindoc V1 wiki SoT**와
어디서 어긋나는지 정리해서, **mockup data shape를 spec과 일치시키는 수정 범위**를 확정한다.

원칙: **pindoc V1 wiki가 SoT**. mockup이 따라간다. 로컬 `docs/02_design/*.md`는 V1 narrative
reskin (ADR-0024 인간 중심) 이전 spec이라 표기 (Human/Beastkin/Undead 등)와 수치
(permanent augment 12 등)가 옛 상태로 남아 있을 수 있음 — 본 audit은 pindoc 기준을 따른다.

## 정합 layer — Row vs Column

본 audit은 두 layer로 분리해서 본다:

### Row 정합 (instance value)

- fixture value를 spec count/표기에 맞추는 것 — augment 12 → 4, "Human" → "솔라룸", archetype 이름 정확히.
- mock data만 교정하면 해결. UI structure는 그대로.

### Column 정합 (UI schema / data shape)

- mock UI에 **어떤 field/축**이 노출되어 있는가. runtime presenter wiring 시 mock UI element가
  실 시스템 data shape (`GameSessionState`, `ProfileView`, `RecruitUnitPreview` 등)의 어떤 field에
  binding 가능한가.
- column이 누락되면 mock UI 자체를 재설계해야 함 (단순 value 교체로 해결 안 됨).
- **conceptual mismatch는 대부분 column-level 문제** (예: TW의 per-unit tactic이 hero당 1 chain vs
  spec의 hero당 RuleSet (N rules), Recruit 카드에 tags / signature / flex 정보 통째로 누락).

본 audit의 2차 round (column 정합)는 row 정합 작업보다 우선순위가 높다 — runtime wiring을 막는 게
row가 아니라 column이기 때문.

## 1. pindoc V1 wiki SoT 기준 카운트

| 시스템 | 항목 | 수치 | 한국어 표시명 (있는 경우) | pindoc SoT |
| --- | --- | --- | --- | --- |
| Deployment | anchor | `6` (Front × 3 + Back × 3) | — | `wiki-combat-archetype-matrix-v1` |
| Deployment | battle deploy | `4` | — | 같은 |
| Roster | town cap / expedition / deploy | `12 / 8 / 4` | — | `roster-expedition-combat-시스템지도` |
| Archetype | character matrix | `12` (race 3 × class 4) | Iron Warden / Crypt Guardian / Fang Bulwark / Oath Slayer / Pack Raider / Grave Reaver / Longshot Hunter / Trail Scout / Dread Marksman / Dawn Priest / Grave Hexer / Storm Shaman | `wiki-combat-archetype-matrix-v1` |
| Posture | team posture | `5` | **전열 사수 / 표준 전진 / 캐리 보호 / 약측 무너뜨리기 / 후열 깊이 침투** | `wiki-combat-posture-tactic-v1` |
| Per-unit tactic | V1 사용자 편집 | **불가** (read-only display only) | condition → action → target chain | 같은 |
| Counter | threat lane | `8` | **방어 전열 / 저항 외피 / 가드 보루 / 회피형 산개 / 제어 사슬 / 지속력 덩어리 / 후열 침투 / 군중 범람** | `wiki-combat-counter-topology-v1` |
| Synergy | family | `7` (race 3 + class 4) | **솔라룸 / 이리솔 부족 / 회상 결사** (race) + **전위 / 결투가 / 궁수 / 신비** (class) | `wiki-combat-synergy-v1` |
| Synergy | race breakpoint | `2 / 4` | — | 같은 |
| Synergy | class breakpoint | `2 / 3` | — | 같은 |
| Encounter | chapter / site / encounter | `3 / 6 / 24` (V1 floor) | 잿빛 변경 / 굴 깊이 / 폐허 묘소 | `wiki-combat-encounter-v1` |
| Encounter | family | `8` | 앵커 전열 돌파 / 캐리 보호 돌파 / 약측 다이브 / 템포·산개 정리 / 지속전 견디기 / 표식 처형 견디기 / 제어·정화 필요 / 소환 압박 대응 | 같은 |
| Recruit | offer slot | `4` (StandardA / StandardB / OnPlan / Protected) | — | local `recruitment-contract.md` (pindoc 미발행) |
| Recruit | tier | `3` (Common / Rare / Epic) | — | 같은 |
| Recruit | free refresh | `1회/phase` | — | 같은 |
| Recruit | paid refresh cost | `2 → 4 → 6 Gold` cap | — | 같은 |
| Recruit | scout cost | `35 Echo`, 1회/phase | — | 같은 |
| Passive | class board | `4` | 전위 / 결투가 / 궁수 / 신비 | local `passive-board-node-catalog.md` |
| Passive | nodes per board (V1 floor) | `18` (12 small + 5 notable + 1 keystone) | — | 같은 |
| Passive | toggle model | per-hero on/off, **point-budget 없음** | — | 같은 |
| Augment | temp augment (live subset) | `24` (5 bucket: Hero Rewrite 5 / Tactical Rewrite 7 / Scaling Engine 4 / Economy & Loot 5 / Synergy Pact 3) | — | `wiki-combat-augment-v1` |
| Augment | **permanent augment** | **`4`** (1:1 자세 매핑) | Citadel Doctrine (HoldLine) / Guardian Detail (ProtectCarry) / Breakthrough Orders (CollapseWeakSide) / Night Hunt Mandate (AllInBackline) | 같은 |
| Augment | equipped permanent slot | `1` per active blueprint | — | 같은 |
| Item | equipment slot | `3` | 방패 / 검 / 활 / 매개체 (weapon family) | local `item-and-affix-system.md` |
| Item | affix line | `5` (implicit 1 + prefix 2 + suffix 2) | IMPLICIT / PREFIX / SUFFIX | 같은 |
| Item | refit cost | `15 Echo` single-affix | — | 같은 |

> **⚠ 옛 audit과의 차이**: 영구 증강이 12개가 아니라 **4개**. 로컬 `meta/augment-catalog-v1.md`가 옛 12 candidate를 갖고 있지만 pindoc V1 wiki는 24+4=28 settled. 자세 1:1 매핑 모델이 V1 진실.

## 2. surface별 mismatch

### 2.1 RosterGridPreview (Town hub default)

- mock: 12 hero card grid + filter chip + Quick Battle CTA.
- spec (`pindoc://ux-surface-catalog-v1-draft#Town.RosterGrid` + `pindoc://wiki-combat-archetype-matrix-v1`):
  - **Town.RosterGrid P0, default 진입 panel**. 12명 roster 카드 grid + archetype/race/class 필터 chip + hero count(N/12) + KO/injured 배지 + Quick Battle floating button.
  - 12 archetype 정확한 이름: Iron Warden, Crypt Guardian, Fang Bulwark, Oath Slayer, Pack Raider, Grave Reaver, Longshot Hunter, Trail Scout, Dread Marksman, Dawn Priest, Grave Hexer, Storm Shaman.
- production TownScreen.uxml은 single-hero cycle + character sheet sidebar의 **레거시 layout** — V1 spec과 mismatch. Town은 RosterGrid hub로 전환되어야 함.
- **결정 (2026-05-14)**: Town hub = RosterGrid (spec 채택). 기존 production TownScreen.uxml은 별도 task로 마이그레이션.
- 본 mockup 수정 (deeper):
  - **12 hero fixture를 archetype matrix 그대로** (Iron Warden / Crypt Guardian / Fang Bulwark / Oath Slayer / Pack Raider / Grave Reaver / Longshot Hunter / Trail Scout / Dread Marksman / Dawn Priest / Grave Hexer / Storm Shaman). 옛 mock의 `공한 / 선영 / 회조 / 침월 / 백규` 등 NPC/DLC lore-only 6명 제거.
  - **filter chip 9 + divider 2** (전체 / 3 race / 4 class). 한국어 표시명을 narrative reskin baseline에 정합: **솔라룸 / 이리솔 부족 / 회상 결사** (race) + **전위 / 결투가 / 궁수 / 신비** (class).
  - KO/injured 배지는 HeroPortraitCard atom의 `--ko`/`--injured` class가 이미 처리.

### 2.2 TacticalWorkshopPreview (deeper redo)

| 항목 | 옛 mock | pindoc V1 SoT | 수정 |
| --- | --- | --- | --- |
| anchor 배치 | 4 standee (2×2) | 6 anchor (Front × 3 + Back × 3) — SquadBuilder 책임 | **6 anchor read-only** (2 empty slot) |
| posture row | 5 card sprite only | 5 posture | **5 card + 한국어 label** (전열 사수 / 표준 전진 / 캐리 보호 / 약측 무너뜨리기 / 후열 깊이 침투) |
| per-unit tactic | 4 portrait + glyph + 3 추상 icon | **V1 단계 사용자 편집 X** — condition → action → target chain은 콘텐츠/코드 미리 세트 | **4 row read-only display** — hero name + condition chip → action chip → target chip 3-chip chain (`EnemyInRange → BasicAttack → FirstEnemyInRange` 등) |
| synergy chip | 4 class | 7 family (race 3 + class 4) — narrative reskin 적용 | **7 chip + 한국어 표시명** — 솔라룸 / 이리솔 부족 / 회상 결사 (race), 전위 / 결투가 / 궁수 / 신비 (class) |
| threat lane | 1 abstract hex | 8 lane (`wiki-combat-counter-topology-v1`) | **8 chip + 한국어 표시명** — 방어 전열 / 저항 외피 / 가드 보루 / 회피형 산개 / 제어 사슬 / 지속력 덩어리 / 후열 침투 / 군중 범람 + answered/unanswered state |

**Conceptual 분리**: TW의 anchor pad는 SquadBuilder의 책임이라 read-only reference로 강등. TW core는 **posture 편집 + per-unit tactic 읽기 + live preview + threat coverage 진단**.

### 2.3 RecruitPreview

| 항목 | mock | spec | drift |
| --- | --- | --- | --- |
| candidate card | 4 | 4 (StandardA/StandardB/OnPlan/Protected) | ✓ count |
| card identity | portrait + class glyph + synergy pip + star | tier + slot type badge + plan fit + gold cost + race/class/role/formation + tags(≤3) + counter hints(≤2) + signature active/passive | **P0** |
| Recruit CTA | 단일 (action bar) | per-card | **P0** |
| Refresh CTA | 단일, cost 미표시 | free 1회 → 2/4/6 Gold cap, 현재 cost 노출 | **P0** |
| Scout CTA | abstract glyph | `35 Echo`, directional bias (다음 refresh의 OnPlan slot only) | **P0** |

### 2.4 EquipmentRefitPreview

| 항목 | mock | spec | drift |
| --- | --- | --- | --- |
| equipment slot | 3 (hex) | 3 (weapon/armor/accessory) | ✓ |
| affix list | 5 row, 평탄 | 5 line, **implicit 1 + prefix 2 + suffix 2 그룹** | **P1** |
| refit CTA | abstract cost glyph | `15 Echo` 고정 single-affix reroll | **P1** |
| affix 선택 | row--selected | 어떤 affix를 reroll할지 선택 (semantics) | **P1** |
| inventory pool | 8 row | item browse subset | ✓ |

### 2.5 PermanentAugmentPreview (model 자체 변경)

| 항목 | 옛 mock | pindoc V1 SoT (`wiki-combat-augment-v1`) | 수정 |
| --- | --- | --- | --- |
| augment count | **12 motif grid (3×4)** | **4개 only** (1:1 자세 매핑) | **4 grid (2×2)** — Citadel Doctrine / Guardian Detail / Breakthrough Orders / Night Hunt Mandate |
| 자세 매핑 | 없음 | `HoldLine` / `ProtectCarry` / `CollapseWeakSide` / `AllInBackline`에 1:1 | **각 cell에 매핑된 자세 한국어 표시명 노출** |
| equip slot | 1 large slot | `MaxPermanentAugmentSlots = 1` | ✓ |
| stat compare | before → after stat rows | thesis level (signature rule + 자세 강화), 단순 stat 부적합 | **thesis impact 4 row** — Family / 자세 매핑 / 강화 효과 / Unlock Chain |
| detail flavor | placeholder glyph | unlock chain (어떤 임시 augment first-pick이 trigger했는가) | unlock chain hint text |

> **⚠ 옛 audit의 가장 큰 오류**: 영구 증강 12개로 잘못 카운트했음. 로컬 `meta/augment-catalog-v1.md`의 12 legacy_* candidate는 V1 narrative reskin 이전 옛 spec이고, pindoc V1 wiki는 **4개로 closed** (자세 1:1 매핑). model 자체가 다르다.

### 2.6 PassiveBoardPreview

| 항목 | mock | spec | drift |
| --- | --- | --- | --- |
| class tab | 4 (vanguard/duelist/ranger/mystic) | 4 board | ✓ |
| node count | 15 (root 1 + inner 6 + outer 8) | **18** per board (12 small + 5 notable + 1 keystone) | **P0** |
| node state | root / active / available / locked | kind = small / notable / keystone (visual) + per-hero active 여부 (state) — **다른 축** | **P0** |
| NODE POINTS 6/45 footer | 있음 (POE 식 budget 점수) | **point-budget 없음**, per-hero toggle on/off | **P0** |
| Activate CTA / 1/3 progress | 있음 | toggle (Activate / Deactivate), progress 없음 | **P0** |

### 2.7 InventoryPreview

| 항목 | mock | spec | drift |
| --- | --- | --- | --- |
| category sidebar | 6 (ALL/WEAPON/ARMOR/ACCESSORY/CONSUMABLE/BLUEPRINT) | 4 (ALL + weapon/armor/accessory). V1에 CONSUMABLE/BLUEPRINT 없음 | **P1** |
| item grid | 5×4 = 20 cell | cap TBD, browse 단위 OK | ✓ |
| detail affix row | 4 | **5** (implicit 1 + prefix 2 + suffix 2 그룹) | **P1** |
| detail button | 3 (equip primary + sell/compare secondary) | runtime 미정. equip+sell+compare는 합리적 추정 | ✓ |

### 2.8 TheaterPreview

- mock: entry list (chapter grouped) + replay player + chapter detail + Replay CTA.
- spec (`town-ui-ux-시안-갤러리-v1` #7 + `flow-campaign-story-scene-schedule-v1`): **StoryArchive replay — cutscene 다시보기 only**.
  - **Battle replay은 별개** (Battle scene 재호출, Theater 영역 아님).
  - chapter group: **Prologue + Ch1 + Ch2 + Ch3 + Ch4 + Ch5 = 6 group**.
  - 진입표는 `cutscene_*` + 핵심 `dialogue_scene_*` (media_cue 보유 scene).
  - V1 prototype 기준 unlock 진행도: Prologue + Ch1 진행 중 + Ch2~Ch5 locked.
- **결정 (2026-05-14)**: Theater = cutscene-only replay (battle replay 배제). 6 group + per-chapter cutscene entries.
- 본 mockup 수정: 3 group (Prologue + CH1 + CH2) → **6 group** (Prologue + Ch1~Ch5). 각 group cutscene entries spec 정합. state 축에 `unwatched` (열려 있지만 미시청) 추가.

### 2.9 SettingsPreview (Settings.Global, cross-scene)

- mock: 5 category sidebar (Display / Audio / Control / Account / Help) + option list + Apply / Cancel / Reset.
- spec (`ux-surface-catalog-v1-draft#Settings.Global`): **Settings.Global P0, cross-scene modal**. **4 tab**:
  - **Audio** — BGM/SE/일본어 Voice volume + voice mute
  - **Video** — resolution / fullscreen / vsync / fps cap
  - **Language** — UI ko/en, voice ja off-able
  - **Controls** — keybinding
  - + Save management (sub-section, V1 미정 — mock에는 미노출)
- 트리거: Boot.Title / Town hub topbar gear icon / Battle.SettingsPanel "Open Global Settings".
- **결정 (2026-05-14)**: Settings = V1 scope (post-V1 연기 아님). 4 tab + cross-scene modal.
- 본 mockup 수정: 5 tab → **4 tab** (Audio / Video / Language / Controls). default selected: Video. options: 화질 프리셋 / 해상도 / 전체화면 / V-Sync / FPS 상한 / 밝기 / UI 스케일 / HDR (Video tab 본문 시안).
- sprite reuse: `settings_audio` ✓ / `settings_display` (video proxy) / `settings_help` (language placeholder) / `settings_control`. **`settings_video.png` + `settings_language.png` art-pipeline 발행 필요** (현재는 임시 매핑).

## 3. 수정 우선순위 결론

### 1차 round (얕은 정합 — 카운트/그룹화)

| Surface | 핵심 수정 |
| --- | --- |
| TacticalWorkshop | anchor 4→6, threat 1→8, synergy 4→7 |
| Recruit | per-card Recruit + slot type badge + tier dots + gold cost + scout/refresh cost label |
| PassiveBoard | NODE POINTS 제거 + 18 node/board + kind 시각 분류 |
| Theater | cutscene-only StoryArchive 6 group (Prologue + Ch1~Ch5) + state 4종 |
| Settings | 5 tab → 4 tab (Audio/Video/Language/Controls). cross-scene modal. |
| RosterGrid | Town hub default 채택. filter chip 9개 (전체 + 3 race + 4 class) |
| EquipmentRefit | affix 그룹화 (implicit/prefix/suffix) + 15 Echo CTA |
| PermanentAugment | stat compare → build thesis impact + unlock chain hint |
| Inventory | sidebar 6→4 + affix detail 4→5 |

### 2차 round (deeper — pindoc V1 wiki SoT 정합, narrative reskin 적용)

| Surface | 핵심 수정 |
| --- | --- |
| **TacticalWorkshop** | 5 posture **한국어 label** (전열 사수 등) / 8 threat **한국어 표시명** (방어 전열 등) / per-unit tactic을 **V1 read-only display** + condition→action→target chain 시각 (`EnemyInRange → BasicAttack → FirstEnemyInRange` 식) |
| **PermanentAugment** | **12 grid → 4 grid** (pindoc V1 wiki: temp 24 + perm **4** = 28). 4 augment 1:1 자세 매핑 (Citadel Doctrine/Guardian Detail/Breakthrough Orders/Night Hunt Mandate) |
| **RosterGrid** | 12 hero fixture를 **archetype matrix 그대로** (Iron Warden 외 11). filter 한국어 표시명을 reskin baseline에 정합 (솔라룸 / 이리솔 부족 / 회상 결사 / 전위 / 결투가 / 궁수 / 신비) |
| **Recruit** | candidate fixture를 V1 활성 archetype matrix에서만 (DLC 그물 결사 `echo_savant` 제거) |

### 3차 round (column 정합 — schema/축 mismatch 해소)

위 2차까지는 row 단위 (instance value) 교정. 3차는 **mock UI에 누락된 column field를 추가**해서
실 시스템 data shape와 정합 맞추는 작업.

| Surface | Column-level mismatch | 수정 (적용 완료) |
| --- | --- | --- |
| **TacticalWorkshop** | per-unit tactic이 hero당 **1 chain**으로 표시 → spec은 hero당 **RuleSet (N rules)** | RuleSet schema로 재구성: hero block (portrait+name+추천 자세+rule count) + rule list (P1/P2/P3 priority + cond→arrow→act→arrow→tgt) |
| **Recruit card** | 카드에 portrait + class glyph + tier dot + plan + cost + recruit btn만. recruitment-contract 명시 column **unit name / formation line / tags(≤3) / signature active / signature passive / flex active / flex passive** 전부 누락 | displayName / formation·class meta row / tier+plan compact row / tag chip row / skills block (4 line: SIG A / SIG P / FLX A / FLX P) 추가 |
| **PermanentAugment** | cell에 icon + name만. 시스템 column: **family bucket / unlock chain progress / locked unlock hint** 누락 | family chip (5 bucket color-coded) + status footer (`✓ UNLOCKED 2/3` 또는 `🔒 Tactical Rewrite augment first-pick`) 추가 |
| **Inventory cell** | rarity border + icon만. 시스템 column: **weapon family (shield/blade/bow/focus) / equipped marker** 누락 | weapon family badge (top-left, 4 color) + equipped marker (top-right gold dot) 추가 |
| **PassiveBoard node** | icon + active state만. 시스템 column: **node_id / rule_summary / tags / kind** 누락 | tooltip에 `passive_duelist_keystone_01\n[Keystone] phys_power +1.5, attack_speed +0.12, crit_multiplier +0.15\ntags: frontline · burst\nACTIVE` 전체 column 노출 |
| **Theater entry** | thumb + abstract glyph 2개 + state icon만. 시스템 column: **duration / completion % / watched_at** 누락 | scene_id label + meta row (`0:58 · 62% · 2026-05-13`) — completion % 색상 분기 (partial=주황 / watched=녹색) |

### 4차 round (대기 — 큰 작업)

| Surface | Column 누락 | 비고 |
| --- | --- | --- |
| **RosterGrid hero card** | 추천 자세 / equipped item 슬롯 수 / level·xp / last expedition node | HeroPortraitCard atom 확장 필요 (다른 consumer에 영향) |
| **Settings option row** | default value / dirty 상태 / reset 버튼 / keybind capture state | V1 Settings 내부 field가 미정인 상태라 우선순위 낮음 |

## 4. 런타임 데이터 모델 정합 (실 구현 축 — 3-way gap)

> 2026-05-14 추가. 위 1~3장은 mockup ↔ **pindoc V1 spec** 정합이었다. 본 장은 그 위에
> **실제 runtime 데이터 모델**(`SaveProfile` / persistence record / `*Definition` / `GameSessionState` API)을
> 세 번째 축으로 넣는다. spec을 따라간 mockup이라도 runtime 모델에 해당 구조가 없으면
> presenter wiring이 불가능하다 — 이게 "시안만 따라갔다"의 실체. 일부 "발명된" field는 UI 버그가
> 아니라 **pindoc spec에는 있으나 runtime 모델이 아직 구현 안 한 개념**이다 (그건 제품/구조 결정 대상).

### 4.0 runtime 모델 핵심 (조사 결과)

- **`SaveProfile`** = hero / loadout / progression / inventory / passive / augment / squad 가 전부
  **side-table, `HeroId` (또는 `BlueprintId`) join**. nested hero aggregate 없음.
- hero 1명 = `HeroInstanceRecord`(정체성) + `HeroProgressionRecord`(Lv/XP/unlock) +
  `HeroLoadoutRecord`(장비/스킬/보드/노드) + `InventoryItemRecord.EquippedHeroId`(역참조).
- **passive 선택 = per-hero** (`HeroLoadoutRecord.SelectedPassiveNodeIds` canonical, `PassiveSelectionRecord`는 파생 미러).
  `PassiveBoardDefinition.ClassId` — board tree는 **클래스 단위** 4개, 선택만 hero 단위.
- **permanent augment ≠ per-hero** — `PermanentAugmentLoadoutRecord`는 `BlueprintId` 키,
  `MaxPermanentAugmentSlots = 1` **단일 슬롯** (`EquipPermanentAugment` = 리스트 통째 덮어쓰기).
  `HeroLoadoutRecord.EquippedPermanentAugmentIds` field는 있으나 equip path가 안 씀 (사실상 dead).
- **squad/deploy** = `SquadBlueprintRecord`, `SaveProfile.ActiveBlueprintId`로 선택.
  anchor = `DeploymentAnchorId` enum **정확히 6개** (Front/Back × Top/Center/Bottom).
  posture = **squad 단위** (`TeamPosture` string / runtime `SelectedTeamPosture` enum 5종).
  caps: `TownRosterCap=12 / ExpeditionSquadCap=8 / BattleDeployCap=4`.
- **recruit offer** = `RecruitUnitPreview(UnitBlueprintId, UnitInstanceSeed, FlexActiveId, FlexPassiveId, Metadata)`.
  `Metadata` = SlotType / Tier / PlanFit / PlanScore(6-component) / ProtectedByPity / BiasedByScout / GoldCost.
  **name·race·class·tags·signature skill·formation 은 offer에 없음** — archetype lookup 파생 or 미존재.
- **per-unit AI** = `BehaviorProfileDefinition`(range/bias/chance 튜닝 float 다발) +
  `RoleInstructionDefinition`(anchor + role bias float). **condition→action→target rule 모델은 runtime에 없음.**
- **`AugmentDefinition`** — `FamilyId`(string)만 있고 **`TeamPostureType` 링크 field 없음**.
  `AugmentFamilyPickHistory` 같은 unlock-progress field도 `SaveProfile`에 **없음**.
- **edit API 부재**: skill equip/unequip 없음 · hero level-up 없음 · item sell 없음 · atomic swap 없음
  (anchor 재배정 `AssignHeroToAnchor`/`CycleDeploymentAssignment`만, 밀려난 hero는 미배치로 떨어짐).
- **Settings 모델 없음** — `SettingProfile` 클래스 자체가 미존재. Settings는 순수 mock.

### 4.1 surface별 3-way gap + 수정 방향

표기: ✅ 바인딩 가능 · ◐ 파생(real field 조합) · ❌ 미백업(발명 or spec-only) · ⚑ 플로우 mismatch.

#### PassiveBoard — **구조 결함 (P0)**
- ✅ 4 class tab = `PassiveBoardDefinition.ClassId` 4개 · node `NodeKind`/`BoardDepth` · per-hero `SelectedPassiveNodeIds`.
- ❌ node **prerequisite tree** (`PassiveNodeDefinition.PrerequisiteNodeIds` / `MutualExclusionTags`) 미표현 — 지금은 ring에 노드만 떠 있고 연결선·게이팅 없음.
- ⚑ **4개 자유 전환 탭** = 시안 갤러리 브라우즈 framing. 실 플로우는 **hero 컨텍스트 진입 → 그 hero 클래스 보드 고정 → 그 hero 노드만 토글**. `SelectPassiveBoard`/`TogglePassiveNode` 둘 다 `heroId` 인자를 받고 Presenter엔 `SetSelectedHero`가 이미 있음 — View/UXML만 mockup 모양.
- ⚑ surface에 **hero 정체성 표시 없음** (누구 보드인지 모름).
- **수정**: 탭 → 비대화 클래스 인디케이터 + hero 헤더(portrait/name) 추가. 노드 간 prerequisite 연결선 + locked 게이팅 시각. `OnBoardSelected` 자유 전환 제거.

#### PermanentAugment — **스키마 발명 (P0, 결정 필요)**
- ✅ `Unlocked` = `UnlockedPermanentAugmentIds` · equip = `EquipPermanentAugment`(단일 슬롯, 덮어쓰기).
- ❌ **augment ↔ posture 1:1 매핑** — `AugmentDefinition`에 posture field 없음. mockup·spec 전체가 이 매핑 위에 서 있는데 runtime엔 그 링크가 없음.
- ❌ `ProgressCurrent/Max` + `AugmentFamilyPickHistory` — `SaveProfile`에 backing 없음.
- ◐ `FamilyBucket` 5종 = `AugmentDefinition.FamilyId` (값 정합 확인 필요) · `SignatureEffect` = `Effects`/`Modifiers`/`DescriptionKey` 파생.
- **결정 (2026-05-14, V1 오너)**: **(c) 다운스코프**. 포스처 시스템 검토 결과 — 포스처는 실재 메커닉이나 얕음(위치+공격성 프리셋: `MovementResolver` switch + 손authored float 6개, `CombatPace`는 dead code, animation/idle 무관). `MaxPermanentAugmentSlots=1` 단일 슬롯 + augment 4개뿐인데 4×4 매핑 그리드 UI는 얇은 메커닉을 과대 포장. augment↔posture 연결은 augment 설명문 플레이버로만 두고 구조 축에서 폐기.
- **수정 방향**: PermanentAugment surface = "장착한 영구 augment 1개 + 해금 후보 풀" 단순 모델. posture 그리드 / `PostureId` / `ProgressCurrent·Max` / `FamilyBucket--*` 색축 폐기. `AugmentDefinition` 스키마 변경 없음.
- **별건 메모**: 포스처 시스템 자체가 절반만 지어짐(`CombatPace` dead, float 5/12 미authored) — V1에서 포스처 깊이를 더 갈지는 별도 시스템 설계 판단. 본 audit 범위 밖.

#### Recruit — **카드 컬럼 발명 (P0)**
- ✅ `SlotType`/`Tier`/`PlanFit`/`GoldCost` = `RecruitOfferMetadata` · `FlexActive/Passive` = offer의 `FlexActiveId/Id` · 액션바 cost/refresh = `RecruitmentBalanceCatalog`/`RecruitPhaseState`/`CurrentRecruitRefreshCost`.
- ◐ `DisplayName`/`ClassKey` = `UnitBlueprintId`(archetype) lookup 파생.
- ❌ `HeroId` — offer엔 hero 없음 (`UnitBlueprintId`+`UnitInstanceSeed`만). `Formation` — offer에 anchor/formation field 없음. `Tags[]` — offer에 tag 리스트 없음. `SigActive/SigPassive` — offer엔 **flex 2개만**, signature는 archetype 고정 (offer field 아님).
- ❌ 미노출인데 real: `ProtectedByPity`(bool) · `PlanScore`(6-component breakdown).
- ⚑ scout = `ScoutDirective`(6종 방향 directive: Frontline/Backline/Physical/Magical/Support/SynergyTag) — mockup의 `ScoutBias bool`은 과단순화.
- **수정**: 카드 컬럼을 offer 실 shape로 — archetype id 기반 displayName/class 파생, `Formation` 제거(또는 archetype의 `RoleInstruction.Anchor` 파생으로 재정의), `Tags`는 archetype `CompileTags` 파생으로 명시, signature는 "archetype 고정" 파생 표기. `ProtectedByPity` 배지 + `PlanScore` 툴팁 추가. scout는 6-kind directive 선택 UI로.

#### RosterGrid — **컬럼 오귀속 (P0)**
- ✅ `HeroId` · `RarityKey` = `RecruitTier` · `Level` = `HeroProgressionRecord.Level` · `EquipSlots` = `EquippedItemIds` count · `RosterCap=12` = `TownRosterCap` ✅.
- ◐ `XpPct` = `Experience`(raw int) + level curve 파생 · `ArchetypeLabel`/`FamilyKey` = archetype/class lookup.
- ❌ `PostureLabel` — posture는 **squad 단위**, hero에 없음. (hero 단위로 보여줄 게 있다면 `RoleInstruction` = role/anchor이지 posture 아님.)
- ❌ `StateKey` ko/injured — `HeroInstanceRecord`에 KO/injured 영속 field 없음. battle/run 상태이지 roster 영속 상태 아님.
- ❌ `LastNodeId` — run-scoped (`ActiveRun`/`CampaignProgress`), hero field 아님.
- ❌ `DisplayNameKo/En` — 모델은 `Name` 단일 (+`CharacterId`). ko/en split은 ContentTextResolver 파생이거나 발명.
- **수정**: `PostureLabel` → `RoleLabel`(또는 제거) · `StateKey`는 run/battle 컨텍스트 있을 때만, roster default엔 제거 · `LastNodeId` 제거 · 이름은 `Name` 1개 + (있으면) resolver 파생 en. portrait는 `portrait_full.png` 경로 wiring (3 hero 보유).

#### TacticalWorkshop — **per-unit tactic 스키마 발명 (P1) + anchor pad 플로우 (P1)**
- ✅✅ anchor 6 = `DeploymentAnchorId` enum 정확 일치 · posture 5 = `TeamPostureType` enum 정확 일치 · `SetTeamPosture` edit API 있음.
- ❌ per-unit tactic **RuleSet(priority, condition, action, target)** — runtime엔 그런 모델 없음. 실제는 `BehaviorProfileDefinition`(튜닝 float) + `RoleInstructionDefinition`(anchor+bias). condition→action→target rule은 pindoc spec 표현이고 runtime 미구현.
- ⚑ anchor pad가 **display-only** — 실 모델은 `AssignHeroToAnchor`/`CycleDeploymentAssignment` edit 지원. (단 2.2장 결정대로 anchor 편집을 SquadBuilder로 분리한다면 read-only가 의도된 것일 수 있음 — 그 경계 재확인 필요.)
- ◐ synergy 7 / threat 8 = `SynergyDefinition` / `TeamPlanProfile.CounterCoverage` 파생 (개수 정합 확인 필요).
- **수정**: per-unit tactic 블록을 runtime 실재(`RoleInstruction` anchor/role + `BehaviorProfile` 요약)로 재정의 — 가짜 rule chip 폐기. anchor pad는 2.2장 경계 결정에 맞춰 read-only 명시 or 편집 wiring.

#### EquipmentRefit — **affix row 빈약 (P1) + hero 컨텍스트 (P1)**
- ✅ pool = `Profile.Inventory` · `RarityKey` = `ItemBaseDefinition.RarityTier` · 3 slot = `ItemSlotType`(Weapon/Armor/Accessory) · refit = `RefitItem(itemInstanceId, affixSlotIndex)` API 있음.
- ◐ affix `GroupKey` = `AffixDefinition.Tier`(Implicit/Prefix/Suffix).
- ❌ affix row에 **name·rolled value 없음** (지금은 icon bar만). `AffixDefinition`은 `NameKey`+`ValueMin/Max` 보유 — 단 instance의 **확정 roll 값**이 `InventoryItemRecord.AffixIds`(id만)에 안 들어있음 → roll 저장 위치 확인 필요.
- ⚑ hero 컨텍스트 필요 (standee = 어떤 hero인가). equip(`EquipItem`)과 refit(`RefitItem`) 두 동작 혼재 — surface 의도 확인.
- **수정**: affix row에 name + value 컬럼 추가 · hero 컨텍스트 진입 모델 명시 · roll 값 저장 위치 확인 후 binding.

#### Inventory — **통화/affix roll (P1)**
- ✅ `Gold`/`Echo` = `CurrencyRecord` · item `RarityKey`/`WeaponFamilyKey` = `ItemBaseDefinition` · `IsEquipped` = `EquippedHeroId` non-empty.
- ❌ `CurrencyRecord`는 통화 **8종** (Gold/Echo/TraitReroll/TraitLock/TraitPurge/EmberDust/EchoCrystal/BossSigil) — mockup은 2종만. 의도면 OK, 아니면 확장.
- ❌ affix detail의 rolled value (`+256` 등) — `InventoryItemRecord.AffixIds`는 id만. roll 저장 위치 동일 미확인.
- ⚑ `OnSellItem` — `GameSessionState`에 sell API 없음. equip은 hero 타깃 필요.
- 4 category vs `ItemSlotType` 3종 — 4번째(ALL?) 확인.
- **수정**: sell API 부재 명시(액션 제거 or API 신설 task) · affix roll binding · 통화 표시 범위 결정.

#### Theater — **모델 미발행 (P2)**
- ❌ mockup의 `ReplayLedgerEntry`(scene_id + duration + completion_pct + watched_at) 모델이 **runtime에 없음** — `NarrativeProgressRecord` 필드 미확인, 별도 replay ledger 미발견.
- ◐ chapter 6 group = `CampaignChapterDefinition.StoryOrder` 파생.
- presenter 미마이그레이션 (Bootstrap 직접 inject).
- **수정**: `NarrativeProgressRecord` 실 구조 확인 → cutscene 시청 추적 모델 정의(or 발행). 그 후 presenter화. **모델이 없으면 surface 자체가 보류 후보.**

#### Settings — **모델 부재 (P2)**
- ❌ `SettingProfile` 클래스 자체가 없음. Settings는 app-level (save 아님 — Boot/Town/Battle 공용)이라 `SaveProfile` 밖에 있어야 하는 게 맞지만 **그 모델이 아직 미존재**.
- presenter 미마이그레이션.
- **수정**: `SettingProfile`(current/default value + dirty + keybind) 모델 신설이 선행. 그 전엔 mockup 동결.

### 4.2 수정 우선순위 (runtime 정합 기준)

> **구현 상태 (2026-05-14)**: P0-1~P0-4 구현 + 컴파일 green + 캡처 검증 완료
> (PassiveBoard / RosterGrid / Recruit / PermanentAugment). P1-1~P2 미착수.

| 순위 | surface | 성격 | 선행 결정/확인 |
| --- | --- | --- | --- |
| **P0-1** | PassiveBoard | 플로우 재구성 (per-hero 컨텍스트) — 고확신, 결정 불필요 | 없음 — 바로 착수 가능 |
| **P0-2** | RosterGrid | 컬럼 오귀속 제거 (posture/state/lastNode) — 고확신 | 없음 |
| **P0-3** | Recruit | 카드 컬럼을 offer 실 shape로 — 고확신 | 없음 (signature는 archetype 파생으로 정의) |
| **P0-4** | PermanentAugment | (c) 다운스코프 — posture 그리드 폐기, "1슬롯 + 후보 풀" 단순 모델 | ✅ 결정됨 (2026-05-14) |
| **P1-1** | TacticalWorkshop | per-unit tactic 재정의 + anchor pad 경계 | anchor 편집 경계(2.2장) 재확인 |
| **P1-2** | EquipmentRefit | affix row 컬럼 + hero 컨텍스트 | affix roll 저장 위치 확인 |
| **P1-3** | Inventory | sell API / affix roll / 통화 범위 | sell 정책, roll 저장 위치 |
| **P2** | Theater / Settings | 백킹 모델 자체 부재 — 모델 신설 선행 | `NarrativeProgressRecord` / `SettingProfile` 정의 |

**프레임 구조 (별건, 병행 가능)**: 풀패널 `panel_modal_frame_9slice.png` 9-slice → `ornament-corner-vine.svg` 4코너 + 엣지 trim + mood-color 중앙 **합성 구조**로 전환. 해상도 독립 + ceremony 네이비 mood 복원 (Fix 5 땜빵 불필요해짐).

## 5. 본 audit이 닫지 않는 것

- **pindoc V1 wiki 개정** — §4가 드러낸 spec↔runtime 불일치(augment↔posture 1:1 매핑, per-unit RuleSet 모델, posture 깊이 등)는 일부가 **wiki 자체의 stale/오류**다 (로컬 `docs/02_design/*` reskin-이전 문제와는 별개). P0~P1 구조 수정이 어느 정도 진행된 뒤, §4를 diff source로 `wiki-combat-augment-v1` / `wiki-combat-posture-tactic-v1` 등을 개정한다. 단순 정정이 아니라 **항목별 triage** 필요 — "wiki 오류(→정정)" vs "aspirational spec, runtime 미구현(→목표 유지 + runtime status 주석)". triage 판단은 V1 오너 몫.
- **runtime presenter 연결** — 각 Preview Bootstrap이 real `GameSessionState` / `ProfileView` / `LoadoutView`를 읽도록 wiring하는 작업.
- **production TownScreen.uxml → RosterGrid hub 마이그레이션** — single-page dashboard 폐기, RosterGrid를 default 진입 panel로.
- **pindoc V1 wiki ↔ 로컬 docs/02_design/*.md 동기화** — 로컬은 V1 narrative reskin (ADR-0024) 이전 spec이라 옛 표기 (Human/Beastkin/Undead, permanent 12, etc) 잔존. 동기화 task 별도 필요. `wiki-combat-v1-index`에 "다음 사이클로 넘긴 것"에 명시됨 (e.g. `SynergyService.BuildForTeam` 코드 폴백 정리).
- **art-pipeline regen** — `settings_video.png` / `settings_language.png` / Theater chapter cutscene thumbnail / 8 canonical threat lane sprite (lane 이름 기준) / race synergy chip sprite (솔라룸/이리솔/회상결사 art) / archetype별 portrait_full.png 12장 (현재 3장만) / 영구 augment 4 motif (Citadel/Guardian/Breakthrough/Night Hunt).
- **per-unit tactic 자유 편집 surface** — V1에서는 read-only. V2 이후 자유 편집을 열 surface는 별도 spec 필요.

본 audit의 범위는 **mockup fixture data + UI 구조 + 표시명 layer를 pindoc V1 wiki SoT에 맞추는 것**까지.
