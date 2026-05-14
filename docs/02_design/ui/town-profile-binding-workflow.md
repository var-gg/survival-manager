# Town 모킹 → Profile binding → BattleTest 즉시 반영 workflow

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-14
- 소스오브트루스: `docs/02_design/ui/town-profile-binding-workflow.md`
- 관련문서:
  - `docs/02_design/ui/town-mockup-vs-runtime-audit.md` (mock ↔ pindoc V1 wiki 정합 작업)
  - `docs/02_design/ui/town-character-sheet-ui.md` (Town character sheet IA)
  - `pindoc://ux-surface-catalog-v1-draft` (V1 surface 카탈로그)
  - `pindoc://roster-expedition-combat-시스템지도` (roster 12/8/4 + profile shape)
  - `Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs` (현재 profile binding presenter)
  - `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/RosterGridPreviewController.cs` (현재 mock controller)

## 목적

8 Town Preview surface mockup을 단순 시각 demo가 아니라
**player profile (`GameSessionState` / `ProfileView` / `LoadoutView`)에 binding된 실제 편집 surface**로 키운다.
최종 목표 워크플로우:

1. 사용자가 SM 메뉴 (Tactical Workshop / Recruit / Equipment Refit / Passive Board / Permanent Augment 등)에서
   **내 덱 (roster, deploy, equip, skill, posture, passive)을 편집**한다.
2. 편집은 즉시 profile state에 반영된다.
3. **BattleTest는 그 profile을 그대로 읽어** 다음 전투에 즉시 적용된다 — 별도 export / save / re-import 없음.

즉 mock surface ↔ profile ↔ BattleTest 사이에 **single source of truth** 구조를 만든다.

## UI 품질 기준 — 콘솔급 JRPG (RimWorld식 아님)

본 프로젝트의 production 전략:

- **강점**: 고품질 이미지 생성 파이프라인 → UI 텍스쳐 / 엣지 ornament / 일러를 콘솔급으로 채울 수 있다.
- **약점**: 3D 런타임은 에셋 의존 — 그대로 두면 인게임 비주얼이 가벼워 보일 위험.
- **전략**: **UI/UX를 JRPG 콘솔급으로 탄탄하게 만들어 전체 퀄리티 인상을 끌어올린다.** 3D의 가벼움을 UI 밀도가 보완.

따라서 8 Town surface는 다음을 충족해야 한다.

- **9-slice frame texture** — flat color border가 아니라 ornate gold trim 이미지로 modal/card 엣지 마감.
- **ornament / divider / glow** — 헤더 ornament, 구분선, glow halo 등 요소요소 image layer.
- **modal open/close 애니메이션** — 등장/퇴장 transition (scale + opacity), hover/select micro-animation.
- **요소 단위 motion** — RimWorld식 정적 패널이 아니라, JRPG처럼 카드 hover / 탭 전환 / 노드 toggle에 짧은 motion.

Sprint A+B에서 잡은 frame/corner/CTA atom + 토큰은 **골격**이고, 그 위에 image texture + animation layer를
올리는 것이 콘솔급 마감. 본 binding workflow와 별개 lane이지만 같은 surface를 공유하므로 함께 추적한다.
컴포넌트 카탈로그: `pindoc://analysis-town-ui-component-system-v1` (애니메이션 3-tier 명시).

### Texture / animation layer 진행 (2026-05-14)

| 항목 | 상태 |
| --- | --- |
| RuntimePanelTheme atom — 9-slice frame texture / ornament / glow / 애니메이션 3-tier | ✓ `.sm-modal-frame__texture` / `.sm-card-frame__texture` / `.sm-ornament-divider` / `.sm-glow-halo` + `.sm-modal-anim` (tier1) / `.sm-hover-raise` (tier2) / `.sm-select-snap` (tier3) |
| 8 surface에 textured frame overlay 적용 | ✓ 각 modal frame에 `sm-modal-frame__texture` 자식 + `sm-modal-anim` class |
| interactive 요소 micro-motion | ✓ Recruit card / PermanentAugment cell / Inventory cell → `sm-hover-raise`. PassiveBoard node / TacticalWorkshop threat chip → `sm-select-snap`. TacticalWorkshop posture card → `sm-hover-raise` |
| frame texture 공용화 | ✓ `Foundation/Sprites/Frame/` 로 relocate (panel_modal/card frame 9-slice + ornament divider + glow halo). 전 surface 공용 |
| modal open/close C# trigger | 잔여 — Presenter Initialize에서 `--enter` 제거 timing wire (애니메이션 atom은 준비됨) |
| mood별 frame texture variant | 잔여 — 현재 1 generic gold-trim frame. ceremony(navy) / parchment 톤 차이는 bg color로만. imagen으로 mood-specific frame 생성 가능 |
| ornament header / glow halo 실배치 | 잔여 — atom은 있고, 각 surface header divider → `sm-ornament-divider`, selected/keystone → `sm-glow-halo` 적용 |

## 현재 상태 진단

| layer | 상태 |
| --- | --- |
| mock UI surface (8개) | ✓ pindoc V1 wiki SoT 시각 정합 + row/column 정합 완료 (audit 문서 참조) |
| mock UI ↔ profile binding | ✗ 미연결. 모든 fixture가 Bootstrap 내부 static array (`MockHero[]`, `Candidate[]`, etc) |
| profile state (`GameSessionState`) | ✓ 이미 존재 — `_root.SessionState.Profile.Heroes` / `HeroLoadouts` / `PermanentAugmentLoadouts` / `UnlockedPermanentAugmentIds` 등 |
| profile edit API | ✓ 일부 존재 — `Recruit(index)`, `RefitItem(...)`, `SelectPassiveBoard(...)`, `TogglePassiveNode(...)`, `EquipPermanentAugment(...)`, `CycleDeploymentAssignment(...)`, `CycleTeamPosture()` |
| BattleTest ↔ profile | ⚠ 부분 연결. `QuickBattle` 버튼은 `_root.BeginTransientTownSmoke()` 호출. 일반 expedition은 `_root.SessionState.BeginNewExpedition()`. 둘 다 profile을 source로 함 — 즉 BattleTest는 이미 profile read 구조. mock UI가 profile에 write하기만 하면 자동 연결. |

핵심 진단: **profile 양쪽 (write + read) 인프라는 이미 있다. 끊긴 건 mock UI ↔ profile write 사이.**

## SoT 정의

- **player profile (`GameSessionState.Profile`)** = 단일 source of truth.
- Town surface는 profile의 **view + edit action**. 자체 state를 갖지 않는다.
- BattleTest (Quick Battle, Smoke, Expedition) 진입 시점에 profile snapshot을 그대로 읽는다.
- profile의 write는 명시적 action (Recruit confirm, Refit, Toggle node, Equip permanent, Posture change, Anchor cycle 등)으로만 일어난다. 사용자 UI 조작이 곧 profile mutation.

이 구조에서:

- mock fixture → profile 데이터로 교체 = "binding 작업"
- mock action 버튼 → profile edit API 호출 = "wire 작업"
- profile 변경 → mock UI refresh = "presenter 패턴" (이미 TownScreenPresenter가 이 패턴 사용)

## Surface별 binding map

각 mock surface가 어떤 profile field를 **read**하고, 어떤 **edit action**으로 profile을 mutate하는가.

### Town.RosterGrid

| column | profile field | edit action |
| --- | --- | --- |
| 12 hero card | `Profile.Heroes` (`HeroInstanceRecord[]`) | — (선택 → 다른 surface로 navigate) |
| hero name / archetype | `HeroInstanceRecord.HeroId` + `ContentTextResolver.GetArchetypeName(...)` | — |
| KO / injured 상태 | `HeroInstanceRecord.Status` (KO / Injured / Healthy) | `HeroRecovery` surface에서 Heal Now |
| 추천 자세 | `ArchetypeDefinition.RecommendedPosture` (archetype-matrix V1) | — read-only |
| equip slot count | `HeroInstanceRecord.EquippedItemIds.Count` (max 3) | `Town.RefitModal` / Loadout 편집 |
| level / xp | `HeroInstanceRecord.Level`, `XpProgress` (TBD profile field) | — read-only (전투로만 증가) |
| last expedition node | `HeroInstanceRecord.LastSeenAt` 또는 `Profile.RunHistory` (TBD) | — read-only |
| filter chip (race/class) | client-side filter, profile 영향 없음 | — UI-only |

### Town.TacticalWorkshop

| column | profile field | edit action |
| --- | --- | --- |
| 6 anchor + 4 deploy standee | `LoadoutView.Deployments` (anchor → heroId 매핑) | `_root.SessionState.CycleDeploymentAssignment(anchor)` |
| 5 posture card | `GameSessionState.SelectedTeamPosture` (enum) | `_root.SessionState.CycleTeamPosture()` (또는 명시 set) |
| per-unit tactic RuleSet | `HeroInstanceRecord.TacticPreset` / `RoleInstructionDefinition` (콘텐츠) | — V1 read-only |
| 8 threat chip | runtime calc — `TeamCounterCoverage` 분석 결과 | — read-only |
| 7 synergy chip | runtime calc — `SynergyService.BuildForTeam(deployedHeroes)` | — read-only |
| live preview | runtime sim — `BattleSimPreview.Run(loadout)` 결과 frame | — read-only |

### Town.RecruitPack

| column | profile field | edit action |
| --- | --- | --- |
| 4 candidate card | `GameSessionState.RecruitOffers` (`RecruitUnitPreview[]`, 정확히 4 slot) | per-card `_root.SessionState.Recruit(slotIndex)` |
| slot type / tier / plan fit | `RecruitUnitPreview.Metadata` | — |
| signature / flex skills | `ArchetypeDefinition.SignatureSkillIds` + `RecruitUnitPreview.RolledFlex*` | — |
| tags | `ArchetypeDefinition.RecruitPlanTags` | — |
| gold cost | `RecruitUnitPreview.Metadata.GoldCost` | (Recruit 확정 시 차감) |
| Scout 버튼 | `Profile.Currencies.Echo` + `ScoutDirective` | `_root.SessionState.UseScout(directive)` |
| Refresh 버튼 | `RecruitPhase.FreeRefreshesRemaining` / `CurrentRecruitRefreshCost` | `_root.SessionState.RerollRecruitOffers()` |

### Town.RefitModal

| column | profile field | edit action |
| --- | --- | --- |
| 3 hex slot (weapon/armor/accessory) | `HeroInstanceRecord.EquippedItemIds` (slot type별) | — display only (스왑은 Loadout panel 영역) |
| 5 affix line (implicit + prefix 2 + suffix 2) | `ItemInstanceRecord.AffixLines` | — |
| reroll 대상 선택 | UI state (선택 affix index) | client-side selection |
| 15 Echo CTA | `Profile.Currencies.Echo` >= `MetaBalanceDefaults.RefitEchoCost` | `_root.SessionState.RefitItem(itemInstanceId, affixIndex)` |
| 8 inventory pool | `Profile.Inventory` (`InventoryItemRecord[]`) | (스왑은 Loadout panel) |

### Town.PermanentAugment

| column | profile field | edit action |
| --- | --- | --- |
| 4 augment cell | `CombatContentLookup.GetPermanentAugmentDefinitions()` (V1 floor 4개) | — read |
| 해금 상태 | `Profile.UnlockedPermanentAugmentIds.Contains(augmentId)` | — read-only (Reward 단계에서 unlock) |
| 자세 매핑 | `AugmentDefinition.BoundPostureId` | — read |
| family bucket | `AugmentDefinition.FamilyBucket` | — read |
| unlock chain progress | `Profile.AugmentFamilyPickHistory` (TBD field) | (전투 중 임시 augment first-pick으로 증가) |
| equip slot | `Profile.PermanentAugmentLoadouts.EquippedAugmentIds` (max 1) | `_root.SessionState.EquipPermanentAugment(augmentId)` |

### Town.PassiveBoard

| column | profile field | edit action |
| --- | --- | --- |
| 4 class tab | `CombatContentLookup.GetCanonicalPassiveBoardIds()` | `_root.SessionState.SelectPassiveBoard(heroId, boardId)` |
| 18 node | `PassiveBoardDefinition.Nodes` (12 small + 5 notable + 1 keystone) | — |
| node active 상태 | `HeroLoadoutRecord.ActivePassiveNodeIds.Contains(nodeId)` | `_root.SessionState.TogglePassiveNode(heroId, nodeId)` |
| rule summary / tags | `PassiveNodeDefinition.RuleSummary` / `Tags` | — read |
| footer breakdown | runtime calc | — read-only |

### Town.Inventory

| column | profile field | edit action |
| --- | --- | --- |
| currency header | `Profile.Currencies.Gold` / `Echo` | — read |
| 4 category sidebar | client-side filter | — UI-only |
| 5×4 item grid | `Profile.Inventory.Where(category filter)` | (선택 → detail 표시) |
| weapon family badge | `ItemDefinition.WeaponFamilyTag` | — read |
| rarity border | `ItemDefinition.Rarity` | — read |
| equipped marker | `Profile.Heroes.Any(h => h.EquippedItemIds.Contains(itemInstanceId))` | — read-only |
| detail affix lines | `ItemInstanceRecord.AffixLines` | — read |
| equip / sell / compare | TBD profile edit | TBD |

### Theater (Settings.Global) — cross-scene

cross-scene이라 Town profile뿐 아니라 게임 전역 setting profile (별도 `SettingsProfile`)을 read/write.

| Theater column | profile field | edit action |
| --- | --- | --- |
| 6 chapter group | `CombatContentLookup.GetOrderedCampaignChapters()` | — read |
| entry list | `Profile.StoryArchive.WatchedScenes` (TBD) | (시청 시 watched_at 기록) |
| duration / completion | `SceneCutsceneDefinition.Duration` + watch state | — read |

| Settings column | profile field | edit action |
| --- | --- | --- |
| 4 tab (Audio/Video/Language/Controls) | `SettingsProfile.{Audio, Video, Language, Controls}` | tab switch UI-only |
| option current value | `SettingsProfile.{key}` | option-specific set |
| option default value | `SettingsProfile.GetDefault({key})` | — read |
| dirty marker | `current != default` | — calc |
| reset 버튼 | — | `SettingsProfile.ResetToDefault({key})` |

## BattleTest 연결 — 별도 export 없음

V1 워크플로우:

1. Town hub에서 사용자가 어떤 surface (예: Town.SquadBuilder)에 진입.
2. 편집 action 호출 → `_root.SessionState.*` 메서드가 profile mutation.
3. profile mutation은 즉시 `_root.SaveProfile(SessionCheckpointKind.ManualSave)`로 직렬화 (또는 expedition exit 시점).
4. 사용자가 **Quick Battle** 또는 **Start Expedition** 클릭.
5. `_root.SessionState.PrepareTownQuickBattleSmoke()` (또는 `BeginNewExpedition()`) 호출 → 현재 profile을 그대로 BattleTest snapshot으로 변환.
6. Battle scene 진입 — `_root.SessionState.Profile.Heroes` / `LoadoutView.Deployments` / `SelectedTeamPosture` / `HeroLoadouts.ActivePassiveNodeIds` 등 profile read.
7. 별도 export / load 없음. profile = single state.

즉 **mock UI ↔ profile write가 wired되면, BattleTest read는 자동으로 따라온다** — 이미 BattleTest는 profile reader다.

## 구현 단계

워크플로우 완성까지 3 sprint:

### Sprint 1: presenter 추출

각 mock Preview Bootstrap이 직접 fixture를 inject하는 대신 **presenter class를 통해 profile view-state를 받는** 패턴으로 분리.

| 작업 | 대상 | 상태 |
| --- | --- | --- |
| `TacticalWorkshopPresenter` 신설 | profile → `TacticalWorkshopViewState` | ✓ 완료 (2026-05-14) |
| `RecruitPresenter` 신설 | profile → `RecruitViewState` (4 candidate card) | ✓ 완료 (2026-05-14) |
| `EquipmentRefitPresenter` 신설 | profile → `EquipmentRefitViewState` | ✓ 완료 (2026-05-14) |
| `PermanentAugmentPresenter` 신설 | profile → `PermanentAugmentViewState` (4 augment) | ✓ 완료 (2026-05-14) |
| `PassiveBoardPresenter` 신설 | profile → `PassiveBoardViewState` (선택 hero + board nodes) | ✓ 완료 (2026-05-14) |
| `InventoryPresenter` 신설 | profile → `InventoryViewState` | ✓ 완료 (2026-05-14) |
| `RosterGridPresenter` 신설 | profile → `RosterGridViewState` (12 hero) | ✓ 완료 (2026-05-14) |
| Preview Bootstrap은 dev tool로 유지 | mock fixture를 ViewState로 변환해 View에 inject | ✓ 7 surface 모두 완료 |

**Sprint 1 종료** — 모든 7 surface가 같은 4-file 구조 (ViewState + View + Presenter + Bootstrap refactor)로 lock.
Editor Bootstrap은 mock fixture를 ViewState로 빌드해 View에 inject. Runtime Presenter는 profile read scaffold 완료
(BuildState 본체는 Sprint 2에서 정확한 archetype matrix lookup / synergy service / counter coverage 등 wire).

#### TacticalWorkshop 패턴 lock (2026-05-14)

**namespace 결정**: `SM.Unity.UI.Town.Preview` — Codex legacy `SM.Unity.UI.TacticalWorkshop`과
충돌 회피. V1 redesign surface 묶음 자리. 나머지 6 surface도 같은 namespace 사용.

**파일 4종 구조** (`TownScreenPresenter` 패턴 따름):

1. `Runtime/Unity/UI/Town/Preview/TacticalWorkshopViewState.cs` — record 6종 (Anchor / Posture / SynergyChip / Threat / Rule / HeroTactic) + 컨테이너 record.
2. `Runtime/Unity/UI/Town/Preview/TacticalWorkshopView.cs` — UXML root에서 container 참조 캡처 (`twp-anchor-pad` / `PostureCardRow` / `twp-synergy-row` / `ThreatGrid` / `TacticPresetRows`). `Render(ViewState)` 시 각 container clear + 재구축. sprite 로드 안 함 — caller가 Texture2D pre-resolve.
3. `Runtime/Unity/UI/Town/Preview/TacticalWorkshopPresenter.cs` — `GameSessionRoot` + View + 3 SpriteLoader delegate ctor. `BuildState()` 메서드가 profile read → ViewState. `OnPostureSelected` / `OnAnchorClicked` 액션이 SessionState edit API 호출 후 Refresh.
4. `Editor/Bootstrap/UI/TacticalWorkshopPreviewBootstrap.cs` — `EditorWindow` dev tool. Bootstrap은 GameSessionRoot 없이 mock ViewState를 직접 빌드해서 View에 inject. Presenter의 static catalog (Postures / Synergies / Threats) 재사용 → mock fixture와 spec single source.

**Sprint 1 미구현 TODO** (Sprint 2 wire):
- `SessionState.SetTeamPosture(string postureId)` named-set API (현재 cycle only)
- `ProfileQueries.GetLoadoutView` 통한 anchor → hero figure resolve
- `SynergyService.BuildForTeam(deployedHeroes)` 통한 synergy active/breakpoint
- `TeamCounterCoverage` 계산을 통한 threat lane answered/unanswered
- deployed hero × `RoleInstructionDefinition.RuleSet` 매핑 (per-unit tactic)

**나머지 6 surface 동일 패턴 적용**: 같은 4-file 구조, 같은 namespace, 같은 View ↔ Presenter ↔ Bootstrap 분리. mock fixture는 Bootstrap에서 정확한 ViewState를 빌드해서 View.Render() 호출.

### Sprint 2: profile edit API 보강 + BuildState 실 데이터 wire

#### 이미 존재하는 API (Sprint 1에서 wire)

- `SessionState.Recruit(slotIndex)` / `RerollRecruitOffers()` / `UseScout(directive)`
- `SessionState.RefitItem(itemInstanceId, affixIndex)`
- `SessionState.SelectPassiveBoard(heroId, boardId)` / `TogglePassiveNode(heroId, nodeId)`
- `SessionState.EquipPermanentAugment(augmentId)`
- `SessionState.CycleDeploymentAssignment(anchor)` / `CycleTeamPosture()`
- **`SessionState.SetTeamPosture(TeamPostureType)` ← 이미 존재** (Sprint 2에서 발견, named-set 신설 불필요)

#### Sprint 2 wire 진행 (2026-05-14)

| Surface | Wire 상태 |
| --- | --- |
| **TacticalWorkshop** | ✓ `OnPostureSelected` → enum parse + `SetTeamPosture(posture)` named-set wire / `BuildAnchors` → `ProfileQueries.GetLoadoutView` + `Profile.Heroes` lookup으로 anchor → hero class sprite 실 wire |
| **Recruit** | ✓ `BuildState` → `session.RecruitOffers` 실 매핑 (SlotType / Tier / PlanFit / GoldCost / ScoutBias) + `CombatContentLookup.TryGetArchetype` → class sprite. tags / signature / flex는 Sprint 3 |
| **Inventory** | ✓ `BuildState` → `Profile.Inventory` 실 매핑 + equipped state (Heroes.EquippedItemIds 집계). rarity / weapon family / affix는 Sprint 3 (ItemDefinition lookup 필요) |
| **PermanentAugment** | ✓ 이미 `Profile.UnlockedPermanentAugmentIds` + `PermanentAugmentLoadouts.EquippedAugmentIds` wire (Sprint 1) |
| **RosterGrid** | ✓ `BuildState` → archetype matrix lookup (`CombatContentLookup.TryGetArchetype`)으로 race/class 정확화 |
| PassiveBoard | Sprint 3 — node 위치 계산 + per-hero `ActivePassiveNodeIds` wire 필요 (selected hero context도) |
| EquipmentRefit | Sprint 3 — ItemDefinition.AffixLines lookup 필요 |

#### Sprint 3 wire 진행 (2026-05-14)

| 항목 | 상태 |
| --- | --- |
| **ContentTextResolver 도입** | ✓ RosterGrid / Recruit / PassiveBoard / EquipmentRefit Presenter ctor에 추가. archetype / race / class / passive node 한국어 표시명 localized read |
| **RosterGrid 표시명 정확화** | ✓ `GetClassName` / `GetRaceName` / `GetArchetypeName`으로 archetype label 정확화 |
| **Recruit 표시명 정확화** | ✓ `GetArchetypeName`으로 candidate DisplayName 정확화 |
| **PassiveBoard Presenter wire** | ✓ `CombatContentLookup.TryGetPassiveBoardDefinition` → board.Nodes. BoardDepth 기반 ring layout (depth 0=center, 1=inner r0.18, 2=outer r0.36). per-hero `HeroLoadoutRecord.SelectedPassiveNodeIds`로 active state. `SelectPassiveBoard` / `TogglePassiveNode` edit wire. `SetSelectedHero(heroId)` 컨텍스트 메서드 추가 |
| **EquipmentRefit Presenter wire** | ✓ `Profile.Inventory` → pool (`ItemBaseDefinition.RarityTier`로 gem 색상). selected item `AffixIds` → affix list (V1 floor 규약 index 기반 implicit/prefix/suffix 추정). `RefitItem(itemInstanceId, affixIndex)` edit wire |

#### Sprint 3 잔여 (다음 세션)

- `Profile.AugmentFamilyPickHistory` 필드 신설 (permanent augment unlock chain progress 추적) — SM.Persistence 스키마 변경, save 버전 bump 필요
- `EquipItem(heroId, slotType, itemInstanceId)` / `UnequipItem(heroId, slotType)` — Loadout 슬롯 swap, SM.Meta 구조 검토 후
- `SwitchHeroSquadAssignment(heroId, squadSlot)` — 8 expedition squad 편집
- Recruit tags (`RecruitPlanTags`) / signature skills / rolled flex 정확화 — `CombatContentLookup.TryGetCombatSnapshot` template lookup 필요
- PassiveBoard node icon mapping — `CompileTags` 기반 affix sprite 매핑
- affix group (implicit/prefix/suffix) 정확한 flag — `AffixDefinition` group field read (현재 index 기반 추정)
- Town hub UXML modal entry 버튼 wire (production scene 통합)
- BattleTest end-to-end 검증 — 시나리오 A/B/C (deploy swap / posture change / refit → BattleTest 즉시 반영)

### Sprint 3: scene wiring + BattleTest 검증

| 작업 | 검증 시나리오 |
| --- | --- |
| Town hub UXML에 8 modal entry 버튼 wire | "Town에서 Tactical Workshop 클릭 → modal 열림 → posture 변경 → modal 닫기 → Quick Battle → 변경된 posture로 전투" |
| Quick Battle 진입 전 profile snapshot 검증 | "Town에서 hero A 영입 → 즉시 Quick Battle → hero A가 deploy 가능" |
| Refit 후 BattleTest 검증 | "Town.Refit에서 affix reroll → Quick Battle → 변경된 affix로 stat 계산" |
| Posture change → battle behavior | "Town에서 posture를 HoldLine → AllInBackline 변경 → Quick Battle → 적 후열까지 적극 침투하는 진형으로 출발" |

## 검증 시나리오 — 사용자 워크플로우 end-to-end

> 시나리오 A: deploy swap

1. Town hub 진입 — RosterGrid에 12명 노출. Lv 14 Oath Slayer가 Front-Top에 deploy되어 있음.
2. SquadBuilder 진입. Oath Slayer를 Front-Top에서 빼고 Pack Raider를 Front-Top에 배치.
3. SquadBuilder 닫고 Quick Battle 클릭.
4. Battle scene 시작 — Front-Top 위치에 Pack Raider standee가 보이고, Oath Slayer는 대기. **profile write → BattleTest read 연결 검증.**

> 시나리오 B: posture change

1. Tactical Workshop 진입. 현재 posture = StandardAdvance.
2. 5 posture 카드 중 AllInBackline 카드 클릭.
3. Tactical Workshop 닫고 Quick Battle 클릭.
4. Battle scene 시작 — 전열이 평소보다 깊게 전진하는 행동을 보임. **posture write → behavior read 연결 검증.**

> 시나리오 C: refit

1. Inventory 진입. weapon slot Item A의 prefix 1 (CRIT +5%) 확인.
2. Refit Modal 열기. prefix 1을 reroll target으로 선택. 15 Echo 차감 후 reroll → CRIT +8% / 또는 다른 affix.
3. Refit Modal 닫고 Quick Battle 클릭.
4. Battle scene 시작 — Item A 장착 hero의 stat이 새 affix 값으로 계산됨. **refit write → stat compile 연결 검증.**

## 본 워크플로우가 닫지 않는 것

- **art-pipeline regen** — 4 permanent augment motif, 12 archetype portrait 등 별도 task.
- **save format migration** — profile field 추가 (예: `AugmentFamilyPickHistory`) 시 save schema 버전 bump.
- **production TownScreen.uxml 폐기** — single-page dashboard → modal-driven hub 전환은 별도 task (현재 Preview가 그 미래 형태의 시각 demo).
- **runtime BattleSimPreview 구현** — Tactical Workshop의 live preview frame 생성. V1 시점에서는 정적 placeholder 유지 가능, V2에서 actual sim run.

본 워크플로우는 **mock UI ↔ profile binding + BattleTest 연결까지의 골격**을 정의한다.
art / sim / production migration 같은 별도 lane은 자기 task에서 진행.
