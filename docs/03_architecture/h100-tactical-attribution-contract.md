# H100 tactical attribution 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/03_architecture/h100-tactical-attribution-contract.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/h100-build-space-census-contract.md`
  - `docs/03_architecture/h100-headless-policy-contract.md`
  - `docs/03_architecture/telemetry-contract.md`
  - `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`
  - `docs/04_decisions/adr-0032-h100-build-space-census-boundary.md`

## 목적

이 문서는 BT1-E09에서 Stage 4 placement leverage를 player-visible 전술과 raw 거리·targeting·pathing으로 분해하는 paired corpus, 귀속 규칙, Pro 4조건, formation non-use/trap 판정, 결정적 산출물 계약을 고정한다. 이 lane은 진단만 하며 전투·콘텐츠 수치나 gate threshold를 수정하지 않는다.

## 소유 경계

`SM.HeadlessMetrics`는 실콘텐츠나 census 참조가 없는 `PlacementAttributionBattleRecord`, trace/report DTO, `PlacementAttributionEvaluator`, 결정적 writer를 소유한다. `SM.Editor.Validation`은 concept catalog, `FormationFeatureClassifier`, 실제 `GameSessionState`와 기존 Stage 4/E05/E06 report를 조립한다. sibling pure asmdef 참조, 새 asmdef, `InternalsVisibleTo`를 추가하지 않는다.

## Paired corpus

기본 corpus는 BT1 concept catalog의 서로 다른 medoid composition 8개, `site_ashen_gate`·`site_wolfpine_trail`·`site_sunken_bastion` 세 encounter family, paired decision seed 2개다. 각 composition·family·seed stratum에서 build, encounter, content, battle seed와 최대 step을 고정하고 placement만 바꾼다.

- semantic adjacent swap: 같은 실제 role의 두 unit이 인접 anchor를 교환하고 `FormationFeatures` 일곱 필드가 모두 동일한 pair
- profile transition: catalog medoid profile과 다른 canonical profile의 pair
- anchor sweep: Stage 3의 자동 medoid 8개를 모두 재실행한 anchor 사용/비사용 표본

기본 8×3×2 실행은 576 battles와 432 pair를 만든다. semantic swap과 profile transition은 stratum당 각 1 pair이고, anchor sweep은 8 battles에서 기준 medoid와 나머지 7개를 비교한다. semantic corpus에서 full feature equality가 깨지면 wrapper는 실패한다.

## Trace와 귀속

trace는 paired decision seed와 실제 파생 battle seed, 다섯 typed formation channel의 eligibility/count, 첫 적대 contact tick·edge distance, 첫 target signature, target switch 수, pathing 재계획, 아군 travel distance, approach stall ratio를 보존한다. evaluator는 run, comparison, composition, concept variant, encounter family, scenario, 두 seed, fixed step이 pair 안에서 같고 placement variant만 다른지 fail-closed로 검사한다. material outcome은 승패가 바뀌거나 정규화 최종 전력차 절댓값이 0.10 이상인 pair다.

귀속은 다음 우선순위를 적용한다.

1. typed flank/rear/screen/save/dive count가 달라지면 `visible_tactical_channel`
2. 첫 target이 달라지거나 target switch 차이가 2 이상이면 `target_selection_discontinuity`
3. 첫 contact 존재 여부, 시간 0.50초, edge distance 0.35 중 하나가 달라지면 `raw_contact_geometry`
4. pathing 재계획 2회, travel 1.0, approach stall ratio 0.10 중 하나가 넘으면 `pathing_artifact`
5. 앞선 설명 없이 material이면 `unexplained_raw`; material이 아니면 `no_material_outcome_delta`

typed 전술이 있는 pair를 raw 접촉 변화가 함께 있다는 이유로 다시 raw에 중복 귀속하지 않는다. component share는 승패 변화에 1, 승패가 같으면 정규화 전력차 절댓값을 weight로 사용한다.

배치 정책을 호출하지 않는 fixed-placement pair이므로 pair-level `policy_noise` count/share는 0으로 명시한다. 정책이 intended formation을 선택했는지와 선택 후 channel이 발동했는지는 아래 Stage 4/E05/E06 join에서 별도 판정한다.

## Pro 검토 조건

보고서는 다음 네 조건을 각각 독립적으로 기록한다.

1. semantic swap의 같은 방향 25%p 이상 반복 reversal이 composition×family group의 25% 이상이며 두 family에 걸친다.
2. raw contact geometry와 target-selection discontinuity가 material weight의 절반을 넘고 두 family에 걸친다.
3. 한 anchor가 8개 composition과 모든 family에서 양의 차이를 보이고 composition×family stratum median이 25%p 이상이다.
4. typed player-visible tactical 설명이 없는 material weight가 절반을 넘고 두 family에 걸친다.

조건 발동은 `bug_or_trap_candidate` 검토 신호다. 조건 미발동은 표본 범위 안에서 `no_bug_grade_condition_observed`이며 전역 무결함을 뜻하지 않는다. 어느 경우에도 evaluator나 wrapper가 수치·콘텐츠·gate threshold를 바꾸지 않는다.

## Formation non-use와 trap 후보

join adapter는 Stage 4 competent channel summary, E05 formation-profile track availability/realization/payoff, E06 preview formation rule evidence를 channel intended profile에 연결한다. E09 intended-profile 실행의 실제 eligibility/firing도 함께 보므로 다음 상태를 분리한다.

- `situation_not_actually_eligible`: intended profile 표본에서도 channel 상황이 실제 성립하지 않음
- `policy_did_not_select_visible_formation_response`: 상황은 성립했지만 E06의 가시 formation 선택 증거가 없음
- `eligible_after_visible_policy_choice_but_channel_did_not_fire`: 상황과 가시 선택은 있었지만 typed channel witness가 없음
- `stage4_policy_selection_gap_but_intended_profile_positive_witness_exists`: Stage 4 정책 표본에서는 미발동했지만 E09 intended profile에서 positive witness가 있음
- `positive_witness_observed`: 기존 또는 E09 표본에 positive witness가 있음

formation option은 intended context가 실제 eligible이고 positive channel witness가 0이며 E05 track이 존재하고, 동일 비용 profile comparator가 8쌍 이상에서 non-worse 95% 이상·strictly-better 50% 이상일 때만 trap 후보가 된다. generic payoff만으로 formation channel positive witness를 대신하지 않는다.

## 산출물과 실행

기본 산출물은 `Logs/h100-tactical-attribution/placement_attribution_report.json` 하나다. invariant snake_case, ordinal 정렬, UTF-8 no-BOM을 사용하며 wallclock과 GUID를 넣지 않는다.

```powershell
pwsh -File tools/h100-tactical-attribution.ps1 -CompositionCount 8 -SeedCount 2
```

wrapper는 technical failure 0, 요청한 composition·seed와 세 family coverage, semantic invariant 위반 0, Pro 조건 4개, anchor 6개, formation channel 5개 행을 검사한다. 같은 인자로 output directory만 바꿔 반복했을 때 report byte가 같아야 한다. 측정된 bug/trap 후보 true는 report verdict이며 정상적인 runner 완료를 실패로 바꾸지 않는다.
