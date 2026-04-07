# 에셋 intake 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/03_architecture/asset-intake-boundary.md`
- 관련문서:
  - `docs/04_decisions/adr-0007-thirdparty-asset-policy.md`
  - `docs/05_setup/asset-workflow.md`
  - `docs/03_architecture/battle-actor-wrapper-and-asset-intake-seam.md`

## 목적

이 문서는 서드파티 에셋 도입 시 프로젝트 소유 영역과 벤더 원본 영역의 경계를 정의한다.

## 핵심 규칙

- `Assets/ThirdParty/**`는 벤더 원본 보호 구역이다.
- 프로젝트 소유 wrapper, adaptation, integration asset은 `Assets/_Game/**`에 둔다.
- 벤더 원본을 참조하기 쉽게 만들려고 `_Game` 쪽으로 이동시키지 않는다.
- battle actor는 `BattleActorWrapper` prefab으로만 production path에 들어온다.
- vendor socket이 부족하면 `_Game` proxy transform + `BattleSocketFollower`로 보완한다.
- vendor HUD, camera, combat runtime을 truth로 승격하지 않는다.

## 금지 사항

- `Assets/ThirdParty/**` 원본 직접 수정
- 벤더 스크립트에 프로젝트 gameplay logic 직접 병합
- 예제 scene을 production content처럼 사용하는 방식

## 권장 대안

- `_Game` 아래 wrapper prefab 작성
- `_Game/Scripts` 아래 adapter/bridge script 작성
- 프로젝트 소유 `ScriptableObject` catalog나 설정 asset으로 연결
- sandbox scene이나 integration harness에서 먼저 검증
- battle intake는 `BattleActorPresentationCatalog` + `BattleAssetIntakeSandbox` 조합으로 먼저 검증

## 사람 리뷰가 필요한 경우

- 라이선스나 사용 권리 불명확
- 기본 사용만으로도 원본 수정이 필요한 에셋
- 전역 프로젝트 설정을 바꾸는 plugin import
- 렌더링 파이프라인 호환성 변경
- wrapper 바깥에서 scene singleton이나 camera rig takeover가 필요한 에셋
