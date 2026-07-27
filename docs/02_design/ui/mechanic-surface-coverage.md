# Mechanic-Surface Coverage Map

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-27
- 감사 기준 커밋: `1f59f5da` + 이 작업 단위의 working tree
- 카탈로그 버전: `1`
- 관련문서:
  - `docs/02_design/index.md`
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/02_design/ui/town-character-sheet-ui.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `tools/content-reachability/field-catalog.tsv`

## 목적과 범위

이 문서는 runtime mechanic과 player-visible surface 사이의 정적 coverage를 기록한다. 코드와 authored content가 실제로 소비하는 mechanic만 대상으로 삼고, 일반적인 시각 품질이나 미술 완성도는 평가하지 않는다. PlayMode, screenshot, scene 편집 없이 C# runtime consumer, content asset, scene route, UXML/USS, presenter wiring만 대조했다.

향후 lint가 읽을 수 있도록 mechanic 행은 고유한 `id`와 정확히 하나의 `classification`을 가진다. 분류 의미는 다음과 같다.

- `visible`: 실제 play route의 surface가 mechanic의 의사결정 정보를 정확히 표시한다.
- `partial`: surface는 있으나 단위, 조건, downside, source, exact value 가운데 하나 이상이 빠지거나 잘못된 설명을 제공한다.
- `invisible`: 결과를 바꾸는 live runtime consumer가 있으나 surface가 없다.
- `n/a`: 현재 runtime outcome을 바꾸지 않는 schema placeholder이거나 production read가 없는 값이다. 각 행에 근거를 적는다.

## 결론

| 지표 | 수 |
| --- | ---: |
| 전체 mechanic 행 | 110 |
| `visible` | 19 |
| `partial` | 65 |
| `invisible` | 22 |
| `n/a` | 4 |

`craft-operation-seal`은 기존 `EquipmentRefit`의 실제 Town route에서 닫혔다. 플레이어는 `Reforge`와 `Seal`을 선택하고, affix별 lock을 지정하며, service가 계산한 lock 수별 정확한 Echo cost와 실행 불가 이유를 확인한 뒤 되돌릴 수 없는 소비를 확인할 수 있다. 현재 가장 높은 미해결 gap은 War Wound의 발생·효과 표시다.

## Surface inventory

### Scene 8개

production build에는 `Boot`, `Town`, `Atlas`, `Battle`, `Reward`만 들어간다. `SceneFlowController`도 이 다섯 scene만 route하며, binder 역시 같은 production set을 검증한다 (`ProjectSettings/EditorBuildSettings.asset:7-22`, `Assets/_Game/Scripts/Runtime/Unity/SceneFlowController.cs:18-37`, `Assets/_Game/Scripts/Runtime/Unity/FirstPlayableRuntimeSceneBinder.cs:75-101`).

| surface | player reachable | 플레이어가 하는 일 |
| --- | --- | --- |
| `Assets/_Game/Scenes/Boot.unity` | yes | local run을 시작하고 Town으로 진입한다 (`Assets/_Game/Scripts/Runtime/Unity/BootScreenController.cs:37-63`). |
| `Assets/_Game/Scenes/Town.unity` | yes | roster, squad, inventory, refit, passive, augment, recruit, tactic을 관리하고 출격한다. |
| `Assets/_Game/Scenes/Atlas.unity` | yes | region, sigil, node를 선택하고 위험·보상을 비교한 뒤 sortie와 warrant를 확정한다. |
| `Assets/_Game/Scenes/Battle.unity` | yes | 자동 전투를 관찰하고 unit detail과 observer control을 사용한다. |
| `Assets/_Game/Scenes/Reward.unity` | yes | reward를 고르고 settlement를 확인한 뒤 Town으로 돌아간다. |
| `Assets/_Game/Scenes/Expedition.unity` | no | 폐기된 중간 scene이다. Atlas가 Battle 또는 Reward로 직접 이동한다 (`Assets/_Game/Scripts/Runtime/Unity/FirstPlayableRuntimeSceneBinder.cs:87-90`). |
| `Assets/_Game/Scenes/Theater.unity` | no | authored story를 다시 보는 용도지만 production build와 route에 없고 Town entry도 opener가 없으면 숨겨진다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs:166-169`, `Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs:283-312`). |
| `Assets/_Game/Scenes/TacticalWorkshopSandbox.unity` | no | editor/dev bootstrap 전용 tactical workshop sandbox다. |

### UXML 42개

`TownScreen.uxml`은 production panel template 10개를 선언하고 instance로 포함하며, `TownScreenController`가 이들을 실제 presenter와 연결한다 (`Assets/_Game/UI/Screens/Town/TownScreen.uxml:9-18`, `Assets/_Game/UI/Screens/Town/TownScreen.uxml:244-271`, `Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs:104-121`).

| surface | player reachable | 플레이어가 하는 일 |
| --- | --- | --- |
| `Foundation/Components/Button.uxml` | no | component/gallery authoring reference를 본다. production action surface는 아니다. |
| `Foundation/Components/ConsoleCompare.uxml` | no | editor showcase에서 console variant를 비교한다. |
| `Foundation/Components/Gallery.uxml` | no | editor showcase에서 foundation component를 살핀다. |
| `Foundation/Components/HeroPortraitCard.uxml` | no | editor preview용 portrait card를 본다. production roster는 이 template을 전달하지 않고 runtime card를 만든다 (`Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs:327-357`). |
| `Foundation/Details/ItemDetailModal.uxml` | no | 현재 production route가 clone하지 않는 item detail shell이다. |
| `Foundation/Details/SkillDetailModal.uxml` | no | 현재 production route가 clone하지 않는 skill detail shell이다. |
| `Foundation/Details/StatusEffectTooltipPanel.uxml` | no | 현재 production Battle이 clone하지 않는 status tooltip shell이다. |
| `Narrative/DialogueOverlay.uxml` | yes | 대사 overlay를 읽고 진행한다. |
| `Narrative/DialogueScene.uxml` | yes | scene형 대사를 읽고 진행한다. |
| `Narrative/StoryCard.uxml` | yes | story card를 읽고 닫거나 진행한다. |
| `Narrative/StoryToastBanner.uxml` | yes | 짧은 story 알림을 확인한다. |
| `Narrative/TheaterMode.uxml` | no | unreachable Theater scene에서 archive를 탐색한다. runtime story surface 선택은 `Assets/_Game/Scripts/Runtime/Unity/Narrative/StoryPresentationRunner.cs:32-38`에 있다. |
| `Panels/EquipmentRefit/EquipmentRefit.uxml` | yes | item과 Reforge/Seal operation을 고르고, affix lock, 정확한 quote, 실행 불가 이유, 확인 단계를 조작한다. |
| `Panels/InventoryTab/InventoryTab.uxml` | yes | item, rarity, identity, affix와 currency를 확인한다. |
| `Panels/PassiveBoard/PassiveBoard.uxml` | yes | passive node를 선택하거나 해제한다. |
| `Panels/PermanentAugment/PermanentAugment.uxml` | yes | permanent augment를 골라 equip한다. |
| `Panels/RecruitPack/RecruitPack.uxml` | yes | recruit offer를 비교하고 고용한다. |
| `Panels/SettingsGlobal/SettingsGlobal.uxml` | no | Phase 2 showcase용 settings shell이며 production presenter가 없다. |
| `Panels/SkillCompendium/SkillCompendium.uxml` | yes | skill 목록과 상세 수치를 읽는다. |
| `Panels/TacticalWorkshop/TacticalWorkshop.uxml` | yes | posture, target directive, threat coverage를 편집·검토한다. |
| `Panels/TownCharacterSheet/TownCharacterSheet.uxml` | yes | hero stat, skill, progression, loadout을 검토한다. |
| `Panels/TownRosterGrid/TownRosterGrid.uxml` | yes | roster를 검색·필터하고 hero를 선택한다. |
| `Panels/TownSquadBuilder/TownSquadBuilder.uxml` | yes | hero를 anchor에 배치하고 posture를 고른다. |
| `Screens/Atlas/AtlasScreen.uxml` | yes | map node와 modifier를 비교하고 진입한다. |
| `Screens/Atlas/Resources/SortieConfirm.uxml` | yes | 배치 squad와 synergy를 확인하고 출격한다. |
| `Screens/Atlas/Resources/WarrantSelection.uxml` | yes | warrant modifier를 비교하고 pledge 또는 skip한다. |
| `Screens/Battle/BattleScreen.uxml` | yes | battle observer와 selected-unit detail을 조작한다. |
| `Screens/Reward/RewardScreen.uxml` | yes | reward choice와 run settlement를 확인한다. |
| `Screens/Town/Preview/CompendiumPreview.uxml` | no | editor preview에서 compendium panel을 본다. |
| `Screens/Town/Preview/EquipmentRefitPreview.uxml` | no | editor preview에서 refit panel을 본다. |
| `Screens/Town/Preview/InventoryPreview.uxml` | no | editor preview에서 inventory panel을 본다. |
| `Screens/Town/Preview/PassiveBoardPreview.uxml` | no | editor preview에서 passive board를 본다. |
| `Screens/Town/Preview/PermanentAugmentPreview.uxml` | no | editor preview에서 augment panel을 본다. |
| `Screens/Town/Preview/RecruitPreview.uxml` | no | editor preview에서 recruit panel을 본다. |
| `Screens/Town/Preview/RosterGridPreview.uxml` | no | editor preview에서 roster panel을 본다. |
| `Screens/Town/Preview/SettingsPreview.uxml` | no | editor preview에서 settings panel을 본다. |
| `Screens/Town/Preview/TacticalWorkshopPreview.uxml` | no | editor preview에서 workshop panel을 본다. |
| `Screens/Town/Preview/TheaterPreview.uxml` | no | editor preview에서 Theater panel을 본다. |
| `Screens/Town/SquadBuilder.uxml` | no | production panel보다 오래된 standalone squad-builder document다. |
| `Screens/Town/TownScreen.uxml` | yes | Town hub에서 service panel을 열고 출격한다. |
| `TacticalWorkshop/TacticalWorkshop.uxml` | no | legacy standalone workshop presenter가 쓰는 sandbox document다. |
| `TacticalWorkshop/TacticalWorkshopSandbox.uxml` | no | editor/dev sandbox에서 tactical workshop을 조작한다. |

### USS 45개

USS는 독립 action surface가 아니므로 `player reachable`은 production UXML 또는 runtime-created element가 실제로 이 style을 소비하는지를 뜻한다.

| surface | player reachable | 플레이어가 하는 일을 지원하는 역할 |
| --- | --- | --- |
| `Foundation/Components/Button.uss` | yes | production button의 hover, focus, disabled 상태를 식별한다. |
| `Foundation/Components/ConsoleCompare.uss` | no | console comparison showcase를 꾸민다. |
| `Foundation/Components/Gallery.uss` | no | component gallery를 꾸민다. |
| `Foundation/Components/HeroFaceCard.uss` | yes | runtime-created hero face card의 class·state를 구분한다. |
| `Foundation/Components/HeroPortraitCard.uss` | yes | runtime-created roster/portrait card의 상태를 구분한다. |
| `Foundation/Styles/RuntimePanelTheme.uss` | yes | production runtime panel 공통 theme를 제공한다. |
| `Foundation/Styles/ThemeTokens.uss` | yes | production surface가 참조하는 color·spacing token을 제공한다. |
| `Foundation/USS/common_detail.uss` | yes | item·skill·status detail의 공통 layout을 제공한다. |
| `Foundation/USS/foundation_glow.uss` | yes | focus와 selected state의 공통 glow를 제공한다. |
| `Narrative/DialogueOverlay.uss` | yes | dialogue overlay를 읽고 진행하는 상태를 구분한다. |
| `Narrative/DialogueScene.uss` | yes | dialogue scene을 읽고 진행하는 상태를 구분한다. |
| `Narrative/StoryCard.uss` | yes | story card의 선택·dismiss 상태를 구분한다. |
| `Narrative/StoryCommon.uss` | yes | narrative surface 공통 typography와 layout을 제공한다. |
| `Narrative/StoryToastBanner.uss` | yes | story toast의 등장과 내용을 표시한다. |
| `Narrative/TheaterMode.uss` | no | unreachable Theater archive를 꾸민다. |
| `Panels/EquipmentRefit/EquipmentRefit.uss` | yes | refit target, operation, affix lock, cost, quality, confirmation, action state를 구분한다. |
| `Panels/InventoryTab/InventoryTab.uss` | yes | item rarity, selection, currency와 affix row를 구분한다. |
| `Panels/PassiveBoard/PassiveBoard.uss` | yes | passive node의 active·selected·locked visual state를 구분한다. |
| `Panels/PermanentAugment/PermanentAugment.uss` | yes | augment의 equipped·unlocked·locked state를 구분한다. |
| `Panels/RecruitPack/RecruitPack.uss` | yes | recruit class, price, selection state를 구분한다. |
| `Panels/SettingsGlobal/SettingsGlobal.uss` | no | production route가 없는 settings shell을 꾸민다. |
| `Panels/SkillCompendium/SkillCompendium.uss` | yes | skill list와 detail state를 구분한다. |
| `Panels/TacticalWorkshop/TacticalWorkshop.uss` | yes | posture, threat, tactic selection을 구분한다. |
| `Panels/TownCharacterSheet/TownCharacterSheet.uss` | yes | character sheet section과 selected hero state를 구분한다. |
| `Panels/TownRosterGrid/TownRosterGrid.uss` | yes | roster filter, family, selection state를 구분한다. |
| `Panels/TownSquadBuilder/TownSquadBuilder.uss` | yes | anchor assignment와 posture state를 구분한다. |
| `Screens/Atlas/AtlasScreen.uss` | yes | node, modifier, risk, selected route를 구분한다. |
| `Screens/Atlas/Resources/SortieConfirm.uss` | yes | deploy readiness와 synergy preview를 구분한다. |
| `Screens/Atlas/Resources/WarrantSelection.uss` | yes | warrant card와 pledge/skip selection을 구분한다. |
| `Screens/Battle/BattleScreen.uss` | yes | observer control, unit selection, status chip을 구분한다. |
| `Screens/Reward/RewardScreen.uss` | yes | reward card, selected choice, settlement 상태를 구분한다. |
| `Screens/Town/Preview/CompendiumPreview.uss` | no | editor compendium preview를 꾸민다. |
| `Screens/Town/Preview/EquipmentRefitPreview.uss` | no | editor refit preview를 꾸민다. |
| `Screens/Town/Preview/InventoryPreview.uss` | no | editor inventory preview를 꾸민다. |
| `Screens/Town/Preview/PassiveBoardPreview.uss` | no | editor passive preview를 꾸민다. |
| `Screens/Town/Preview/PermanentAugmentPreview.uss` | no | editor augment preview를 꾸민다. |
| `Screens/Town/Preview/RecruitPreview.uss` | no | editor recruit preview를 꾸민다. |
| `Screens/Town/Preview/RosterGridPreview.uss` | no | editor roster preview를 꾸민다. |
| `Screens/Town/Preview/SettingsPreview.uss` | no | editor settings preview를 꾸민다. |
| `Screens/Town/Preview/TacticalWorkshopPreview.uss` | no | editor workshop preview를 꾸민다. |
| `Screens/Town/Preview/TheaterPreview.uss` | no | editor Theater preview를 꾸민다. |
| `Screens/Town/SquadBuilder.uss` | no | 오래된 standalone squad-builder document를 꾸민다. |
| `Screens/Town/TownScreen.uss` | yes | Town hub와 embedded service panel을 구분한다. |
| `TacticalWorkshop/TacticalWorkshop.uss` | no | legacy standalone workshop을 꾸민다. |
| `TacticalWorkshop/TacticalWorkshopSandbox.uss` | no | editor/dev workshop sandbox를 꾸민다. |

### Presenter 17개

`Preview` namespace 아래 presenter 8개는 이름과 달리 production Town에 연결돼 있다. standalone `UI/TacticalWorkshop/TacticalWorkshopPresenter`만 sandbox 전용이다.

| surface | player reachable | 플레이어가 하는 일 |
| --- | --- | --- |
| `UI/Atlas/AtlasScreenPresenter.cs` | yes | region, sigil, node, route modifier를 비교한다. |
| `UI/Atlas/SortieConfirmPresenter.cs` | yes | deploy roster와 synergy를 확인하고 출격한다. |
| `UI/Atlas/WarrantSelectionPresenter.cs` | yes | warrant condition과 stat modifier를 비교해 pledge 또는 skip한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/WarrantSelectionPresenter.cs:73-157`). |
| `UI/Battle/BattleScreenPresenter.cs` | yes | battle observer state와 selected unit을 본다. |
| `UI/Reward/RewardScreenPresenter.cs` | yes | reward와 settlement를 비교·확정한다. |
| `UI/TacticalWorkshop/TacticalWorkshopPresenter.cs` | no | legacy sandbox에서 posture를 고른다. |
| `UI/Town/Preview/CompendiumPresenter.cs` | yes | skill을 검색하고 상세를 읽는다. |
| `UI/Town/Preview/EquipmentRefitPresenter.cs` | yes | refit item, Reforge/Seal, affix lock, service quote, 확인 단계를 비교하고 실행한다. |
| `UI/Town/Preview/InventoryPresenter.cs` | yes | inventory item, rarity, identity, affix를 비교한다. |
| `UI/Town/Preview/PassiveBoardPresenter.cs` | yes | passive node를 선택·해제한다. |
| `UI/Town/Preview/PermanentAugmentPresenter.cs` | yes | permanent augment를 선택·equip한다. |
| `UI/Town/Preview/RecruitPresenter.cs` | yes | recruit offer와 cost를 비교한다. |
| `UI/Town/Preview/RosterGridPresenter.cs` | yes | roster를 필터하고 hero를 선택한다. |
| `UI/Town/Preview/TacticalWorkshopPresenter.cs` | yes | posture, target directive, threat answer를 관리한다. |
| `UI/Town/SquadBuilderPresenter.cs` | yes | anchor assignment와 team posture를 편집한다. |
| `UI/Town/TownCharacterSheetPresenter.cs` | yes | hero detail, skill, loadout, progression을 검토한다. |
| `UI/Town/TownScreenPresenter.cs` | yes | Town service를 열고 save/load/expedition action을 실행한다. |

### Panel registry와 host

`RuntimePanelAssetRegistry`는 `Town`, `Atlas`, `Battle`, `Reward` panel document만 canonical registry로 해석한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/RuntimePanelAssetRegistry.cs:36-79`). `RuntimePanelHost`는 registry가 반환한 `VisualTreeAsset`을 clone하고 한 screen DOM의 visibility와 input을 관리한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/RuntimePanelHost.cs:83-109`, `Assets/_Game/Scripts/Runtime/Unity/UI/RuntimePanelHost.cs:205-233`). 둘은 직접 player action을 제공하지 않지만 production surface reachability를 결정하는 infrastructure다.

## Mechanic coverage catalog

### Crafting

| id | 하는 일 | runtime evidence | classification | player surface evidence | 빠진 정보 또는 `n/a` 근거 |
| --- | --- | --- | --- | --- | --- |
| `craft-operation-temper` | `Temper` operation kind를 정의한다. | `Assets/_Game/Scripts/Runtime/Core/Content/ContentSchema.cs:45-52` | `n/a` | none | live item은 `Reforge`와 `Seal`만 허용하고 validation도 그 둘만 받는다 (`Assets/_Game/Scripts/Editor/Validation/ContentDefinitionCatalogRules.cs:886-893`). |
| `craft-operation-reforge` | item identity와 affix set을 유지하며 magnitude를 다시 굴린다. | `Assets/_Game/Scripts/Runtime/Meta/Services/RefitService.cs:72-77` | `visible` | `EquipmentRefitPresenter`가 target, quote, old/new quality, Echo cost를 표시하고 실행한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs:96-193`). | - |
| `craft-operation-seal` | 일부 affix를 lock하고 나머지 magnitude를 deterministic하게 다시 굴린다. | `Assets/_Game/Scripts/Runtime/Meta/Services/RefitRollQuality.cs:214-270`; `Assets/_Game/Scripts/Runtime/Unity/Session/SessionItemRefitFlow.cs` | `visible` | `EquipmentRefitPresenter`가 `Seal` operation, affix별 lock, 확인 단계를 노출하고 기존 session command로 실행한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs`). | - |
| `craft-operation-imprint` | `Imprint` operation kind를 정의한다. | `Assets/_Game/Scripts/Runtime/Core/Content/ContentSchema.cs:45-52` | `n/a` | none | authored item과 live service가 이 operation을 허용하거나 소비하지 않는다. |
| `craft-operation-salvage` | `Salvage` operation kind를 정의한다. | `Assets/_Game/Scripts/Runtime/Core/Content/ContentSchema.cs:45-52` | `n/a` | none | authored item과 live service가 이 operation을 허용하거나 소비하지 않는다. |
| `refit-roll-quality` | rolled affix magnitude를 `BudgetScore` 가중 percentile로 요약한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/RefitRollQuality.cs:10-52` | `visible` | Refit panel이 current/expected quality와 delta를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs:228-267`). | - |
| `affix-instance-magnitude-roll` | 각 item instance가 definition 범위 안의 개별 magnitude를 가진다. | `Assets/_Game/Scripts/Runtime/Meta/Services/AffixMagnitudePackageResolver.cs:15-55` | `partial` | Inventory와 Refit가 roll value 또는 percent/range를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryPresenter.cs:378-397`, `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs:166-193`). | 어떤 stat, unit, trigger condition에 적용되는 값인지 표시하지 않는다. |
| `seal-cost-multiplier-per-locked-affix` | lock 수마다 Seal Echo cost를 증가시킨다. | `Assets/_Game/Scripts/Runtime/Meta/Services/RefitCostCurve.cs:83-115`; `Assets/_Game/Scripts/Runtime/Content/Definitions/RefitBalanceDefinition.cs:39-42` | `visible` | lock 변경마다 `GetSealQuote`를 다시 호출해 service가 계산한 정확한 Echo cost를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs`). | - |
| `allowed-craft-operations` | item별로 허용할 craft action을 gate한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentPresentationPolicy.cs:142-156` | `visible` | `EquipmentRefit`가 `Reforge`와 `Seal` operation을 함께 표시하고, 허용되지 않은 operation은 session preflight와 같은 이유로 비활성화한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs`, `Assets/_Game/Scripts/Runtime/Unity/Session/SessionItemRefitFlow.cs`). | - |

### Affix 44개

모든 affix는 item loadout compile에서 numeric package, rule package, triggered effect를 실제 전투 artifact로 만든다 (`Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:144-166`, `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:470-493`, `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:681-715`). Inventory와 Refit는 공통적으로 이름, tier/category, roll 값만 표시하며 stat 이름, unit, condition, downside, trigger, rule tag를 표시하지 않는다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryPresenter.cs:378-397`, `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitPresenter.cs:166-193`). 따라서 44개 모두 `partial`이다.

`decision-bearing` 표시는 단순 scalar보다 선택 의미가 큰 14개를 뜻한다. 6개는 실제 downside가 있고, 7개는 runtime trigger가 있으며, 1개는 `RuleModifierTag`를 compile한다. trigger enum 의미는 `Assets/_Game/Scripts/Runtime/Core/Contracts/TriggeredEffectContracts.cs:8-39`에 있다.

| id | 하는 일 | runtime evidence | classification | player surface evidence | 빠진 정보 |
| --- | --- | --- | --- | --- | --- |
| `affix_blessed` | `heal_power` scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_blessed.asset:60`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_blood_price` | `lifesteal +4%` 대신 `max_health -4%`를 적용한다. `decision-bearing: tradeoff`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_blood_price.asset:68`; common compile path above | `partial` | Inventory/Refit affix row | 생명력 downside를 포함한 양쪽 stat |
| `affix_bracing` | durability scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_bracing.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_brittle_focus` | `mag_power +2` 대신 `resist -1`을 적용한다. `decision-bearing: tradeoff`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_brittle_focus.asset:60`; common compile path above | `partial` | Inventory/Refit affix row | resist downside를 포함한 양쪽 stat |
| `affix_burdened_reach` | `attack_range +0.2` 대신 `move_speed -3%`를 적용한다. `decision-bearing: tradeoff`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_burdened_reach.asset:60`; common compile path above | `partial` | Inventory/Refit affix row | 이동속도 downside와 range 단위 |
| `affix_channeling` | cast-speed scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_channeling.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_cleansing` | cleanse 관련 scalar/tag package를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_cleansing.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | cleanse 대상과 조건 |
| `affix_desperate_focus` | HP 40% 이하 최초 1회 self energy 20을 얻는다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_desperate_focus.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | HP threshold, 1회 latch, energy effect |
| `affix_executioners_edge` | low-HP execute rule tag와 physical-power package를 적용한다. `decision-bearing: rule`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_executioners_edge.asset:30`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:692-715` | `partial` | Inventory/Refit affix row | execute condition과 `RuleModifierTag` |
| `affix_fallen_chorus` | ally death마다 allied combatants를 5 회복한다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_fallen_chorus.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | trigger, target scope, heal amount |
| `affix_farshot` | ranged pressure scalar/tag package를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_farshot.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 condition |
| `affix_fierce` | physical-power scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_fierce.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_first_light` | battle start에 self barrier 4를 얻는다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_first_light.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | battle-start trigger, target, barrier amount |
| `affix_focusing` | magic-power scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_focusing.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_guarded` | block/guard scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_guarded.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_hallowed` | support scalar/tag package를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_hallowed.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 condition |
| `affix_hasty` | attack/cast tempo scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_hasty.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_heavy` | durability scalar/tag package를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_heavy.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_ironclad` | armor scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_ironclad.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_last_ward` | HP 50% 이하 최초 1회 self barrier 6을 얻는다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_last_ward.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | HP threshold, 1회 latch, barrier amount |
| `affix_lightfooted_plate` | `move_speed +4%` 대신 `armor -1`을 적용한다. `decision-bearing: tradeoff`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_lightfooted_plate.asset:60`; common compile path above | `partial` | Inventory/Refit affix row | armor downside를 포함한 양쪽 stat |
| `affix_lithe` | move-speed scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_lithe.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_lucid` | cooldown/tempo scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_lucid.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_mender` | healing scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_mender.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_mourning_aegis` | ally death마다 self barrier 6을 얻는다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_mourning_aegis.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | trigger, target scope, barrier amount |
| `affix_overclocked` | `attack_speed +4%` 대신 `max_health -4%`를 적용한다. `decision-bearing: tradeoff`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_overclocked.asset:60`; common compile path above | `partial` | Inventory/Refit affix row | 생명력 downside를 포함한 양쪽 stat |
| `affix_packborn` | allied/link synergy scalar/tag package를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_packborn.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected rule과 condition |
| `affix_piercing` | pierce scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_piercing.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_precise` | critical scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_precise.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_quick` | tempo scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_quick.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_ravenous` | lifesteal/sustain scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_ravenous.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_reaching` | range scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_reaching.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 distance unit |
| `affix_reaper_spark` | kill마다 self energy 10을 얻는다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_reaper_spark.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | kill trigger, target, energy amount |
| `affix_reckless_edge` | `phys_power +2` 대신 `armor -1`을 적용한다. `decision-bearing: tradeoff`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_reckless_edge.asset:60`; common compile path above | `partial` | Inventory/Refit affix row | armor downside를 포함한 양쪽 stat |
| `affix_relentless` | charge/tempo scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_relentless.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 condition |
| `affix_resolute` | physical-resistance scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_resolute.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_sharp` | physical-power scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_sharp.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_spined` | retaliation/thorn scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_spined.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected rule과 trigger |
| `affix_sturdy` | armor scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_sturdy.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_vital` | max-health scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_vital.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_war_chorus` | battle start에 allied combatants에게 barrier 3을 준다. `decision-bearing: trigger`. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_war_chorus.asset:69`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:699-715` | `partial` | Inventory/Refit affix row | battle-start trigger, allied scope, barrier amount |
| `affix_warded` | magic-resistance scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_warded.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_watchful` | critical/reliability scalar를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_watchful.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected stat과 unit |
| `affix_wraithbound` | damage amplification scalar/tag package를 제공한다. | `Assets/Resources/_Game/Content/Definitions/Affixes/affix_wraithbound.asset:15`; common compile path above | `partial` | Inventory/Refit affix row | affected rule과 condition |

### Affix pool과 item identity

14개 `AffixPoolTag`는 generated item 후보를 실제로 filter한다 (`Assets/_Game/Scripts/Runtime/Meta/Services/GeneratedItemAffixStateGraph.cs:162-183`). 어떤 Inventory/Refit surface도 pool 이름이나 그 pool에서 가능한 affix를 표시하지 않는다.

| id | 하는 일 | runtime evidence | classification | player surface evidence | 빠진 정보 |
| --- | --- | --- | --- | --- | --- |
| `pool_accessory` | accessory item의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_wayfinder_trinket.asset:24`; filter path above | `invisible` | none | - |
| `pool_armor` | generic armor의 affix 후보를 제한한다. | `Assets/_Game/Scripts/Editor/Validation/EquipmentContentV1Contract.cs:59-74`; filter path above | `invisible` | none | - |
| `pool_blade` | blade item의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_reaver_blade.asset:24`; filter path above | `invisible` | none | - |
| `pool_bow` | bow item의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_rift_bow.asset:24`; filter path above | `invisible` | none | - |
| `pool_duelist_armor` | Duelist armor의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_reaver_armor.asset:24`; filter path above | `invisible` | none | - |
| `pool_duelist_trinket` | Duelist trinket의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_reaver_trinket.asset:24`; filter path above | `invisible` | none | - |
| `pool_focus` | focus item의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_shaman_focus.asset:24`; filter path above | `invisible` | none | - |
| `pool_mystic_armor` | Mystic armor의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_shaman_armor.asset:24`; filter path above | `invisible` | none | - |
| `pool_mystic_trinket` | Mystic trinket의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_shaman_trinket.asset:24`; filter path above | `invisible` | none | - |
| `pool_ranger_armor` | Ranger armor의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_scout_armor.asset:24`; filter path above | `invisible` | none | - |
| `pool_ranger_trinket` | Ranger trinket의 affix 후보를 제한한다. | `Assets/_Game/Scripts/Editor/Validation/EquipmentContentV1Contract.cs:59-74`; filter path above | `invisible` | none | - |
| `pool_shield` | shield item의 affix 후보를 제한한다. | `Assets/_Game/Scripts/Editor/Validation/EquipmentContentV1Contract.cs:59-74`; filter path above | `invisible` | none | - |
| `pool_vanguard_armor` | Vanguard armor의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_warden_armor.asset:24`; filter path above | `invisible` | none | - |
| `pool_vanguard_trinket` | Vanguard trinket의 affix 후보를 제한한다. | `Assets/Resources/_Game/Content/Definitions/Items/item_warden_trinket.asset:24`; filter path above | `invisible` | none | - |
| `affix-budget-score-tier` | rarity step의 목표 budget에 가까운 affix를 가중 선택한다. | `Assets/_Game/Scripts/Runtime/Content/Definitions/AffixDefinition.cs:37`; `Assets/_Game/Scripts/Runtime/Meta/Services/GeneratedItemAffixStateGraph.cs:89-120`; `Assets/_Game/Scripts/Runtime/Meta/Services/GeneratedItemAffixStateGraph.cs:234-249` | `invisible` | none | - |
| `item-identity` | item을 `Baseline`, `Named`, `Unique`로 구분하고 badge/규칙을 바꾼다. | `Assets/_Game/Scripts/Runtime/Core/Content/ContentSchema.cs:43`; `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentPresentationPolicy.cs:132-139` | `visible` | Inventory와 Refit가 identity badge와 label을 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryPresenter.cs:350-366`). | - |
| `item-rarity-tier` | rarity에 따라 affix step 수와 item presentation을 바꾼다. | `Assets/_Game/Scripts/Runtime/Meta/Services/GeneratedItemAffixStateGraph.cs:149-159` | `visible` | Inventory가 rarity key, label, frame을 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryView.cs:233-298`). | - |
| `item-granted-skills` | equipped item이 추가 active/passive skill을 loadout에 넣는다. | `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:791-807` | `partial` | 결과 skill은 Character Sheet와 Battle에 나타난다. | 어느 item이 skill을 grant했는지 Inventory/Refit가 밝히지 않고, skill surface도 source를 표시하지 않는다. |

### Combat status, synergy, formation, threat

11개 status behavior kind는 authored family에서 runtime set/channel로 compile되어 action, movement, target, damage, healing을 실제로 바꾼다 (`Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:26-56`, `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:101-110`, `Assets/_Game/Scripts/Runtime/Combat/Services/StatusResolutionService.cs:92-177`). Battle read model은 status id 목록만 운반하고, formatter는 icon 없이 duration `0`, stack `1`, generic description, 실제 cleanse와 무관한 `Cleanse profile pending`을 만든다 (`Assets/_Game/Scripts/Runtime/Combat/Model/BattleSimulationStep.cs:7-28`, `Assets/_Game/Scripts/Runtime/Combat/Services/BattleReadModelBuilder.cs:34-55`, `Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitMetadataFormatter.cs:365-389`). 따라서 behavior kind 11개는 모두 `partial`이다.

| id | 하는 일 | runtime evidence | classification | player surface evidence | 빠진 정보 또는 `n/a` 근거 |
| --- | --- | --- | --- | --- | --- |
| `status-kind-grants-barrier-on-apply` | status 적용 시 barrier로 즉시 전환한다. | `Assets/_Game/Scripts/Runtime/Combat/Services/StatusResolutionService.cs:172-177` | `partial` | Battle은 현재 barrier 수치를 별도 chip으로 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitMetadataFormatter.cs:416-424`). | 어떤 status가 얼마의 barrier를 만들었는지와 지속 규칙 |
| `status-kind-grants-unstoppable` | control cleanse 뒤 unstoppable을 부여해 control 재적용을 막는다. | `Assets/_Game/Scripts/Runtime/Combat/Services/StatusResolutionService.cs:209-228` | `partial` | Battle status chip | control 면역 의미, duration, source |
| `status-kind-blocks-active-skills` | silence family가 active skill 사용을 막는다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:101-103`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:252` | `partial` | Battle status chip | active만 차단한다는 행동 의미와 남은 duration |
| `status-kind-blocks-movement` | root family가 자발 이동을 막는다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:103`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:251` | `partial` | Battle status chip | 이동 차단 의미와 남은 duration |
| `status-kind-blocks-action` | stun family가 action을 막는다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:104`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:250` | `partial` | Battle status chip | 행동 차단 의미와 남은 duration |
| `status-kind-amplifies-incoming-damage` | marked/exposed가 받는 피해를 증가시킨다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:106`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:258-259` | `partial` | Battle status chip | 실제 증가율, stack, duration |
| `status-kind-grants-guarded-defense` | guarded가 받는 피해를 줄인다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:107`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:261` | `partial` | Battle status chip | 실제 감소율과 duration |
| `status-kind-shreds-defense` | sunder magnitude와 stack으로 방어를 깎는다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:108`; `Assets/_Game/Scripts/Runtime/Combat/Model/UnitSnapshot.cs:200` | `partial` | Battle status chip | 방어 감소량, stack, duration |
| `status-kind-reduces-healing` | wound가 받는 healing을 감소시킨다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:109`; `Assets/_Game/Scripts/Runtime/Combat/Model/UnitSnapshot.cs:949-953` | `partial` | Battle status chip | healing 감소율, stack, duration |
| `status-kind-dampens-tempo` | slow가 attack/move tempo를 감소시킨다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:110`; `Assets/_Game/Scripts/Runtime/Combat/Model/UnitSnapshot.cs:955-959` | `partial` | Battle status chip | 어느 tempo channel이 얼마나 감소하는지 |
| `status-kind-marks-target` | marked target에 target-score preference를 준다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:105`; `Assets/_Game/Scripts/Runtime/Combat/Services/TargetScoringService.cs:447-459` | `partial` | Battle status chip | mark가 타게팅에 미치는 우선순위와 damage channel |
| `status-magnitude-channels` | `magnitude × stack × family scale`로 damage, defense, healing, tempo 값을 계산한다. | `Assets/_Game/Scripts/Runtime/Combat/Model/UnitSnapshot.cs:949-979` | `invisible` | none | - |
| `periodic-status-damage` | burn/bleed가 periodic damage를 적용한다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:177-178`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:254-255` | `partial` | Battle status chip에 burn/bleed label은 나타난다. | tick damage, interval, remaining duration, stack |
| `status-cleanse-profile` | cleanse profile별 제거 대상과 unstoppable grant를 결정한다. | `Assets/_Game/Scripts/Runtime/Combat/Services/StatusResolutionService.cs:190-228`; `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:267-269` | `partial` | Battle chip이 모든 status에 `Cleanse profile pending`을 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitMetadataFormatter.cs:376-389`). | 실제 cleanse 가능 여부와 profile을 표시하지 않아 현재 문구가 misleading하다. |
| `control-diminishing-window` | 반복 control의 duration에 resistance window와 multiplier를 적용한다. | `Assets/_Game/Scripts/Runtime/Combat/Model/CombatStatusRules.cs:75-88`; `Assets/_Game/Scripts/Runtime/Combat/Services/StatusResolutionService.cs:32-53` | `invisible` | none | - |
| `synergy-tiers` | deployed tags의 count가 threshold를 넘으면 minor/major synergy package를 활성화한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/SquadSynergyPreview.cs:20-87` | `partial` | Squad Builder, Tactical Workshop, Sortie Confirm이 count/threshold와 active state를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/SquadBuilderPresenter.cs:322-340`, `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/TacticalWorkshopPresenter.cs:221-240`). | 활성화되는 stat/rule 효과 |
| `formation-anchor-and-posture` | hero anchor와 team posture가 deployment와 combat behavior를 바꾼다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Town/SquadBuilderPresenter.cs:116-226` | `visible` | Squad Builder가 anchor assignment와 5 posture를 편집하고 Tactical Workshop이 결과를 확인한다. | - |
| `tactic-condition-and-target-directive` | per-hero target directive와 tactic profile이 target selection을 바꾼다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/TacticalWorkshopPresenter.cs:295-390` | `visible` | Tactical Workshop이 role, selector/fallback, target directive를 표시하고 cycle한다. | - |
| `enemy-threat-patterns` | 8개 counter-threat lane에 대한 현재 squad의 answer strength를 계산한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/SquadCounterCoveragePreview.cs:8-75` | `visible` | Tactical Workshop이 `answered`, `partial`, `unanswered`로 8 lane을 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/TacticalWorkshopPresenter.cs:246-294`). | - |
| `aggro-radius` | attack range와 `StatKey.AggroRadius` 중 큰 값을 계산한다. | `Assets/_Game/Scripts/Runtime/Combat/Model/UnitSnapshot.cs:219` | `n/a` | none | production code가 `AggroRadius` property를 읽지 않는다. live decision mechanic이 되기 전 field-reachability 정리 대상이다. |
| `battle-target-selection` | selector, fallback, screen/guard/mark bias와 switch penalty로 공격 대상을 고른다. | `Assets/_Game/Scripts/Runtime/Combat/Services/TargetScoringService.cs:429-478` | `partial` | Battle detail이 current target, selector/fallback, retarget lock을 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitMetadataFormatter.cs:239-249`). | score reason, screen/guard penalty, marked bias 때문에 왜 그 대상을 골랐는지는 알 수 없다. |

### Meta와 progression

| id | 하는 일 | runtime evidence | classification | player surface evidence | 빠진 정보 |
| --- | --- | --- | --- | --- | --- |
| `passive-active-node-budget` | hero level에 따라 active node budget을 5에서 8까지 늘리고 초과 선택을 거부한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/PassiveBoardSelectionValidator.cs:23-45` | `invisible` | none | - |
| `passive-prerequisites` | prerequisite node가 없으면 선택을 거부한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/PassiveBoardSelectionValidator.cs:127-134` | `invisible` | none | - |
| `passive-mutual-exclusion` | mutually exclusive node 동시 선택을 거부한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/PassiveBoardSelectionValidator.cs:154-161` | `invisible` | none | - |
| `passive-keystone-cap` | active keystone을 1개로 제한한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/PassiveBoardSelectionValidator.cs:142-150` | `invisible` | none | - |
| `endless-heat-enemy-scaling` | Heat마다 enemy max health `+10%`, primary power `+6%`를 누적한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/EndlessCycleService.cs:18-29`; `Assets/_Game/Scripts/Runtime/Meta/Services/EndlessCycleService.cs:80-97` | `partial` | Town tooltip이 Heat와 적이 강해진다는 일반 문구를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownScreenPresenter.cs:429-436`). | 정확한 HP/power 비율과 현재 누적값 |
| `endless-echo-bonus` | Heat마다 Echo reward를 `+15%` 증가시킨다. | `Assets/_Game/Scripts/Runtime/Meta/Services/EndlessCycleService.cs:39-40`; `Assets/_Game/Scripts/Runtime/Meta/Services/EndlessCycleService.cs:217-225` | `partial` | Town tooltip이 Echo가 커진다는 일반 문구를 표시한다. | 정확한 per-Heat 비율과 현재 bonus |
| `drop-grade-economy` | rarity weight, latent mean, jackpot weight, Heat shift로 drop grade를 결정한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/DropGradeEconomy.cs:26-140`; `Assets/_Game/Scripts/Runtime/Meta/Services/EndlessCycleService.cs:43-58` | `partial` | 획득 뒤 item rarity는 Inventory와 Reward에서 보인다. | 선택 전 grade distribution, jackpot, Heat shift |
| `currency-gold` | recruit와 service cost에 쓰이고 reward로 획득한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs:866-874` | `visible` | Inventory, Recruit, Reward가 balance, cost, gain을 숫자로 표시한다. | - |
| `currency-echo` | refit, scout, retrain, recovery에 쓰이고 reward로 획득한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs:866-884` | `visible` | Inventory, Refit, Character Sheet, Reward가 balance, cost, gain을 숫자로 표시한다. | - |
| `site-event-choice-outcomes` | event node가 선택지별 recruit, consumable/item, Gold 결과를 pending state로 만들고 적용한다. | `Assets/_Game/Scripts/Runtime/Unity/Session/SiteEventSessionController.cs:42-105`; `Assets/Resources/_Game/Content/Definitions/SiteEvents/site_event_collapsed_aid_station.asset:18-50` | `visible` | Atlas가 pending state를 `SiteEventChoice` panel로 열고, 선택마다 저작 icon identity, 결과 범주, 상대 강도 pip, 비용 여부, 대상 변동성을 표시한 뒤 실제 `ApplyChoice` 경로로 적용한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenController.cs:664-695`, `Assets/_Game/UI/Panels/SiteEventChoice/SiteEventChoicePanelController.cs:53-224`). | - |
| `war-wound` | low-HP deployed hero에게 run-scoped wound를 부여하고 active skill power와 status duration을 낮춘다. | `Assets/_Game/Scripts/Runtime/Meta/Services/WarWoundResolutionService.cs:14-75`; `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:629-668` | `invisible` | none | - |
| `reward-choice-amount-and-type` | reward choice의 type과 Gold/Echo/item/augment payload를 적용한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs:751-763` | `visible` | Reward cards가 type, amount, build impact를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs:866-887`). | - |
| `skill-readout` | skill damage, type, delivery, target, range, cooldown과 effect를 설명한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/CompendiumPresenter.cs:241-257` | `visible` | Skill Compendium과 Character Sheet가 skill detail을 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/CompendiumPresenter.cs:399-405`, `Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownCharacterSheetFormatter.cs:603-606`). | - |
| `permanent-augment-effect-preview` | equipped permanent augment의 numeric, rule, triggered package를 combat loadout에 compile한다. | `Assets/_Game/Scripts/Runtime/Meta/Services/LoadoutCompiler.cs:191-205` | `partial` | Permanent Augment panel은 선택/equip 상태와 4개 hard-coded signature 문구를 보여 준다. | presenter가 authored `AugmentDefinition`을 읽지 않는 scaffold라고 명시하며 실제 effect와 source를 보장하지 않는다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/PermanentAugmentPresenter.cs:126-136`). |
| `atlas-route-risk-and-reward` | selected route의 node kind, reward, threat, modifiers를 preview한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/AtlasScreenPresenter.cs:287-326` | `visible` | Atlas preview와 modifier chips가 위험·보상을 표시한다. | - |
| `warrant-selection` | faction standing에 따라 warrant combat modifier를 만들고 pledge/skip한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Atlas/WarrantSelectionPresenter.cs:73-147` | `visible` | Warrant overlay가 issuer, opposed faction, kind, stat delta를 표시한다. | - |
| `progression-level-and-experience` | hero level/experience가 passive budget과 성장 상태를 바꾼다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Town/TownCharacterSheetFormatter.cs:330-365` | `visible` | Character Sheet와 Reward settlement가 level과 experience progression을 표시한다. | - |
| `temporary-augment-choice` | reward로 temporary augment를 고르고 current run build에 적용한다. | `Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs:751-763` | `visible` | Reward cards가 augment name, effect/build impact, selection state를 표시한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs:998-1045`). | - |

Passive Board는 gap을 단순히 생략하는 데 그치지 않는다. view-state 주석이 point budget이 없다고 적고, presenter action도 validator 실패 결과를 player에게 전달하지 않는다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/PassiveBoardViewState.cs:7-10`, `Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/PassiveBoardPresenter.cs:81-88`).

Site event closure는 6개 authored event의 17개 선택을 모두 검사한다. `SiteEventOutcomePreviewBuilder`는 여러 outcome을 합치지 않고 순서대로 보존하며, 정확한 수치를 노출하지 않는 범주와 0~5단계 강도, 비용 여부, `TargetVaries` 또는 `Unknown` 불확실성을 만든다 (`Assets/_Game/Scripts/Runtime/Unity/SiteEventOutcomePreviewBuilder.cs:13-81`). BatchOnly witness는 6개 event가 한국어와 영어에서 raw key, 비어 있는 제목, 비어 있는 preview 없이 production UXML/controller로 렌더되는지 확인한다. PlayMode witness는 실제 Atlas pending route에서 panel을 보고 `burn_it`을 클릭한 뒤 `SiteEventOutcomeApplier`를 거쳐 pending state가 사라지고 Reward로 이동하는지 확인한다 (`Assets/Tests/EditMode/BatchOnly/SiteEventChoiceSurfaceWitnessTests.cs`, `Assets/Tests/PlayMode/UxBiblePlayModeWitnessTests.SiteEventChoice.cs`).

canonical `ui_ux_bible_dialogue_event_choice_v0.png` 대비 구현한 핵심은 우측 세로 choice card, 번호 diamond, authored choice icon slot, 선택 gold state와 chevron, 정성적 outcome badge와 pip, 좌하단 dialogue block이다. portrait/frame/crest/nameplate는 대응하는 event portrait·speaker 저작 계약이 없어서 넣지 않았고, transport history/play/fast-forward/dots도 실제 transport state와 동작이 없어 넣지 않았다. 17개 choice icon은 identity와 전용 resolver 경로까지만 저작했으며, 최종 PNG가 없는 사실을 localized placeholder와 `known-missing-art.tsv`로 정직하게 드러낸다.

## Ranked gaps

순위는 구현 난이도가 아니라 player decision과 진행을 망가뜨리는 정도다.

| rank | mechanic | 플레이어가 답할 수 없는 질문 | decision impact | proposed host | 기존/신규 | effort |
| ---: | --- | --- | --- | --- | --- | --- |
| 1 | `war-wound` | "누가 전상을 입었고 왜 그 hero의 skill이 약해졌나?" | 전투 성능 저하가 source 없이 발생해 squad/recovery 선택을 설명할 수 없다. | Reward settlement, Town Character Sheet, squad/roster badge | existing surfaces | M |
| 2 | passive budget와 constraint 4종 | "몇 node를 더 켤 수 있고 왜 이 node 선택이 거부됐나?" | level progression의 핵심 build budget을 보지 못하고 실패 이유도 받지 못한다. | `PassiveBoard` header, node lock reason, footer | existing | M |
| 3 | affix semantics 44종, 특히 decision-bearing 14종 | "이 affix가 어느 stat을 얼마나 바꾸며 downside나 trigger가 무엇인가?" | item/refit 비교가 이름과 무맥락 roll 숫자에 의존한다. | Inventory/Refit affix row와 기존 `ItemDetailModal` | existing panel plus dormant detail shell | L |
| 4 | status behavior, magnitude, cleanse, control DR | "왜 행동하지 못하고, 얼마나 더 받거나 덜 받으며, 무엇으로 지울 수 있나?" | battle 결과의 원인을 설명하지 못하고 cleanse tooltip은 현재 misleading하다. | Battle selected-unit status area와 기존 `StatusEffectTooltipPanel` | existing panel plus dormant tooltip shell | L |
| 5 | affix pool 14종과 `BudgetScore` | "이 item을 다시 굴리면 어떤 affix가 나올 수 있고 어떤 roll이 유리한가?" | crafting 확률과 item family 차이를 비교할 수 없다. | `EquipmentRefit` possible-rolls/odds disclosure | existing | M |
| 6 | `item-granted-skills` | "이 skill은 어느 item이 주며, item을 빼면 무엇을 잃나?" | loadout change의 skill 손익을 item 화면에서 예측할 수 없다. | Inventory/Refit item detail과 Character Sheet skill source chip | existing | S-M |
| 7 | endless Heat와 drop-grade exact values | "Heat 3이 적, Echo, rarity chance를 정확히 얼마나 바꾸나?" | endless risk/reward 선택이 vague prose에 의존한다. | Town endless CTA tooltip, Atlas preview, Reward settlement | existing | S-M |
| 8 | `synergy-tiers` effect | "이 threshold를 넘기면 실제로 무엇이 좋아지나?" | count를 맞출 유인은 보이지만 build payoff는 비교할 수 없다. | Squad Builder, Tactical Workshop, Sortie Confirm chip detail | existing | S |
| 10 | target score와 `MarksTarget` | "왜 이 unit이 표시된 적 대신 다른 적을 공격했나?" | tactic/mark 결과를 진단하기 어렵다. | Battle selected-unit targeting detail | existing | M |

## Image asset audit

### 방법과 한계

`Assets` 아래 `ThirdParty` 경로를 제외한 PNG는 4,475개다. missing/broken 판정은 `ContentIconResolver`의 실제 search order인 Skill, Item, Augment, Affix, Character, SiteEventChoice, Direct와 Foundation USS keyspace를 함께 따라 확인했다 (`Assets/_Game/Scripts/Runtime/Unity/UI/ContentIconResolver.cs:10-14`, `Assets/_Game/Scripts/Runtime/Unity/UI/ContentIconResolver.cs:86-91`, `Assets/_Game/Scripts/Runtime/Unity/UI/ContentIconResolver.cs:143-148`). authored `IconId`는 `tools/icon-routing/lint.ps1`이 실제 PNG 또는 `known-missing-art.tsv`의 명시적 missing 선언과 대조하며, 이 검사는 `tools/test-harness-lint.ps1` Check 9에 포함된다.

unreferenced 후보는 20,896개 first-party text/serialized file에서 PNG filename/stem token 445,832개와 non-self `.meta` GUID reference 17,183개를 대조해 다시 계산했다. PNG 자신의 `.meta`는 reference로 세지 않았다. 이 방식은 dynamic `Resources.Load`, convention-based lookup, package runtime lookup을 완전히 증명하지 못하므로 삭제 목록이 아니라 cleanup 조사 후보 목록이다.

### 확정 라우팅 감사

최초 감사의 silent routing defect 19개는 모두 기존 Foundation/Resources art로 연결했다. 당시 live authored reference의 missing art 18개와 이번 site-event choice icon 17개는 resolver가 추측하지 않고 authored data와 명시적 missing 선언으로 드러나며, 현재 lint 집계는 resolved 201개, declared missing 35개, authored reference 236개다.

| keyspace | 요청/해석 경로 | content key | 현재 resolve | 판정 |
| --- | --- | ---: | ---: | --- |
| Skill | `SkillDefinition.IconId` -> Skill PNG -> normalized fallback | 97 | 93 | art missing 4; 모두 명시 선언 |
| Item | `ItemBaseDefinition.IconId` -> Item PNG -> category fallback | 42 | 42 | 6개 family icon의 의도적 공유 |
| Augment | `AugmentDefinition.IconId` -> Augment PNG | 36 | 36 | 12개 family icon의 의도적 공유 |
| Affix | `AffixDefinition.IconId` -> Affix PNG -> migration-only legacy map -> convention | 44 | 30 | art missing 14; 모두 명시 선언 |
| Character | character alias -> subject subdirectory -> portrait/standee convention | 16 + Town NPC 4 | 16 + 4 | subject별 convention route |
| Currency | Inventory/Refit modifier class -> Foundation Currency PNG | 2 | 2 | routing broken 2개 수정 |
| Class | Recruit/Passive/Tactical modifier class -> Foundation Class PNG | 4 | 4 | routing broken 4개 수정 |
| Posture | Tactical key class -> Foundation Posture PNG | 5 | 5 | routing broken 5개 수정 |
| Threat | Tactical key class -> Foundation Threat PNG | 8 | 8 | routing broken 8개 수정; `pierce`의 잘못된 affix 우선 해소 |
| Site event choice | `SiteEventChoiceDefinition.IconId` -> SiteEventChoice PNG | 17 | 0 | art missing 17; 모두 선택별 identity와 expected path를 명시 선언 |
| Status | runtime status family -> 예정된 Battle chip/tooltip key | 13 | 0 | 아직 authored icon contract가 없는 art gap |
| Craft operation | authored `AllowedCraftOperations` -> Refit text selector | 2 live (5 enum) | 0 | Reforge/Seal은 text-only control로 노출하고 dedicated icon은 별도 art gap으로 유지한다. |
| Affix pool | pool id -> 예정된 Refit possible-roll legend key | 14 | 0 | 아직 authored icon contract가 없는 art gap |
| Passive node | node definition -> Passive Board presenter | 96 | 0 | schema에 `IconId`가 없고 presenter가 `null`을 반환 |

### Needed but missing

| needed id | mechanic | 나타날 위치 | 상태 |
| --- | --- | --- | --- |
| `site_event_choice_icon_set_v1` | 6개 event의 17개 choice identity | Atlas Site Event Choice card | `SiteEventChoiceDefinition.IconId`와 전용 resolver route는 연결됨. 17개 expected PNG는 `known-missing-art.tsv`에 개별 선언했으며 UI는 localized `Icon pending`을 표시한다. |
| `craft_operation_reforge` | `craft-operation-reforge` | Equipment Refit operation selector | 의도적으로 text-only control을 사용한다. dedicated icon은 없지만 현재 action은 usable하다. |
| `craft_operation_seal` | `craft-operation-seal` | Equipment Refit operation selector | 의도적으로 text-only control을 사용한다. dedicated icon은 없으며 `augment_seal.png`은 다른 mechanic이라 대체 사용하지 않는다. |
| `status_barrier` | barrier on apply | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_unstoppable` | unstoppable | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_silence` | blocks active skills | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_root` | blocks movement | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_stun` | blocks action | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_exposed` | incoming damage amplification | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_marked` | damage amplification and target mark | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_guarded` | guarded defense | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_sunder` | defense shred | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_wound` | healing reduction | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_slow` | tempo reduction | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_burn` | periodic damage | Battle status tooltip/chip | dedicated status icon 없음. |
| `status_bleed` | periodic damage | Battle status tooltip/chip | dedicated status icon 없음. |
| `affix_pool_accessory` | `pool_accessory` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_armor` | `pool_armor` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_blade` | `pool_blade` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_bow` | `pool_bow` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_duelist_armor` | `pool_duelist_armor` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_duelist_trinket` | `pool_duelist_trinket` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_focus` | `pool_focus` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_mystic_armor` | `pool_mystic_armor` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_mystic_trinket` | `pool_mystic_trinket` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_ranger_armor` | `pool_ranger_armor` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_ranger_trinket` | `pool_ranger_trinket` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_shield` | `pool_shield` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_vanguard_armor` | `pool_vanguard_armor` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_pool_vanguard_trinket` | `pool_vanguard_trinket` | Equipment Refit possible-roll legend | pool-specific icon 없음. |
| `affix_blood_price` | tradeoff affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_brittle_focus` | tradeoff affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_burdened_reach` | tradeoff affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_desperate_focus` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_executioners_edge` | rule-tag affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_fallen_chorus` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_first_light` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_last_ward` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_lightfooted_plate` | tradeoff affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_mourning_aegis` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_overclocked` | tradeoff affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_reaper_spark` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_reckless_edge` | tradeoff affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `affix_war_chorus` | triggered affix | Inventory/Refit affix row | resolver target PNG 없음. |
| `skill_icon_last_bastion` | `skill_last_bastion` | Compendium, Character Sheet, Battle skill row | authored `IconId`에 대응하는 PNG 없음. |
| `skill_icon_sunder_rhythm` | `skill_sunder_rhythm` | Compendium, Character Sheet, Battle skill row | authored `IconId`에 대응하는 PNG 없음. |
| `skill_icon_sunken_anticluster_bombardment` | `skill_sunken_anticluster_bombardment` | Compendium, Character Sheet, Battle skill row | authored `IconId`에 대응하는 PNG 없음. |
| `skill_icon_veil_breach` | `skill_veil_breach` | Compendium, Character Sheet, Battle skill row | authored `IconId`에 대응하는 PNG 없음. |
| `passive_node_icon_set_v1` | 96 passive nodes | Passive Board node와 detail icon | schema에 `IconId`가 없고 presenter가 96개 node 모두 `IconSprite: null`을 반환한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/PassiveBoardPresenter.cs:168-169`). |

### Broken or fallback-resolved references

모든 affix asset 44개에 `IconId`를 저작했다. resolver는 authored field를 우선하고, 기존 24개 동작의 byte-identical 경로를 보존하는 migration-only dictionary를 거친 뒤 convention을 사용한다 (`Assets/_Game/Scripts/Runtime/Unity/UI/ContentIconResolver.cs:131-139`, `Assets/_Game/Scripts/Runtime/Unity/UI/ContentIconResolver.cs:246-258`). 기존 24개 외에도 scalar 의미가 정확히 맞는 existing art 6개를 재사용했고, decision-bearing 14개는 misleading한 stat icon을 붙이지 않고 art missing으로 선언했다.

Skill의 missing 4개, affix의 missing 14개, site-event choice의 missing 17개는 `tools/icon-routing/known-missing-art.tsv`에 exact content id, icon key, expected path, 사유를 기록했다. 이 35개 외에는 authored icon reference가 silent `null`로 남지 않는다. Currency 2개와 Class 4개는 Foundation USS modifier class로 연결했고, Posture 5개와 Threat 8개는 generic resolver callback을 제거해 이미 존재하던 정확한 Foundation key route가 항상 이기게 했다 (`Assets/_Game/UI/Panels/PassiveBoard/PassiveBoard.uss:271-274`, `Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uss:312-316`, `Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uss:438-445`).

### Unreferenced candidates

- 후보 수: **1,191**
- 해석: first-party token/GUID scan에서 reference가 발견되지 않은 PNG 수다. dynamic lookup 가능성 때문에 삭제 근거로 사용하지 않는다.
- 이전 1,193 수치는 같은 audit의 초기 근사치였고, 이번 20,896-file 재계산으로 1,191로 교정했다. `affix_lifesteal.png`은 `affix_ravenous`의 authored `IconId`로 새로 live reference가 되었으며 candidate에서 빠졌다. 나머지 차이는 초기 근사 scan과 최종 token/GUID scan의 집계 방법 차이이므로 특정 파일 삭제나 신규 reference로 추정하지 않는다.
- sample:
  - `Assets/Resources/_Game/Art/Icons/Affix/affix_aura.png`
  - `Assets/Resources/_Game/Art/Icons/Affix/affix_dodge.png`
  - `Assets/Resources/_Game/Art/Icons/Affix/affix_mana.png`
  - `Assets/Resources/_Game/Art/Icons/Affix/affix_revive.png`
  - `Assets/Resources/_Game/Art/Icons/Affix/affix_taunt.png`
  - `Assets/Resources/_Game/Art/Icons/Affix/affix_thorn.png`
  - `Assets/Resources/_Game/Art/Icons/Augment/augment_flame.png`
  - `Assets/_Game/UI/Backdrops/atlas_wolfpine_overworld.png`
  - `Assets/Epic Toon FX/Textures/circle_blurred.png`

## Incidental quality notes

이 목록은 generic UI audit backlog가 아니며 이번 unit에서 수정하지 않는다.

- `Screens/Town/Preview/**`와 production `Panels/**`가 함께 남아 있어 static inventory에서 production 여부를 이름만으로 판단하기 어렵다.
- `Foundation/Details`의 세 detail shell은 존재하지만 production host가 clone하지 않는다. mechanic gap을 메울 때 새 modal을 만들기 전에 이 shell을 검토할 가치가 있다.
- Town의 Settings/Theater entry가 opener 부재 시 숨겨지는 동작은 dead affordance를 노출하지 않는 올바른 fallback이다.

## 후속 구현 권고

`craft-operation-seal`과 lock 수별 exact cost는 기존 `EquipmentRefit` route의 operation selector, affix lock, service quote, 실행 불가 이유, 확인 단계와 production UXML binding witness로 닫혔다. 다음 구현 unit은 War Wound와 Passive Board constraint처럼 player가 현재 결과 원인을 전혀 알 수 없는 gap을 순서대로 닫는 것이 맞다.

향후 lint는 이 문서의 110개 `id`를 unique key로 읽고, `classification`이 허용 enum인지 검사한 뒤 `invisible` 또는 `partial` 행에 runtime evidence와 gap text가 남아 있는지 확인할 수 있다. 새 mechanic을 추가할 때는 runtime field catalog와 이 catalog를 같은 change에서 갱신한다.
