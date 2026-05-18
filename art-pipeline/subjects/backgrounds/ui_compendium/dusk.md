---
slug: ui_compendium--dusk
kind: environment_site
subject_id: ui_compendium
variant: dusk
emotion: default
refs:
  - hero_dawn_priest:portrait_full
  - town_frontier_village:dusk
  - ui_inventory:dusk
  - ui_theater:dusk
  - ui_permanent_augment:dusk
  - ui_passive_board:dusk
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: rendered
---

# UI Mockup: ui_compendium (시스템 / 스킬 도감) — dusk

Use case: Town 씬의 **System Compendium modal** 1차 기준 시안. Skills / Status / Synergy / Characters 도감 중 가장 구현이 닫힌 **Skills tab selected** 상태를 먼저 시각화한다. 이 시안은 `pindoc://analysis-compendium-ui-mockup-brief-v0`의 1차 mockup 브리프를 따른다.

```prompt
# UI Mockup: System Compendium modal — Skills tab selected, dusk village backdrop

An illustrated UI mockup screenshot of a premium mobile JRPG / strategy RPG game's System Compendium modal, floating over the frontier village hub at dusk. The Compendium is an always-visible reference catalog for skills, status effects, synergies, and characters. This specific screen shows the **Skills** tab selected. 16:9 wide.

**STYLE LOCK (CRITICAL)**: Same visual family as the attached Town UI mockup gallery references. Use the attached `town_frontier_village:dusk` as the world/backdrop baseline, `ui_inventory:dusk` as the catalog grid + selected detail structure reference, and `ui_theater:dusk` as the preview/replay frame reference. Anime-leaning painterly base, sparse cel-edge, premium mobile RPG UI, slate-blue frosted glass, warm gold accents, subtle parchment texture. NOT photoreal, NOT oil-painted realism, NOT wireframe, NOT generic web dashboard.

**FIGURE GUARD**: The character portrait ref is only a stylization reference. Do not place a large character figure in the modal. The only character-like marks allowed are tiny abstract caster/target markers inside the VFX preview theater.

**TEXT GUARD**: All text must be abstract unreadable glyph blocks. Do not render readable Korean, English, or numbers. Use glyph bars, dots, short pseudo-runes, and icon labels only.

## Composition

Layered composition:
1. **Background layer**: frontier village hub at dusk, recognizable but quiet, dimmed about 30-35% with a cool slate tint.
2. **Main modal**: centered frosted-glass / parchment hybrid panel, about 82% width and 86% height. Warm parchment rim, steel-grey trim, soft inner glow.
3. **Compendium layout**: header, tab strip, filter row, left catalog rail, right detail rail with VFX mini theater.

### Header and tabs

Top header bar:
- Left: compact title block with abstract glyph title and small subtitle line.
- Right: small result counter glyph and close button.
- Below header: four tab buttons — Skills selected, Status, Synergy, Characters.
- Skills tab has warm gold underline / cap ornament, other tabs cool slate.
- Tab icons should be symbolic and painterly: skill spark, status knot, synergy link, character silhouette.

### Filter row

Under the tab strip, a narrow filter row:
- Search field with magnifier icon and glyph placeholder.
- Three compact chip/dropdown controls: class, slot, VFX family.
- Small count/summary glyph at far right.
- The row should look like game UI controls, not a browser form.

### Left catalog rail — skill card grid

Left side is a scrollable catalog rail, about 48% of modal width. Show a **2-column grid of skill cards**, enough cards visible to imply an 88-skill catalog.

Each skill card:
- Square skill icon, painterly, centered. Icons should look like finalized game skill icons: protection shield, projectile arrow, memory sigil, recovery glow, control snare, melee burst. Each icon distinct.
- Small intent chip near the top: damage / protection / recovery / control / support, represented by color/icon/glyph, not readable text.
- Short title glyph block.
- Two tiny chips for slot and class.
- One compact stat line with power/cooldown placeholder bars.
- Optional status payload dot or tiny status icon strip on some cards.
- Selected card has restrained warm gold rim glow.

The card rail should feel dense but readable, more like a game catalog than a spreadsheet.

### Right detail rail — selected skill

Right side is a selected skill detail panel, about 52% of modal width.

Top detail block:
- Large square selected skill icon at upper left.
- Title glyph, slot/class chips, VFX family/skin chips.
- Effect summary block with 3-4 short abstract glyph lines.
- Keyword/status chip row with small icons: barrier, guarded, burn, silence, root, cleanse-like motifs.
- Scaling/cooldown/readout rows: label glyph + value glyph, compact and game-like.

### VFX mini theater preview

Below the effect summary, a large rectangular **VFX mini theater** preview frame. This is the emotional focus of the Compendium.

Inside the preview:
- Small caster marker on the left, target marker on the right.
- Route line/projectile path traveling left-to-right.
- Impact burst rings near the target.
- A faint tactical grid / stage floor.
- Three HUD chips on top edge: family/skin, route, prefab/hook, all as glyph blocks.
- Replay button or small round play icon at bottom-right edge.
- The preview should echo the Theater mockup's replay frame, but it is a combat-skill preview, not story video.

Use painterly UI shapes and soft glow, not raw vector debug lines. The VFX preview should make the selected skill feel inspectable in Play Mode.

### Secondary catalog hints

Subtly imply the other tabs without clutter:
- Status tab preview: tiny status family icons in the tab row or edge legend.
- Synergy tab preview: small linked-node motif.
- Characters tab preview: small portrait/silhouette motif.

Do not show full separate panels for all tabs. This image is Skills-selected.

## Mood

**Readable reference codex.** The player should feel this is a premium in-game encyclopedia that doubles as a developer-friendly review tool: every skill has an icon, card, effect summary, and VFX preview. It should feel calmer than Recruit, more functional than Theater, and more visually game-like than a debug inspector.

## Palette

- Background: dim dusk village, slate-blue, warm horizon.
- Modal: frosted slate glass + warm parchment rim.
- Accents: restrained warm gold for selected state and primary controls.
- Skill icon colors: varied but harmonious; protection blue-gold, recovery green-gold, control violet-blue, damage orange-red, memory teal-violet.
- Locked/disabled states not dominant in this Skills view.

## Reproduction feasibility

The design should be easy to translate to Unity UITK:
- Modal and cards are simple framed shapes, not ornate fantasy carvings.
- Tab/button/card states use border, fill, icon, and subtle glow.
- VFX theater uses simple layers: grid, marker circles, route bar/projectile, burst rings, HUD chips.
- Use abstract glyph blocks instead of actual text.
- Avoid nested card-inside-card clutter; every card is an individual catalog item, detail rail is a panel.

## What this is NOT

- NOT a wireframe or flat web dashboard
- NOT an editor inspector with raw data rows
- NOT a character splash screen
- NOT a full battle HUD
- NOT readable text
- NOT neon sci-fi; this remains frontier-town fantasy strategy UI
- NOT heavily ornamental magical parchment; keep it premium, restrained, and buildable
```
