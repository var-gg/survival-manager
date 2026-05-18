---
slug: ui_art_bible--master_tone_v1
kind: ui_mockup_reference
subject_id: ui_art_bible
variant: master_tone_v1
emotion: default
refs:
  - town_frontier_village:dusk
  - ui_inventory:dusk
  - ui_theater:dusk
  - ui_compendium:dusk_v2
  - ui_compendium_unity_current
aspect: "16:9"
output_size: "1920x1080"
chroma: false
status: rendered
---

# UI Art Bible Master Tone v1

```prompt
# UI Art Bible v1 — Master Tone Reference

Create one 1920x1080 full-screen UI mockup reference that locks the final UI tone for a premium anime fantasy strategy RPG.

This is the master tone reference for all future UI panels and reusable component generation. Use the attached Compendium v2 mockup and Unity capture as the structural anchor, and the town/inventory/theater refs as visual family anchors. Do not create a new panel concept. Create the most polished, unified version of the System Compendium surface that can become the game's UI Art Bible.

## Canonical palette

- bg_deep_navy: #101522
- bg_mid_navy: #1a1f3a
- bg_panel_warm: #2a2630
- gold_primary: #d4a544
- gold_highlight: #f0c060
- text_primary: #e8e4d8
- text_muted: #9b927c
- danger_locked: #8f3e3e
- heal_accent: #5ce7a3
- arcane_accent: #76d4ff

The output should visibly lock these relationships: dark navy/charcoal body, antique warm-gold trim, warm parchment wash only as a low-opacity information layer, and colored gameplay accents used sparingly.

## Material and component grammar

Use this material mix:
- Dark stone / charcoal glass panel body.
- Antique warm gold metal trims with painterly bevel.
- Subtle parchment texture only inside readable information zones, never bright full-panel parchment.
- Recessed icon slots with gold bevel and dark interior.
- Selected/hover glow as separate state language, visible but controlled.

Make the UI look like it could be rebuilt from reusable pieces:
- panel_frame_outer
- panel_frame_inner
- card_frame_normal
- card_frame_selected + separate glow overlay
- icon_slot_frame
- button_gold / button_dark
- input_bg / dropdown_bg
- tab_active / tab_inactive
- divider_horizontal with small diamond endpoints
- header_decoration_center
- scroll_thumb / scroll_track

## Ornament density lock

The density should be premium but restrained:
- Outer modal: corner caps and sparse edge ticks only.
- Header: one central rune/flourish line or center ornament.
- Cards: small corner accents only.
- Stretch edges: clean, repeatable, no center-edge diamonds unless they do not stretch.
- Dividers: thin gold line with small diamond/cap endpoints.
- Buttons: small ornamental ends, readable shape.

Avoid heavy carved fantasy borders, giant corner plates, and any ornament that crosses text blocks.

## Screen composition

Preserve the System Compendium structure:
1. Dim dusk town backdrop.
2. Large centered modal, dark navy body, warm gold outer frame.
3. Header with compact seal/emblem on the left, abstract title glyph block, subtitle glyph line, close button right.
4. Four tabs under the header. First tab selected in warm gold, others dark slate with gold trim.
5. Filter/search row with input, dropdowns, and compact count glyph area.
6. Main area split into left skill catalog and right selected skill detail.
7. Left catalog: two-column skill cards with varied icons and compact chips.
8. Right detail top: selected skill icon slot, title glyph, subtitle, short abstract description, keyword chips.
9. VFX preview theater: framed rectangular stage with tactical floor tint, caster marker, route/projection, target marker, impact rings, replay button.
10. Metrics rows below with thin dividers and compact readout bars.

## Skill/icon visibility

Visible skill icons must be varied and non-repeating:
- shield interception
- echo wave
- tactical signal
- swift arrow
- ice shard
- flame slash
- healing bloom
- binding sigil

Icons may have their own small framed backplates, but they must still fit the global Art Bible palette.

## Text guard

All readable text must be abstract glyphs only. Do not render Korean, English, numbers, readable abbreviations, raw IDs, or code-like labels. Use glyph blocks, pseudo-rune bars, dots, short chip marks, and symbolic UI marks.

## Reference roles

- town_frontier_village:dusk: world/backdrop dusk lighting reference.
- ui_inventory:dusk: catalog/list + item detail reference.
- ui_theater:dusk: preview theater reference.
- ui_compendium:dusk_v2: current strongest compendium mockup reference.
- ui_compendium_unity_current: layout constraint and implementation reality.

The result should be more unified and art-bible-like than ui_compendium:dusk_v2, while remaining implementable in Unity UI Toolkit.

## Negative

No readable text. No browser dashboard. No landing page. No full character figure. No debug code labels. No repeated placeholder icons. No huge fantasy carvings. No bright parchment full panel. No photoreal UI. No generic sci-fi HUD. No UI overlap. No one-note purple/blue gradient.
```
