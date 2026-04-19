# Tests

This directory contains the repository test layout.

## FastUnit

Use `Assets/Tests/EditMode/FastUnit` for `[Category("FastUnit")]` tests.
This folder owns the dedicated `SM.Tests.FastUnit` asmdef and must not reference `SM.Editor`.

## EditMode

Use `Assets/Tests/EditMode` for lightweight smoke tests that validate repository assumptions, basic editor-time behavior, and low-cost guardrails.
Editor-facing validation tests that reference `SM.Editor` belong here as `BatchOnly`.

## PlayMode

Use `Assets/Tests/PlayMode` as a placeholder for future runtime-oriented tests.
Do not overbuild PlayMode coverage yet.

## Policy

- Prefer EditMode tests first.
- Keep smoke tests minimal and stable.
- Do not modify original vendor contents under `Assets/ThirdParty` for testing.
