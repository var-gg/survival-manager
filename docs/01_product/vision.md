# 제품 비전

- 상태: active
- 최종수정일: 2026-04-02
- 단계: prototype

## 목적

이 문서는 `survival-manager`의 최신 제품 비전을 고정한다.
핵심은 구현 전에 어떤 규칙을 MVP에서 진짜 증명할지, 어떤 규칙을 장기 비전으로 남길지를 분리하는 것이다.

## 제품 정의

`survival-manager`는 다음 세 축을 조합한 운영형 전략 게임이다.

- Unicorn Overlord식 세팅과 행동 규칙 준비
- Teamfight Tactics식 종족/직업 시너지 압박
- Darkest Dungeon식 expedition -> 귀환 -> 재정비 루프

플레이어는 직접 손컨을 하는 것이 아니라, 세팅과 편성, 리롤, 아이템, augment, 로스터 운영으로 결과를 만든다.

## V1 재미축

이 게임에서 증명해야 할 재미는 네 가지다.

1. **4인 편성 압박** — 4칸 안에서 race/class를 맞추고, front/back, protect/dive, sustain/burst를 읽는 재미.
2. **같은 archetype의 다른 용도 (same-archetype variance)** — 같은 뼈대라도 아이템, retrain, passive board, augment에 따라 역할이 달라져야 한다. archetype은 출발점이지 최종 역할이 아니다.
3. **회복 가능한 경제 압박 (recovery economy)** — Gold는 새 파워를 사는 돈, Echo는 RNG를 복구하는 돈이다. 실패해도 다음 선택이 있어야 한다.
4. **squad blueprint 정체성** — run 바깥 준비가 다음 run의 스타일을 실제로 바꾸어야 한다. 그 역할은 team posture + permanent augment가 맡는다.

이 네 가지를 강화하지 않는 시스템은 지금 단계에서는 parked다.

## MVP 비전

MVP는 **목각인형 수준 playable vertical slice**를 증명한다.

MVP에서 증명할 핵심은 다음이다.

- 작은 town roster 안에서도 선발/탈락 압박이 생긴다.
- 4인 배치와 race/class synergy가 실제 선택 압력을 만든다.
- 같은 archetype도 아이템, retrain, passive board, augment에 따라 역할이 달라진다.
- 12개의 temporary augment와 4개의 permanent augment가 run 안팎에서 서로 다른 레버로 작동한다.
- expedition 종료 보상이 다음 run 준비로 자연스럽게 이어진다.
- Town build-management(Refit, passive board, permanent augment 장착)가 squad blueprint 정체성을 만든다.

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
- recruit archetypes: 12
- synergy families: 7 (race 3 + class 4)
- synergy grammar: race `2 / 4`, class `2 / 3`
- temporary augments: 12
- permanent augments: 4
- permanent augment equip slot: 1
- passive boards: 4
- affixes: 24
- equipment slots: 3

## 유료 출시 기준 요약

현재 MVP 값은 위 항목으로 유지한다.
유료 출시 기준의 floor와 safe target 숫자는 제품 문서가 아니라 디자인 허브가 소유한다.

- launch scope hub: `docs/02_design/systems/launch-content-scope-and-balance.md`
- roster / archetype: `docs/02_design/deck/roster-archetype-launch-scope.md`
- skill taxonomy / damage model: `docs/02_design/combat/skill-taxonomy-and-damage-model.md`
- item / passive / augment budget: `docs/02_design/meta/item-passive-augment-budget.md`
- synergy / soft counter: `docs/02_design/meta/synergy-breakpoints-and-soft-counters.md`

출시 기준 safe target의 headline은 아래와 같다.

- archetypes: 16
- skills: 40~48
- equippables: 42~54
- passive nodes: 96 전후
- temporary augments: 24
- permanent augments: 12

## MVP 범위 밖에 두는 것 (parked)

- PVP 실제 구현, live matchmaking, leaderboard backend
- run 중 temporary augment를 반영하는 PVP 규칙
- 고급 crafting recipe 체계, socket/gem
- 복잡한 rarity ladder
- material economy 확장
- specialist archetype 4종 실장
- extra synergy family 추가
- story/lore/worldbuilding 확장
- 외부 에셋 연동 완료형 파이프라인

## 성공 조건

MVP는 아래를 만족하면 성공이다.

- auto-battle 결과가 세팅의 결과로 납득된다.
- roster / synergy / trait / item / augment가 각자 의미 있는 축으로 느껴진다.
- same-archetype variance가 눈에 보인다.
- 귀환 후 다음 run 준비가 자연스럽게 이어진다.
