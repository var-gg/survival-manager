---
slug: panel_card_frame_9slice--default
kind: skill_icon
subject_id: panel_card_frame_9slice
variant: default
emotion: default
refs: []
aspect: "1:1"
output_size: "512x512"
chroma: "#FF00FF"
status: idea
---

# Asset: panel_card_frame_9slice — 작은 card chrome 9-slice PNG

Use case: posture card / augment card / recruit candidate card / equipment slot / inventory item slot / theater entry strip — 모든 작은 card / slot frame chrome. 9-slice로 어떤 card 사이즈에도 fit.

## Output target

- `art-pipeline/output/icons/panel/panel_card_frame_9slice.png`
- Unity import: `Assets/_Game/UI/Foundation/Sprites/PanelCardFrame9Slice.png`

```prompt
# Asset: 9-slice ready card frame chrome PNG

A single small **rounded rectangular UI card frame** for a premium mobile JRPG / gacha-banner game. Painterly anime UI chrome, lighter weight than the modal frame (smaller, less ornate). Centered on solid #FF00FF magenta background. 9-slice safe.

## Composition

- **Canvas**: 512×512
- **Card outer rectangle**: ~32px from canvas edge, rounded corners (~24px radius)
- **Frame border thickness**: ~8-10px painterly brush stroke (thinner than modal)
- **Inside card**: warm vellum-cream `#f5ead0` with ~85% alpha
- **Outside card**: pure #FF00FF magenta

## Visual details

- **Frame border**: warm gold painterly brush `#e6b751` with subtle hand-drawn irregularity
- **4 corners**: small gold corner accent (smaller than modal — ~24×24 each)
- **Inner edge**: very thin darker line 2-3px inside (subtle double-line)
- **Center**: vellum-cream fill, tileable

## 9-slice break points

- left/top/right/bottom: 48px each
- canvas 512×512 = 4 corners (48×48) + 4 edges + 1 center (416×416 tileable)

## Style

Same painterly anime chrome as modal frame but lighter / smaller weight. Card chrome is more frequent in UI (12+ instances per modal) so visual restraint, no ornate detail. Subtle warm gold rim, no heavy corner ornament.

## What this is NOT

- NOT identical to modal frame — card is lighter, less ornate, smaller corner accent
- NOT readable text
- NOT decoration inside frame interior
- NOT chrome metal photoreal
- NOT magenta hue on frame
```
