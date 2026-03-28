# Discord Handoff Format

- Status: active
- Owner: repository
- Last Updated: 2026-03-29
- Source of Truth: `docs/00_governance/discord-handoff-format.md`
- Applies To: Codex only

## Purpose

This document defines the standard Discord reporting format for Codex in this repository.
The goal is to keep handoffs short, scannable, and operationally useful.

## Required Sections

Every completion report on Discord should use the following section order:

1. 완료
2. 변경 파일
3. 결정사항
4. 리스크
5. 다음 단계

## Section Rules

### 1. 완료

Summarize what was finished in short Korean bullet points.
Only include completed work, not intention.

Example:

- Unity 프로젝트 루트 평탄화 완료
- `AGENTS.md` 상위 규칙 작성 완료
- Discord 보고 형식 문서 추가 완료

### 2. 변경 파일

List the files created or modified.
Use repository-relative paths.

Example:

- `AGENTS.md`
- `docs/00_governance/agent-operating-model.md`
- `docs/00_governance/discord-handoff-format.md`

### 3. 결정사항

State the decisions that now govern future work.
Keep these concise and concrete.

Example:

- Codex 운영 상위 규칙은 `AGENTS.md`에만 둔다
- 상세 운영 규칙은 `agent-operating-model.md`에서 관리한다
- Discord 보고는 지정된 5개 섹션 형식을 따른다

### 4. 리스크

List unresolved issues, uncertainty, or deferred work.
If there is no meaningful risk, say so plainly.

Example:

- 현재 리스크 없음
- ADR 문서 체계는 추후 실제 의사결정 누적 시 확장 필요

### 5. 다음 단계

State the most logical next actions without expanding scope automatically.
This section should guide the next task, not silently start it.

Example:

- 초기 README 구조 정리
- Unity 기본 씬 정책 문서화
- 첫 ADR 작성 기준 확정

## Style Rules

- Write the report in Korean.
- Keep code, commands, file names, and identifiers in English.
- Prefer bullets over long paragraphs.
- Keep the handoff compact and easy to scan in Discord.
- Do not include internal chain-of-thought or hidden reasoning.
- Do not inflate the report with unrelated context.

## Recommended Template

```md
완료
- ...
- ...

변경 파일
- `...`
- `...`

결정사항
- ...
- ...

리스크
- ...
- ...

다음 단계
- ...
- ...
```

## When to Use

Use this format for:

- task completion reports
- meaningful progress updates
- handoff summaries after repository changes

If the update is extremely small, Codex may shorten content, but should preserve the same section order when reporting formal completion.
