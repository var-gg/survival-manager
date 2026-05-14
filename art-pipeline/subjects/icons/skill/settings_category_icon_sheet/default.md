---
slug: settings_category_icon_sheet--default
kind: skill_icon
subject_id: settings_category_icon_sheet
variant: default
emotion: default
refs: []
aspect: "5:1"
output_size: "2560x512"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: settings_category_icon_sheet (5 Settings sidebar category icons / 설정 카테고리 5종) — sheet

Use case: Settings/Pause modal 좌측 sidebar — 5 카테고리 (Display / Audio / Control / Account / Help). 작은 sidebar tab icon.

## Output target (sheet_split 분할 후)

- `settings_display.png`, `settings_audio.png`, `settings_control.png`, `settings_account.png`, `settings_help.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/settings_category_icon_sheet/default.png `
    --rows 1 --cols 5 `
    --emotions display,audio,control,account,help `
    --out-dir art-pipeline/output/icons/settings/ `
    --prefix settings
```

```prompt
# Icon Sheet: 5 Settings category icons on a single sheet — for sprite atlas

A 5-cell horizontal sheet of stylized Settings sidebar category icons for a premium mobile JRPG / gacha-banner game's options UI. Each cell contains a single utility icon centered on solid #FF00FF magenta background. Same painterly anime icon style as P0 sheet baselines — clean stylized silhouette + painterly base + sparse cel-edge.

## Sheet structure (CRITICAL)

- **2560×512 canvas = 1 row × 5 columns = 5 cells**
- Each cell ~480×480 active icon area
- **Magenta gutter (#FF00FF, ~30-50px)** between cells and outer edges
- Each cell pure #FF00FF background
- Icon centered with breathing room ~60-80px from cell edges

## Cell contents (reading order: left-to-right)

- **Cell 1 (display)**: A stylized **monitor / screen frame** with subtle abstract image inside (mountain silhouette), steel-grey body. Painterly with cel-edge at frame
- **Cell 2 (audio)**: A **musical note** + **sound wave arcs** combo, warm gold. Clean iconic
- **Cell 3 (control)**: A stylized **gamepad** silhouette (NOT photoreal — abstract iconic shape), steel-grey with subtle warm trim. Could also be keyboard+mouse silhouette if simpler
- **Cell 4 (account)**: A stylized **person silhouette in circle** (avatar / account symbol), warm cream
- **Cell 5 (help)**: A stylized **question mark inside speech bubble**, warm gold question mark, parchment-cream bubble

## Style consistency

5 icons in same painterly anime icon style as P0 baseline. Same outline weight, same cel-edge density, same magenta background discipline. Each readable at 64×64 final UI size.

## Color discipline

- Display: steel-grey + subtle warm hint
- Audio: warm gold
- Control: steel-grey + warm trim
- Account: warm cream
- Help: warm gold + parchment-cream
- Magenta strict

## What this is NOT

- NOT photoreal device photography
- NOT realistic icon set (anime-painted, not corporate)
- NOT decorative ornament
- NOT readable text on icons (NO letters)
- NOT cluttered
- NOT chibi / mascot
- NOT magenta hue on icon
- NOT different style per cell
```
