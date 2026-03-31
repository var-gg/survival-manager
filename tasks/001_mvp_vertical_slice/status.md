# 작업 상태: 001 MVP Vertical Slice

- 상태: 진행 중
- 최종수정일: 2026-03-31
- 단계: prototype
- 작업 ID: 001

## 먼저 실행할 메뉴

- `SM/Bootstrap/Prepare Observer Playable`

## 이 문서의 역할

- playable boundary, 현재 리스크, 다음 우선순위에 대한 단일 live source of truth
- `docs/06_production/**` 문서는 이 상태 문서를 요약하거나 보조하는 역할만 가진다.

## 관련 운영 계약

- 큰 Unity migration이나 catalog closure 작업은 `tasks/004_launch_floor_catalog_closure/status.md`를 parent live state로 참고한다.
- task shaping, loop budget, validator-first 규칙은 `docs/03_architecture/unity-agent-harness-contract.md`를 따른다.
- asmdef/persistence ownership은 `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`를, validator/evidence 규칙은 `docs/03_architecture/validation-and-acceptance-oracles.md`를 우선한다.

## 현재 검증된 playable 경계

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- Town `Quick Battle`로 `Town -> Battle -> Reward -> Town` smoke 가능
- Town `Debug Start`는 진행 중 원정을 이어서 `Expedition`으로 복귀 가능

## 현재 구현 상태

- Battle은 `resolve once -> replay`가 아니라 fixed-step live simulation으로 동작한다.
- Battle setup은 scene-local 하드코딩 테이블이 아니라 canonical content lookup + `BattleSetupBuilder`를 통해 구성된다.
- 4인 배치는 3x2 anchor 버튼 UI로 조정되며, team posture 선택이 Town/Expedition 양쪽에 노출된다.
- trait / item / affix / temporary augment modifier가 실제 전투 세팅으로 전달된다.
- recruit cost `3 Gold`, reroll cost `1 Gold`, town roster cap `12`가 세션 로직에서 강제된다.

## 지금 바로 보이는 화면

- Town operator UI
- Expedition 5노드 branching UI
- Battle observer UI
- Reward 3카드 UI

## 아직 남은 리스크

- Battle 연출은 readable observer 단계이며 high-fidelity animation, VFX, camera polish 단계는 아니다.
- canonical sample content는 현재 `Assets/Resources/_Game/Content/Definitions/**` 계약을 전제로 하며, 일부 editor 경로는 raw asset fallback에 의존한다.
- Expedition은 고정 5노드 그래프 단계라 procedural depth와 장기 메타 다양성은 아직 부족하다.
- operator UI는 placeholder UGUI 품질이며 최종 UX 기준과는 거리가 있다.
- Unity MCP console에는 환경에 따라 `UnityCliTools` 관련 로그가 남을 수 있다.

## 다음 우선순위

1. Battle camera / floor / hit timing / readability polish
2. Expedition graph와 node effect 종류 확장
3. reward-to-item / permanent progression / combat feedback 연결 강화
