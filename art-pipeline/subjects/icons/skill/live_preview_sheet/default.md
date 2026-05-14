---
slug: live_preview_sheet--default
kind: skill_icon
subject_id: live_preview_sheet
variant: default
emotion: default
refs:
  - town_frontier_village:dusk
aspect: "3:1"
output_size: "2304x768"
chroma: "#FF00FF"
status: idea
---

# Sheet: live_preview_sheet (3 sandbox combat snapshot) — Tactical Workshop Live Preview window

Use case: Tactical Workshop modal 우측 Live Preview frame — 3 sandbox scenario (anti_burst / endgame_fortress / balanced_opening) 의 mid-engagement painterly snapshot.

## Output target

- `art-pipeline/output/icons/preview/preview_anti_burst.png`
- `art-pipeline/output/icons/preview/preview_endgame_fortress.png`
- `art-pipeline/output/icons/preview/preview_balanced_opening.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/live_preview_sheet/default.png `
    --rows 1 --cols 3 `
    --emotions anti_burst,endgame_fortress,balanced_opening `
    --out-dir art-pipeline/output/icons/preview/ `
    --prefix preview
```

```prompt
# Sheet: 3 sandbox combat preview snapshots — for Tactical Workshop Live Preview

A 1×3 horizontal sheet of **small painterly battle scene snapshots** — each cell shows a tiny mid-engagement battle frame with 4 ally figures (left side) vs enemy figures (right side). Same painterly anime style as the project's character roster splash and Town hub baseline (`town_frontier_village:dusk` attached as ref). Each cell is a card-scale illustrated scene, NOT a simple icon — but smaller than full character art.

## Sheet structure (CRITICAL)

- **2304×768 canvas = 1 row × 3 cols = 3 cells**, each ~720×720 active scene
- Magenta gutter (#FF00FF, ~30-50px) between cells and outer edges
- Each cell pure #FF00FF background EXCEPT the small umber/grass ground tile under figures
- Figures at small standee scale (face detail absent)

## Cell contents (left-to-right)

### Cell 1: anti_burst
4 ally figures clustered defensively on the **left** of the cell — vanguard front raising a shield, duelist behind shield, ranger+mystic further back. 1 enemy mystic on the **right** casting a burst spell — red explosion blast caught against the vanguard shield mid-air between forces. Mood: tank stops the burst, defensive wall holds.

### Cell 2: endgame_fortress
4 ally figures **spread across the cell forming a long defensive line** facing right. Vanguard front-center with raised shield, duelist + ranger flanking, mystic supporting from back. 2-3 enemy figures pushing toward the line from right edge with weapons drawn. Mood: late-stage attrition fortress, multiple waves.

### Cell 3: balanced_opening
4 ally figures advancing balanced from the **left**, 4 enemy figures advancing from the **right**, meeting mid-cell. Vanguard vs vanguard front, duelist clashing, ranger arrows mid-flight, mystic spell glow building. Mood: opening engagement, equal forces meeting.

## Style consistency

Same painterly anime brushwork as character roster + hub baseline. Figures at small standee scale (silhouette + class identifiers like shield / blade / bow / staff readable, but no facial detail). Tiny ground tile umber dirt / sage grass under each side. Each cell has subtle mood tint:
- anti_burst: cool slate + warm red explosion focal
- endgame_fortress: warm earth + steel grey defensive
- balanced_opening: warm gold + clashing sparks

## What this is NOT

- NOT a full battle scene (small standee scale)
- NOT character close-up
- NOT photoreal
- NOT cluttered
- NOT readable text
- NOT magenta hue on figures or ground
```
