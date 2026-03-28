# Local Automation

- Status: active
- Owner: repository
- Last Updated: 2026-03-29
- Source of Truth: `docs/05_setup/local-automation.md`
- Applies To: Codex and local contributors

## Purpose

This document defines the minimum local automation and CI entry points for documentation checks, Unity tests, and basic smoke checks.
The goal is to keep validation lightweight and immediately useful without overbuilding deployment automation.

## Principles

- Prioritize markdown linting and link/structure checks first.
- Prefer EditMode tests as the initial Unity smoke-test layer.
- Keep PlayMode as a placeholder until runtime test coverage is justified.
- Do not store secrets or tokens in the repository.
- Document local commands and CI commands together.

## Document Checks

### Local

```powershell
pwsh -File tools/docs-check.ps1 -RepoRoot .
```

### CI

Workflow: `.github/workflows/docs-lint.yml`

Core commands:

```bash
npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"
npx --yes markdown-link-check "docs/**/*.md" "*.md" --quiet
```

## Unity Tests

### Local

Run EditMode smoke tests with a local Unity editor path:

```powershell
pwsh -File tools/unity-editmode-smoke.ps1 -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.0.56f1\Editor\Unity.exe" -ProjectPath .
```

### CI

Workflow: `.github/workflows/unity-tests.yml`

Current CI target:

- EditMode smoke tests via `game-ci/unity-test-runner@v4`
- PlayMode placeholder job only

Expected environment keys in GitHub Actions secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

Do not commit real values for these keys.

## Basic Smoke Check

### Local

```powershell
pwsh -File tools/smoke-check.ps1 -RepoRoot .
```

### CI-aligned intent

The smoke check verifies that key repository paths and governance documents still exist.
This is intended as a fast sanity layer before or alongside heavier checks.

## Failure Triage: Check These First

When checks fail, a human should verify these items first:

1. whether required files or folders were renamed or moved intentionally
2. whether documentation links reference files that no longer exist
3. whether Unity editor version and package state match project expectations
4. whether local Unity path is correct for the machine running the test
5. whether GitHub Actions secrets for Unity test execution are configured correctly
6. whether the failure came from project-owned code or third-party/vendor boundaries

## Scope Boundary

This automation layer intentionally does **not** include full build/deploy automation yet.
It is limited to:

- documentation validation
- EditMode-oriented Unity smoke testing
- basic repository smoke checks

## Current Readiness Split

### Immediately runnable in CI

- markdown lint
- markdown link check
- PlayMode placeholder job

### Requires follow-up configuration

- Unity EditMode CI execution requires Unity license-related secrets:
  - `UNITY_LICENSE`
  - `UNITY_EMAIL`
  - `UNITY_PASSWORD`

### Immediately runnable locally

- `tools/docs-check.ps1`
- `tools/smoke-check.ps1`
- `tools/unity-editmode-smoke.ps1` once a valid local Unity editor path is supplied
