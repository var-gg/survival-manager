# Narrative transition hold

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: Pindoc Wiki narrative artifacts
- 관련문서:
  - `docs/02_design/index.md`
  - `docs/03_architecture/narrative-code-architecture.md`
  - `pindoc://decision-doc-harness-pindoc-migration`

## 목적

이 폴더는 더 이상 narrative/world/campaign/lore의 source-of-truth가 아니다.
창작 방향과 production draft는 Pindoc Wiki를 우선한다.

repo에 남은 파일은 runtime schema 또는 seed 동기화 전 reference로만 둔다.
새로운 narrative 결정, 캐릭터 lore, chapter beat, world bible, script production draft는 Pindoc에 작성하고 repo Markdown으로 복원하지 않는다.

## 남은 repo 파일

| path | 역할 | 현재 판정 |
| --- | --- | --- |
| `dialogue-event-schema.md` | story event ID, trigger, once policy, presentation grade의 code-facing schema | 코드/authoring contract로 일시 잔류 |
| `master-script.md` | 과거 repo seed baseline과 runtime seed 동기화 전 reference | Prologue~Chapter 1 최신 production draft는 Pindoc 우선 |

## 제거된 creative planning 문서군

세계관, 페이싱 공식, 캠페인 아크, chapter beat, 세력 충돌, authoring guide longform은 Pindoc 이관 대상으로 보고 active repo corpus에서 제거했다.
필요하면 git history가 아니라 Pindoc search/context에서 최신 artifact를 찾는다.

## 변경 시 체크리스트

- narrative creative truth를 repo Markdown에 새로 추가하지 않는다.
- runtime schema 변경이면 `docs/03_architecture/narrative-code-architecture.md`와 코드/테스트를 함께 갱신한다.
- script seed를 runtime에 반영할 때는 Pindoc 최신본을 먼저 확인한다.
