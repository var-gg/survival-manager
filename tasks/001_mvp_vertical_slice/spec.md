# Task Spec: 001 MVP Vertical Slice

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype
- Task ID: 001

## Purpose

Define the scope of the first prototype-phase playable vertical slice.
This document is intentionally narrow and should not expand into a full production feature list.

## Task Goal

Produce a **playable vertical slice at wooden-dummy fidelity** that proves the core management-driven auto-battle loop.

## Success Shape

The slice should eventually demonstrate, at minimum:

- a controllable prototype path inside approved implementation zones
- recognizable pre-battle or between-battle setup decisions
- visible auto-battle resolution or battle observation payoff
- enough feedback to judge whether the loop is promising

## Constraints

- implementation scope remains limited to `Assets/_Game/**` and `Assets/Tests/**`
- no direct modification of original vendor contents under `Assets/ThirdParty/**`
- do not turn this task into broad production architecture expansion
- document and implementation changes should remain synchronized

## Out of Scope for This Task Track

- large-scale content production
- broad polish work
- production-quality balance
- unrestricted system expansion outside slice needs

## Open Questions

- what is the minimum battle scenario needed for the slice?
- what setup decisions must be present to prove the fantasy?
- what can be faked or simplified safely at wooden-dummy fidelity?
