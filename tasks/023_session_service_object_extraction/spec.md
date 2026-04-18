# session service object extraction 사양

## 메타데이터

- 작업명: session service object extraction
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-19
- 소스오브트루스: `tasks/023_session_service_object_extraction/spec.md`

## 목적

`GameSessionState`의 public surface를 유지하면서 session flow 단위를 실제 `internal sealed` service object로 분리한다. 기존 UI/controller 호출부 migration은 만들지 않고, facade method 이름과 public constructor를 보존한다.

## 범위

- 허용: `Assets/_Game/Scripts/Runtime/Unity/GameSessionState.cs`, `Assets/_Game/Scripts/Runtime/Unity/Session/**`, 관련 테스트, task 문서.
- 금지: scene/prefab/package 구조 편집, 새 localization/content asset authoring, `SM.Meta` forbidden using 재도입.

## Service objects

- `SessionProfileSync`
- `SessionDeploymentFlow`
- `SessionRecruitmentFlow`
- `SessionExpeditionFlow`
- `SessionRewardSettlementFlow`

## Acceptance

- `GameSessionState` public constructor와 기존 public facade method 이름을 유지한다.
- public/internal flow entrypoint는 service object를 통해 실행된다.
- `GameSessionState.cs`는 2,500줄 이하를 유지한다.
- `Assets/_Game/Scripts/Runtime/Meta/**`에 `SM.Content`, `UnityEngine`, `UnityEditor` using을 재도입하지 않는다.
- 새 `Manager`, `Helper`, `Util`, `Common` 명명과 static mutable session truth를 만들지 않는다.
