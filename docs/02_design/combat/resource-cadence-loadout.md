# resource cadence와 loadout

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/resource-cadence-loadout.md`
- 관련문서:
  - `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
  - `docs/02_design/combat/skill-authoring-schema.md`
  - `docs/02_design/meta/retrain-contract.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 Loop A의 6-slot topology, energy cadence, action lane 우선순위를 고정한다.

## 6-slot topology

모든 roster unit은 정확히 아래 6개 슬롯을 가진다.

| slot | activation | mutable | 설명 |
| --- | --- | --- | --- |
| `BasicAttack` | 항상 가능 | 고정 | 기본 공격 cycle과 attack speed의 기준 |
| `SignatureActive` | `Energy` | 고정 | archetype identity active, cast start에 energy 100 사용 |
| `FlexActive` | `Cooldown` 또는 `Trigger` | 가변 | recruit/retrain이 바꿀 수 있는 active |
| `SignaturePassive` | `Passive` | 고정 | archetype identity passive |
| `FlexPassive` | `Passive` 또는 `Trigger` | 가변 | recruit/retrain이 바꿀 수 있는 passive |
| `MobilityReaction` | `Trigger + Cooldown` | 고정 | compact card에서는 숨길 수 있지만 topology에는 항상 존재 |

## activation model

| model | 허용 slot | 규칙 |
| --- | --- | --- |
| `Passive` | `SignaturePassive`, `FlexPassive` | 전투 내 explicit cast 없음 |
| `Energy` | `SignatureActive` | v1에서는 `Energy`만 사용, 다른 slot은 금지 |
| `Cooldown` | `FlexActive` | energy 사용 금지 |
| `Trigger` | `FlexActive`, `MobilityReaction` | event gating 가능, mobility는 cooldown 동반 |

## energy rule

- `MaxEnergy = 100`
- `DefaultStartingEnergy = 10`
- clamp는 `[0, 100]`
- passive decay 없음
- `SignatureActive`는 cast start에 `100` 사용
- cast start 이후 interrupt되면 `50` refund

## energy gain

| event | 획득량 | 비고 |
| --- | --- | --- |
| `BasicAttackResolved` | `+12` | hit, crit, block, dodge 모두 resolve로 본다 |
| `DirectHitTakenAfterMitigation` | `+6` | internal cooldown `0.35s` |
| `ActualKillBySelf` | `+15` | actual killer만 획득 |
| `ActualAssistBySelf` | `+8` | assist contributor만 획득 |

## 명시적 금지

- summon의 hit/kill은 owner 기본 energy를 주지 않는다.
- `FlexActive`는 energy를 쓰지 않는다.
- `MobilityReaction`은 energy를 쓰지 않는다.
- `SkillHaste`는 cast time과 signature energy 획득량을 바꾸지 않는다.

## action lane와 arbitration

### ground state 원칙

`BasicAttack`은 "가장 낮은 우선순위 행동"이 아니라 **기본 전투 상태(ground state)**다.
스킬은 ground state를 중단(interrupt)하는 것이지, ground state와 우선순위를 경쟁하는 것이 아니다.

이 구분이 중요한 이유: ground state가 에너지 축적의 유일한 통상 경로(`BasicAttackResolved → +12 energy`)이므로,
ground state가 차단되면 에너지 기반 스킬(`SignatureActive`)이 영원히 발동할 수 없다.

### 평가 순서

1. `HardCC` 확인 — stunned면 모든 행동 불가
2. `MobilityReaction` trigger 확인 — reaction lane, primary lane과 독립
3. `SignatureActive` (에너지 충족) — ground state를 중단(interrupt)
4. `FlexActive` (쿨다운 충족, **전투형만**) — `Strike`/`Debuff` Kind의 Flex는 ground state를 중단
5. `BasicAttack` — **ground state**, 에너지 생성기, 기본 전투 행동
6. `FlexActive` (비전투형) — 기본공격 불가 시 유틸리티 폴백 (`Heal`/`Shield`/`Buff`/`Utility` Kind)
7. 없으면 range discipline 이동

전투형 Flex 판정 기준: `SkillKind`가 `Strike` 또는 `Debuff`인 스킬은 직접 데미지를 적용하므로 ground state를 중단할 가치가 있다.
비전투형 Flex(`Heal`, `Shield`, `Buff`, `Utility`)는 기본공격 타겟이 없을 때만 폴백으로 사용한다.

### lane 규칙

- `Primary`: `BasicAttack`, `SignatureActive`, `FlexActive`
- `Reaction`: `MobilityReaction`
- `Locomotion`: 이동/재배치
- `Primary`는 동시 실행 금지
- `Reaction`은 `Locomotion`을 선점할 수 있고, `Primary`의 `SoftCommit` 전에는 basic attack windup을 끊을 수 있다

## commit rule

| slot | lock rule | 세부 |
| --- | --- | --- |
| `BasicAttack` | `SoftCommit` | windup의 40% 전까지 끊길 수 있다 |
| `SignatureActive` | `HardCommit` | cast start부터 `Stun`, `ForcedDisplacement`, `Death`만 끊을 수 있다 |
| `FlexActive` | `HardCommit` | cast start부터 같은 rule 적용 |
| `MobilityReaction` | `HardCommit` | translation 시작부터 유지 |

## action restriction matrix

| status | movement | basic attack | active skill | mobility |
| --- | --- | --- | --- | --- |
| `Stun` | 금지 | 금지 | 금지 | 금지 |
| `Root` | translation 금지 | range valid면 허용 | non-move cast만 허용 | 금지 |
| `Silence` | 허용 | 허용 | `SignatureActive`, `FlexActive` 금지 | 허용 |

`Disarm`, `Fear`, `Charm`은 Loop A runtime 범위 밖이며 `v1-exclusions.md`가 소유한다.

## stat interpretation

- `AttackSpeed`는 basic attack cycle만 바꾼다.
- `SkillHaste`는 `FlexActive`, `MobilityReaction`의 cooldown recovery만 바꾼다.
- `cooldown_recovery`는 migration alias일 뿐이고 canonical stat id는 `skill_haste`다.
