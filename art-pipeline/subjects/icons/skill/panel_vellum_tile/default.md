---
slug: panel_vellum_tile--default
kind: skill_icon
subject_id: panel_vellum_tile
variant: default
emotion: default
refs: []
aspect: "1:1"
output_size: "512x512"
chroma: false
status: idea
---

# Asset: panel_vellum_tile — 양피지 texture repeating tile

Use case: modal / card interior background texture overlay. USS `background-image: url(...)` + `background-repeat: repeat` 로 tileable. 단순 단색보다 양피지 painterly texture로 깊이감.

## Output target

- `art-pipeline/output/icons/panel/panel_vellum_tile.png` (chroma OFF — full vellum fill, no transparent)
- Unity import: `Assets/_Game/UI/Foundation/Sprites/PanelVellumTile.png` (Wrap Mode: Repeat)

```prompt
# Asset: Seamless vellum parchment texture tile — for UI background repeat

A 512×512 seamless tileable vellum parchment texture for game UI panel background. Subtle painterly weathered paper feel, warm cream color base. NOT photoreal paper, NOT realistic fiber detail — anime-painted vellum tone matching the project's character roster splash and Town hub baseline.

## Composition

- **Canvas**: 512×512, fully filled (NO magenta background, no transparency)
- **Base color**: warm vellum cream `#f5ead0` with subtle variation
- **Painterly variation**: very subtle warm umber + cool grey tone variation (~5% range), painterly brush stroke hint
- **Seamless edges**: top edge tiles with bottom, left edge tiles with right (NO visible seam when repeated)
- **No focal point**: texture is uniform across the tile, no center detail that would draw eye when tiled

## Style

Painterly anime parchment — same warm vellum feel as character splash backgrounds and Town hub. NOT photoreal paper photography, NOT realistic fiber, NOT distressed leather.

## Tileability check

When the tile is placed in a 4×4 repeating pattern (2048×2048), the boundaries should be invisible. Brush stroke variation should be subtle enough that no specific brush stroke recurs as a recognizable pattern.

## What this is NOT

- NOT solid flat color (subtle painterly variation required)
- NOT photoreal paper grain
- NOT distressed / aged leather
- NOT bright white — warm vellum cream
- NOT containing readable text, runes, or any focal motif
- NOT transparent — fully filled
- NOT a magenta chroma background — this asset doesn't use chroma key
```
