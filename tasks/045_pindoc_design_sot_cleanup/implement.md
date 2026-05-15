# Pindoc design SOT cleanup implement

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: `tasks/045_pindoc_design_sot_cleanup/implement.md`
- 관련문서:
  - `tasks/045_pindoc_design_sot_cleanup/spec.md`
  - `tasks/045_pindoc_design_sot_cleanup/plan.md`
  - `tasks/045_pindoc_design_sot_cleanup/status.md`

## Phase summary

- Phase 1: completed
  - `AGENTS.md`, `CLAUDE.md`, `docs/index.md`, governance 문서, SoT matrix를 Pindoc product/design primary 기준으로 갱신했다.
  - `docs/01_product/**`, `docs/02_design/deck/**`, creative narrative longform, progression premise, UI handoff README/visual audit Markdown을 active working corpus에서 제거했다.
  - `docs/02_design/index.md`와 `docs/02_design/narrative/index.md`를 code-facing contract / transition hold 인덱스로 축소했다.
  - 남은 repo contract 문서의 삭제된 product/deck/narrative 참조를 Pindoc artifact 범주로 교체했다.
  - Pindoc `wiki-start-프로젝트지도`를 revision 5로 갱신했다.
- Phase 2: not applicable
- Phase 3: completed

## Deviation

- `docs-check` 첫 targeted 실행은 `markdown-link-check` 기본 30초 timeout 때문에 중단됐다. 같은 대상에 `-LinkCheckTimeoutSeconds 60`을 사용해 재실행했고 통과했다.

## Blockers

없음.

## Diagnostics

- 시작 시 `docs/02_design/narrative/index.md`와 `docs/02_design/narrative/master-script.md`에 사용자 변경이 있었다.
- 해당 두 파일은 삭제하지 않고 Pindoc 우선 전환 상태를 명시하는 방향으로 처리한다.
- `docs/02_design/combat/**`, `meta/**`, `systems/**`는 이번에 삭제하지 않았다. 현재 C# runtime, content validator, architecture 문서가 기대하는 code-facing contract로 남아 있기 때문이다.

## why this loop happened

repo product/design Markdown과 Pindoc Wiki가 동시에 source-of-truth처럼 남아 있어 에이전트 시작 컨텍스트와 설계 판단이 흔들렸다.
