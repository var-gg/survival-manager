# 코딩 원칙

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/coding-principles.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/bounded-contexts.md`
  - `docs/04_decisions/adr-0012-code-structure-and-dependency-policy.md`

## 목적

이 문서는 AI가 기능 해결만 보고 구조를 망치지 않게 하는 기본 코딩 원칙의 소스오브트루스다.
판단 기준은 이 저장소의 `SM.*`, content/runtime/persistence/Unity adapter 분리에 직접 연결되어야 한다.

## 비목표

- C# 문법 일반론을 다시 설명하지 않는다.
- 성능 미세 최적화 규칙을 정의하지 않는다.
- 디자인 규칙이나 밸런스 규칙을 대체하지 않는다.

## 클래스와 함수 책임 규칙

- 클래스 하나는 한 종류의 truth만 소유한다.
- `SM.Combat` 클래스는 전투 규칙을, `SM.Persistence.*` 클래스는 저장 계약/직렬화를, `SM.Unity` 클래스는 orchestration과 표시를 담당한다.
- 한 클래스가 domain rule + persistence + UI + scene orchestration을 동시에 가지면 분리 신호다.
- 메서드가 I/O와 핵심 규칙 계산을 동시에 수행하면 단계 분리를 검토한다.
- 함수 이름이 `And`, `Orchestrate`, `HandleEverything` 같은 접속 의미를 품기 시작하면 책임이 섞였는지 확인한다.

## 기존 파일을 확장할 때의 판정 규칙

기존 파일을 확장해도 되는 조건은 아래를 모두 만족할 때다.

- 추가하려는 로직이 현재 파일의 책임 축과 같다.
- 같은 asmdef와 같은 계층 안에서 끝난다.
- public surface를 새 추상화 하나로 더 쪼갤 필요가 없다.
- Unity, persistence, content, domain 중 다른 truth를 끌어오지 않는다.

다음 중 하나라도 해당하면 새 파일을 우선 검토한다.

- 기존 파일에 두 번째 public abstraction이 생긴다.
- 같은 파일 안에 domain rule과 adapter code가 함께 들어간다.
- 새 로직 때문에 클래스 이름을 더 일반적인 이름으로 바꿔야 한다.
- 같은 파일에 private helper가 계속 늘어나며 섹션 주석이 여러 개 필요해진다.

## 새 파일을 만들 때의 판정 규칙

새 파일은 아래 상황에서 기본 선택이다.

- 새 책임이 기존 클래스의 생명주기와 다르다.
- 다른 계층으로 테스트하거나 교체하고 싶다.
- 동일 파일에서 public type이 둘 이상 필요해진다.
- 하나의 파일이 입력 해석, 핵심 규칙, 출력 변환을 모두 다루기 시작한다.

파일이 250~350줄을 넘는다고 바로 분리하지는 않는다.
다만 다음 중 둘 이상이면 분리 신호로 본다.

- public method가 7개 이상
- private field가 10개 이상
- constructor dependency가 4개 이상
- region/주석 블록으로 책임 구획을 인위적으로 나누고 있음

## 파일 분리 트리거

- `Manager`, `Helper`, `Util`, `Common` 같은 쓰레기통 이름이 아니면 이름을 붙이기 어려운 경우
- public abstraction이 둘 이상인 경우
- 파일 안에 `SM.Combat` 규칙과 `SM.Unity` 참조가 함께 있는 경우
- 파일 하나가 authored definition, runtime state, save model을 모두 아는 경우
- 테스트를 작성하려면 Unity object를 억지로 만들어야 하는 경우

## interface / abstract class 사용 기준

interface는 아래 중 하나가 있을 때만 도입한다.

- 외부 경계가 있다. 예: `SM.Persistence.Abstractions`
- 실제 대체 구현이 둘 이상 있다. 예: 런타임 JSON adapter와 테스트 fake
- 테스트 이점이 실제로 있다. 예: 무거운 I/O 경계를 가짜로 바꾸고 싶다.

다음 이유만으로는 interface를 만들지 않는다.

- “언젠가 바꿀 수도 있으니까”
- 구현이 하나뿐인데 있어 보이기 위해
- private helper 호출을 감추기 위해

abstract class는 shared invariant와 템플릿 메서드가 명확할 때만 사용한다.
공통 필드 복사를 위해 얕은 상속 계층을 늘리는 용도로 쓰지 않는다.

## composition over inheritance 규칙

- 기본 선택은 작은 협력 객체 조합이다.
- 상속은 Unity lifecycle hook을 따르거나, 안정된 템플릿 알고리즘을 강제할 때만 허용한다.
- domain 계층에서 3단계 이상 상속 계층이 보이면 구조 재검토 신호다.
- 공통 동작이 필요하면 base class보다 조합 가능한 정책 객체나 value object를 먼저 검토한다.

## state / immutability / DTO / value object 기준

- 핵심 규칙 입력과 출력은 가능하면 immutable하게 만든다.
- runtime mutable state는 소유 aggregate나 owning service 안에서만 바뀌게 한다.
- DTO는 계층 경계 전달용으로만 사용하고, 규칙 메서드를 넣지 않는다.
- value object는 id, tag, stat key처럼 불변식이 중요한 값에 우선 적용한다.
- save model은 runtime instance와 별도 타입으로 둔다.
- `ScriptableObject` authored definition을 runtime mutable state 저장소로 쓰지 않는다.

## 금지 안티패턴

- `BattleManager`, `GameManager`, `CommonHelper` 같은 거대한 god file
- `static` mutable global state로 세션 truth를 저장하는 방식
- presentation 계층이 battle truth나 save truth를 직접 생성하는 방식
- interface를 모든 service 앞에 기계적으로 붙이는 방식
- 하위 구현 세부를 상위 계층 public API로 새게 만드는 방식
- scene script에서 직접 DB 호출, JSON 직렬화, 규칙 계산을 함께 처리하는 방식
- 편의상 enum 하나로 모든 콘텐츠 identity를 조기 고정하는 방식

## 이 저장소에 맞는 예시

### 좋은 예

- `SM.Combat`에서 피해 계산과 타겟 선택을 끝내고 `SM.Unity.BattleScreenController`는 결과를 재생만 한다.
- `SM.Persistence.Abstractions`에 save contract를 두고 `SM.Persistence.Json`과 테스트 fake가 구현을 나눈다.
- `SM.Editor.Validation`이 콘텐츠 무결성을 검사하고 런타임은 검증 결과를 소비한다.

### 나쁜 예

- `SM.Unity.SceneFlowController`가 보상 계산, 저장, scene 전환을 한 메서드에서 모두 처리한다.
- `RewardHelper`가 reward rule, UI label, save payload shape를 한 파일에서 다룬다.
- `ICombatService`, `ICombatCalculator`, `ICombatResolver`를 모두 만들었지만 실제 구현은 하나뿐이다.
