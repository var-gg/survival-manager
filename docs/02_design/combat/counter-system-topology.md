# 카운터 시스템 토폴로지

## 목적

V1 counter-system은 damage triangle이 아니라 "적이 어떤 패턴으로 이기고,
플레이어가 어떤 답을 갖는가"를 읽게 하는 8-lane threat/answer topology다.

## canonical lane

| Threat | 설명 | Primary answer |
| --- | --- | --- |
| `ArmorFrontline` | 높은 armor와 전열 유지 | `ArmorShred` |
| `ResistanceShell` | resistance와 debuff 둔화 | `Exposure` |
| `GuardBulwark` | guard/block로 opener를 흡수 | `GuardBreakMultiHit` |
| `EvasiveSkirmish` | dodge/reposition으로 단일타 회피 | `TrackingArea` |
| `ControlChain` | stun/slow/knockback으로 cadence 절단 | `TenacityStability` |
| `SustainBall` | heal/barrier/regen attrition 버티기 | `AntiHealShatter` |
| `DiveBackline` | 접근/암살/분산 압박 | `InterceptPeel` |
| `SwarmFlood` | summon/body clog/single-target 압박 | `CleaveWaveclear` |

- topology 밖 lane 추가는 금지한다.
- `AntiShieldOnly`, `AntiMageOnly`, `AntiProjectileOnly` 같은 분기는 새 lane이 아니라
  위 8개 안에서 해석한다.

## answer semantics

- `ArmorShred`
  - armor 감소 또는 armor ignore
  - 단순 raw damage 증가와 구분한다
- `Exposure`
  - resistance 감소 또는 magical vulnerability
  - anti-heal과 혼동하지 않는다
- `GuardBreakMultiHit`
  - guard 효율 감소 또는 multi-hit로 빠른 guard 소모
- `TrackingArea`
  - tracking projectile, persistent ground AoE, wide volley
- `TenacityStability`
  - CC duration 감소, interrupt/displacement 저항
- `AntiHealShatter`
  - heal received 감소, barrier break, regen punish
- `InterceptPeel`
  - taunt, intercept, peel reaction, bodyguard logic
- `CleaveWaveclear`
  - splash, cleave, nova, short-cooldown waveclear
  - target cap 없는 full-screen clear는 금지한다

## declaration cap

| Domain | threat cap | counter cap |
| --- | ---: | ---: |
| `UnitBlueprint Common` | 1 | 1 |
| `UnitBlueprint Rare` | 1 | 2, 둘째는 `Light` |
| `UnitBlueprint Epic` | 2 | 2 |
| `Skill / Passive / Mobility` | 1 | 1 |
| `Affix` | 1 | 1, `Rare+`만 허용 |
| `Augment` | 2 | 2 |

## coverage strength

- `Light`: incidental answer
- `Standard`: normal rotation에서 안정적 대답
- `Strong`: team identity 수준

team report는 discrete bucket으로 합산한다.

- `Light 1개 -> Light`
- `Light 2개` 또는 `Standard 1개 -> Standard`
- `Standard 2개` 또는 `Strong 1개 -> Strong`

`Strong`은 cap이다.

## budget coupling

- declared counter tool이 있으면 `BudgetVector.counterCoverage > 0`이어야 한다.
- `counterCoverage > 0`인데 declared counter tool이 없으면 fail이다.
- `Strong` coverage 1개는 budget 최소 `4`를 쓴다.
- `Light` coverage는 `1~2` budget으로 허용한다.

## status / keyword family

- status/keyword rulebook은 아래 family를 canonical로 둔다.
  - `ArmorShred`
  - `Exposure`
  - `GuardBroken`
  - `MortalWound`
  - `Stable`
  - `Intercepting`
- `Splash`, `Cleave`, `Waveclear`는 status보다 delivery/property category로 다룬다.

## validator fail 조건

- 허용되지 않은 threat/counter enum을 선언한다
- domain별 count cap을 넘는다
- broad answer가 topology 밖 lane처럼 동작한다
- declared counter와 budget `counterCoverage`가 불일치한다
- team coverage가 debug report에 누락된다

## good / bad 예시

### good 1. Rare ranger anti-evasion tool

- `TrackingArea:Standard`
- threat는 `EvasiveSkirmish`
- wide volley delivery로 answer semantics가 읽힌다

### good 2. Support peel tool

- `InterceptPeel:Standard`
- taunt 또는 bodyguard verb가 실제 존재한다

### good 3. Anti-sustain rider

- `MortalWound`를 짧게 건다
- `AntiHealShatter:Light`
- execute나 true damage는 쓰지 않는다

### bad 1. Tankiness만 높은 anti-dive

- interception verb 없이 단순 체력만 높으면 `InterceptPeel`로 선언할 수 없다

### bad 2. Cleanse all as universal answer

- 모든 CC/DoT/buff를 한 번에 지우면 topology 밖 broad answer drift다

### bad 3. New lane invention

- `AntiSummonerOnly` 같은 lane을 새 enum으로 추가하면 fail이다
