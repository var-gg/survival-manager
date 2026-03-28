# Tests

This directory contains the repository test layout.

## EditMode

Use `Assets/Tests/EditMode` for lightweight smoke tests that validate repository assumptions, basic editor-time behavior, and low-cost guardrails.

## PlayMode

Use `Assets/Tests/PlayMode` as a placeholder for future runtime-oriented tests.
Do not overbuild PlayMode coverage yet.

## Policy

- Prefer EditMode tests first.
- Keep smoke tests minimal and stable.
- Do not modify original vendor contents under `Assets/ThirdParty` for testing.
