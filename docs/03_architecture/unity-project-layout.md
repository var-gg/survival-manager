# Unity 프로젝트 레이아웃

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/unity-project-layout.md`
- 관련문서:
  - `docs/03_architecture/technical-overview.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/unity-boundaries.md`

## 목적

이 문서는 현재 prototype 단계에서 유지할 Unity 프로젝트 레이아웃과 asmdef 배치를 정의한다.
폴더 구조를 scene-first 해킹 경로로 무너뜨리지 않도록 실제 기준 경로를 고정한다.

## 프로젝트 소유 영역

- 코드와 씬: `Assets/_Game/**`
- 테스트: `Assets/Tests/**`
- 현재 sample content 로딩 자산: `Assets/Resources/_Game/**`

## 보호 영역

- `Assets/ThirdParty/**` 원본 직접 수정 금지

## 런타임 레이아웃

```text
Assets/Resources/
  _Game/
    Content/
      Definitions/
Assets/_Game/
  Scenes/
    Boot.unity
    Town.unity
    Expedition.unity
    Battle.unity
    Reward.unity
  Scripts/
    Runtime/
      Core/
      Content/
      Combat/
      Meta/
      Persistence/
        Abstractions/
        Json/
        Postgres/
      Unity/
    Editor/
      Bootstrap/
      SeedData/
      Validation/
```

## 테스트 레이아웃

```text
Assets/Tests/
  EditMode/
  PlayMode/
```

## asmdef 매핑

- `Assets/_Game/Scripts/Runtime/Core` -> `SM.Core`
- `Assets/_Game/Scripts/Runtime/Content` -> `SM.Content`
- `Assets/_Game/Scripts/Runtime/Combat` -> `SM.Combat`
- `Assets/_Game/Scripts/Runtime/Meta` -> `SM.Meta`
- `Assets/_Game/Scripts/Runtime/Persistence/Abstractions` -> `SM.Persistence.Abstractions`
- `Assets/_Game/Scripts/Runtime/Persistence/Json` -> `SM.Persistence.Json`
- `Assets/_Game/Scripts/Runtime/Persistence/Postgres` -> `SM.Persistence.Postgres`
- `Assets/_Game/Scripts/Runtime/Unity` -> `SM.Unity`
- `Assets/_Game/Scripts/Editor` -> `SM.Editor`
- `Assets/Tests/EditMode` -> `SM.Tests.EditMode`
- `Assets/Tests/PlayMode` -> `SM.Tests.PlayMode`

## namespace 방향

- `SM.Core.*`
- `SM.Content.*`
- `SM.Combat.*`
- `SM.Meta.*`
- `SM.Persistence.*`
- `SM.Unity.*`
- `SM.Editor.*`
- `SM.Tests.*`
