# Postgres Setup

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document explains the local development Postgres setup for `survival-manager`.
The MVP must remain playable even when Postgres is unavailable.

## Policy Summary

- Postgres is a local development adapter, not a gameplay requirement
- direct client -> production DB is not assumed
- JSON fallback must keep the game playable when DB is unavailable
- content definitions remain Unity assets, not DB-managed records
- per-frame combat state is not persisted to DB

## Schema Name

Use Postgres schema:

- `survival_manager`

## SQL Scripts

Apply in order:

1. `tools/sql/001_survival_manager_schema.sql`
2. `tools/sql/002_survival_manager_bootstrap.sql`

## Connection Source

Connection settings should be read only from:

- environment variables
- local machine config

Recommended env var:

- `SM_POSTGRES_CONNECTION`

Optional mode env var:

- `SM_PERSISTENCE_MODE=postgres`

## MVP Runtime Behavior

If Postgres is configured and available, a local-dev adapter may be used.
If Postgres is missing, unavailable, or unsupported in the current build, the runtime must fall back to JSON saves.

## Suggested Local Flow

1. start local Postgres
2. apply schema/bootstrap SQL
3. set `SM_POSTGRES_CONNECTION`
4. optionally set `SM_PERSISTENCE_MODE=postgres`
5. launch the game
6. if DB path fails, confirm JSON fallback still works

## Non-Goals

- no direct production database access
- no DB ownership of content definitions
- no persistence of per-frame combat simulation state
- no requirement that MVP gameplay depends on remote infra

## Open Questions

- when should the local Postgres adapter move from placeholder to full CRUD implementation?
- which local config format is preferred alongside env vars for Unity editor workflows?
- when should migration tooling be added beyond raw SQL files?
- should run summaries remain append-only in the first real DB adapter?
