# sim sweep과 balance KPI

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/sim-sweep-and-balance-kpis.md`
- 관련문서:
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/02_design/meta/synergy-family-catalog.md`

## 목적

이 문서는 launch floor authoring 이후 어떤 deterministic sweep를 돌리고, 어떤 KPI로 review/fail을 판단할지 고정한다.
의도는 “밸런스 감상”이 아니라 `LoadoutCompiler -> BattleFactory -> BattleSimulator` 경로가 같은 입력에서 같은 산출을 내는지와, launch floor 예산이 크게 벗어나지 않는지를 기계적으로 확인하는 데 있다.

## 현재 smoke sweep 범위

- runner: `SM.Editor.Validation.BalanceSweepRunner`
- scenario source: `SM.Editor.Validation.BalanceSweepScenarioFactory`
- report path:
  - `Logs/content-validation/content-validation-report.json`
  - `Logs/content-validation/content-validation-summary.md`
  - `Logs/balance-sweep/balance-sweep-report.json`
  - `Logs/balance-sweep/balance-sweep-summary.csv`

## deterministic input 계약

### content snapshot

- source: committed `ScriptableObject` assets under `Assets/Resources/_Game/Content/Definitions/**`
- compile contract: `core_active / utility_active / passive / support`
- compile version: `LoadoutCompiler.CurrentCompileVersion`
- runtime truth는 committed asset이고, generator는 bootstrap repair 용도다.

### scenario templates

- `mixed_floor_control`
  - archetypes: `warden / raider / marksman / priest`
  - team tactic: `team_tactic_standard_advance`
  - temporary augments: `augment_silver_guard`, `augment_silver_hunt`
  - permanent augment: `augment_perm_legacy_oath`
- `focused_beastkin_push`
  - archetypes: `bulwark / raider / scout / shaman`
  - team tactic: `team_tactic_collapse_weak_side`
  - temporary augments: `augment_silver_stride`, `augment_silver_ward`
  - permanent augment: `augment_perm_legacy_fang`

### enemy template

- smoke encounter는 현재 `BattleEncounterPlans.CreateObserverSmokePlan()`을 사용한다.
- enemy build는 `BattleSetupBuilder.Build(...)`를 경유한다.
- 이 경로는 launch floor pass에서는 `migration-only encounter path`로 취급한다.
- follow-up TODO:
  - encounter asset authoring이 들어오면 `BattleSetupBuilder` 의존을 줄이고, authored encounter catalog를 primary source로 승격한다.

### seed policy

- smoke sweep seed ladder: `17 / 23 / 29`
- same scenario를 두 번 build했을 때 compile hash가 같아야 한다.
- 같은 snapshot + 같은 seed로 두 번 돌렸을 때 winner, step count, final state hash가 같아야 한다.

## KPI 정의

| KPI | 정의 | threshold | review/fail |
| --- | --- | --- | --- |
| compile hash determinism | 같은 authored input에서 `CompileHash` 일치 여부 | `100%` | 하나라도 다르면 fail |
| final state determinism | 같은 snapshot + seed에서 winner / step count / final state hash 일치 여부 | `100%` | 하나라도 다르면 fail |
| validation error count | content validator error 수 | `0` | `> 0` fail |
| validation warning count | content validator warning 수 | review only | 경향 추적, 단독 fail 아님 |
| average first cast seconds | scenario별 첫 active cast 평균 시점 | `<= 3.0s` | 초과 시 review flag |
| average battle duration seconds | scenario별 전투 평균 시간 | `4s ~ 40s` | 범위 밖이면 review flag |
| temporary augment dead-offer ratio | 현재 팀 tags와 무관한 temporary augment 비율 | `<= 0.6` | 초과 시 review flag |
| synergy tier uplift win rate | focused synergy comp가 mixed control comp보다 잃는 정도 | `>= -0.05` | 그보다 낮으면 review flag |
| damage share | ally damage 분포 | artifact 출력 필수 | top-heavy 여부 review |
| heal share | ally heal 분포 | artifact 출력 필수 | sustain concentration review |

## artifact 출력 규칙

### JSON

- 파일: `Logs/balance-sweep/balance-sweep-report.json`
- 포함 항목:
  - generation time
  - validation report path
  - validation error/warning count
  - scenario별 compile hash
  - compile hash determinism
  - final state determinism
  - win rate
  - average battle duration
  - average first cast
  - damage share / heal share
  - temporary augment dead-offer ratio
  - synergy tier uplift
  - global outlier flags

### CSV

- 파일: `Logs/balance-sweep/balance-sweep-summary.csv`
- 목적: branch/PR artifact에서 빠르게 scan 가능한 summary
- 포함 항목:
  - scenario id
  - team tactic id
  - determinism booleans
  - win rate
  - avg duration
  - avg first cast
  - dead-offer ratio
  - validation error/warning count
  - scenario flags

## review와 fail 조건

### 즉시 fail

- content validation error가 1개 이상
- compile hash nondeterministic
- final state nondeterministic

### review required

- `first_cast_over_3_0s`
- `battle_duration_out_of_band`
- `dead_offer_ratio_over_0_6`
- `global:synergy_uplift_negative`

### 이번 패스의 운영 원칙

- smoke runner는 outlier를 flag로 남기고 artifact에 기록한다.
- branch CI는 determinism/validation fail만 즉시 차단하고, balance outlier는 artifact review를 요구한다.

## 후속 sweep 확장

### unit micro sweep

- 단일 archetype / item / passive / augment의 delta를 측정한다.
- target: item 1개, notable 1개, keystone 1개, synergy 1단계, augment 1개가 예산 문서 범위를 벗어나는지 확인한다.

### canonical squad sweep

- launch floor 12 archetypes로 만든 대표 4인 조합들을 대진표로 돌린다.
- top / bottom win-rate spread를 추적한다.

### delta sweep

- 장비 1개, passive notable 1개, synergy 1단계, augment 1개가 compile hash와 전투 KPI에 주는 변화를 기록한다.
