---
slug: faction_sigil_sheet--default
kind: skill_icon
subject_id: faction_sigil_sheet
variant: default
emotion: default
refs: []
aspect: "4:1"
output_size: "2048x512"
chroma: "#FF00FF"
status: idea
---

# Sheet: faction_sigil_sheet (4 인간 세력 sigil / 4 faction sigil) — frame chrome decor

Use case: Town UI / Theater chapter banner / Recruit candidate card / Equipment 무기 새겨진 sigil 등 4 인간 세력 식별 marker. SoT [`wiki-world-building-bible-mirror`](http://localhost:5830/p/survival-manager/wiki/wiki-world-building-bible-mirror) 4 인간 세력 (Solarum / Wolfpine / Pale Conclave / Lattice Order).

## Output target

- `art-pipeline/output/icons/faction/faction_solarum.png` (광휘 왕좌의 잔당 sunburst)
- `art-pipeline/output/icons/faction/faction_wolfpine.png` (이리솔 부족 wolf pine 토템)
- `art-pipeline/output/icons/faction/faction_pale_conclave.png` (회상 결사 memory vessel)
- `art-pipeline/output/icons/faction/faction_lattice_order.png` (그물 결사 lattice knot)

## Sheet 분할 호출

```powershell
python art-pipeline/postprocess/sheet_split.py `
    art-pipeline/output/faction_sigil_sheet/default.png `
    --rows 1 --cols 4 `
    --emotions solarum,wolfpine,pale_conclave,lattice_order `
    --out-dir art-pipeline/output/icons/faction/ `
    --prefix faction
```

```prompt
# Sheet: 4 faction sigil icons — for UI chrome / banner / item decoration

A 4-cell horizontal sheet of stylized faction sigil icons for a premium mobile JRPG / gacha-banner game's UI. Each cell contains a single circular sigil emblem centered on solid #FF00FF magenta background. Same painterly anime icon style as P0 sheet baseline. Each sigil is **a paint emblem** (NOT magical glowing rune, NOT photoreal metal embossing) representing one of the 4 human factions.

## Sheet structure (CRITICAL)

- **2048×512 canvas = 1 row × 4 cols = 4 cells**, each ~480×480 active area
- Magenta gutter (#FF00FF, ~30-50px) between cells and outer edges
- Each cell pure #FF00FF background
- Sigil emblem centered with breathing room

## Cell contents (reading order)

### Cell 1: Solarum (광휘 왕좌의 잔당)
A stylized **8-rayed sunburst** within a circular border, faded paint look (NOT freshly minted). Steel-grey base with subtle warm gold rays. The rays radiate outward from a central dot. Border has minor faded chip suggesting age (잔당 정체성). Cool warm restraint — not bright golden, more weathered "lost glory" tone.

### Cell 2: Wolfpine Tribes (이리솔 부족)
A stylized **pine tree silhouette flanked by two wolf head silhouettes** facing inward, contained in a rough circular border. Forest-green and warm umber. Border has organic texture (carved wood feel rather than minted metal). Tribal tribal totem aesthetic. Bone-white wolf eyes as small dot accents.

### Cell 3: Pale Conclave (회상 결사)
A stylized **closed eye or sealed urn** silhouette within a circular border. Cool pale-grey base with subtle violet-green inner glow. Border ornate with simple memorial knot pattern. Mood: stored memory, sealed. Faint inner radiance suggesting "contained knowledge".

### Cell 4: Lattice Order (그물 결사)
A stylized **square knot or interlaced lattice pattern** (4-strand knot) within a circular border. Cool deep violet + crystal white. Pattern intricate but flat (not 3D), 4 strands interweaving symmetrically. Mood: 1800-year silence, woven secret. Faint crystal hint at lattice intersections.

## Style consistency

All 4 sigils in same painterly anime icon style as P0 baseline. Same outline weight, same cel-edge density. **Paint emblem aesthetic** — faded paint, NOT magical glow, NOT freshly stamped metal. Each readable at 64×64 final UI size.

## Color discipline

- Solarum: steel-grey + faded gold
- Wolfpine: forest-green + warm umber + bone-white
- Pale Conclave: pale-grey + violet-green
- Lattice Order: deep violet + crystal white
- Magenta strict — NO tint into sigil

## What this is NOT

- NOT magical glowing rune (paint only, minimal inner radiance)
- NOT photoreal metal stamping
- NOT freshly minted heraldry (faded paint feel for all 4)
- NOT readable text or letters
- NOT cluttered ornament
- NOT chibi / mascot
- NOT magenta hue on sigil
```
