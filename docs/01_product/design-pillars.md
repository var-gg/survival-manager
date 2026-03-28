# Design Pillars

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the design pillars that should constrain MVP decisions for `survival-manager`.
Each pillar should help keep the project focused on a playable vertical slice rather than a broad genre mash-up.

## Pillar 1. Setup Decides the Fight

The player should feel that the battle outcome begins before combat starts.
Formation, class mix, race synergy, and preparation choices should matter more than direct input during battle.

### MVP Meaning

For MVP, this means a small but meaningful set of deploy, roster, and synergy decisions.

### Long-Term Direction

Later versions may allow richer tactical scripting or deeper formation rules, but the MVP should not depend on those expansions.

## Pillar 2. Auto-Battle Must Stay Readable

Combat is auto-battle and presented in 3D, but the underlying logic should stay simple and verifiable first.
The player should be able to understand cause and effect from composition and setup.

### MVP Meaning

The MVP should favor clear resolution logic over spectacle or simulation depth.

### Long-Term Direction

Later versions may increase encounter complexity and presentation density if readability remains strong.

## Pillar 3. Synergy Must Create Real Roster Pressure

Race and class combinations should create meaningful roster-building decisions.
The player should care not only about individual unit strength, but also about combinations.

### MVP Meaning

The MVP fixed set of 3 races and 4 classes must be enough to create noticeable synergy tradeoffs.

### Long-Term Direction

Additional races, classes, and edge-case interactions can come later only if the base synergy grammar works first.

## Pillar 4. Expedition and Return Must Matter

The game needs more than isolated combat.
The player should feel pressure from taking a squad out, surviving the run, and returning to town with consequences and choices.

### MVP Meaning

The MVP needs a visible expedition/return loop, even if simplified.

### Long-Term Direction

Longer-term structure may deepen into richer runs, events, or risk layers after the loop is proven.

## Pillar 5. Narrow Scope Beats Broad Promise

The project should prefer a small but working loop over premature feature spread.
If a feature does not strengthen the wooden-dummy playable slice, it should likely wait.

### MVP Meaning

Fixed counts and explicit non-goals are part of the design pillar set, not temporary suggestions.

### Long-Term Direction

Future expansion is allowed only after the MVP loop is genuinely validated.

## 열린 질문

- which pillar is most likely to fail first under real implementation pressure?
- how much roster synergy depth is enough to feel meaningful with only 3 races and 4 classes?
- what is the minimum expedition structure needed to satisfy Pillar 4?
- which long-term ideas are most dangerous because they sound valuable but weaken Pillar 5?
