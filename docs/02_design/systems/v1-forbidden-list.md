# V1 금지 목록

## 목적

Loop C의 forbidden list는 참고사항이 아니라 build-blocking policy다. 이 문서의 항목은
validator가 fatal fail로 막는다.

## fatal policy

- forbidden flag가 하나라도 있으면 error가 아니라 fatal fail이다.
- editor menu, CI, pre-commit, batch validation이 같은 결과를 내야 한다.
- 미구현 상태로 남겨 두지 않는다.

## forbidden flag table

| Flag | 금지 이유 | 허용되는 근사 표현 |
| --- | --- | --- |
| `TrueDamage` | defense lane을 무력화해 topology를 붕괴시킨다 | `ArmorShred`, `Exposure`, heavy finisher |
| `AccuracyRoll` | hidden miss chance가 readability를 무너뜨린다 | 명시적 dodge/reposition과 `TrackingArea` |
| `ReviveOrDeathDeny` | kill confirm과 attrition 판단을 흐린다 | death-trigger barrier, delayed summon |
| `MindControl` | AI/targeting complexity가 급상승한다 | taunt, fear 없는 displacement, peel |
| `LongUntargetableOrStealth` | read loss와 cadence break가 크다 | `<= 0.20s` presentation blink |
| `ResourceBurnOrSteal` | cadence/economy 축을 다시 열게 된다 | silence, anti-cast window |
| `ExtraActionOrCooldownReset` | budget validator 의미가 약해진다 | haste burst, partial refund |
| `PermanentUncappedStacking` | sim/readability/validator가 모두 붕괴한다 | capped stack, windowed ramp |
| `StatOrSkillTheft` | authoring explosion과 replay 해석 비용이 크다 | temporary debuff 또는 self-buff |
| `ReflectLoop` | symmetric loop와 kill attribution이 흐려진다 | capped thorns, once-per-window punish |
| `FriendlyFire` | targeting/sim complexity가 급상승한다 | enemy-only splash |
| `TerrainOrProjectileCollision` | prototype phase에 비해 sim 비용이 과하다 | simple line/area delivery |
| `SpawnChain` | board clog와 recursive authoring cost가 커진다 | capped delayed summon |
| `GlobalAuraSameKeyStack` | comp readability와 stacking math가 무너진다 | strongest-wins aura |
| `NonAugmentEconomyOrOfferChange` | run economy를 다시 열게 된다 | augment 안의 bounded economy |
| `LoadoutTopologyMutation` | Loop A loadout contract를 다시 연다 | existing slot의 local modifier |
| `CrossRunPowerGrant` | run-local balance와 persistence가 섞인다 | separate progression system |
| `LegendaryOrUniqueException` | governance rarity 바깥 예외 설계가 퍼진다 | `Common/Rare/Epic` 내 local hook |

## 추가 금지 항목

- 전장 전체 hard displacement
- full-team teleport류 board reset
- broad cleanse-all
- steal-all-buffs
- all-CC immunity
- true damage execute

위 항목은 shape heuristic으로도 막는다.

## validator fail 조건

- authored content가 forbidden flag를 선언한다
- validator heuristic이 broad answer drift를 감지한다
- `DamageType.True`를 실제 authored payload에서 사용한다
- global aura 같은 forbidden shape를 flag 없이 우회한다

## good / bad 예시

### good 1. True damage fantasy 대체

- armor ignore window
- `ArmorShred:Standard`
- heavy finisher, execute threshold 없음

### good 2. Revive fantasy 대체

- on-death barrier
- delayed deployable reinforcement

### good 3. Cooldown reset fantasy 대체

- one-time 30% cooldown refund
- short haste burst

### bad 1. Mana burn caster

- 상대 resource를 직접 태우거나 훔치면 fail이다

### bad 2. Legendary-only exception skill

- `Epic` ladder 밖 예외 규칙을 새로 만들면 fail이다

### bad 3. Infinite rage stack

- uncapped permanent stack는 scale와 readability 둘 다 붕괴시킨다
