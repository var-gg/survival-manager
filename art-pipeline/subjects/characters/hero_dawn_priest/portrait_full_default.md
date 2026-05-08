---
slug: hero_dawn_priest--portrait_full_default
kind: character_portrait_full
subject_id: hero_dawn_priest
variant: portrait_full
emotion: default
refs:
  - hero_dawn_priest
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 단린 — Dawn Priest (v6 — REF-first prompt restructure)

## v6 변경 사유

V1~V5: spec block에 "WHO / OUTFIT / paladin-priest / Vanguard / Armor 007 paladin set" 같은 archetype 어휘가 들어가면 ChatGPT가 그 어휘를 일반 archetype으로 매핑(=Catholic priest, fantasy paladin)하고 ref를 보조 reference로 strip해버림. 결과: V1 cape+hood, V5 paladin plate armor — 둘 다 ref와 다름.

V6 핵심 재설계: spec block에서 **archetype 묘사를 모두 제거**하고, **REF가 캐릭터 BLUEPRINT 전체**임을 강제. spec block은 archetype 정의가 아니라 ref가 못 잡는 색 hex + 미세 painterly enhancement만 담당. "이 캐릭터는 X archetype이다" 어휘 자체가 prompt에서 사라짐 → ChatGPT가 외부 archetype 매핑 못하고 ref만 따름.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. Your illustration is the high-resolution, painterly version of this EXACT character — not a reinterpretation, not a fantasy archetype variant.

Match the REF for ALL of the following (the ref is the canonical source):
- Outfit type, layer count, layer order top-to-bottom.
- Garment shapes (tunic, wrap, trousers, cape, etc. — whatever the ref shows).
- Color zones (top tier vs bottom tier, exactly as ref shows).
- Hair length, hair shape, hair color.
- Eye color.
- Weapon type, weapon size, weapon shape.
- Shield type, shield size, shield position.
- Cape / drape / accessory presence and placement.
- Skin tone.
- Body proportions and silhouette.
- Pose composition (with VARIANT adjustments noted below).

The ref is WHAT THE CHARACTER LOOKS LIKE. Your job is to render that exact character with painterly detail and stylized JRPG illustration quality.

DO NOT:
- Add garments, ornaments, weapons, or accessories that aren't in the ref.
- Reinterpret the character into a different archetype (e.g., paladin plate armor when ref shows priest tunic, or knight when ref shows hunter).
- Change the outfit's layer structure or color zones.
- Lengthen, shorten, or restyle hair beyond what the ref shows.
- Substitute the shield/sword design for a more "fitting" or more "fantasy-iconic" one.
- Add chest crosses, large rosaries, pectorals, or religious ornaments not visible in the ref.
- Add hood, large cape (beyond what ref shows), or layered vestments.

The illustration is a HIGH-RES PAINTERLY ENHANCEMENT of the ref. Nothing more, nothing less.

=== REF DETAIL ENHANCEMENT (allowed — surface enhancements only) ===
Render the following with painterly fidelity. These are surface-level enhancements on what the ref already contains, NOT new content:
- Cloth weave and fabric drape detail on garments already visible in ref.
- Hair strand definition matching ref's hair shape and color exactly.
- Material reflections: warm metal sheen on sword blade and shield surface that the ref shows.
- Subtle skin warmth and idealized soft tone on visible skin areas.
- Eye catch light matching ref's eye color.
- Cinematic three-point lighting (key warm upper-left, cool rim upper-back-right, soft fill below) on the silhouette ref shows.
- Subtle decorative engraving on shield emblem ONLY where the ref's surface already shows the form — do NOT introduce new ornament shapes.

=== EXACT COLOR ANCHORS (P09 asset material override hex) ===
Use these hex values when the ref's lighting obscures the canonical color. The ref shape + these hex = the canonical character.

- Hair: #9B643F (warm copper-auburn). NOT dark chestnut. NOT navy-tinted. NOT black.
- Top color zone (head ornament + chest tunic + arm guards + shield surface): #D8C8A8 (warm ivory).
- Bottom color zone (waist wrap + trousers): #8E6A45 (soft brown).
- Sword blade: #C9A24E (warm gold) with subtle warm emission glow.
- Shield body: #D8C8A8 (warm ivory) with #C9A24E (warm gold) trim. Simple round emblem — NOT a Christian cross design.
- Cape / shoulder drape (visible at shoulders in ref): render the purple/maroon textile color exactly as the ref shows it.
- Eyes: yellow/amber iris (#D8B040 to #E0A030 family).
- Skin: light ivory tone (#E8DDC8 family), soft idealized.

=== VARIANT: portrait_full / default emotion ===

EXPRESSION: composed, calm — the resting state of someone whose duty is her identity. Eyes amber-bright with sharp focus. Lips closed gently. Faithful but quietly carrying questions. Not smiling, not severe.

POSE (ref-derived, with portrait composition framing):
- Three-quarter front view (~20° body rotation toward camera-left), matching ref orientation.
- Sword in right hand, tip pointed downward — matching ref pose.
- Shield at left side near hip — matching ref placement.
- Cape hangs from shoulders, matching ref drape.
- Slight low camera angle (~5–10°) for subject presence.
- Both hands visible.
- Weight even on both feet.

COMPOSITION: full body portrait. Head to feet fully visible with small margin top (~8%) and bottom (~6%). Vertical 2:3.

OUTPUT: 1024 × 1536 PNG.

=== HARD CONSTRAINTS ===
- Single illustration, single subject (no companions, no environment).
- Background: solid #FF00FF magenta, flat fill, no gradient, no shadow on background.
- 1–2 px clean dark outline along the entire subject silhouette for clean chroma cleanup.
- No magenta tint on subject anywhere — clothing, skin, hair, weapon, shield, cape, all must avoid #FF00FF and adjacent fuchsia.
```
