# 048 Skill VFX Animation Coverage Audit Status

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-18
- 소스오브트루스: `tasks/048_skill_vfx_animation_coverage_audit/status.md`

## Current state

스킬 88개와 보유 유료 VFX/애니메이션의 표현 커버리지를 감사했다. 결론은 raw asset은 충분하지만 현재 C# runtime mapping이 generic cue 중심이라, 다음 구현은 `SkillPresentationFamily`와 skin/accent 기반 resolver를 먼저 닫아야 한다는 것이다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| skill demand audit | 88개 스킬 표현 수요 집계 | done | `report.md` Skill demand |
| asset supply audit | VFX/animation 보유 범위 집계 | done | `report.md` Asset supply, Animation supply |
| coverage decision | Green/Yellow/Red 판정 | done | `report.md` Coverage grades |
| next implementation gate | pilot 스킬과 구현 순서 지정 | done | `report.md` Pilot skills, Next implementation order |
| validator | 문서 검증 | done | Evidence 명령 통과 |
| targeted tests | 코드 변경 없음 | not_required | 다음 C# 구현 task에서 수행 |
| runtime smoke | prefab playback 미구현 | deferred | 다음 C# 구현 task에서 수행 |

## Evidence

- `git diff --check`: pass
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass
- `pwsh -File tools/docs-check.ps1 -RepoRoot . -Paths tasks/048_skill_vfx_animation_coverage_audit`: pass
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass
- `npx markdownlint-cli2 "tasks/048_skill_vfx_animation_coverage_audit/**/*.md"`: pass

## Remaining blockers

- Pindoc wiki MCP write tool이 현재 세션에 노출되어 있지 않아 repo task/report가 handoff source다.
- 열린 Unity 인스턴스가 project lock을 잡고 있으면 batchmode FastUnit은 실패한다.

## Deferred / debug-only

- `SkillPresentationFamily` C# 타입 또는 content field 추가
- hook id별 real prefab resolver
- 도감 3D dummy stage와 prefab playback
- 상태이상/시너지 전용 VFX preview
- 스킬 아이콘 imagegen batch 생성

## Loop budget consumed

- compile-fix: 0
- refresh/read-console: 0
- asset authoring retry: 0
- budget 초과 시 남긴 diagnosis: 없음

## Handoff notes

다음 세션은 `report.md`의 Next implementation order에서 시작한다. 우선 C# content schema 또는 derived presentation lookup 위치를 정하고, pilot 스킬 6개를 real prefab으로 연결해 도감 preview 검수 루프를 만든다.
