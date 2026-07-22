# 콘텐츠 authoring 모델

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-07-23
- 소스오브트루스: `docs/03_architecture/content-authoring-model.md`
- 관련문서:
  - `docs/03_architecture/data-model.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/unity-project-layout.md`
  - `docs/TESTING.md`

## 목적

이 문서는 MVP 콘텐츠를 어떻게 authored asset으로 만들고 검증할지 정의한다.
핵심은 scene 로직에 규칙을 하드코딩하지 않고도 vertical slice를 구동할 수 있게 하는 것이다.

## 기본 규칙

- 현재 prototype의 sample content 기준 저장소는 `Assets/Resources/_Game/Content/Definitions/**`다.
- 이 경로의 concrete contract는 `content-loading-contract.md`, 선택 이유는 `content-loading-strategy.md`를 따른다.
- authored definition과 runtime instance는 같은 타입으로 합치지 않는다.
- `Condition`, `Effect` 같은 다형 규칙은 giant switch 대신 데이터 노드 구조를 우선 검토한다.
- validation은 가능하면 editor 단계에서 수행하고, play 진입 전에 실패를 드러낸다.

## authored field reachability 계약

- `Assets/Resources/_Game/Content/Definitions/**` 아래 asset에 직렬화 가능한 모든 top-level 및 nested field는 `tools/content-reachability/field-catalog.tsv`에 한 번씩 등록한다.
- 각 field는 `live`, `dead`, `unwired`, `presentation-only`, `shadowed` 중 하나로 분류한다. `live`, `presentation-only`, `shadowed`는 executable runtime consumer의 `file:line` 증거가 필요하다.
- editor inspector, validator, test, content parser, fact projector는 runtime consumer로 인정하지 않는다. field 이름이나 validator가 있다는 사실만으로 gameplay effect를 추론하지 않는다.
- 새 authored field는 같은 변경에서 consumer 증거와 recursive nested surface를 catalog에 추가해야 한다. 의도한 consumer가 없으면 `unwired`, 삭제 후보면 `dead`로 남기고 별도 ratification 전에는 wire/delete하지 않는다.
- `dead` field의 강제 marker는 `reachability-catalog+lint-warning`이다. YAML에 같은 key가 남아 있으면 lint가 runtime이 아무것도 하지 않는다는 경고를 출력한다.

## sentinel과 fallback 계약

- `0`, 음수, 빈 문자열, null collection, clamp, merge, precedence override처럼 authored value가 다른 runtime 값으로 바뀌는 지점은 `tools/content-reachability/fallback-registry.tsv`에 등록한다.
- 의도된 optional/default 의미는 `legitimate-sentinel`, 저자가 적은 값이 조용히 다른 동작이 되는 경우는 `trap`이다.
- trap guard 메시지는 잘못된 저작값, runtime이 실제 사용하는 값, `wire it / delete it / mark it` 세 처분을 모두 적는다. 문서를 읽지 않은 세션도 lint 출력만으로 실제 winner를 알아야 한다.
- 이 검사는 `tools/test-harness-lint.ps1` Check 8에서 source/YAML만으로 실행한다. `FastUnit`이나 `BatchOnly` test discovery에 가려지지 않으며 gameplay 값을 변환하거나 asset을 고치지 않는다.

## 권장 authoring 영역

```text
Assets/Resources/_Game/Content/Definitions/
  Stats/
  Traits/
  Classes/
  Races/
  Skills/
  Conditions/
  Effects/
  Items/
  Affixes/
  Augments/
  Rewards/
  Encounters/
```

## 권장 지원 코드

- `SM.Content`: definition 해석과 runtime 친화 모델
- `SM.Editor.Validation`: authored data 검증
- `SM.Editor.SeedData` 또는 동등 editor 경로: seed/bootstrap 지원

## 금지 패턴

- scene script 안에 콘텐츠 표를 직접 박아 넣는 방식
- authored asset에 runtime/save mutable state를 직접 저장하는 방식
- `Helper`, `Util` 하나로 여러 콘텐츠 타입을 뒤섞는 방식

## launch 기준 연결

- launch authoring schema의 필드 단위 소유권은 `docs/03_architecture/content-authoring-and-balance-data.md`가 소유한다.
- 이 문서는 기본 authoring 경계, reachability/fallback 검증 계약, 금지 패턴만 유지한다.
