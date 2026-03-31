# 전투 공간 계약

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/combat/combat-spatial-contract.md`
- 관련문서:
  - `docs/02_design/combat/deployment-and-anchors.md`
  - `docs/02_design/combat/combat-readability.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-content-mapping.md`

## 목적

placeholder capsule 단계에서도 blob이 아니라 읽히는 footprint와 engagement 규칙을 정의한다.

## 원칙

- visual size는 gameplay footprint truth가 아니다.
- mesh bounds와 collider 크기로 spacing을 결정하지 않는다.
- authored footprint profile만 바꾸면 placeholder, 인간형, 거대형을 같은 규칙으로 다룰 수 있어야 한다.

## footprint 용어

- `navigationRadius`: 이동과 leash 계산의 기본 반경
- `separationRadius`: 같은 팀 분리 보정 반경
- `combatReach`: 실제 공격 접촉 거리
- `preferredRangeMin`, `preferredRangeMax`: 이 유닛이 유지하고 싶은 전투 거리 밴드
- `engagementSlotCount`: 한 target 주변에 읽히게 붙을 수 있는 근접 슬롯 수
- `engagementSlotRadius`: target 주변 slot ring 반경
- `bodySizeCategory`: `Small`, `Medium`, `Large`
- `headAnchorHeight`: overhead UI anchor 높이

## slotting 규칙

- 근접 또는 short-range actor는 target pivot이 아니라 slot ring을 향해 접근한다.
- slot이 넘치면 무한히 같은 점에 겹치지 않고 outer ring 또는 standby position을 사용한다.
- slotting은 nav avoidance를 대체하지 않지만, blob 방지의 truth는 slotting/occupancy다.

## spacing 규칙

- 같은 팀 분리 최소값은 `left.separationRadius + right.separationRadius`다.
- large footprint는 mesh 교체와 무관하게 separation을 더 크게 가져간다.
- range band 안에 있어도 reserved slot에 아직 못 들어갔으면 `SecurePosition`으로 남는다.

## acceptance

- 동일 target을 치는 근접 유닛이 한 점에 무한히 겹치지 않는다.
- large footprint placeholder도 separation을 유지한다.
- ranged/mage는 melee와 다른 distance band를 유지한다.
