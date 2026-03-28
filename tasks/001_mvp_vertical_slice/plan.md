# Task Plan: 001 MVP Vertical Slice

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## Purpose

Track the near-term plan for the first prototype-phase playable vertical slice.
This is a planning shell, not an implementation dump.

## Current Plan

1. lock the prototype-phase repository rules
2. confirm the playable-slice target and constraints
3. define the narrowest proof scenario
4. identify the minimum data, prefab, and test assets needed
5. implement only after the slice boundary is clear

## Current Guardrails

- keep implementation inside `Assets/_Game/**` and `Assets/Tests/**`
- do not modify original vendor contents under `Assets/ThirdParty/**`
- prefer narrow, reviewable changes
- keep docs synchronized with implementation decisions

## Immediate Next Questions

- what exact encounter or demo flow proves the loop fastest?
- what should be simulated versus actually implemented?
- what testing signal is sufficient for prototype success?
