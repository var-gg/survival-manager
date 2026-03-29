# ADR-0003 Unity 프로젝트 구조 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0003-unity-project-structure.md`
- 관련문서:
  - `docs/03_architecture/unity-project-layout.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/asset-intake-boundary.md`

## 문맥

Unity 구조가 scene 중심 즉흥 편집이나 벤더 혼합 구조로 흐르면, 리뷰 가능성과 소유권 경계가 빠르게 무너진다.
특히 AI가 반복적으로 수정하는 저장소에서는 구조 자체가 안전 장치 역할을 해야 한다.

## 결정

다음 프로젝트 구조를 공식 채택한다.

- `Assets/_Game/**`는 프로젝트 소유 게임 구현, authored content, wrapper, prefab, integration asset을 둔다.
- `Assets/ThirdParty/**`는 벤더 원본을 둔다.
- 일반 확장 경로는 scene 직접 수정이 아니라 data asset, prefab, 설정 asset 우선으로 둔다.
- 주요 gameplay domain은 asmdef와 폴더 구조 수준에서 분리 가능해야 한다.

## 결과

### 기대 효과

- 소유권과 수정 책임이 분명해진다.
- AI가 만든 변경의 리뷰 표면이 좁아진다.
- 벤더 원본 오염 위험을 줄인다.

### 감수할 비용

- wrapper와 초기 설정 비용이 늘어난다.
- 구조를 의식적으로 지키지 않으면 바로 어긋난다.

## 후속

- 구조 상세는 `unity-project-layout.md`와 `dependency-direction.md`에서 유지한다.
- 서드파티 intake 예외는 별도 ADR이나 정책 문서로만 허용한다.
