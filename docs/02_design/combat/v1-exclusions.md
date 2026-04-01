# v1 exclusions

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/v1-exclusions.md`
- 관련문서:
  - `docs/02_design/combat/resource-cadence-loadout.md`
  - `docs/02_design/combat/targeting-and-ai-vocabulary.md`
  - `docs/02_design/combat/summon-ownership-and-deployables.md`

## 목적

이 문서는 Loop A에서 명시적으로 닫지 않는 항목을 적어 임의 확장을 막는다.

## excluded in Loop A

| 항목 | 상태 |
| --- | --- |
| `Disarm`, `Fear`, `Charm` full runtime | 제외, 문서 명시만 유지 |
| persistent summon chain | 금지 |
| summon/deployable synergy count 포함 | 금지 |
| summon kill의 기본 owner energy 지급 | 금지 |
| summon kill의 기본 generic owner on-kill proc | 금지 |
| signature active의 cooldown 기반 전환 | 금지 |
| flex active의 energy 기반 전환 | 금지 |
| target reevaluation frame-by-frame 수행 | 금지 |
| skill haste가 cast time까지 단축 | 금지 |

## 운영 메모

- exclusion은 `추후 가능`의 완곡한 표현이 아니라 현재 validator/runtime가 막아야 하는 경계다.
- follow-up이 필요하면 새 task와 새 source-of-truth 문서를 만들어서 승격한다.
