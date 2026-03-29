# 03 아키텍처

## 목적

이 폴더는 기술 구조, 코딩 경계, 의존 방향, Unity 특화 제약을 정의한다.

## 문서 분리 기준

- 전체 구조 요약은 `technical-overview.md`
- bounded context 책임 구분은 `bounded-contexts.md`
- 코드 작성 기본 원칙은 `coding-principles.md`
- asmdef/context/layer 의존 방향은 `dependency-direction.md`
- Unity 특화 경계는 `unity-boundaries.md`
- 데이터 정의/런타임/save 분리는 `data-model.md`
- 콘텐츠 authoring 규칙은 `content-authoring-model.md`
- Unity 프로젝트 폴더/asmdef 배치는 `unity-project-layout.md`
- scene 흐름과 composition root는 `unity-scene-flow.md`
- battle replay adapter 계약은 `battle-replay-model.md`
- persistence 저장 경계는 `persistence-strategy.md`, `persistence-schema.md`
- 콘텐츠 파이프라인과 로딩은 `content-pipeline.md`, `content-loading-contract.md`, `content-loading-strategy.md`
- 테스트 전략은 `testing-strategy.md`
- 서드파티 에셋 intake 경계는 `asset-intake-boundary.md`

## 현재 기준 문서

- `technical-overview.md`: 현재 구현과 문서 체계를 연결하는 상위 개요
- `coding-principles.md`: AI가 구조를 무너뜨리지 않기 위한 코딩 원칙
- `dependency-direction.md`: 어셈블리/계층 의존 허용·금지 규칙
- `unity-boundaries.md`: `MonoBehaviour`, `ScriptableObject`, scene 책임 규칙
- `bounded-contexts.md`: `SM.*` context 책임 분리 기준
- `data-model.md`: authored definition, runtime instance, save model 분리 기준
- `content-authoring-model.md`: 콘텐츠 authoring/validation 경계
- `unity-project-layout.md`: 실제 폴더와 asmdef 배치
- `unity-scene-flow.md`: Boot 중심 scene flow 규칙
- `battle-replay-model.md`: resolve once -> replay track -> observer presentation 계약
- `persistence-strategy.md`: persistence adapter 경계
- `persistence-schema.md`: 저장 대상과 비대상
- `content-pipeline.md`: 콘텐츠 intake와 파이프라인 운영 방향
- `content-loading-contract.md`: canonical content root와 runtime/editor 계약
- `content-loading-strategy.md`: MVP content loading 계약
- `testing-strategy.md`: 저비용 검증 우선 테스트 전략
- `asset-intake-boundary.md`: `Assets/ThirdParty/**` intake boundary

## 운영 메모

구조 정책을 바꾸면 관련 기준 문서와 `docs/04_decisions/` ADR을 같이 갱신한다.
