---
slug: posture_card_sheet--default
kind: skill_icon
subject_id: posture_card_sheet
variant: default
emotion: default
refs:
  - town_frontier_village:dusk
aspect: "5:2"
output_size: "2560x1024"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: posture_card_sheet (5 posture illustration cards / 5 자세 카드 일러) — sheet

Use case: Tactical Workshop modal 중앙 — 사용자가 선택하는 **5 team posture**의 작은 illustrated scene. SoT [`wiki-combat-posture-tactic-v1`](http://localhost:5830/p/survival-manager/wiki/wiki-combat-posture-tactic-v1) 의 5 posture (HoldLine / StandardAdvance / ProtectCarry / CollapseWeakSide / AllInBackline) 각각을 작은 isometric 일러로 시각화. 일반 icon보다 큰 illustrated scene 톤.

## Output target (sheet_split 분할 후)

- `art-pipeline/output/icons/posture/posture_hold_line.png`
- `art-pipeline/output/icons/posture/posture_standard_advance.png`
- `art-pipeline/output/icons/posture/posture_protect_carry.png`
- `art-pipeline/output/icons/posture/posture_collapse_weak_side.png`
- `art-pipeline/output/icons/posture/posture_all_in_backline.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/posture_card_sheet/default.png `
    --rows 1 --cols 5 `
    --emotions hold_line,standard_advance,protect_carry,collapse_weak_side,all_in_backline `
    --out-dir art-pipeline/output/icons/posture/ `
    --prefix posture
```

```prompt
# Icon Sheet: 5 team posture illustrated cards on a single sheet — for sprite atlas

A 5-cell horizontal sheet of stylized **illustrated posture cards** for a premium mobile JRPG / gacha-banner game's tactical UI. Each cell contains a tiny **isometric scene** depicting 4 unit figures arranged in the formation that the posture name suggests. Same painterly anime style as the project's character roster splash art and hub baseline (`town_frontier_village:dusk` attached as visual baseline ref). Each cell is a card-sized illustration, **not a simple icon** — it shows the posture as a small tactical scene.

## Sheet structure (CRITICAL)

- **2560×1024 canvas = 1 row × 5 columns = 5 cells**
- Each cell ~480×900 active illustration area (taller than wide — vertical card shape)
- **Magenta gutter (#FF00FF, ~30-50px wide)** between cells AND on outer edges
- Each cell pure #FF00FF background EXCEPT the small ground tile patch under figures — the ground patch is painterly umber/grass, suggesting the battle stage floor briefly
- Figures rendered at small scale (each ~60-80px tall in 480px cell width)

## Cell contents (reading order: left-to-right) — 4 figure arrangement per posture

Each cell shows **4 small painterly anime figures** representing a 4-unit squad in the posture configuration. Front row figures slightly closer to viewer (lower in the cell), back row slightly higher. Tiny ground tile under each figure. NO faces — figures are silhouette-level stylized standees at this scale.

### Cell 1: HoldLine (전열 사수)
**4 figures clustered tightly toward the LEFT of the cell** (the ally side). Front row 2 figures (vanguard + duelist silhouettes) holding ground, back row 2 figures (ranger + mystic) standing close behind. Minimal forward spread. Implicit "enemy approaches from right" suggested by faint red glow at right cell edge. Mood: defensive, locked.

### Cell 2: StandardAdvance (표준 전진)
**4 figures evenly spaced in the middle of the cell**, moving slight-right (forward). Front row 2 + back row 2, balanced gap. Mood: balanced, neutral forward step.

### Cell 3: ProtectCarry (캐리 보호)
**4 figures clustered TIGHTLY around a central mystic/ranger carry figure** (slightly back-left). Vanguard front, duelist flanking, ranger or mystic central protected. Implicit "carry needs protection". Mood: defensive cluster.

### Cell 4: CollapseWeakSide (약측 무너뜨리기)
**4 figures angled diagonally toward TOP-RIGHT of cell** (suggesting "leaning toward weak side"). Asymmetric — figures slanted, not symmetric formation. Mood: opportunistic flank.

### Cell 5: AllInBackline (후열 깊이 침투)
**1 dive figure (vanguard or duelist) far to the RIGHT** (deep into enemy territory), with 3 others following behind. Forward-thrust composition. Implicit "dive deep, others follow". Mood: aggressive dive.

## Style — illustrated card scale

This is **card scale illustration**, not simple icon — each cell is a small painterly scene with:
- 4 figure silhouettes (no facial detail at this scale)
- Tiny ground tile (umber dirt / sage grass patch)
- Painterly base + cel-edge at hard equipment (shield rim, blade)
- Soft atmospheric glow per posture mood (cool defensive / neutral / warm aggressive)

NOT icon-simple, NOT character splash full detail. **Mid-scale illustrated card**.

## Color discipline

- Steel-grey / weathered wood / earth-brown palette (matching Town hub baseline + character roster splash)
- Each posture has subtle mood tint:
  - HoldLine: cool slate-blue (defensive)
  - StandardAdvance: neutral mid-tone
  - ProtectCarry: warm cream (protective)
  - CollapseWeakSide: amber-orange (opportunistic)
  - AllInBackline: red accent (aggressive)
- Magenta background **strict** between cells

## Style consistency with hub baseline

Attached reference (`town_frontier_village:dusk`) shows the painterly anime baseline. These 5 posture cards must read as **scenes that could happen on the Wolfpine trail battle stage** — same illustrated world.

## What this is NOT

- NOT a simple icon — illustrated card scale
- NOT a character close-up — 4 figures at standee scale
- NOT a full battle scene — minimal ground tile only
- NOT photoreal terrain
- NOT readable text or numbers
- NOT magical glow effects beyond subtle mood tint
- NOT identical figure poses between cells — each posture has distinct figure arrangement
- NOT chibi — anime-painted figures at small scale
- NOT a different stylization per cell — all 5 in same painterly anime card illustration style
- NOT magenta hue on figures or ground (chroma key forbidden)
```
