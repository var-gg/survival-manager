---
slug: ornament_divider_warm_gold--default
kind: skill_icon
subject_id: ornament_divider_warm_gold
variant: default
emotion: default
refs: []
aspect: "4:1"
output_size: "1024x256"
chroma: "#FF00FF"
status: idea
---

# Asset: ornament_divider_warm_gold — 헤더/divider 장식 한 컷

Use case: modal header bar 아래 / 카드 사이 divider / status bar 위 — 가로 ornamental separator. 기존 `ornament-vine-flourish.svg`의 variant 또는 보완.

## Output target

- `art-pipeline/output/icons/panel/ornament_divider_warm_gold.png`
- Unity import: `Assets/_Game/UI/Foundation/Sprites/OrnamentDividerWarmGold.png`

```prompt
# Asset: Horizontal divider ornament — gold vine flourish style

A single horizontal gold ornamental divider for game UI. Painterly anime style — gold vine flourish with subtle diamond center and tapered ends. Centered on solid #FF00FF magenta background. ~1024px wide × ~256px tall (4:1 wide).

## Composition

- **Canvas**: 1024×256
- **Divider**: centered horizontally, ~80% canvas width, ~80px tall
- **Center**: small diamond gold motif (~40×40, rotated 45°), painterly
- **Left/right of center**: thin warm gold painterly line extending outward, gently tapered to fade at the ends
- **Above/below divider**: pure #FF00FF magenta (for chroma key)

## Visual details

- **Gold tone**: warm `#e6b751` (matching `--gold-300` token)
- **Painterly brush**: hand-drawn feel, subtle irregularity, NOT vector clean
- **Diamond center**: solid gold with subtle inner highlight (slight 3D suggestion)
- **Line ends**: gentle fade-out alpha (about last 10% of each side fades to transparent)

## Style

Same painterly anime UI ornament as existing `Assets/_Game/UI/Foundation/Ornaments/ornament-vine-flourish.svg` motif — paint, not enchanted glow. Subtle, NOT loud. Used as separator, NOT focal element.

## What this is NOT

- NOT a magical glowing rune divider
- NOT photoreal metal
- NOT containing readable text or letters
- NOT vector-clean — painterly brush feel
- NOT thick / heavy line — thin elegant
- NOT magenta hue on divider
```
