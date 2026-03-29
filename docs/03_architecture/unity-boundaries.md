# Unity 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/unity-boundaries.md`
- 관련문서:
  - `docs/03_architecture/coding-principles.md`
  - `docs/03_architecture/unity-scene-flow.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/04_decisions/adr-0012-code-structure-and-dependency-policy.md`

## 목적

이 문서는 Unity 특화 타입과 scene이 어디까지 책임질 수 있는지 정의한다.
핵심은 `MonoBehaviour`와 `ScriptableObject`가 편한 진입점이더라도 battle truth, save truth, domain rule을 삼키지 못하게 막는 것이다.

## `MonoBehaviour` 기준

- 기본 책임은 orchestration, view binding, input forwarding이다.
- scene graph, animation, UI 갱신, lifecycle hook 연결을 담당한다.
- 전투 규칙 계산, 보상 확정, save 직렬화는 담당하지 않는다.

## `ScriptableObject` 기준

- authored definition, 설정값, catalog entry를 담는 용도로 쓴다.
- runtime mutable state 저장소로 쓰지 않는다.
- save payload를 직접 품지 않는다.

## runtime instance와 save model 분리

- runtime instance는 플레이 도중 바뀌는 메모리 상태다.
- save model은 직렬화와 마이그레이션을 위한 별도 타입이다.
- presentation cache나 view model이 save truth를 대신해서는 안 된다.

## Boot scene과 composition root

- Boot scene은 runtime composition root다.
- `GameBootstrap` 같은 entrypoint가 세션 루트, persistence entrypoint, scene flow를 조립한다.
- 다른 scene은 이미 조립된 세션을 소비하는 쪽이어야 한다.

## scene script 얇게 유지 규칙

- scene script는 가능한 한 “읽고, 넘기고, 표시하고, 전환하는” 역할로 제한한다.
- scene script가 두 개 이상의 domain 서비스를 새로 조립하면 두꺼워진 신호다.
- scene object 참조를 편하게 쓰기 위해 규칙 계산을 끌어오지 않는다.

## presentation은 truth를 만들지 않는다

- battle truth는 `SM.Combat`에서 만든다.
- save truth는 `SM.Persistence.Abstractions` 뒤에서 만든다.
- presentation은 outcome, read model, view state를 소비할 뿐 확정 truth를 만들지 않는다.

## 금지 규칙

- `Assets/ThirdParty/**` 직접 수정 금지
- scene-first hack을 기본 경로로 채택 금지
- `MonoBehaviour` 안에서 JSON/DB I/O와 규칙 계산을 동시에 수행하는 방식 금지
- `ScriptableObject`를 session singleton처럼 사용하는 방식 금지

## AI가 흔히 만드는 잘못된 예와 바른 대안

### 잘못된 예

- `BattleScreenController`가 클릭 이벤트 안에서 피해 계산과 보상 저장을 한 번에 처리한다.
- `RewardDefinition` `ScriptableObject`에 “선택됨” 같은 runtime flag를 직접 기록한다.
- Battle scene에 편하다는 이유로 전투 규칙 테이블을 inspector 배열로만 유지한다.

### 바른 대안

- controller는 입력을 domain/service 호출로 넘기고 결과만 재생한다.
- reward 선택 상태는 runtime state와 save model로 분리한다.
- 전투 규칙은 `SM.Combat` 또는 `SM.Content` 정의로 유지하고 scene은 참조만 한다.
