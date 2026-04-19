# Scope-correct closure docs spec

## Goal

030 이후 상태를 repo-wide full editor-free separation으로 오해하지 않도록 durable docs와 historical task status에 closure scope를 명확히 남긴다.

## Authoritative boundary

- 닫힌 범위는 pure asmdef boundary와 `FastUnit` editor-free/resource-free/authored-object-free lane이다.
- `SM.Unity`, authored content conversion/bootstrap, `RuntimeCombatContentLookup`, `NarrativeRuntimeBootstrap`, session/UI/scene loop는 boundary adapter 또는 editor-light/editor-required lane으로 남는다.
- historical task status는 해당 시점 snapshot이며 current source-of-truth를 대체하지 않는다.

## In scope

- `AGENTS.md`, `docs/TESTING.md`, architecture docs에 closure scope를 명시한다.
- `tasks/025`~`030` status 상단에 historical/current-state marker를 추가한다.
- `031` task 문서에 문서 변경 이유와 검증 근거를 남긴다.

## Out of scope

- runtime code 변경.
- asmdef reference 변경.
- deprecated lifecycle 정리.
- 새로운 ADR 작성.

## asmdef impact

- 없음.

## persistence impact

- 없음.

## validator / test oracle

- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
- targeted changed-docs `docs-check`
- markdownlint when available.

## done definition

- active docs가 `SM.Unity`/authored content/UI loop를 pure editor-free closure 내부로 표현하지 않는다.
- historical task status가 current-state source-of-truth로 오독되지 않는다.
- validation evidence가 `031` status에 남는다.

## deferred

- repo-wide dependency simplification, production bootstrap/provider injection, content adapter ownership split은 후속 runtime/editor-light 작업으로 둔다.
