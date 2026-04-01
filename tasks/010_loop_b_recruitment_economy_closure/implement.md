# 구현 기록

## 메타데이터

- 작업명: Loop B Recruitment / Retrain / Economy Closure
- 담당: Codex
- 상태: 완료
- 최종수정일: 2026-04-01

## Phase log

### Phase 1. Contract와 runtime 도입

- recruit/retrain/economy seam enum과 DTO를 전용 파일로 분리했다.
- `UnitArchetypeDefinition`, `HeroInstanceRecord`, `ActiveRunRecord`, `CurrencyRecord`에 Loop B state를 연결했다.
- `RecruitmentTemplateResolver`, `TeamPlanEvaluator`, `RecruitCandidateScoringService`, `RecruitPackGenerator`, `RecruitPreviewBuilder`, `RefreshCostService`, `RetrainService`, `DuplicateResolver`, `DismissService`를 추가했다.
- `GameSessionState`는 Town command orchestration과 save sync 역할로 정리했다.

### Phase 2. UI와 sample content 정렬

- Town recruit UI를 4-card 구조와 wallet/pity/scout/flex preview 표시로 맞췄다.
- Reward/Expedition wording을 Echo 기준으로 정렬했다.
- bootstrap/runtime binder를 4 recruit button 구조로 갱신했다.
- canonical sample content drift를 허용하는 fallback과 recruit metadata fallback을 runtime/content lookup에 추가했다.

### Phase 3. Validator, test, harness

- `LoopBContractValidator`를 `ContentDefinitionValidator` 뒤에 연결했다.
- `LoopBContractClosureTests`로 pack simulation, retrain pity, duplicate conversion, Town phase guard를 고정했다.
- editor-side `Recruitment Sandbox` window를 추가해 pack inspection, retrain, duplicate, dismiss 흐름을 수동 재현 가능하게 했다.

## deviation

- 별도 `UnitBlueprintDefinition` asset은 만들지 않고 기존 `UnitArchetypeDefinition.Id`를 blueprint identity로 유지했다.
- 기존 craft 통화는 삭제하지 않았고, Loop B active economy에서만 제외했다.

## blockers

- 신규 테스트 추가 직후 `TraitPoolDefinition`과 `SerializableStatModifier` 타입 불일치로 compile이 한 번 깨졌다.
- Unity test runner가 직전 test assembly를 잡아 이전 실패 메시지를 재사용해 보여서, 강제 compile 후 재실행으로 정리했다.

## diagnostics

- compile failure는 `LoopBContractClosureTests` 보조 자산 정의 mismatch였다.
- refresh가 항상 다른 blueprint 집합을 보장하지 않으므로, `GameSessionStateTests`는 비용/phase progression oracle로 수정했다.

## why this loop happened

- 기존 vertical slice는 recruit/reroll이 있었지만 `3-card + trait reroll token` 시절 계약이 남아 있었다.
- Loop A 이후 flex-only identity contract를 meta layer까지 닫으려면 recruit/retrain/economy recovery seam을 별도로 확정해야 했다.
