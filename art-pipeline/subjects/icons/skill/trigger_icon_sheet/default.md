---
slug: trigger_icon_sheet--default
kind: skill_icon
subject_id: trigger_icon_sheet
variant: default
emotion: default
refs: []
aspect: "2:1"
output_size: "2048x1024"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: trigger_icon_sheet (8 tactic trigger icons / 전술 트리거 아이콘 8종) — sheet

Use case: Tactical Workshop의 per-unit tactic strip 우측 trigger icon — 캐릭터 행동 trigger 조건을 시각화. SoT [`wiki-combat-posture-tactic-v1`](http://localhost:5830/p/survival-manager/wiki/wiki-combat-posture-tactic-v1) 의 6 condition + 2 priority indicator를 8 icon으로 표현.

## Output target (sheet_split 분할 후)

- `art-pipeline/output/icons/trigger/trigger_self_hp.png` (자기 체력 — heart)
- `art-pipeline/output/icons/trigger/trigger_ally_hp.png` (아군 체력 — shield+heart)
- `art-pipeline/output/icons/trigger/trigger_enemy_in_range.png` (사거리 안 적 — target)
- `art-pipeline/output/icons/trigger/trigger_lowest_hp_enemy.png` (가장 체력 낮은 적 — broken heart)
- `art-pipeline/output/icons/trigger/trigger_enemy_exposed.png` (노출된 적 — eye)
- `art-pipeline/output/icons/trigger/trigger_fallback.png` (폴백 — circular arrow)
- `art-pipeline/output/icons/trigger/trigger_priority_high.png` (우선순위 높음 — upward arrow)
- `art-pipeline/output/icons/trigger/trigger_burst.png` (즉발 — bolt)

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/trigger_icon_sheet/default.png `
    --rows 2 --cols 4 `
    --emotions self_hp,ally_hp,enemy_in_range,lowest_hp_enemy,enemy_exposed,fallback,priority_high,burst `
    --out-dir art-pipeline/output/icons/trigger/ `
    --prefix trigger
```

```prompt
# Icon Sheet: 8 tactic trigger icons on a single sheet — for sprite atlas

A 4×2 grid sheet of stylized **abstract trigger icons** for a premium mobile JRPG / gacha-banner game's tactical UI. Same painterly anime icon style as the class / currency icon sheets — clean stylized silhouette + painterly base + sparse cel-edge. Each cell contains a single icon centered on solid #FF00FF magenta background. These are **abstract symbolic icons**, NOT weapon icons — they represent tactic trigger conditions.

## Sheet structure (CRITICAL)

- **2048×1024 canvas = 2 rows × 4 columns = 8 cells**
- Each cell ~480×480 active icon area
- **Magenta gutter (#FF00FF, ~30-50px wide)** between cells AND on outer edges
- Each cell pure #FF00FF background — NO gradient, NO tint
- Icon centered with breathing room ~60-80px from cell edges
- All 8 icons share painterly anime style, NO style drift between cells

## Cell contents (reading order: top-left to bottom-right, row-major)

### Row 1 (top row, 4 cells)

**Cell 1: self_hp (자기 체력)** — A stylized **heart** shape, anatomical-iconic mix, with subtle warm red glow. **Lower half of the heart slightly darker / "drained"** suggesting "HP below threshold". Painterly red.

**Cell 2: ally_hp (아군 체력)** — A small heart **nested inside a shield outline**, suggesting "protect ally". Heart warm red, shield steel-grey rim. The heart is the focal, shield is the framing context.

**Cell 3: enemy_in_range (사거리 안 적)** — A stylized **target reticle** (concentric circles + crosshair) with a small enemy silhouette dot at the center. Steel-grey reticle, red enemy dot. Iconic, NOT photoreal scope.

**Cell 4: lowest_hp_enemy (가장 체력 낮은 적)** — A **cracked heart** silhouette (heart with vertical crack down the middle), suggesting "vulnerable enemy". Painterly red with grey crack interior. Crack subtle, not gore.

### Row 2 (bottom row, 4 cells)

**Cell 5: enemy_exposed (노출된 적)** — A stylized **eye with concentric rings** (like a "spotted" indicator), the iris glowing warm gold. Painterly, anime-styled eye iconography. Conveys "they see / they're seen".

**Cell 6: fallback (폴백)** — A **circular arrow** (counter-clockwise rotation), like a reset/loop indicator. Painterly metallic, steel-grey with subtle warm trim. Clean iconic.

**Cell 7: priority_high (우선순위 높음)** — An **upward chevron arrow** (^^^) or 3 stacked chevrons pointing up, warm gold. Painterly with cel-edge at chevron tips. Conveys "do this first".

**Cell 8: burst (즉발)** — A **lightning bolt** silhouette, vertical zig-zag, warm orange-yellow with white-hot core. Conveys "instant / fast action". Painterly with sharp cel-edge.

## Style consistency

All 8 icons must read at the same scale + weight + outline density. Same painterly anime icon language as class / currency icon sheets — single iconic silhouette per cell, hard outline, sparse cel-edge, anti-realism guardrail.

## Color discipline

- Each icon uses 1-2 tint colors max (red / gold / steel-grey / etc.)
- Magenta background **strict** — NO tint into icon area
- NO magenta hue on icon
- Background gutter pure #FF00FF for sheet_split chroma detection

## What this is NOT

- NOT realistic weapon icons (this sheet is trigger conditions, not weapons)
- NOT photoreal symbols
- NOT decorative ornament — clean iconic
- NOT magical glowing rune set — minimal glow only where called out (eye iris, bolt core)
- NOT cluttered — single symbol centered per cell
- NOT a different style per cell — all 8 in identical painterly anime icon style
- NOT readable text or numbers on any icon
- NOT chibi / mascot — anime-painted iconic symbol
```
