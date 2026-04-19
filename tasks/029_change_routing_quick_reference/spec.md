# Change routing quick reference spec

## Goal

AI agent와 사람이 변경 유형만 보고 첫 파일, 소유 asmdef, 기본 검증 lane을 빠르게 고를 수 있는 compact routing reference를 만든다.

## Authoritative boundary

- 새 구조나 새 asmdef를 만들지 않는다.
- `SM.Meta` pure boundary와 026/028 FastUnit 의미를 바꾸지 않는다.
- routing 표는 현재 구현 상태를 설명하며, 미래 목표를 현재 사실처럼 쓰지 않는다.
- 문서는 한국어 본문을 유지하고 코드 식별자는 영어로 둔다.

## In scope

- `AGENTS.md`에 agent 운영용 quick reference를 추가한다.
- `docs/03_architecture/index.md`에 구조 문서 진입점용 quick reference를 추가한다.
- task status에 docs impact와 검증 결과를 남긴다.

## Out of scope

- production code, tests, asmdef 수정.
- 028에서 드러난 Loop A 4v4 timeout balance contract 수리.
- 030 semantic guard 자동화.
- 027 이후 session ownership 추가 migration.

## validator / test oracle

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- targeted `tools/docs-check.ps1` for changed docs/task files.
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## done definition

- `AGENTS.md`에서 change type -> owner -> first test -> escalation lane을 한 화면 안에서 볼 수 있다.
- `docs/03_architecture/index.md`가 같은 routing 기준을 아키텍처 문서 진입점에서 제공한다.
- docs policy/check/smoke가 변경 파일 기준으로 통과한다.
