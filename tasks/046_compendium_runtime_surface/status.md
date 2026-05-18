# 046 Compendium Runtime Surface Status

- 상태: in_progress
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/046_compendium_runtime_surface/status.md`

## Current state

Town Play Mode 도감 MVP 구현을 진행했다. C# runtime surface와 UXML/USS, localization seed/table까지 반영했다.

## Acceptance matrix

| 항목 | 상태 | 근거 |
| --- | --- | --- |
| Town 도감 버튼 | done | `CompendiumButton`, `CompendiumTemplate` 추가 |
| 스킬 도감 | done | `CombatContentSnapshot.SkillCatalog` + authored `SkillDefinitionAsset` icon/VFX hook 조회 |
| 상태 도감 | done | `CombatContentSnapshot.StatusFamilies` 조회 |
| 시너지 도감 | done | `GetCanonicalSynergyFamilyIds()` + `SynergyDefinition` 조회 |
| 캐릭터 도감 MVP | done | `CombatContentSnapshot.Characters` + profile 보유 hero 기준 locked/unlocked 표시 |
| Pindoc Wiki 발행 | blocked | Pindoc write tool 미노출 |
| VFX 실제 재생 프리뷰 | deferred | 후속 editor/runtime preview task |

## Evidence

- `git diff --check`: pass
- `pwsh -File tools/test-harness-lint.ps1`: pass
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass
- `npx markdownlint-cli2 "tasks/046_compendium_runtime_surface/**/*.md"`: pass
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass
- UI localization key presence check: pass

## Remaining blockers

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`는 열린 Unity 인스턴스의 project lock으로 실패.
- 로컬 `dotnet` SDK 부재로 `.sln` 대체 빌드 불가.
- repo 전체 `docs-check`는 현재 미추적 `.agents` payload와 third-party package Markdown lint 이슈로 실패한다.

## Deferred / debug-only

- VFX preview runner
- 캐릭터 unlock save/source 모델
- 시크릿 도감 슬롯 숨김 정책
- 에디터 전용 authoring/preview 도구

## Loop budget consumed

- C# MVP: 1 loop
- Runtime UI asset/localization: 1 loop

## Handoff notes

다음 순서는 Unity project lock을 해소한 뒤 `test-batch-fast`와 Town Play Mode smoke를 확인하는 것이다. 그 다음 VFX hook preview 버튼 또는 에디터 preview window를 별도 작업으로 붙인다.
