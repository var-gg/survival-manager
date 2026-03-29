# ADR-0005 데이터 주도 콘텐츠 방향 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 결정일: 2026-03-29
- 소스오브트루스: `docs/04_decisions/adr-0005-data-driven-content.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/data-model.md`
  - `docs/03_architecture/coding-principles.md`

## 문맥

전술 규칙, 특성, augment, item, reward를 모두 scene script나 거대한 switch로 처리하면 vertical slice 이후 확장이 어려워진다.
콘텐츠 수가 늘어도 구조가 버틸 수 있는 데이터 주도 경계가 필요하다.

## 결정

다음 방향을 채택한다.

- `Definition`과 `Instance`를 분리한다.
- gameplay content는 Unity asset을 우선 authoring 수단으로 사용한다.
- `Stat`, `Modifier`, `Trigger`, `Condition`, `Effect`, `Reward`를 주요 확장 seam으로 취급한다.
- stat 체계를 거대한 enum으로 조기 고정하지 않고 stable id와 definition 기반 접근을 우선한다.
- `Condition`, `Effect` 같은 다형 규칙은 giant switch보다 node 기반 데이터 구조를 우선 검토한다.

## 결과

### 기대 효과

- 작은 콘텐츠 추가가 코드 대수술로 번지지 않는다.
- 검증과 테스트 지점을 더 명확히 잡을 수 있다.
- scene logic 비대화를 줄인다.

### 감수할 비용

- authoring과 validation 복잡도가 빨리 올라온다.
- runtime translation 계층이 필요해진다.
- stable id 규율이 필수다.

## 후속

- authored asset은 프로젝트 소유 content 폴더에 둔다.
- giant switch는 최후 수단으로만 허용한다.
