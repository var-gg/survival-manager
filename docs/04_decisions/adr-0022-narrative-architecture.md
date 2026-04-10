# ADR-0022 내러티브 아키텍처

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/04_decisions/adr-0022-narrative-architecture.md`
- 관련문서:
  - `docs/03_architecture/narrative-code-architecture.md`
  - `docs/02_design/narrative/index.md`
  - `docs/02_design/meta/story-gating-and-unlock-rules.md`

## 문맥

현재 저장소는 prototype phase이며 MVP 12영웅 / 3종족 / 4클래스 / 3챕터로 출발했다. 내러티브/캠페인/영웅 확장 설계를 진행하면서, story definitions, runtime state, presentation이 기존 asmdef 의존방향을 깨지 않으면서 어디에 배치되어야 하는지 결정이 필요했다.

주요 제약:
- 기존 `SM.Core -> SM.Content -> SM.Meta -> SM.Persistence.Abstractions -> SM.Unity` 의존방향 유지 필수
- `static` mutable global state 금지 (Enter Play Mode Options 활성화)
- presentation 계층이 battle/save truth를 직접 생성하면 안 됨
- 인디 규모에서 풀 컷씬 엔진/branching dialogue는 유지비 과다
- 선형 캠페인 v1에서는 과도한 branching runtime이 불필요

## 결정

**definitions in SM.Content, runtime state in SM.Meta, save host in SM.Persistence.Abstractions, UI playback in SM.Unity.**

구체적으로:
- `NarrativeMoment`, `StoryOncePolicy` 등 enums는 `SM.Core`
- `StoryEventDefinition`, `ChapterBeatDefinition` 등 authored data는 `SM.Content`
- `StoryDirectorService`, `NarrativeProgressRecord` 등 runtime state/service는 `SM.Meta`
- 기존 root save에 `Narrative` 필드 추가 (새 save 파일 없음)
- 현재 runtime bootstrap은 `SM.Unity.NarrativeRuntimeBootstrap` + `GameSessionState`가 담당한다
- scene bridge / presenter / runner는 `SM.Unity`에 두되 truth는 여전히 `StoryDirectorService`가 가진다
- 연출 kind는 `toast-banner`, `dialogue-overlay`, `dialogue-scene`, `story-card` 4단으로 고정한다

## 검토한 대안

| option | description | pros | cons | verdict |
| --- | --- | --- | --- | --- |
| `option_a_all_in_unity` | presenter/controller가 story logic까지 소유 | 구현이 빨라 보임 | truth/presentation 혼합, EditMode 테스트 불가, 의존방향 위반 | reject |
| `option_b_content_meta_split` | definition/state/presentation 분리 | 의존방향 보존, save/test 용이, SoT 명확 | 초기 문서화 비용, 타입 수 증가 | **accept** |
| `option_c_branching_engine` | 범용 branching dialogue runtime | 분기 표현력 높음 | v1은 선형 트랙, 유지비 과다, 인디 규모 초과 | reject |
| `option_d_separate_save` | narrative 전용 save 파일 분리 | narrative 독립 rollback 가능 | 기존 save와 sync 문제, atomic save 보장 어려움 | reject |

## 결과

채택 구조의 장점:
- 기존 asmdef 의존방향을 100% 유지한다.
- EditMode에서 `StoryDirectorService`를 단독 테스트할 수 있다.
- presentation skip/crash 시에도 narrative truth가 보존된다.
- save root에 `Narrative`만 추가하면 legacy load normalization이 단순하다.

감수할 비용:
- 초기에 enum, definition, record, service 타입을 한꺼번에 추가해야 한다.
- `PresentationKey`만 가진 queue payload를 실제 UI model로 해석하는 adapter를 `SM.Unity`에 별도로 유지해야 한다.
- branching dialogue를 나중에 추가하려면 `StoryEventDefinition` 확장이 필요하다.

## 후속 작업

1. narrative seed content와 validator 추가
2. `RewardCommitted` 시점의 authored trigger 연결
3. concrete battle/reward summary type canonicalization

## 작성 지침

- 감정적 표현보다 기술적 trade-off를 우선한다.
- "왜 이 대안을 기각했는가"를 최소 2개는 남긴다.
- 실제 타입명과 문서 path를 사용한다.
