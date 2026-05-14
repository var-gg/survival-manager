---
slug: class_icon_sheet--default
kind: skill_icon
subject_id: class_icon_sheet
variant: default
emotion: default
refs: []
aspect: "4:1"
output_size: "2048x512"
chroma: "#FF00FF"
status: idea
---

# Icon Sheet: class_icon_sheet (4 class identifier icons / 4 class 식별 아이콘) — sheet

Use case: UI class identifier icon — 4 class (Vanguard 전위 / Duelist 결투가 / Ranger 궁수 / Mystic 신비). Town UI 전체 modal에서 class 표시 chip + Tactical Workshop per-unit tactic strip + Recruit card + Passive Board tab + Equipment Refit 우측 inventory category 등에 재사용된다. 1×4 grid sheet으로 한 컷에 받아 `sheet_split.py`로 분할.

## Output target (sheet_split 분할 후)

- `art-pipeline/output/icons/class/class_vanguard.png`
- `art-pipeline/output/icons/class/class_duelist.png`
- `art-pipeline/output/icons/class/class_ranger.png`
- `art-pipeline/output/icons/class/class_mystic.png`

## Sheet 분할 호출 (시안 받은 후)

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/class_icon_sheet/default.png `
    --rows 1 --cols 4 `
    --emotions vanguard,duelist,ranger,mystic `
    --out-dir art-pipeline/output/icons/class/ `
    --prefix class
```

```prompt
# Icon Sheet: 4 class identifier icons on a single sheet — for sprite atlas

A 4-cell horizontal sheet of stylized class identifier icons for a premium mobile JRPG / gacha-banner game's UI. Each cell contains a single class symbol silhouette centered on solid #FF00FF magenta background. Anime-painted icon style — clean stylized silhouette + painterly base + sparse cel-edge at hard edges. NOT realistic, NOT photoreal, NOT gritty. Same illustrated world as the project's character roster splash art, but icon-scale (not full character art).

## Sheet structure (CRITICAL — for sheet_split chroma boundary detection)

- **2048×512 canvas = 1 row × 4 columns = 4 cells**
- Each cell ~480×480 active icon area
- **Magenta gutter (#FF00FF, ~30-50px wide)** between cells AND on outer edges
- Each cell uses pure #FF00FF as background — NO gradient, NO tint, NO patterned bg
- Icon silhouette centered in its cell with breathing room ~60-80px from cell edge
- All four icons share the same illustration style and palette discipline — they read as a set

## Cell contents (reading order: left-to-right)

### Cell 1: Vanguard (전위) — Shield
A stylized **tower shield**, viewed slight 3/4 perspective. Steel-grey body with a simple stylized sunburst sigil (Solarum motif) embossed at center. Subtle **sapphire blue rim hint** at top edge (Vanguard family color). NO ornate filigree, NO weapon attached — just the shield as class identity. Painterly metallic finish, no chrome reflections.

### Cell 2: Duelist (결투가) — Twin blades crossed
Two slim daggers crossed in X formation, viewed slight 3/4. Steel-grey blades with deep navy + silver hilt wrapping. Hilts simple, no jewels. **Red tint hint** at the blade tips suggesting speed/cut (Duelist family color). NOT motion blur — stylized speed line accent only. Just twin blades, no holder.

### Cell 3: Ranger (궁수) — Drawn bow
A stylized longbow, viewed diagonal 3/4, arrow nocked and string at half-draw tension. Warm weathered wood body, leather grip wrapping. **Forest green hint** at bow grip and arrow fletching (Ranger family color). Arrow head simple silver, no flame or enchant glow. NOT a battle-scarred weapon, NOT photoreal grain — stylized iconic bow.

### Cell 4: Mystic (신비) — Crystal staff
A vertical staff standing centered in the cell, with a stylized **violet crystal orb** at the top. Staff body warm weathered wood or pale bone, simple no carving. **Soft violet glow** in the orb (Mystic family color). Faint particle hint around the orb — NOT a full magical effect, just subtle suggestion. Bottom of staff grounded firmly.

## Style (CRITICAL — all 4 cells consistent)

- Same painterly anime icon style across all 4 cells
- Same hard outline weight + same cel-edge sparingly at hard edges (blade edges, shield rim, staff orb)
- Same painterly base for organic surfaces (wood grain, leather wrap)
- Same magenta background discipline
- Clean iconic silhouette readable at 64×64 final size

## Color discipline

- Steel-grey + class family tint per cell:
  - Vanguard: sapphire blue rim
  - Duelist: red tip glow
  - Ranger: forest green grip
  - Mystic: violet orb
- Magenta background **strict** — no tint into icon area
- NO magenta hue on the icon itself (chroma key forbidden color)
- NO deep magenta / fuchsia / hot pink on icon — keep below #C040A0

## What this is NOT

- NOT a full weapon close-up with hand or character — class identifier silhouette only
- NOT decorative ornament — clean iconic shape
- NOT magical glowing runes — minimal class tint hint only (Mystic orb glow is the only allowed magical accent)
- NOT photoreal weapon photography
- NOT cluttered — single silhouette centered in each cell, breathing room
- NOT a different stylization per cell — all 4 cells in identical painterly anime icon style
- NOT realistic metal scratching / leather wear — clean stylized surface
- NOT gradient background — strict #FF00FF flat magenta
- NOT character holding the weapon — weapon / shield / staff alone
- NOT chibi / overly cartoony — anime-painted icon, not mascot icon
```
