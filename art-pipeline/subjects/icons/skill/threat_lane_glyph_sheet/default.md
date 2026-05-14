---
slug: threat_lane_glyph_sheet--default
kind: skill_icon
subject_id: threat_lane_glyph_sheet
variant: default
emotion: default
refs: []
aspect: "2:1"
output_size: "2048x1024"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: threat_lane_glyph_sheet (8 threat lane glyphs / 8 위협 lane 글리프) — sheet

Use case: Tactical Workshop의 우측 Threat-Answer Hexagon Chart — 8 vertex 각각의 small icon glyph. 8 threat lane (burst / sustain / control / swarm / dive / pierce / heal / summon-pressure)을 시각화. SoT [`counter-system-topology`](http://localhost:5830/p/survival-manager/wiki/) 의 8-lane threat/answer topology 정합.

## Output target (sheet_split 분할 후)

- `threat_burst.png`, `threat_sustain.png`, `threat_control.png`, `threat_swarm.png`
- `threat_dive.png`, `threat_pierce.png`, `threat_heal.png`, `threat_summon.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/threat_lane_glyph_sheet/default.png `
    --rows 2 --cols 4 `
    --emotions burst,sustain,control,swarm,dive,pierce,heal,summon `
    --out-dir art-pipeline/output/icons/threat/ `
    --prefix threat
```

```prompt
# Icon Sheet: 8 threat lane glyphs — for sprite atlas

A 4×2 grid sheet of stylized **threat lane glyph icons** for a premium mobile JRPG / gacha-banner game's tactical hexagon chart UI. Each cell contains a single abstract glyph centered on solid #FF00FF magenta background. Same painterly anime icon style as P0 sheet baselines. Each glyph is an **abstract threat type indicator** — small symbol that reads at hexagon vertex scale.

## Sheet structure (CRITICAL)

- **2048×1024 canvas = 2 rows × 4 columns = 8 cells**
- Each cell ~480×480 active icon area
- **Magenta gutter (#FF00FF, ~30-50px)** between cells and outer edges
- Each cell pure #FF00FF background
- Icon centered with breathing room ~60-80px from cell edges

## Cell contents (reading order: top-left to bottom-right)

### Row 1
- **burst**: Explosion star burst silhouette, warm orange-red with white-hot center. Different from trigger burst (lightning bolt) — this is radial explosion
- **sustain**: Hourglass with sand evenly flowing both directions, warm gold. Implies "lasting damage / DoT"
- **control**: Open hand with concentric rings emanating, steel-grey (suggesting CC / lock-down)
- **swarm**: Multiple small dots clustered (~6-8 small circles), suggesting "many small enemies", warm earth-brown

### Row 2
- **dive**: Arrow plunging from upper-left to lower-right, diagonal, warm red (suggesting "jumps into backline")
- **pierce**: Spear / lance head with motion line behind, steel-grey (suggesting "penetrates frontline")
- **heal**: Cross + heart combo (different from affix heal — larger, more ornate at this scale), warm green
- **summon**: Smaller silhouettes appearing from circle (suggesting "summoned units"), violet circle + cream silhouettes

## Style consistency

8 glyphs in same painterly anime icon language as P0 baseline. Each glyph is an **abstract threat type** — not a weapon, not a unit, just the *concept*. Hard outline + sparse cel-edge.

## Color discipline

- Each glyph uses 1-2 tint colors
- Magenta strict — NO tint into icon

## What this is NOT

- NOT a weapon icon set (already have class icons)
- NOT a character icon
- NOT realistic infographic
- NOT cluttered
- NOT readable text
- NOT chibi
- NOT magenta hue on glyph
- NOT different style per cell
```
