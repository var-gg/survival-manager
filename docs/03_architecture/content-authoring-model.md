# 콘텐츠 authoring 모델

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/content-authoring-model.md`
- 관련문서:
  - `docs/03_architecture/data-model.md`
  - `docs/03_architecture/unity-boundaries.md`
  - `docs/03_architecture/unity-project-layout.md`

## 목적

이 문서는 MVP 콘텐츠를 어떻게 authored asset으로 만들고 검증할지 정의한다.
핵심은 scene 로직에 규칙을 하드코딩하지 않고도 vertical slice를 구동할 수 있게 하는 것이다.

## 기본 규칙

- 현재 prototype의 sample content 기준 저장소는 `Assets/Resources/_Game/Content/Definitions/**`다.
- 이 경로의 concrete contract는 `content-loading-contract.md`, 선택 이유는 `content-loading-strategy.md`를 따른다.
- authored definition과 runtime instance는 같은 타입으로 합치지 않는다.
- `Condition`, `Effect` 같은 다형 규칙은 giant switch 대신 데이터 노드 구조를 우선 검토한다.
- validation은 가능하면 editor 단계에서 수행하고, play 진입 전에 실패를 드러낸다.

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
