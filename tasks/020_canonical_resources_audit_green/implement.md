# canonical Resources audit green 구현 기록

## 메타데이터

- 작업명: canonical Resources audit green
- 담당: Codex
- 상태: done
- 최종수정일: 2026-04-19

## Phase summary

- Phase 0: task 문서 생성.
- Phase 1: canonical Resources asset의 `m_Script: {fileID: 0}` 참조를 editor-only repair utility로 복구하고 `SampleSeedGenerator.Generate()` explicit lane 끝에 연결했다.
- Phase 2: Unity typed asset load가 authored ScriptableObject를 읽지 못하는 문제를 `SM.Content` authored definition 타입과 `FirstPlayableSliceDefinitionAsset`의 block namespace 전환으로 해소했다.
- Phase 3: stale permanent augment rarity 값은 generator governance에서 지원 rarity로 normalize해 seed lane이 중단되지 않게 했다.
- Phase 4: `BindProfile()` 중 recruit sync가 기존 `Profile.ActiveRun`을 빈 record로 덮어쓰던 round-trip 결함을 `SessionProfileSync.SyncRecruitState()`에서 보존하도록 수정했다.

## Deviation

- `pwsh -File tools/unity-bridge.ps1 seed-content`는 실행 중인 editor menu lane을 요구해 no-editor 상태에서 실패했다.
- 같은 explicit repair intent를 유지하기 위해 batch `executeMethod`로 `SM.Editor.SeedData.SampleSeedGenerator.Generate`를 호출했다.
- `content-validate` 전체 green은 021 scope로 남겼다. 020에서는 canonical missing issue와 `BuildCompileAuditTests` green을 닫았다.

## Blockers

- 없음. `BuildCompileAuditTests`는 10/10 green이다.
- `content-validate` 잔여 error 40건과 warning 12건은 specialist coverage, Loop C tuning, recruit banned pairing/tag/build lane governance로 021에서 처리한다.

## Diagnostics

- 742개 canonical `.asset`에 missing script reference가 있었다.
- missing script reference 복구 후에도 `Resources.LoadAll<T>`가 0건을 반환했고, 원인은 일부 ScriptableObject authored 타입이 file-scoped namespace로 import되어 Unity typed load에서 빠지는 상태였다.
- `FirstPlayableSliceDefinitionAsset` 역시 같은 이유로 validator catalog에서 누락되었고, block namespace 전환 후 `first_playable.asset_missing`이 사라졌다.
- `SessionProfileSync.SyncRecruitState()`는 active run이 아직 runtime field로 복원되기 전에 recruit phase를 sync하면서 `Profile.ActiveRun`을 새 record로 바꾸고 있었다.

## Why this loop happened

- 019는 code/docs/asmdef refactor였고, Resources asset repair는 scope 밖으로 남겼다.
- boundary refactor 후 runtime path가 editor fallback 없이 canonical Resources를 실제로 읽게 되면서, stale YAML script reference와 typed import 문제가 audit blocker로 노출되었다.
- content validation 잔여는 canonical load 자체가 아니라 gameplay content governance 문제라 021로 분리한다.
