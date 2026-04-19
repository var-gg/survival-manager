# 의존 방향

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-19
- 소스오브트루스: `docs/03_architecture/dependency-direction.md`
- 관련문서:
  - `docs/03_architecture/coding-principles.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/unity-project-layout.md`
  - `docs/04_decisions/adr-0012-code-structure-and-dependency-policy.md`

## 목적

이 문서는 assembly, context, layer 의존 방향의 기준을 정의한다.
실제 asmdef 참조를 문서화하고, AI가 편의상 순환 참조나 상향 누수를 만들지 못하게 하는 것이 목적이다.

## 책임과 허용 의존 관계

| 어셈블리 | 책임 | 허용 의존 |
| --- | --- | --- |
| `SM.Core` | 공통 primitive, id, result, stat, rng, content schema enum | 없음 |
| `SM.Content` | ScriptableObject authored definition과 content 친화 모델 | `SM.Core` |
| `SM.Combat` | 전투 규칙과 시뮬레이션 | `SM.Core` |
| `SM.Meta` | town, expedition, reward, progression 규칙과 pure runtime spec/model | `SM.Core`, `SM.Combat` |
| `SM.Meta.Serialization` | Meta snapshot serialization helper와 pure DTO 변환 | `SM.Core`, `SM.Combat`, `SM.Meta` |
| `SM.Persistence.Abstractions` | save contract, repository port, save model | `SM.Core`, `SM.Meta` |
| `SM.Persistence.Json` | JSON serializer/repository adapter | `SM.Persistence.Abstractions`, `SM.Core`, `SM.Meta` |
| `SM.Unity` | Boot, scene/input/view orchestration | `SM.Core`, `SM.Content`, `SM.Combat`, `SM.Meta`, `SM.Persistence.Abstractions`, `SM.Persistence.Json` |
| `SM.Editor` | bootstrap, validation, editor utility | `SM.Core`, `SM.Content`, `SM.Combat`, `SM.Meta`, `SM.Meta.Serialization`, `SM.Persistence.Abstractions`, `SM.Unity` |
| `SM.Tests` | 테스트 전용 조합 레이어 | 대상 시나리오에 필요한 runtime asmdef, EditMode/EditMode.Integration은 `SM.Editor` 추가 허용 |

## 금지 의존 관계

- `SM.Core` -> 다른 어떤 상위 계층도 금지
- `SM.Content` -> `SM.Meta`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor` 금지
- `SM.Combat` -> `SM.Meta`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor` 금지
- `SM.Meta` -> `SM.Content`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor` 금지
- `SM.Meta.Serialization` -> `SM.Content`, `SM.Persistence.*`, `SM.Unity`, `SM.Editor` 금지
- `SM.Persistence.Abstractions` -> `SM.Persistence.Json`, `SM.Unity`, `SM.Editor` 금지
- `SM.Persistence.Json` -> `SM.Unity`, `SM.Editor` 금지
- `SM.Unity` -> `SM.Editor`, `SM.Tests` 금지
- production asmdef -> `SM.Tests.*` 금지

## 순환 의존 금지 규칙

- asmdef 순환 참조는 금지한다.
- namespace 분리만으로 순환을 숨기지 않는다.
- “지금 편해서” 상호 참조를 추가하지 않는다.
- 순환을 끊기 위해 필요한 shared contract는 더 낮은 계층으로 내리거나 port를 새로 만든다.

## shared enum / contract를 더 낮은 계층으로 내리는 기준

아래를 모두 만족하면 더 낮은 계층으로 내리는 것을 검토한다.

- 둘 이상의 runtime asmdef가 사용한다.
- Unity, editor, DB 구현 세부를 몰라도 된다.
- 비즈니스 불변식이 작고 안정적이다.

내리는 위치 기준은 다음과 같다.

- 정말 공통 primitive면 `SM.Core`
- save contract와 직렬화 경계면 `SM.Persistence.Abstractions`
- authored definition 해석과 id 해상도면 `SM.Content`
- authored definition과 pure runtime/model 양쪽에서 쓰는 schema enum이면 `SM.Core.Content`

## authored content adapter boundary

- `SM.Content`의 `ScriptableObject` definition은 `SM.Meta`로 직접 들어가지 않는다.
- `SM.Unity.ContentConversion` 또는 runtime bootstrap이 authored definition을 pure snapshot/spec로 변환한 뒤 `SM.Meta` 서비스에 넘긴다.
- `SM.Meta`는 `CombatContentSnapshot`, story/dialogue spec, reward/loot/passive template 같은 pure model만 소비한다.
- `Assets/_Game/Scripts/Runtime/Meta/**` 소스에서 `using SM.Content`, `UnityEngine`, `UnityEditor`를 사용하지 않는다.

## composition root 위치

- 런타임 composition root는 `Assets/_Game/Scenes/Boot.unity`와 `SM.Unity.GameBootstrap`이다.
- editor tooling composition root는 `SM.Editor.Bootstrap.*`다.
- composition root 밖에서는 concrete adapter를 새로 생성하지 말고 주입된 포트를 사용한다.

## UI runtime boundary

- `Assets/_Game/UI/**`의 `UXML` / `USS` / `PanelSettings` 자산은 `SM.Unity`만 소비한다.
- `SM.Unity.UI.*` namespace는 별도 asmdef가 아니라 `SM.Unity` 내부 하위 경계다.
- `RuntimePanelHost`가 UI backend seam을 소유하며, controller / presenter / view state는 `UIDocument`에 직접 결합하지 않는다.
- `SM.Meta`, `SM.Combat`, `SM.Persistence.*`는 `UnityEngine.UIElements`, `UXML`, `USS` asset path를 직접 참조하지 않는다.
- Battle actor-follow overlay처럼 GameObject 기반 presentation이 남아 있는 경우에도 gameplay truth와 screen shell contract를 섞지 않는다.

## 테스트 어셈블리 예외 규칙

- 문서에서는 `SM.Tests`를 테스트 어셈블리 그룹의 약칭으로 쓴다.
- 실제 asmdef는 `SM.Tests.EditMode`, `SM.Tests.EditMode.Integration`, `SM.Tests.PlayMode`다.
- EditMode와 EditMode.Integration은 editor bootstrap과 validator 확인을 위해 `SM.Editor` 참조를 허용한다.
- 현재 BatchOnly 테스트 일부는 `SM.Tests.EditMode` 루트에 category 기반으로 남아 있다.
- EditMode.Integration은 asset pipeline이나 editor validation을 더 강하게 요구하는 BatchOnly 성격의 테스트를 점진 이동할 reserved lane이다.
- PlayMode는 런타임 시나리오 검증용이므로 `SM.Editor` 참조를 기본 금지한다.
- 테스트 편의를 위해 만든 helper는 production asmdef로 올리지 않는다.
- `FastUnit`은 editor-free/resource-free/authored-object-free lane으로 유지한다. authored `ScriptableObject` fixture, `SM.Content.Definitions`, production content lookup, public session constructor coverage는 `BatchOnly`로 격리한다.

## AI가 흔히 만드는 잘못된 예와 바른 대안

### 잘못된 예

- `SM.Combat`에서 save payload를 직접 만들어 `SM.Persistence.Json`으로 넘긴다.
- `SM.Unity`가 JSON repository concrete를 직접 new 한다.
- `SM.Meta`와 `SM.Combat`이 서로 결과 타입을 직접 참조하도록 순환 구조를 만든다.

### 바른 대안

- 전투 결과는 `SM.Combat`의 순수 결과 타입으로 끝내고 저장 변환은 persistence adapter에서 한다.
- 런타임 composition root에서 `PersistenceEntryPoint` 또는 implementation factory를 조립한다.
- 공통 결과 계약이 필요하면 `SM.Core` 또는 `SM.Persistence.Abstractions`로 내린다.
