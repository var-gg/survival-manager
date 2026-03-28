# Current Known Issues

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Scene Loop

- full Boot -> Town -> Expedition -> Battle -> Reward -> Town playable wiring is not yet verified end to end
- placeholder scene files exist, but scene logic ownership is still incomplete

## Persistence

- Postgres adapter is a policy/boundary placeholder, not a full CRUD implementation
- JSON fallback is the intended safe path, but full gameplay integration still depends on Unity-side wiring

## Tooling

- current shell environment did not support standalone C# smoke compilation during persistence verification
- Unity-side validation remains the source of truth for compile/play checks

## Content

- seed data generator exists, but generated assets still need Unity editor execution to materialize `.asset` files
- trait pools currently embed trait entries; later standalone trait assets may be needed

## Presentation

- primitive-only rendering target is not yet fully assembled into the playable slice
- no final visual readability pass exists for battle/town/reward screens

## Testing

- EditMode tests exist for core rules, but broader integration coverage is still thin
- PlayMode coverage is only at smoke-test level

## Backlog Pressure Areas

- PVP
- advanced crafting
- more synergy families
- external asset integration
