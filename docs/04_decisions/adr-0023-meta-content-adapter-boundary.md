# ADR-0023 Meta content adapter boundary

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-19
- 소스오브트루스: `docs/04_decisions/adr-0023-meta-content-adapter-boundary.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/unity-project-layout.md`
  - `tasks/019_meta_session_boundary_refactor/status.md`

## 문맥

prototype phase에서 전투 core는 `SM.Core`/`SM.Combat` 중심으로 분리되어 있었지만, meta/session 경계에는 authored content와 runtime rule이 섞여 있었다. 특히 `SM.Meta`가 `SM.Content`를 참조하고 engine reference를 허용하면, story/reward/passive/encounter rule을 검증할 때 Unity asset pipeline이 함께 끌려 들어온다.

이 상태는 AI와 사람이 작은 기능을 고칠 때도 `GameSessionState`, `Resources.LoadAll`, ScriptableObject definition, batch/editor loop를 동시에 고려하게 만들어 iteration cost를 키운다.

## 결정

`SM.Meta`는 authored `ScriptableObject`와 `SM.Content`를 직접 참조하지 않는다.

구체적으로:

- content schema enum은 `SM.Core.Content`가 소유한다.
- authored definition은 `SM.Content`가 소유한다.
- authored definition을 runtime rule에 넣는 변환은 `SM.Unity.ContentConversion` 또는 bootstrap이 수행한다.
- `SM.Meta` 서비스는 `CombatContentSnapshot`, story/dialogue spec, reward/loot/passive template 같은 pure model만 받는다.
- `SM.Meta`와 `SM.Meta.Serialization`은 `noEngineReferences: true`로 유지한다.
- `SM.Meta`의 `*Record` 이름은 이번 단계에서 보존하되 immutable domain/read model로만 해석한다.
- persistence contract ownership은 `SM.Persistence.Abstractions.Models`에 둔다.

`GameSessionState`는 public constructor와 기존 public facade method 이름을 유지한다. 새 규칙, 정산, 상태전이는 본 파일에 직접 누적하지 않고 `SM.Meta` service 또는 `SM.Unity/Session` 하위 흐름 파일로 보낸다.

## 검토한 대안

| option | description | pros | cons | verdict |
| --- | --- | --- | --- | --- |
| `option_a_keep_meta_content_ref` | `SM.Meta`가 `SM.Content` definition을 계속 받는다 | 변경량이 가장 작음 | Unity-bound authoring model이 domain rule에 남고 batch/editor loop 비용이 지속됨 | reject |
| `option_b_move_schema_to_core` | 공유 enum은 `SM.Core.Content`, conversion은 Unity boundary에 둔다 | asmdef cycle 없이 authored/runtime 양쪽이 같은 value를 쓴다 | 변환 adapter와 pure spec 타입이 늘어남 | accept |
| `option_c_move_lookup_to_meta_now` | `ICombatContentLookup`까지 Meta 쪽으로 이동한다 | Unity facade가 더 얇아질 수 있음 | 현재 content/resource lookup과 snapshot assembly 책임까지 한 번에 흔들림 | defer |
| `option_d_rename_all_records` | Meta `*Record`를 domain state명으로 대량 rename한다 | 이름 혼동을 줄임 | save compatibility와 diff 규모가 커짐 | defer |

## 결과

채택 구조의 장점:

- `SM.Meta`가 Unity/editor/content asset pipeline 없이 compile/test 가능한 pure layer가 된다.
- story/reward/passive/encounter rule test가 ScriptableObject authoring과 분리된다.
- asmdef guard로 `SM.Meta -> SM.Content` 재퇴행을 자동으로 잡는다.
- `GameSessionState.cs` 본 파일 line budget이 구조 guard에 들어간다.

감수할 비용:

- `SM.Core.Content`가 authored/runtime 공유 enum을 소유하므로 schema 추가 시 더 보수적인 검토가 필요하다.
- conversion adapter가 늘어나고, 누락 필드는 pure spec/template에 명시적으로 추가해야 한다.
- `GameSessionState` 내부 흐름은 아직 완전한 독립 service 객체가 아니라 session 하위 파일로 분리된 단계다.

## 후속 작업

1. `SM.Meta`의 `*Record` naming을 domain/read-model 기준으로 점진 정리
2. `ICombatContentLookup` 이동 여부 재검토
3. `GameSessionState` session flow를 독립 service 객체로 추가 분리

## 작성 지침

- authored definition, runtime model, persistence contract를 한 문장 안에서 섞지 않는다.
- 새 content schema enum을 추가할 때는 `SM.Core.Content` 소유 여부와 `SM.Content` authoring ownership을 같이 적는다.
