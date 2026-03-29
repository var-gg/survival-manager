# ADR-0004 콘텐츠 파이프라인 경계 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0004-content-pipeline-boundary.md`
- 관련문서:
  - `docs/03_architecture/content-pipeline.md`
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/05_setup/asset-workflow.md`

## 문맥

scene-first 작업 흐름은 숨겨진 상태와 merge 위험을 만들고, 저장소가 커질수록 AI가 안전하게 다루기 어려워진다.
콘텐츠 추가를 구조적으로 reviewable하게 유지할 경계가 필요하다.

## 결정

다음 콘텐츠 파이프라인 경계를 채택한다.

- `ScriptableObject` 중심 authored content를 기본 authoring 모델로 둔다.
- 프로젝트 확장 기본 경로는 data asset, prefab, settings asset이다.
- scene 편집은 기본 경로가 아니라 예외적이고 좁은 작업으로 취급한다.
- 위험한 콘텐츠 변경은 sandbox나 검증 단계를 먼저 거친다.

## 결과

### 기대 효과

- 콘텐츠 변경이 diff 친화적으로 바뀐다.
- scene drift와 숨은 연결이 줄어든다.
- AI가 구조를 덜 깨뜨리면서 콘텐츠를 늘릴 수 있다.

### 감수할 비용

- 초기 authoring 규칙과 validator 비용이 필요하다.
- 일회성 작업도 파이프라인 규칙을 지켜야 한다.

## 후속

- asset workflow와 validator 규칙을 단계적으로 보강한다.
- scene 편집이 필요한 예외 조건을 별도 운영 문서에 명시한다.
