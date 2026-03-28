# 모집과 리롤

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 roster 성장과 reroll 규칙을 정의한다.
목표는 같은 archetype이라도 다른 역할을 가진 recruit를 고를 수 있게 하고, reroll을 가볍지 않은 전략 자원으로 만드는 것이다.

## 핵심 규칙

### recruit 차별화 규칙

recruit 후보는 최소 다음 축으로 달라진다.

- race
- class
- archetype
- positive trait
- negative trait

그리고 같은 archetype이라도 이후 아래에 따라 역할이 달라진다.

- trait roll
- 장비
- augment

### archetype trait pool 규칙

시스템 규칙으로는 각 archetype마다 다음을 정의한다.

- 좋은 특성 3개 이상
- 나쁜 특성 3개 이상

단, MVP 시드 데이터는 재사용을 허용한다.
즉 시스템 문법은 archetype별 풀을 상정하지만, 초반 데이터는 중복 재사용 가능하다.

### reroll 경제 규칙

reroll은 두 종류로 분리한다.

1. recruit candidate refresh
   - 주로 gold 기반
2. trait reroll
   - 희소 재화 기반

### trait reroll 재화 가치 규칙

trait reroll 재화는 **영구증강체와 동급 가치의 희소 재화**로 취급한다.
이는 가벼운 반복 소모 골드가 아니라, 장기적인 빌드 방향을 바꾸는 귀한 자원이다.

## MVP 구현 범위

- recruit 후보 제시
- recruit refresh
- trait reroll 재화 정의
- trait reroll을 장기적 가치 자원으로 문서화

## 장기 규칙

- town facility가 recruitment pool에 영향
- rarity tier recruit
- lineage/training 시스템
- PVP 전용 recruit source

## 밸런스 기준

- gold refresh가 너무 싸면 recruit 압박이 사라진다.
- trait reroll 재화가 흔하면 영구 progression과의 가치 축이 무너진다.
- same-archetype variance가 너무 약하면 roster 선택이 평평해진다.
- variance가 너무 과하면 계획보다 운빨이 커진다.
