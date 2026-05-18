---
slug: ui_compendium--dusk_v2
kind: environment_site
subject_id: ui_compendium
variant: dusk_v2
emotion: default
refs:
  - hero_dawn_priest:portrait_full
  - town_frontier_village:dusk
  - ui_inventory:dusk
  - ui_theater:dusk
  - ui_compendium:dusk
  - ui_compendium_unity_current
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: rendered
---

# UI Mockup: ui_compendium (시스템 / 스킬 도감) — dusk v2

Use case: System Compendium modal 2차 기준 시안. This pass must bridge the generated v1 REF and the current Unity Play Mode implementation screenshot. It should become the canonical example for future REF → implementation → REF UI calibration work.

```prompt
# UI Mockup v2: System Compendium modal — Skills tab selected, REF-calibrated

Create a polished 1920x1080 illustrated UI mockup screenshot for a premium anime-painted strategy RPG "System Compendium" modal floating over the dusk frontier town hub. This is the second-pass mockup for UI calibration, so it must preserve the current Unity layout structure while upgrading it toward the richer v1 reference style.

**REFERENCE ROLE LOCK**
- `town_frontier_village:dusk` is the world/backdrop and dusk color baseline.
- `ui_inventory:dusk` is the catalog + selected detail structure reference.
- `ui_theater:dusk` is the preview/replay frame reference.
- `ui_compendium:dusk` is the first-pass ideal mood reference: ornate dark frame, rich skill catalog, selected detail, VFX theater.
- `ui_compendium_unity_current` is the current implementation constraint. Preserve its real layout proportions: wide dark modal, header, four tabs, filter row, left two-column skill catalog, right selected skill detail, VFX theater, metrics area.

**STYLE LOCK (CRITICAL)**: Same visual family as the Town UI gallery. Anime-leaning painterly game UI, sparse cel-edge, dark slate/navy glass, warm gold trims, restrained parchment texture, premium indie strategy RPG. Not photoreal, not oil-paint realism, not a flat web dashboard, not raw debug UI.

**TEXT GUARD**: All text must be abstract unreadable glyph blocks. Do not render readable Korean, English, IDs, numbers, or code-like labels. Use glyph bars, short pseudo-runes, dots, and icon chips only.

**FIGURE GUARD**: The character portrait ref is style/palette only. Do not place a large character figure in the modal. Caster/target markers inside the VFX stage may be tiny abstract tokens only.

## Composition

Use the current Unity implementation as the skeleton:
1. Dim dusk town hub backdrop.
2. Large centered modal occupying most of the screen, dark navy base with warm gold border.
3. Header with a compact seal/emblem on the left, title glyph block, subtitle glyph line, close button on the right.
4. Four tabs: Skills selected, Status, Synergy, Characters. Skills selected is warm gold; the other tabs are cool slate with gold trim.
5. Filter row: search field, class dropdown, slot dropdown, VFX family dropdown, result count glyph at far right.
6. Main content: left catalog shell, right detail rail.

## Modal frame and reusable UI grammar

Make the modal frame a reusable Town UI grammar example:
- Dark navy frosted glass body, not light parchment.
- Warm gold outer stroke, thin inner stroke, subtle parchment wash layer.
- Small corner caps and restrained vine/flourish hints, but still buildable in Unity UITK.
- Header seal or emblem should be painterly and symbolic, not readable.
- Border ornaments should feel like a high-quality game UI skin, not heavy fantasy carving.

## Left catalog rail — skill card grid

Left side is a scrollable two-column skill catalog, matching the current Unity layout. Show enough visible cards to imply a large skill catalog.

Each skill card:
- Distinct square skill icon, painterly and game-ready.
- Intent chip at top: support, control, damage, guard, recovery, aura represented by color/icon/glyph.
- Title glyph block.
- Slot/class chips as compact tags.
- One compact power/cooldown line using glyph bars and small dots.
- Optional status payload strip.
- Selected card has a warm gold rim and slightly brighter fill.

Important: avoid repeated placeholder icons. The visible icons should be clearly different: shield interception, echo wave, tactical signal, swift arrow, ice shard, flame slash, healing bloom, binding sigil.

## Right detail rail — selected skill

The selected detail rail should feel like a polished in-game encyclopedia, not a developer inspector.

Top identity block:
- Large square selected skill icon in a framed icon shell.
- Title glyph block, subtitle glyph line, and 1-2 description glyph lines.
- Gameplay keyword chips below the description: damage type, delivery, target, cooldown, status, VFX family. These are chip shapes with small symbols and glyph bars.
- Keep the top block readable and prevent overlap.

VFX mini theater:
- Large rectangular preview stage below the identity block.
- Stage floor tint, faint tactical grid, caster reticle left, target reticle right.
- Projectile/route line traveling left to right and impact rings near target.
- HUD chips along the top edge of the stage, all abstract glyphs.
- Replay/play button below or at the edge, warm gold trim.
- This stage should be the emotional focus: it looks like clicking a skill in the codex previews the effect.

Metrics / detail rows:
- Below the theater, include compact separated data rows, but style them as game UI readouts.
- Do not show code-like ID labels; use abstract labels and bars.
- The rows should support developer review without looking like a raw spreadsheet.

## Calibration goal

This v2 mockup should sit between two references:
- More ornate, painterly, and premium than the current Unity screenshot.
- More structurally faithful and implementable than the v1 ideal mockup.

The result must be something a Unity UITK implementation can chase directly: frame layers, catalog card states, detail identity block, keyword chips, VFX theater, metrics rows, and scrollbar all readable.

## Palette

- Backdrop: dim dusk village, slate-blue with warm horizon.
- Modal: deep navy / charcoal glass.
- Lines: warm antique gold, restrained.
- Detail block: slightly warmer dark panel.
- Skill icon colors: varied but harmonious. Guard blue-gold, support teal, control violet-blue, damage orange-red, recovery green-gold, frost cyan.
- Avoid one-note purple or flat monochrome.

## Negative

Do not create a landing page. Do not use readable text. Do not add extra characters. Do not turn the screen into a browser dashboard. Do not over-ornament into unreadable fantasy parchment. Do not show debug code labels, raw IDs, or repeated identical icons. Do not crop the modal. Do not let UI elements overlap.
```
