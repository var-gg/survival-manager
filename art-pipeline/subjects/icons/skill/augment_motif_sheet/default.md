---
slug: augment_motif_sheet--default
kind: skill_icon
subject_id: augment_motif_sheet
variant: default
emotion: default
refs: []
aspect: "4:3"
output_size: "2048x1536"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: augment_motif_sheet (12 augment symbolic motifs / 12 증강 상징 모티프) — sheet

Use case: Permanent Augment modal — 12 unlocked candidate card grid. 각 augment는 distinct symbolic motif (불꽃/뱀/별/방패/달/검/눈/날개/뿔/가시/봉인/공허). 큰 detail card에도 같은 motif 재사용. 4×3 grid sheet.

## Output target (sheet_split 분할 후)

- `art-pipeline/output/icons/augment/augment_flame.png`
- `augment_serpent.png`, `augment_star.png`, `augment_shield.png`
- `augment_moon.png`, `augment_blade.png`, `augment_eye.png`, `augment_wing.png`
- `augment_horn.png`, `augment_thorn.png`, `augment_seal.png`, `augment_void.png`

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/augment_motif_sheet/default.png `
    --rows 3 --cols 4 `
    --emotions flame,serpent,star,shield,moon,blade,eye,wing,horn,thorn,seal,void `
    --out-dir art-pipeline/output/icons/augment/ `
    --prefix augment
```

```prompt
# Icon Sheet: 12 augment symbolic motif icons — for sprite atlas

A 4×3 grid sheet of stylized symbolic motif icons for a premium mobile JRPG / gacha-banner game's permanent augment system. Each cell contains a single distinct symbolic motif centered on solid #FF00FF magenta background. Same painterly anime icon style as the class / currency / trigger sheets — clean stylized silhouette + painterly base + sparse cel-edge at hard edges. Each motif feels **mythic / arcane / fundamental** — abstract symbol of a permanent power source.

## Sheet structure (CRITICAL)

- **2048×1536 canvas = 3 rows × 4 columns = 12 cells**
- Each cell ~480×480 active icon area
- **Magenta gutter (#FF00FF, ~30-50px wide)** between cells AND on outer edges
- Each cell pure #FF00FF background — NO gradient
- Icon centered with breathing room ~60-80px from cell edges
- All 12 motifs in same painterly anime icon style

## Cell contents (reading order: top-left to bottom-right, row-major)

### Row 1
- **Cell 1 (flame)**: A stylized **ember flame**, vertical, warm orange-red core with subtle gold outer flicker. Painterly flame shape, not realistic fire physics
- **Cell 2 (serpent)**: A coiled **serpent silhouette**, S-curve, dark scaled body with single highlight along the spine. Subtle violet undertone
- **Cell 3 (star)**: An 8-pointed **stylized star**, gold center with cool white-blue outer rays. Iconic, not photoreal astronomy
- **Cell 4 (shield)**: A **convex round shield** (different from class vanguard tower shield), warm bronze with concentric ring pattern center. Solid defensive symbol

### Row 2
- **Cell 5 (moon)**: A **crescent moon** silhouette, cool pale-silver against subtle warm halo. Painterly soft edge
- **Cell 6 (blade)**: A single **straight blade** standing vertically, steel-grey with dark navy hilt wrap. Different from class duelist twin blades — single solitary
- **Cell 7 (eye)**: An **open eye iris** with vertical pupil slit, deep violet iris + faint gold ring. Mystic / watching motif. Different from trigger enemy_exposed (which had flame eyelash) — this is calmer, more arcane
- **Cell 8 (wing)**: A single **stylized wing** silhouette, feathered, warm cream with subtle gold tip highlights. Spread but partial — one wing only

### Row 3
- **Cell 9 (horn)**: A spiraling **ram or beast horn**, weathered bone-cream with dark base. Spiral curve readable
- **Cell 10 (thorn)**: A **thorny vine** twisted into a small bundle, dark green-grey with subtle red blood-tip suggestion. Painterly, not photoreal botanical
- **Cell 11 (seal)**: A **circular seal** (like a wax stamp), warm gold rim with stylized geometric glyph at center (abstract — could be a triangle or rune-like). Sacred binding motif
- **Cell 12 (void)**: A **black sphere** with cool violet event-horizon ring, suggesting "absence / null". Empty center, contained nothingness. Painterly soft edge with one bright pinpoint at center

## Style consistency

All 12 motifs in same painterly anime icon language as P0 sheet baseline. Single iconic silhouette per cell. Hard outline + sparse cel-edge at hard geometric edges. Painterly base for organic surfaces (flame, scale, feather). No motif drift between cells.

## Color discipline

Each motif uses 1-2 tint colors:
- flame: orange-red + gold
- serpent: dark scaled + violet
- star: gold + cool white-blue
- shield: bronze
- moon: cool silver + warm halo
- blade: steel-grey + navy
- eye: violet + gold
- wing: cream + gold
- horn: bone-cream
- thorn: dark green + red tip
- seal: gold + abstract glyph
- void: black + violet horizon

Magenta background strict — NO tint into icon. NO magenta hue on icon.

## What this is NOT

- NOT realistic photography
- NOT decorative ornament beyond essential motif
- NOT a magical full effect — symbol with restrained magical hint only
- NOT readable text or numbers
- NOT chibi / mascot
- NOT cluttered — single symbol centered per cell
- NOT different style per cell — 12 cells in identical painterly anime icon style
- NOT magenta hue on motif (chroma key forbidden)
```
