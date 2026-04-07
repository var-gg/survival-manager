# bounded context 기준

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/bounded-contexts.md`
- 관련문서:
  - `docs/03_architecture/technical-overview.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/data-model.md`

## 목적

이 문서는 MVP 기준 bounded context 책임을 정의한다.
의존 방향 자체는 `dependency-direction.md`에서 다루고, 여기서는 각 context가 무엇을 책임지는지에만 집중한다.

## context 기준

### `SM.Core`

- 공통 식별자, 결과 타입, RNG, stat/tag 같은 가장 낮은 공통 규칙
- Unity, persistence, scene 지식 금지

### `SM.Content`

- authored definition 해석에 필요한 런타임 친화 모델
- definition id, catalog 조회, content translation 진입점
- scene 상태나 저장 세부 구현 금지

### `SM.Combat`

- 전투 시뮬레이션, 타겟팅, 해상도 규칙, 전투 결과 산출
- UI 재생, scene 연출, 저장 I/O 금지

### `SM.Meta`

- town, recruitment, expedition, reward, progression 같은 상위 루프 규칙
- `SM.Combat` 결과를 소비할 수 있지만 Unity presentation truth를 만들지 않음

### `SM.Persistence`

- save model, repository contract, serializer/DB adapter
- authored asset 직접 참조나 scene truth 생성 금지

### `SM.Unity`

- Boot, input, view, scene orchestration, adapter glue
- 전투/메타/save 규칙 자체를 정의하지 않음

### `SM.Editor`

- bootstrap, content validation, editor utility
- 런타임 truth를 editor-only 코드에 숨기지 않음

## namespace 정규화 규칙

현재 저장소의 namespace 기준은 `SM.*` 계열로 통일한다.
과거 full-name namespace 제안은 더 이상 기준으로 사용하지 않는다.

권장 예시는 다음과 같다.

- `SM.Core.*`
- `SM.Content.*`
- `SM.Combat.*`
- `SM.Meta.*`
- `SM.Persistence.Abstractions.*`
- `SM.Persistence.Json.*`
- `SM.Unity.*`
- `SM.Editor.*`

## context 경계 신호

- 하나의 타입이 context 두 개 이상의 truth를 동시에 만들면 분리 신호다.
- Unity adapter가 전투 규칙을 직접 계산하면 `SM.Unity` 침범이다.
- save model이 authored asset 인스턴스를 직접 들고 있으면 persistence 경계 누수다.
