# Pruning Playbook

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-02
- 소스오브트루스: `docs/03_architecture/pruning-playbook.md`
- 관련문서:
  - `docs/02_design/systems/first-playable-slice.md`
  - `docs/03_architecture/readability-gate-contract.md`
  - `docs/03_architecture/first-playable-balance-targets.md`

## 목적

Loop D에서 pruning을 subtraction-first 원칙으로 고정한다.

## health grade

- `InsufficientData`
  - minimum exposure 미달
- `Healthy`
  - `TotalDebt <= 15`
- `Watch`
  - `16..30`
- `AtRisk`
  - `31..50`
- `Broken`
  - `> 50`
  - 또는 `ForbiddenLeak`
  - 또는 readability fatal

## prune disposition

- `Keep`
- `RetuneNumbers`
- `RetuneCadence`
- `SimplifyReadability`
- `MergeWithSibling`
- `MoveOutOfV1`
- `Remove`

## 핵심 규칙

### P1 redundant content

- `highestIdentitySimilarity >= 0.90`
- unique coverage 없음
- 같은 slot 경쟁군
- 기본 처분: `MergeWithSibling` 또는 `MoveOutOfV1`

### P2 dominant content

- `pickRate >= 0.38`
- `presenceWinDelta > +0.06`
- 기본 처분: `RetuneNumbers`
- readability/topology debt가 높으면 `SimplifyReadability` 또는 `MoveOutOfV1`

### P3 dead content

- `pickRate <= 0.05`
- unique coverage 없음
- 기본 처분: `MoveOutOfV1` 또는 `Remove`

### P4 readability debt first

- `unexplainedEffectShare > 0.08`
- 또는 `contributionToSalienceOverload > 0.10`
- 또는 `ProcChainOpacity`
- 숫자 retune 전에 `SimplifyReadability`

### P5 topology gap / dominance

- threat producer는 있는데 counter provider가 없으면 `CounterTopologyGap`
- 유일 provider가 broken이면 replacement 먼저
- threat lane과 answer lane을 동시에 과도하게 장악하면 `CounterTopologyDominance`

### P6 unique coverage 보호

- unique coverage는 바로 `Remove`하지 않는다.
- 기본 처분: retune/cadence/readability simplification
- 단 `ForbiddenLeak`이면 V1 제외

### P7 insufficient data 보호

- exposure 부족만으로 자동 prune하지 않는다.
- cap 압박과 coverage value 비교에서 밀리면 `MoveOutOfV1`

## generated artifact

- `Logs/loop-d-balance/content_health_cards.csv`
- `Logs/loop-d-balance/prune_ledger_v1.json`
