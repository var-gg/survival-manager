# Town character sheet UI

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-13
- 소스오브트루스: `docs/02_design/ui/town-character-sheet-ui.md` (정보 구조 / field closure)
- 관련문서:
  - `docs/02_design/deck/launch-core-roster-sheet.md`
  - `docs/02_design/meta/town-and-expedition-loop.md`
  - `docs/03_architecture/town-character-sheet-contract.md`
  - **비주얼 시안 SoT**: `pindoc://town-ui-ux-시안-갤러리-v1-gallery-town-ui-mockups-v1`
  - **컴포넌트 카탈로그**: `pindoc://analysis-town-ui-component-system-v1`

## 목적

이 문서는 Town에서 플레이어가 보는 character sheet의 **정보 구조(IA)**를 고정한다.
스타일보다 IA와 field closure를 우선한다. **비주얼 톤 / 레이아웃 / 일러스트 기준은
pindoc 시안 갤러리(위 SoT)를 본다** — claude.ai/design 핸드오프 v0.2~v0.6의
HTML/CSS는 시각 톤 mismatch로 deprecated.

## 기본 원칙

- Town sheet는 readonly다.
- action row는 기존 `Cycle Hero / Refit / Retrain / Passive / Permanent` 버튼을 유지한다.
- sheet는 현재 선택 hero 하나만 보여 준다.
- locale 전환 시 panel title과 content label은 같은 refresh 루프에서 다시 그린다.

## 5-panel 구조

### 1. Overview

- character / archetype 이름
- race / class
- role / role family
- positive trait / negative trait
- current posture
- current tactic

### 2. Loadout

- weapon
- armor
- accessory
- basic attack
- signature active
- signature passive
- current flex active
- current flex passive

### 3. Passives

- passive board
- active node 목록
- highlighted node
- active node count
- keystone active 여부

### 4. Synergy

- current squad member count
- selected hero race family breakpoint
- selected hero class family breakpoint
- expected synergy family
- declared counter hints
- soft weakness note

### 5. Progression

- recruit tier / source
- retrain state
- retrain cost rail
- dismiss refund
- refit preview
- equipped blueprint permanent
- unlocked permanent candidate 목록
- passive progression 요약

## empty state

- hero가 선택되지 않으면 5개 panel 모두 같은 empty-state copy를 쓴다.
- empty-state는 "hero를 선택하면 Town build / passive / synergy / progression을 볼 수 있다"는 안내만 한다.

## Town 화면 배치

- character sheet 5 panel은 Town 우측 column 상단에 고정한다.
- deploy panel은 character sheet 아래에 유지한다.
- Town sheet는 roster/recruit/build panel과 병렬로 보이는 정보 요약면이지, 별도 modal이 아니다.

## acceptance

- selected hero가 바뀌면 5 panel이 모두 동시에 갱신된다.
- locale flip 시 title/body가 stale 상태로 남지 않는다.
- Town action row만으로 sheet에서 말하는 field를 바꿀 수 있어야 한다.
