# squad blueprint와 빌드 소유권

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-01
- 소스오브트루스: `docs/02_design/systems/squad-blueprint-and-build-ownership.md`
- 관련문서:
  - `docs/01_product/vision.md`
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 deck 용어를 `squad blueprint`와 `build`로 교체하고, 영구 성장, 런 오버레이, 전투 입력의 소유권을 분리한다.

## 핵심 모델

- `SquadBlueprint`는 출전 4인, 후보 4인, team posture, team tactic, anchor 배치, hero role 지시를 가진다.
- hero build는 장비, 스킬 loadout, passive board 선택, 영구 augment 장착으로 구성된다.
- temporary augment, node 상태, pending reward는 run overlay가 소유한다.
- 전투는 항상 `compiled battle snapshot`만 소비한다.

## 소유권 규칙

- 영구 augment 해금은 프로필 공용이다.
- 영구 augment 장착은 squad blueprint 귀속이다.
- passive board는 영웅별 설정이다.
- PvP가 도입되더라도 temporary augment는 blueprint truth에 포함하지 않는다.

## MVP 기본값

- squad blueprint 장착 영구 augment 슬롯 운용값은 `1`이다.
- 데이터 모델은 슬롯 `3`까지 확장 가능해야 한다.
- 스킬 topology는 `BasicAttack / SignatureActive / FlexActive / SignaturePassive / FlexPassive / MobilityReaction`으로 고정한다.
