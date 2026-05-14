---
slug: glow_halo_warm_gold--default
kind: skill_icon
subject_id: glow_halo_warm_gold
variant: default
emotion: default
refs: []
aspect: "1:1"
output_size: "512x512"
chroma: "#FF00FF"
status: idea
---

# Asset: glow_halo_warm_gold — selected state warm gold glow halo

Use case: selected card / equipped slot / active node / CTA button — `--selected` modifier 의 warm gold glow halo. USS box-shadow 미지원이므로 child VisualElement에 background-image로 적용.

## Output target

- `art-pipeline/output/icons/panel/glow_halo_warm_gold.png`
- Unity import: `Assets/_Game/UI/Foundation/Sprites/GlowHaloWarmGold.png`

```prompt
# Asset: Warm gold radial glow halo for selected state

A single soft warm gold radial glow halo on solid #FF00FF magenta background. Used as overlay behind selected cards / equipped slots / active nodes in game UI. 512×512 with the halo centered.

## Composition

- **Canvas**: 512×512
- **Halo center**: 256×256 center
- **Halo bright core**: small warm gold center (~64-96px diameter), warm `#e6b751`
- **Halo gradient**: smooth radial gradient from warm gold center to transparent edge
- **Halo full extent**: ~80% of canvas (~410px radius from center)
- **Outside halo**: pure #FF00FF magenta (chroma key)
- **No visible edge / ring**: soft falloff to transparent, no hard cutoff

## Visual details

- **Inner color**: warm gold `#e6b751` (matches `--gold-300` token)
- **Mid color**: lighter warm cream-gold `#f0d488` (matches `--gold-200`)
- **Outer color**: transparent (alpha 0 fade)
- **Gradient**: smooth exponential falloff, NOT linear
- **Subtle pulse asymmetry**: very minor warm orange tint shift at inner core (so not perfectly mathematical gradient)

## Style

Painterly anime UI glow — soft, warm, NOT magical lightning-flash, NOT photoreal lens flare. Used as ambient state indicator, not a VFX explosion.

## What this is NOT

- NOT a magical spell effect — UI state indicator
- NOT lens flare with star points or chromatic aberration
- NOT a hard-edged ring (smooth radial falloff)
- NOT photoreal light photography
- NOT containing visible particles or sparks
- NOT magenta hue on the glow (chroma key forbidden)
- NOT solid filled (gradient is the entire visual)
```
