# First Playable Balance Targets

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/03_architecture/first-playable-balance-targets.md`
- 관련문서:
  - `docs/02_design/systems/first-playable-slice.md`
  - `docs/03_architecture/readability-gate-contract.md`
  - `docs/03_architecture/sim-sweep-and-balance-kpis.md`

## 목적

Loop D first playable balance pass를 `PureKit / SystemicSlice / RunLite` 3-stage deterministic
suite로 고정한다.

## Stage 1 — PureKit

- 목적: kit baseline, cadence, role identity 확인
- 설정:
  - affix off
  - augment off
  - retrain off
  - synergy baseline-only
- plan:
  - 12 scenario
  - `32 seeds`
  - mirrored
  - randomized start lanes
- gate:
  - `battleDurationP50 = 18 ~ 30`
  - `battleDurationP90 <= 38`
  - `timeoutRate <= 0.02`
  - `timeToFirstDamageP50 <= 1.75`
  - `timeToFirstMajorActionP50 = 1.25 ~ 6.0`
  - `deadBeforeFirstMajorActionRate <= 0.15`
  - `topDamageShareP90 <= 0.65`

## Stage 2 — SystemicSlice

- 목적: affix/synergy/augment/retrain 상호작용 검증
- 설정:
  - first playable slice 내부 content만 허용
  - legal build preset만 사용
  - affix는 unit당 최대 2개
  - augment는 run당 2개 preset부터 시작
- plan:
  - 8 scenario
  - `40 seeds`
  - mirrored
  - randomized start lanes
- gate:
  - synergy/augment `presenceWinDelta = -0.07 ~ +0.07`
  - affix/flex `presenceWinDelta = -0.08 ~ +0.08`
  - slot competition `pickRate > 0.55`면 watch
  - combo `presenceWinDelta > +0.10`이면 broken
  - readability gate는 완화하지 않는다

## Stage 3 — RunLite

- 목적: recruit/scout/retrain/duplicate conversion의 선택 감각 검증
- flow:
  - start roster 3
  - recruit -> battle -> reward
  - recruit -> battle -> reward
  - recruit -> elite battle -> end
- plan:
  - `48 seeds`
- gate:
  - `noAffordableOptionRate == 0`
  - `meaningfulChoicePhaseRate >= 0.90`
  - `echoSpendRatio = 0.35 ~ 0.75`
  - `onPlanPurchaseShare = 0.20 ~ 0.55`
  - `protectedPurchaseShare = 0.10 ~ 0.35`
  - `retrainUseRate = 0.15 ~ 0.45`
  - `scoutUseRate = 0.20 ~ 0.60`
