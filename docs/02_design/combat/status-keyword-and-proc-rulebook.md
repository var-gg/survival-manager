# status / keyword / proc rulebook

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/combat/status-keyword-and-proc-rulebook.md`
- 관련문서:
  - `docs/02_design/combat/status-effects-cc-and-cleanse-taxonomy.md`
  - `docs/02_design/combat/combat-mechanics-glossary.md`
  - `docs/03_architecture/status-runtime-stack-and-cleanse-rules.md`
  - `docs/03_architecture/combat-state-and-event-model.md`

## 목적

이 문서는 status, keyword, proc의 의미와 ownership rule을 한 군데서 잠근다.
목표는 스킬, affix, synergy, augment가 늘어날 때도 on-hit / on-attack / summon / DoT / secondary hit 판정이 흔들리지 않게 만드는 것이다.

## 용어

- `status`: duration, stack, cleanse 규칙을 가진 지속 상태
- keyword: 검색과 authoring 분류를 위한 태그형 의미. 자체 timer나 stack이 없다.
- proc: 특정 사건이 다른 효과를 트리거하는 규칙
- secondary hit: 다른 hit나 status가 만든 파생 hit
- `owner`: kill credit, on-kill, on-hit attribution을 받을 주체

## status taxonomy

### hard CC

| status | launch floor | stack | refresh | 비고 |
| --- | --- | --- | --- | --- |
| `stun` | active | 단일 | refresh | action 불가 |
| `root` | active | 단일 | refresh | 이동 불가 |
| `silence` | active | 단일 | refresh | active skill 금지 |
| `knockback` | deferred | 별도 인스턴스 금지 | replace | displacement runtime은 후속 패스 |

### soft CC / tactical state

| status | launch floor | stack | refresh | 비고 |
| --- | --- | --- | --- | --- |
| `slow` | active | 단일 | refresh | move / attack tempo 저하 |
| `chill` | deferred | additive | refresh | cold family 후보 |
| `marked` | active | 단일 | refresh | focus fire 표식 |
| `exposed` | active | 단일 | refresh | incoming damage 증가 |
| `weaken` | deferred | 단일 | refresh | outgoing damage 감소 |

### defensive state

| status | launch floor | stack | refresh | 비고 |
| --- | --- | --- | --- | --- |
| `guarded` | active | 단일 | refresh | guard / block identity |
| `barrier` | active | magnitude 누적 | refresh | shield로 환산 |
| `unstoppable` | active | 단일 | refresh | hard CC 면역 |
| `untargetable` | deferred | 단일 | replace | 극히 제한적으로만 허용 |

### damage over time / vulnerability

| status | launch floor | stack | refresh | 비고 |
| --- | --- | --- | --- | --- |
| `burn` | active | capped stack | refresh | magic attrition |
| `bleed` | active | capped stack | refresh | physical attrition |
| `poison` | deferred | capped stack | refresh | 후속 family |
| `wound` | active | 단일 | refresh | healing received 감소 |
| `sunder` | active | 단일 | refresh | armor / resist 저하 |

## canonical status rule

- 모든 status family는 아래 계약을 가져야 한다.
  - stack 가능 여부
  - stack cap
  - refresh policy
  - proc attribution policy
  - owner attribution policy
  - cleanse / immunity 관계
  - AI relevance
  - presentation priority
- launch floor에서는 `StatusFamilyDefinition`의 default policy와 `StatusApplicationRule`의 override를 같이 사용한다.
- hard CC는 `1.5초 / 50%` control resist window와 `tenacity` duration reduction을 동시에 받는다.

## proc rulebook

### on-attack vs on-hit

- `on-attack`은 action windup이 commit됐을 때 한 번만 본다.
- `on-hit`은 target validity, dodge, crit, block, armor를 거친 뒤 실제 hit가 성립했을 때 본다.
- 완전히 회피된 hit는 `on-hit`를 발생시키지 않는다.

### crit / block / secondary hit

- secondary hit는 기본적으로 원본 hit를 그대로 복제하지 않는다.
- secondary hit의 crit 허용 여부는 payload가 명시한 경우에만 다시 계산한다.
- block으로 감쇄된 hit는 `hit`로는 남지만, `perfect avoid`처럼 취급하지 않는다.
- block은 mitigation이며, `on-hit`를 막는 수단이 아니다.

### DoT / status tick

- DoT tick은 `on-hit`가 아니다.
- DoT tick은 `on-damage-tick` attribution만 가진다.
- DoT tick의 crit, block, dodge 재계산은 launch floor에서 금지한다.

### summon / owner credit

- summon의 기본 hit, kill, on-kill credit은 summon owner에게 귀속한다.
- summon 독립 스택/trait가 필요한 경우에만 owner override를 둔다.
- deployable / construct도 같은 owner rulebook을 따른다.

### multi-hit / rate limit

- 같은 action payload의 multi-hit proc는 rate limit window를 가진다.
- 기본 launch floor 규칙:
  - 같은 payload source가 같은 target에 주는 proc는 `0.15초` window 안에서 한 번만 본다.
  - 확장 룰이 필요하면 effect definition이 명시적으로 override한다.

## keyword rule

- keyword는 status를 대체하지 않는다.
- `Melee`, `Projectile`, `AoE`, `Dash`, `Blink`, `Guard`, `Execute`, `Mobility`, `Summon`, `Aura`는 keyword다.
- `burn`, `bleed`, `marked`, `guarded`는 status다.
- 같은 단어를 keyword와 status로 동시에 쓰지 않는다.

## AI 인지 규칙

- AI는 최소 아래 status를 인지한다.
  - `marked`
  - `exposed`
  - `guarded`
  - `unstoppable`
  - `burn` / `bleed` / `wound`
- AI relevance가 꺼진 status는 utility scorer와 retreat scorer가 직접 읽지 않는다.

## 비목표

- crowd control 전체 catalog의 full runtime rollout
- displacement, fear, charm, sleep 계열 full closure
- proc scripting DSL
