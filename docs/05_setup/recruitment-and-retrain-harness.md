# Recruitment And Retrain Harness

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/05_setup/recruitment-and-retrain-harness.md`
- 관련문서:
  - `docs/02_design/meta/recruitment-contract.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/02_design/meta/duplicate-handling-contract.md`
  - `tasks/010_loop_b_recruitment_economy_closure/status.md`

## 목적

Loop B recruit/retrain/duplicate/dismiss 흐름을 Editor에서 재현하고 deterministic oracle과 연결한다.

## 진입점

- 메뉴: `SM/Authoring/Recruitment Sandbox`
- 런타임 세션: `GameSessionState` + `RuntimeCombatContentLookup`
- low-level preview: `RecruitPackGenerator`, `TeamPlanEvaluator`, `RecruitCandidateScoringService`

## Harness 1. Recruit pack inspection

지원 항목:

- roster archetype CSV
- temporary/permanent augment CSV
- scout directive
- rare/epic pity counter
- seed
- `Generate Pack`, `Refresh Preview Pack`, `Reset Preview State`

표시 항목:

- slot type
- `planFit`
- `planScore` breakdown
- tier
- pity/scout flag
- gold cost
- current pity/phase 상태

## Harness 2. Retrain sandbox

지원 항목:

- hero 선택
- `RerollFlexActive`, `RerollFlexPassive`, `FullRetrain`
- current/previous memory 확인
- next Echo cost 확인

표시 항목:

- 현재 flex active/passive
- previous result memory
- retrain count / pity counter
- native coherence / plan coherence 판정

## Harness 3. Duplicate conversion sandbox

지원 항목:

- owned blueprint 선택
- `Grant Duplicate`

표시 항목:

- roster count 전후
- Echo 전후
- `DuplicateConversionResult`

## Harness 4. Dismiss refund sandbox

지원 항목:

- hero 선택
- footprint 확인
- `Dismiss Selected Hero`

표시 항목:

- recruit gold / retrain echo footprint
- 실제 환급 Gold/Echo
- equipped item release 수

## Deterministic test oracle

- editmode `LoopBContractClosureTests`는 `1000` pack, `500` retrain, `100` duplicate grant 시뮬레이션을 고정 seed로 검증한다.
- playmode smoke는 Town/Expedition/Reward 흐름이 Loop B wording과 4-card recruit 구조를 깨지 않는지 검증한다.

## 운영 순서

1. `pwsh -File tools/unity-bridge.ps1 compile`
2. `pwsh -File tools/unity-bridge.ps1 test-edit`
3. `pwsh -File tools/unity-bridge.ps1 test-play`
4. 필요 시 sandbox 창에서 pack/retrain/duplicate/dismiss를 수동 재현

## 예시

### 예시 1. Rare pity 확인

- preview pity를 `Rare=3`, `Epic=0`으로 두고 `Generate Pack`
- `Protected` 슬롯이 최소 `Rare`인지 확인

### 예시 2. recruit -> retrain -> dismiss

- runtime session에서 recruit card를 하나 영입
- retrain sandbox에서 `FullRetrain`
- dismiss sandbox에서 같은 hero를 dismiss
- Gold/Echo 환급과 장비 회수 여부를 확인
