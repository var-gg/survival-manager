---
slug: hero_grave_hexer--portrait_full_default
kind: character_portrait_full
subject_id: hero_grave_hexer
variant: portrait_full
emotion: default
refs:
  - hero_grave_hexer
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 묵향 (Grave Hexer) — Portrait Full Default (v3 — P09 canon + house style seed)

## v3 변경 사유

2026-05-13 P09 통합 외형 재산출 기준을 반영한다. `hero_grave_hexer` P09 anchor는 costume/identity/weapon canon이고, `hero_dawn_priest:portrait_full_style_seed_best`는 survival-manager 내부 house style seed다. 목표는 P09를 그대로 tracing하는 3D-to-2D 변환이 아니라, 묵향의 bone robe + jade signal + dry-humor memory keeper 인상을 단린 best seed와 같은 full-body key visual 작화로 번역하는 것이다.

## prompt 명세

```prompt
=== REFERENCE ROLE SPLIT (highest authority) ===
Two reference images are attached.

REF 1 — P09 model anchor:
- Canon for identity, costume family, slot layout, weapon/shield presence, color zones, and silhouette readability.
- Use it as the costume truth, NOT as a line-for-line tracing target.
- Preserve the big readable facts: ash-bone hair, bone ritual robe, muted violet waist sash, small jade signal, Staff_002, no shield.

REF 2 — 단린 best style seed:
- Style-only seed for survival-manager house style.
- Borrow its professional key visual qualities: face appeal, brush density, painterly cloth rhythm, confident line economy, controlled edge detail, premium JRPG full-body finish.
- Do NOT copy 단린's priest costume, sword, shield, copper hair, cape color balance, or body language.

Final image goal:
Translate the P09 costume canon into an original professional fantasy game key visual. It should feel drawn by the same internal game art team as 단린 best seed, not like a direct 3D-to-2D conversion.

=== HARD CANON LOCKS ===
Keep these exact identity/costume facts:
- Female grave hexer / memory keeper, composed and lightly amused at rest.
- Ash-bone hair, pale warm skin, amber eyes.
- Armor_009 ritual robe identity with Armor_008 muted violet waist sash.
- Staff_002 only. No sword, no dagger, no shield, no book, no scythe.
- Bone grey / jade / muted amber / ritual violet palette. Do not shift her into black necromancer, saturated witch, blue mage, or pure white cleric.

=== PROFESSIONAL ILLUSTRATOR TRANSLATION (allowed and desired) ===
Use artistic judgment to make the character feel like premium original game art:
- Improve facial appeal, dry half-smile subtlety, and ash hair rhythm beyond the low-poly model.
- Use tasteful asymmetry in hair strands, robe folds, hand pose, and stance.
- Let the robe layers and sash flow naturally as an illustrator would compose them, while preserving P09 color zones.
- Add fine fabric trim, small seam detail, painterly edge highlights, restrained jade accent, and soft cloth texture only on existing garments/gear.
- Push lighting, value grouping, and silhouette rhythm for a key visual read.
- Maintain full-body clarity for cutout use, but avoid rigid mannequin posture.

Forbidden:
- No external IP costume motifs, no specific franchise/artist imitation.
- No black grim reaper robe, skull ornaments, bone crown, tall pointed wizard hat, hood, scythe, sword, shield, floating tombstones, giant spell circle, hanging bells, or separate talisman props.
- No exact copy of REF 2 costume or pose; REF 2 is style-only.

=== EXACT COLOR ANCHORS (P09 asset material override hex) ===
Use these hex values when the ref's lighting obscures the canonical color. The ref shape + these hex = the canonical character.

- P09 slots: Armor_009_Head / Armor_009_Chest / Armor_009_Arm / Armor_008_Waist / Armor_009_Leg.
- Hair: #C8C5BB main, #A89F8B shadow, #E2DED6 highlight. Ash-bone, not black, brown, blue, or pure platinum.
- Head/robe bone zone: #B8B0A3 main, with #6DBE7C small jade signal and #C4A458 muted amber accent.
- Chest robe zone: #B8B0A3 main, #8E8980 secondary, #6DBE7C jade accent.
- Arm robe zone: #B8B0A3 main, #7FA88C muted green-grey, #C4A458 amber accent.
- Waist sash: #6F647A main, #4C405E secondary, #6DBE7C accent. This muted violet sash must remain visible.
- Leg/lower robe: #5F5A54 main, #3F3B36 secondary, #B8B0A3 accent.
- Staff_002: #4F5856 main, #6DBE7C jade accent, #B8B0A3 trim. Faint jade emission only at existing staff accent, not a large glow halo.
- Eyes: warm amber/brown iris.
- Skin: light pale tone with slight warmth (#E8DDC8 family), soft idealized; not corpse white.

=== NO SHIELD / NO SWORD (CRITICAL) ===
This character carries Staff_002 and NO shield.
- Do NOT add a shield, buckler, sword, dagger, book, or scythe to the hands, back, hip, or ground.
- The staff should be readable from top to bottom if possible.

=== VARIANT: portrait_full / default emotion ===

EXPRESSION: quiet dry humor over old memory weight. A faint one-corner half-smile is allowed; eyes amber-bright but distant, as if listening to yesterday and a thousand years ago at the same time. Not grim, not broad smile, not vacant.

POSE:
- Three-quarter front view (~20° body rotation toward camera-left).
- Staff in one hand, vertical or slightly diagonal beside the body; it should read as a staff, not a blade.
- Other hand relaxed and empty.
- Relaxed standing posture with slight contrapposto; calm, experienced, not battle shout.
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
