# 영웅 특성

- 상태: draft
- 최종수정일: 2026-03-29
- 단계: prototype

## 목적

이 문서는 영웅 trait 규칙을 정의한다.
목표는 같은 archetype이라도 trait roll에 따라 다른 역할을 만들 수 있게 하는 것이다.

## 핵심 규칙

### 기본 recruit trait 규칙

모든 recruit는 시작 시 다음을 가진다.

- positive trait 1개
- negative trait 1개

이 규칙은 MVP에서도 실제로 구현한다.

### same-archetype variance 규칙

같은 archetype, 심지어 같은 캐릭터 계열이라도 아래에 따라 다른 역할을 가질 수 있어야 한다.

- trait roll
- 장비
- augment

trait는 이 차별화의 첫 축이다.

### archetype trait pool 규칙

시스템 규칙으로서 각 archetype은 아래를 가져야 한다.

- 좋은 특성 풀 3개 이상
- 나쁜 특성 풀 3개 이상

이 규칙은 장기적으로 고정 기준이다.
다만 MVP 시드 데이터에서는 동일 trait 데이터를 여러 archetype이 재사용하는 것을 허용한다.
즉, 시스템 문법은 archetype별 3+ / 3+ 이지만, MVP 데이터는 완전한 개별 고유 풀까지 강제하지 않는다.

## MVP 구현 범위

MVP에서 실제로 구현할 최소 범위:

- recruit 시작 시 positive 1 / negative 1
- trait modifier가 기본 전투/메타 수치에 반영
- archetype별 trait pool 참조 구조
- 시드 데이터 재사용 허용

## 장기 규칙

문서상 장기 규칙으로 남기는 항목:

- 행동 규칙을 바꾸는 trait
- synergy 조건부 trait
- rarity tier가 있는 trait
- town/event로 획득되는 추가 trait

## 밸런스 기준

- positive trait는 class/race identity를 완전히 덮어쓰면 안 된다.
- negative trait는 함정 픽만 양산하면 안 된다.
- trait 하나만으로 역할이 완결되면 안 되고, 장비/augment와 결합될 때 역할이 선명해져야 한다.
