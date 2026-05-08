---
slug: hero_pack_raider--portrait_full_default
kind: character_portrait_full
subject_id: hero_pack_raider
variant: portrait_full
emotion: default
refs:
  - hero_pack_raider
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 이빨바람 (Pack Raider) — Portrait Full Default (v2 — REF-first)

## v2 변경 사유

V1: spec block에 "Yri-sol tribe young hunter-leader", "Duelist tracker", "wolf-pack raider" 같은 archetype 어휘 + "layered weathered hunter's leather", "arm guards" 같은 묘사가 들어가서 ChatGPT가 일반 fantasy ranger archetype으로 매핑 → ref보다 더 화려한 layered leather + multiple straps + pouch 추가됨.

V2 fix: V6 단린에서 검증된 REF-first 패턴 적용. spec block에서 archetype 묘사 제거, REF가 캐릭터 BLUEPRINT 전체임을 강제. P09 asset material override hex로 색 anchor.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. Your illustration is the high-resolution, painterly version of this EXACT character — not a reinterpretation, not a fantasy archetype variant.

Match the REF for ALL of the following (the ref is the canonical source):
- Outfit type, layer count, layer order top-to-bottom.
- Garment shapes (tunic, wrap, trousers — whatever the ref shows).
- Color zones (top tier vs bottom tier, exactly as ref shows).
- Hair length, hair shape, hair color.
- Eye color.
- Weapon type, weapon size, weapon shape, weapon position.
- Skin tone.
- Body proportions and silhouette.
- Pose composition (with VARIANT adjustments noted below).
- Presence/absence of accessories — render only what's in the ref.

The ref is WHAT THE CHARACTER LOOKS LIKE. Your job is to render that exact character with painterly detail and stylized JRPG illustration quality.

DO NOT:
- Add garments, ornaments, weapons, or accessories that aren't in the ref.
- Reinterpret the character into a different archetype (e.g., elaborate ranger armor when ref shows simple hunter clothes; tribal warrior when ref shows utilitarian hunter).
- Change the outfit's layer structure or color zones.
- Lengthen, shorten, or restyle hair beyond what the ref shows.
- Substitute the sword for a more "fitting" or "fantasy-iconic" weapon.
- Add multiple belt pouches, satchels, knife sheaths, or hunter accessories not in the ref.
- Add tribal face tattoos, scarification markings, bone hair accessories, feather decorations, or totem ornaments not visible in the ref.
- Add a shield, buckler, or defensive equipment of any kind (this character has NO shield).

The illustration is a HIGH-RES PAINTERLY ENHANCEMENT of the ref. Nothing more, nothing less.

=== REF DETAIL ENHANCEMENT (allowed — surface enhancements only) ===
Render the following with painterly fidelity. These are surface-level enhancements on what the ref already contains, NOT new content:
- Leather sheen and cloth weave on garments visible in ref.
- Hair strand definition matching ref's hair shape and color exactly (wild shag, near-black).
- Material reflection: subtle bone-cream highlight on sword blade visible in ref.
- Subtle skin warmth on visible skin areas.
- Eye catch light matching ref's amber color.
- Cinematic three-point lighting (key warm upper-left, cool rim upper-back-right, soft fill below) on the silhouette ref shows.
- Subtle stitching detail at garment seams (NOT heavy realistic leather grain).

=== EXACT COLOR ANCHORS (P09 asset material override hex) ===
Use these hex values when the ref's lighting obscures the canonical color. The ref shape + these hex = the canonical character.

- Hair: #15120F (near-black with cool deep tone). NOT brown, NOT auburn, NOT chestnut. Dark and cool, almost black, slightly windswept wild shag style.
- Top color zone (head ornament + chest tunic + arm guards): #A96F36 (warm ochre / tan leather). Dominant on the upper body.
- Bottom color zone (waist wrap + trousers): #6B472D (dark warm umber leather). Darker than the top zone, clearly distinguishable from #A96F36.
- Sword blade: #C9B58A (bone/cream tone). NOT shiny gold, NOT mirror silver. A muted bone-cream blade with subtle warm undertone.
- Eyes: yellow/amber iris (#D8B040 to #E0A030 family). NOT brown, NOT green, NOT blue.
- Skin: light tone, slightly tanned (P09 SkinId 1), soft idealized.
- Accent details (small highlights on garment seams, sword fittings, belt buckle): #D08A2E (warm amber) and #C9B58A (bone/cream).

=== NO SHIELD (CRITICAL) ===
This character carries NO shield. The shield slot is EMPTY in the P09 model.
- DO NOT add a shield, buckler, or any defensive equipment to the left hand, back, or hip.
- DO NOT add a strapped weapon (axe, secondary blade) to the back.
- The character carries ONLY the single sword in the right hand. Left hand is empty/relaxed.

=== VARIANT: portrait_full / default emotion ===

EXPRESSION: alert hunter at rest — composed but coiled, sense-driven. Slight head tilt or steady forward gaze, as if reading subtle wind. Lips closed, jaw relaxed but ready. Amber eyes sharp focus, NOT smiling, NOT scowling — the resting state of someone whose senses are always half on.

POSE (ref-derived, with portrait composition framing):
- Three-quarter front view (~20° body rotation toward camera-left), matching ref orientation.
- Right hand: at right side, near or holding the sword hilt at the hip — matching ref's sword position.
- Left arm: relaxed at side or slightly forward, empty (no shield) — matching ref.
- Body weight slightly forward, coiled tracker stance.
- Head: aligned with body, slight tilt (~5° max).
- Slight low camera angle (~5–10°) for subject presence.
- Both feet visible.

COMPOSITION: full body portrait. Head to feet fully visible with small margin top (~8%) and bottom (~6%). Vertical 2:3.

OUTPUT: 1024 × 1536 PNG.

=== HARD CONSTRAINTS ===
- Single illustration, single subject (no companions, no environment).
- Background: solid #FF00FF magenta, flat fill, no gradient, no shadow on background.
- 1–2 px clean dark outline along the entire subject silhouette for clean chroma cleanup.
- No magenta tint on subject anywhere — clothing, skin, hair, weapon, all must avoid #FF00FF and adjacent fuchsia.
```
