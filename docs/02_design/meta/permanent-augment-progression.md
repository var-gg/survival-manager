# Permanent Augment Progression

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/02_design/meta/permanent-augment-progression.md`
- 관련문서:
  - `docs/02_design/meta/augment-system.md`
  - `docs/02_design/meta/economy-protection-contract.md`
  - `docs/02_design/systems/skills-items-and-passive-boards.md`
  - `docs/03_architecture/loadout-compiler-and-battle-snapshot.md`

## 목적

이 문서는 pre-art V1 permanent augment progression 문법을 고정한다.
핵심은 새 메타 시스템을 늘리는 것이 아니라, existing recruit / retrain / item / passive / reward loop 위에
`unlock many, equip one` 구조를 얹어 build thesis를 명확히 만드는 것이다.

## V1 계약

- permanent augment는 run 밖에서 유지되는 squad blueprint thesis다.
- active blueprint에는 equipped permanent augment가 항상 `0..1개`만 존재한다.
- unlock inventory는 여러 개를 가질 수 있지만, equip은 정확히 1개만 허용한다.
- equip / swap / unequip은 `Town only`이며 V1에서는 무료다.
- mid-run equip swap은 허용하지 않는다.
- permanent augment slot reward card는 normal playable lane에서 사용하지 않는다.

## unlock grammar

- temporary augment의 `첫 선택`만 permanent progression trigger로 사용한다.
- 첫 선택된 temp augment의 `FamilyId`와 같은 family를 가진 permanent augment 후보를 찾는다.
- canonical permanent set에서 같은 family counterpart는 정확히 `1개`여야 한다.
- 이미 known candidate면 추가 보상은 없다.
- new candidate면 run 종료 후 Town 복귀 시 해금하고, 자동 장착하지 않는다.
- Reward / Town summary는 `새 permanent candidate 해금`을 반드시 보여 준다.

## runtime 정규화 규칙

- `MaxPermanentAugmentSlots = 1`
- `UnlockedPermanentAugmentIds`는 slot token이 아니라 known candidate inventory로 해석한다.
- `PermanentAugmentLoadoutState.EquippedAugmentIds`는 save shape를 유지하되 runtime contract상 `0 또는 1개`만 허용한다.
- `perm-slot-*` fake token은 bind/migration 단계에서 제거한다.
- legacy `PermanentAugmentSlotCount` 값은 compatibility 용도만 남기고 gameplay progression으로 해석하지 않는다.

## player-facing surface

- Town은 현재 equipped permanent augment와 bench candidates를 함께 보여 준다.
- Town은 posture + permanent augment + selected hero hook를 묶어서 `build thesis`를 한 줄로 요약한다.
- Reward는 temporary augment card에서 현재 run hook와 potential permanent unlock을 함께 설명한다.
- Town 복귀 직후에는 방금 열린 permanent candidate를 status/build panel에서 다시 보여 준다.

## V1에서 하지 않는 것

- multi-slot permanent augment
- permanent augment branch tree
- permanent augment 전용 currency
- mid-run permanent swap
- unlock 즉시 auto-equip
