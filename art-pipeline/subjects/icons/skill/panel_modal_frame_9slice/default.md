---
slug: panel_modal_frame_9slice--default
kind: skill_icon
subject_id: panel_modal_frame_9slice
variant: default
emotion: default
refs: []
aspect: "1:1"
output_size: "1024x1024"
chroma: "#FF00FF"
status: idea
---

# Asset: panel_modal_frame_9slice — 큰 modal chrome 9-slice PNG

Use case: Tactical Workshop / Equipment Refit / Recruit / Permanent Augment / Passive Board / Inventory / Theater / Settings — 모든 큰 modal frame chrome. Unity UIToolkit `-unity-slice-{left,top,right,bottom}` 9-slice로 어떤 사이즈에도 fit.

## Output target

- `art-pipeline/output/icons/panel/panel_modal_frame_9slice.png` (final transparent PNG)
- Unity import path: `Assets/_Game/UI/Foundation/Sprites/PanelModalFrame9Slice.png`

```prompt
# Asset: 9-slice ready modal frame chrome PNG

A single **rounded rectangular UI frame chrome** for a premium mobile JRPG / gacha-banner game's modal panel. Painterly anime UI tone — same illustrated world as the project's character roster splash and Town hub baseline. Centered on solid #FF00FF magenta background. 9-slice safe — corners, edges, and center are distinct so Unity's `-unity-slice` can scale the frame to any size.

## Composition (CRITICAL — 9-slice safety)

- **Canvas**: 1024×1024
- **Frame outer rectangle**: ~64px from canvas edge, large rounded corners (~48px radius)
- **Frame border thickness**: ~16-20px painterly brush stroke
- **Inside frame**: warm vellum-cream fill with subtle inner darker rim
- **Outside frame** (between frame and canvas edge): pure #FF00FF magenta (for chroma key)
- **4 corners**: identical L-bracket gold ornament (small painterly flourish, ~80×80 area each), so 9-slice corner sections are interchangeable
- **4 edges (top/bottom/left/right)**: identical repeatable painterly brush stroke (tileable when stretched)
- **Center**: vellum-cream fill, tileable

## Visual details

- **Frame border**: warm gold painterly brush stroke (warm `#e6b751` to `#a47a2b` gradient), slight irregularity giving hand-painted feel
- **Corner L-brackets**: gold cap with small inner diamond accent (refer to existing `Assets/_Game/UI/Foundation/Ornaments/ornament-corner-vine.svg` motif)
- **Inner edge**: thin darker line 4-6px inside the outer border (double-line ornate frame look)
- **Frosted glass interior**: vellum-cream `#f5ead0` with ~85% alpha (so background behind shows softly through when used in-game)
- **Subtle inner glow**: very faint warm gold halo just inside the inner edge (5-8px soft falloff, ~20% opacity)

## Style

Painterly anime UI chrome — same painterly anime feel as the project's character roster splash and Tactical Workshop modal mockup. Hand-drawn brush stroke, NOT vector clean, NOT photoreal metal embossing, NOT chrome-metal photoreal.

## 9-slice break points (CRITICAL — Unity import 시 이 값 사용)

- left: 96px (corner + some edge)
- top: 96px
- right: 96px
- bottom: 96px

즉 canvas 1024×1024 기준 9 sections:
- 4 corners: 96×96 each
- 4 edges (top / bottom 96 height, left / right 96 width): tileable
- 1 center: 832×832 tileable

## What this is NOT

- NOT a metal photo or chrome render — painterly anime
- NOT readable text on frame
- NOT character / scene illustration — just frame chrome
- NOT photoreal embossing
- NOT magenta hue on frame (chroma key forbidden)
- NOT gradient background behind frame — pure #FF00FF outside
- NOT decorative ornament inside the frame interior (interior is vellum tileable)
- NOT solid filled corners (corners have visible gold L-bracket painterly accent)
```
