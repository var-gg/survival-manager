# 제품 비전

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 `survival-manager`의 최신 제품 비전을 고정한다.
핵심은 구현 전에 어떤 규칙을 MVP에서 진짜 증명할지, 어떤 규칙을 장기 비전으로 남길지를 분리하는 것이다.

## 제품 정의

`survival-manager`는 다음 세 축을 조합한 운영형 전략 게임이다.

- Unicorn Overlord식 세팅과 행동 규칙 준비
- Teamfight Tactics식 종족/직업 시너지 압박
- Darkest Dungeon식 expedition -> 귀환 -> 재정비 루프

플레이어는 직접 손컨을 하는 것이 아니라, 세팅과 편성, 리롤, 아이템, augment, 로스터 운영으로 결과를 만든다.

## 최신 핵심 판별 기준

같은 archetype, 심지어 같은 캐릭터 계열이라도 아래 요소에 따라 다른 역할을 가져야 한다.

- trait roll
- 장비
- augment

즉 archetype은 출발점이지 최종 역할이 아니다.
플레이어는 "같은 뼈대 위에서 다른 용도"를 만드는 재미를 느껴야 한다.

## MVP 비전

MVP는 **목각인형 수준 playable vertical slice**를 증명한다.

MVP에서 증명할 핵심은 다음이다.

- 작은 town roster 안에서도 선발/탈락 압박이 생긴다.
- 4인 배치와 race/class synergy가 실제 선택 압력을 만든다.
- 같은 archetype도 trait, 장비, augment에 따라 역할이 달라진다.
- 임시 augment와 영구 augment 해금 문법이 다음 선택을 기대하게 만든다.
- expedition 종료 보상이 다음 run 준비로 자연스럽게 이어진다.

## 장기 비전

장기적으로는 다음을 확장할 수 있다.

- 더 넓은 synergy 가족
- 고급 crafting
- 더 복잡한 item rarity / affix 구조
- 더 많은 permanent augment 경로
- PVP 규칙과 전용 밸런스
- 외부 에셋 연동과 시각 품질 상승

하지만 장기 비전은 MVP 구현 범위를 오염시키면 안 된다.
MVP는 먼저 "이 조합 게임이 한 판 단위로 재미가 있는가"를 증명해야 한다.

## MVP에서 고정할 값

- battle deployment size: 4
- expedition roster size: 8
- town-held roster cap: 12
- races: 3
- classes: 4
- recruit archetypes: 8
- temporary augments: 9
- permanent augment slot: 1
- equipment slots: 3

## MVP 범위 밖에 두는 것

- PVP 실제 구현
- run 중 temporary augment를 반영하는 PVP 규칙
- 고급 crafting recipe 체계
- 복잡한 rarity ladder
- material economy 확장
- 외부 에셋 연동 완료형 파이프라인

## 성공 조건

MVP는 아래를 만족하면 성공이다.

- auto-battle 결과가 세팅의 결과로 납득된다.
- roster / synergy / trait / item / augment가 각자 의미 있는 축으로 느껴진다.
- same-archetype variance가 눈에 보인다.
- 귀환 후 다음 run 준비가 자연스럽게 이어진다.
