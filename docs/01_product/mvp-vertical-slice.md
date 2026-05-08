# MVP vertical slice

- 상태: draft
- 최종수정일: 2026-03-31
- 단계: prototype

## 목적

이 문서는 더 넓은 구현에 들어가기 전에 MVP vertical slice의 제품 범위를 고정한다.

## MVP 목표

다음 fantasy를 증명하는 **목각인형 수준 playable vertical slice**를 만든다.

- race/class synergy를 가진 작은 squad를 구성한다.
- squad를 expedition에 보낸다.
- 읽을 수 있는 3D auto-battle 결과를 본다.
- 다음 run을 다시 해 보고 싶을 정도의 consequence를 들고 돌아온다.

## 고정 MVP 수치

- battle deployment size: 4
- expedition roster size: 8
- town-held roster cap: 12
- races: 3
- classes: 4
- recruit archetypes: 12
- synergy families: 7 (race 3 + class 4)
- synergy grammar: race `2 / 4`, class `2 / 3`
- temporary augments: 24
- permanent augment live slot: 1 (authored candidates 12)
- permanent augment equip slot: 1
- passive boards: 4
- equipment slots per character: 3
  - weapon
  - armor
  - accessory

## 권장 MVP 콘텐츠 기반

### 권장 race

- Human
- Beastkin
- Undead

### 권장 class

- Vanguard
- Duelist
- Ranger
- Mystic

## MVP에 반드시 포함할 것

- roster selection flow
- roster에서 expedition squad를 고르는 과정
- 4개 유닛을 battle에 배치하는 과정
- 눈에 보이는 race/class synergy 가치
- 3D auto-battle 표현
- 단순하고 검증 가능한 내부 전투 resolution
- return loop consequence와 다음 선택

## MVP에서 가짜로 두거나 단순화해도 되는 것

- 제한된 encounter variety
- 최소한의 story framing
- 단순화된 town presentation
- 적은 animation 범위
- 필요한 경우 placeholder UI와 placeholder content naming

## MVP 이후 장기 비전

이후 단계에서 다음을 추가할 수 있다.

- 더 많은 race와 class
- 더 깊은 recruit identity variation
- 더 넓은 progression system
- 더 풍부한 expedition 구조
- 더 높은 audiovisual polish

하지만 이런 확장은 MVP vertical slice를 검증하는 데 필수 조건이 아니다.

## MVP 종료 기준

플레이어가 아래를 할 수 있으면 slice를 평가할 준비가 된 것으로 본다.

- roster 안의 구성을 이해한다.
- 12명 중 8명을 고르는 expedition 선택을 의미 있게 한다.
- 4-unit deployment 선택을 의미 있게 한다.
- 읽을 수 있는 combat 결과를 관전한다.
- 다음 run을 바꾸는 post-run 결정을 한다.

## 열린 질문

- 구현 압력이 커질 때도 정말 포기할 수 없는 고정 MVP 수치는 무엇인가?
- roster, synergy, augment 선택을 전달하는 최소 UI는 무엇인가?
- 첫 playable proof에서 가장 강하게 드러나야 할 post-run consequence는 무엇인가?
- 이 slice가 단순 battle sandbox와 구분되려면 최소 어느 정도의 expedition 구조가 필요한가?
