# 047 Compendium VFX Preview Status

- 상태: in_progress
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/047_compendium_vfx_preview/status.md`

## Current state

Town 도감의 스킬 상세 영역에 VFX hook 기반 쇼케이스 프리뷰를 추가했다. 구현은 C# runtime UI 우선이며, 실제 particle prefab catalog는 후속 asset authoring 범위로 남긴다.

## Acceptance matrix

| 항목 | 상태 | 근거 |
| --- | --- | --- |
| 스킬 선택 자동 재생 | done | `CompendiumPresenter.SelectEntry` play token |
| replay 버튼 | done | `CompendiumVfxReplayButton` |
| preview stage | done | `CompendiumVfxPreviewView` + UXML/USS |
| style 분류 | done | `BattleSkillSpec` 기반 resolver |
| localization | done | `UI_Town` preview/replay/caption key |
| 실제 particle prefab | deferred | 후속 `BattleVfxCatalog`/asset task |

## Evidence

- `git diff --check`: pass
- `pwsh -File tools/test-harness-lint.ps1`: pass
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass
- `pwsh -File tools/docs-check.ps1 -RepoRoot . -Paths tasks/047_compendium_vfx_preview`: pass
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass
- `npx markdownlint-cli2 "tasks/047_compendium_vfx_preview/**/*.md"`: pass

## Remaining blockers

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`는 열린 Unity 인스턴스의 project lock으로 실패.

## Deferred / debug-only

- hook id별 prefab catalog
- 3D actor/target dummy stage
- editor preview window
- 상태/시너지 VFX 재생

## Loop budget consumed

- C# preview runner: 1 loop
- UXML/USS/localization: 1 loop
- validation/handoff: 1 loop

## Handoff notes

이번 task는 도감 표면에서 스킬 VFX 감성 확인을 가능하게 하는 단계다. 다음 단계는 hook id와 실제 VFX prefab mapping 정책을 확정하고, 2~3개 대표 스킬 prefab을 먼저 붙여 검수 루프를 만든다.
