# 아이템과 affix 시스템

- 상태: draft
- 최종수정일: 2026-03-29
- phase: prototype

## 목적

이 문서는 아이템 방향성과 MVP 범위를 정의한다.
장기 방향성은 Torchlight/ARPG식이지만, MVP는 훨씬 좁게 구현한다.

## 장기 방향성

아이템은 Torchlight/ARPG식으로 다음 감각을 목표로 한다.

- base item이 역할의 뼈대를 만든다.
- affix가 개체 차이를 만든다.
- 같은 archetype도 장비에 따라 역할이 갈린다.
- reroll / crafting이 장기적으로 빌드 파고들기를 만든다.

## MVP 구현 범위

MVP는 아래만 실제 구현 대상으로 둔다.

- base item
- 단순 affix
- gold 기반 재련/리롤

MVP에서는 아직 하지 않는다.

- 복잡한 rarity ladder
- material-based crafting
- recipe crafting
- socket/gem 시스템
- set bonus

## 역할 차별화 규칙

같은 archetype도 다음 요소 조합으로 역할이 달라져야 한다.

- trait roll
- item
- augment

아이템은 이 중 장비 축을 담당한다.

## 장비 슬롯

- weapon
- armor
- accessory

## 재련/리롤 원칙

- gold 기반만 허용
- 값싼 무한 reroll은 금지
- MVP에선 "아이템을 다듬는 느낌"만 제공
- crafting 시스템 전체를 열지는 않는다

## 장기 규칙

문서로만 남길 장기 항목:

- broader base item family
- rarity ladder
- material sink
- recipe crafting
- advanced crafting station

## 밸런스 기준

- 아이템이 trait/augment보다 너무 강하면 안 된다.
- 반대로 아이템이 너무 약해서 존재감이 없어도 안 된다.
- gold reroll은 유용하되 필수 정답이 되면 안 된다.
