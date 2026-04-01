# 영웅 특성과 quirk

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/hero-traits.md`
- 관련문서:
  - `docs/02_design/meta/recruitment-contract.md`
  - `docs/02_design/meta/drop-table-rarity-bracket-and-source-matrix.md`
  - `docs/02_design/meta/economy-protection-contract.md`

## 목적

이 문서는 recruit trait와 quirk가 launch floor에서 어떤 역할을 맡는지 정의한다.
목표는 같은 archetype이라도 trait roll에 따라 다른 빌드 얼굴을 만들되, trait를 noisy post-battle clutter로 만들지 않는 것이다.

## 핵심 규칙

### 기본 recruit trait 규칙

모든 recruit는 시작 시 다음을 가진다.

- positive trait 1개
- negative trait 1개

이 규칙은 launch floor에서도 실제로 구현한다.

### same-archetype variance 규칙

같은 archetype, 심지어 같은 캐릭터 계열이라도 아래에 따라 다른 역할을 가질 수 있어야 한다.

- trait roll
- 장비
- augment

trait는 이 차별화의 첫 축이다.
trait는 archetype을 지우지 않고, item / augment와 결합될 때 역할을 선명하게 만들어야 한다.

### archetype trait pool 규칙

시스템 규칙으로서 각 archetype은 아래를 가져야 한다.

- 좋은 특성 풀 3개 이상
- 나쁜 특성 풀 3개 이상

이 규칙은 장기적으로 고정 기준이다.
다만 launch floor 시드 데이터에서는 동일 trait 데이터를 여러 archetype이 재사용하는 것을 허용한다.

## launch floor 구현 범위

- recruit 시작 시 positive 1 / negative 1
- trait modifier가 기본 전투/메타 수치에 반영
- archetype별 trait pool 참조 구조
- 시드 데이터 재사용 허용
- trait 관련 재화는 `trait_reroll_token`, `trait_lock_token`, `trait_purge_token`만 연다.
- regular battle drop에서 무작위 trait gain을 빈번하게 허용하지 않는다.

## token 정책

- `trait_reroll_token`: positive / negative 조합을 다시 굴린다.
- `trait_lock_token`: 원하는 trait를 고정한 채 나머지 축만 다룬다.
- `trait_purge_token`: 불리한 trait를 제거하는 희소 연산이다.

trait token economy는 item crafting currency와 섞지 않는다.

## 장기 규칙

- 행동 규칙을 바꾸는 trait
- synergy 조건부 trait
- rarity tier가 있는 trait
- town/event로 획득되는 추가 trait

## 밸런스 기준

- positive trait는 class/race identity를 완전히 덮어쓰면 안 된다.
- negative trait는 함정 픽만 양산하면 안 된다.
- trait 하나만으로 역할이 완결되면 안 되고, 장비/augment와 결합될 때 역할이 선명해져야 한다.
- recruit의 결함과 장점은 run을 거치며 조정될 수 있지만, 매 전투마다 무작위로 새 trait를 쌓아 올리지는 않는다.
