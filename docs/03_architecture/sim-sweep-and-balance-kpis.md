# sim sweep과 balance KPI

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-21
- 소스오브트루스: `docs/03_architecture/sim-sweep-and-balance-kpis.md`
- 관련문서:
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/systems/launch-floor-content-matrix.md`
  - `docs/02_design/meta/synergy-family-catalog.md`

## 목적

이 문서는 launch floor authoring 이후 어떤 deterministic sweep를 돌리고, 어떤 KPI로 review/fail을 판단할지 고정한다.
의도는 `LoadoutCompiler -> BattleFactory -> BattleSimulator` 경로가 같은 입력에서 같은 산출을 내는지와, launch floor 예산이 크게 벗어나지 않는지를 기계적으로 확인하는 데 있다.

## 현재 smoke sweep 범위

- runner: `SM.Editor.Validation.BalanceSweepRunner`
- scenario source: `SM.Editor.Validation.BalanceSweepScenarioFactory`
- report path:
  - `Logs/content-validation/content-validation-report.json`
  - `Logs/content-validation/content-validation-summary.md`
  - `Logs/balance-sweep/balance-sweep-report.json`
  - `Logs/balance-sweep/balance-sweep-summary.csv`

## Loop D deterministic suite 범위

- slice/balance runner: `SM.Editor.Validation.FirstPlayableBalanceRunner`
- entrypoint: `ValidationBatchEntryPoint.RunLoopDReadabilityAndBalance()`
- generated artifact:
  - `Logs/loop-d-balance/purekit_report.json`
  - `Logs/loop-d-balance/systemic_slice_report.json`
  - `Logs/loop-d-balance/runlite_report.json`
  - `Logs/loop-d-balance/content_health_cards.csv`
  - `Logs/loop-d-balance/prune_ledger_v1.json`
  - `Logs/loop-d-balance/readability_watchlist.json`
  - `Logs/loop-d-balance/first_playable_slice.md`

## deterministic input 계약

### content snapshot

- source: committed `ScriptableObject` assets under `Assets/Resources/_Game/Content/Definitions/**`
- compile contract: Loop A 6-slot topology
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

### seed policy

- smoke sweep seed ladder: `17 / 23 / 29`
- same scenario를 두 번 build했을 때 compile hash가 같아야 한다.
- 같은 snapshot + 같은 seed로 두 번 돌렸을 때 winner, step count, final state hash가 같아야 한다.
- symmetric mirror 4v4 timeout/draw policy는 default smoke/FastUnit gate가 아니라 `ManualLoopD` balance review에서 별도 추적한다.

## KPI 정의

| KPI | 정의 | threshold | review/fail |
| --- | --- | --- | --- |
| compile hash determinism | 같은 authored input에서 `CompileHash` 일치 여부 | `100%` | 하나라도 다르면 fail |
| final state determinism | 같은 snapshot + seed에서 winner / step count / final state hash 일치 여부 | `100%` | 하나라도 다르면 fail |
| validation error count | content validator error 수 | `0` | `> 0` fail |
| validation warning count | content validator warning 수 | review only | 경향 추적, 단독 fail 아님 |
| average first signature cast seconds | scenario별 첫 signature cast 평균 시점 | `5.0s ~ 9.0s` | 범위 밖이면 review flag |
| time to first meaningful action | ally가 첫 meaningful event를 만드는 평균 시점 | artifact 출력 필수 | cadence review |
| average reposition count | ally 1인당 reposition-like state 진입 평균 | artifact 출력 필수 | blob / jitter review |
| average target access time | ally 1인당 첫 damage contact 평균 시점 | artifact 출력 필수 | 접근성 review |
| average frontline survival time | front row ally 평균 생존 시간 | artifact 출력 필수 | frontline collapse review |
| average battle duration seconds | scenario별 전투 평균 시간 | `4s ~ 40s` | 범위 밖이면 review flag |
| temporary augment dead-offer ratio | 현재 팀 tags와 무관한 temporary augment 비율 | `<= 0.6` | 초과 시 review flag |
| synergy tier uplift win rate | focused synergy comp가 mixed control comp보다 잃는 정도 | `>= -0.05` | 그보다 낮으면 review flag |

## review와 fail 조건

### 즉시 fail

- content validation error가 1개 이상
- compile hash nondeterministic
- final state nondeterministic

### review required

- `first_signature_cast_out_of_band`
- `battle_duration_out_of_band`
- `dead_offer_ratio_over_0_6`
- `global:synergy_uplift_negative`

## 이번 패스의 운영 원칙

- smoke runner는 outlier를 flag로 남기고 artifact에 기록한다.
- branch CI는 determinism/validation fail만 즉시 차단하고, balance outlier는 artifact review를 요구한다.
- Loop D는 readability fatal, required telemetry event 누락, broken content의 slice 잔류도
  즉시 fail로 본다.
