# Unity Project Layout

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the intended Unity project layout for the prototype phase.

## MVP Rules

### Project-Owned Areas

Implementation is currently expected to live under:

- `Assets/_Game/**`
- `Assets/Tests/**`

### Protected Area

Original vendor content under `Assets/ThirdParty/**` must not be directly modified.

## Proposed Runtime Layout

```text
Assets/_Game/
  Content/
    Definitions/
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
      Unity/
    Editor/
```

## Proposed Test Layout

```text
Assets/Tests/
  EditMode/
  PlayMode/
```

## Proposed Assembly Mapping

- `Assets/_Game/Scripts/Runtime/Core` -> `SM.Core`
- `Assets/_Game/Scripts/Runtime/Content` -> `SM.Content`
- `Assets/_Game/Scripts/Runtime/Combat` -> `SM.Combat`
- `Assets/_Game/Scripts/Runtime/Meta` -> `SM.Meta`
- `Assets/_Game/Scripts/Runtime/Persistence/Abstractions` -> `SM.Persistence.Abstractions`
- `Assets/_Game/Scripts/Runtime/Persistence/Json` -> `SM.Persistence.Json`
- `Assets/_Game/Scripts/Runtime/Persistence/Postgres` -> `SM.Persistence.Postgres`
- `Assets/_Game/Scripts/Runtime/Unity` -> `SM.Unity`
- `Assets/_Game/Scripts/Editor` -> `SM.Editor`
- `Assets/Tests` -> `SM.Tests`

## Naming Direction

Namespaces should align with the assembly split, for example:

- `SM.Core.*`
- `SM.Content.*`
- `SM.Combat.*`
- `SM.Meta.*`
- `SM.Persistence.*`
- `SM.Unity.*`
- `SM.Editor.*`
- `SM.Tests.*`

## Open Questions

- should content definitions remain under one root or split by domain sooner?
- how much scene-local scripting is acceptable before it should move into `SM.Unity`?
- what shared conventions are needed for scene bootstrapping and data loading?
- how should test fixtures be partitioned between EditMode and PlayMode as the slice grows?
