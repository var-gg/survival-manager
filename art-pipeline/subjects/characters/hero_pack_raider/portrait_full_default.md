---
slug: hero_pack_raider--portrait_full_default
kind: character_portrait_full
subject_id: hero_pack_raider
variant: portrait_full
emotion: default
refs:
  - hero_pack_raider
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 이빨바람 (Pack Raider) — Portrait Full Default (v3 — P09 canon + house style seed)

## v3 변경 사유

V1: spec block에 "Yri-sol tribe young hunter-leader", "Duelist tracker", "wolf-pack raider" 같은 archetype 어휘 + "layered weathered hunter's leather", "arm guards" 같은 묘사가 들어가서 ChatGPT가 일반 fantasy ranger archetype으로 매핑 → ref보다 더 화려한 layered leather + multiple straps + pouch 추가됨.

V2 fix: V6 단린에서 검증된 REF-first 패턴 적용. spec block에서 archetype 묘사 제거, REF가 캐릭터 BLUEPRINT 전체임을 강제. P09 asset material override hex로 색 anchor.

V3 update: 2026-05-13 P09 통합 외형 재산출 기준을 반영하고, `hero_dawn_priest:portrait_full_style_seed_best`를 내부 house style seed로 붙인다. P09 anchor는 costume/identity canon이고, 단린 best seed는 professional key visual brushwork와 face appeal 기준이다.

## prompt 명세

```prompt
=== REFERENCE ROLE SPLIT (highest authority) ===
Two reference images are attached.

REF 1 — P09 model anchor:
- Canon for identity, costume family, slot layout, weapon/shield presence, color zones, and silhouette readability.
- Use it as the costume truth, NOT as a line-for-line tracing target.
- Preserve the big readable facts: near-black wild hair, warm amber/burnt leather, moss forest accent on arm/waist, single short sword, no shield.

REF 2 — 단린 best style seed:
- Style-only seed for survival-manager house style.
- Borrow its professional key visual qualities: face appeal, brush density, painterly cloth rhythm, confident line economy, controlled edge detail, premium JRPG full-body finish.
- Do NOT copy 단린's priest costume, cape shape, shield, sword shape, color balance, or feminine body language.

Final image goal:
Translate the P09 costume canon into an original professional fantasy game key visual. It should feel drawn by the same internal game art team as 단린 best seed, not like a direct 3D-to-2D conversion.

=== HARD CANON LOCKS ===
Keep these exact identity/costume facts:
- Male young pack raider / hunter-leader, alert and coiled at rest.
- Head slot empty: no hood, helmet, crown, antlers, feathers, or large hair ornament.
- Near-black wild shag hair, warm brown/amber eyes, light slightly tanned skin.
- Armor_004 leather chest and leg with Armor_010 forest arm and waist accent.
- Sword_001 only. No shield, no bow, no axe, no staff, no back weapon.
- Warm leather / burnt amber / moss green palette. Do not shift him into cold black rogue, generic green ranger, or metal knight armor.

=== PROFESSIONAL ILLUSTRATOR TRANSLATION (allowed and desired) ===
Use artistic judgment to make the character feel like premium original game art:
- Improve facial appeal, gaze intensity, and hair rhythm beyond the low-poly model.
- Use tasteful asymmetry in wild hair, leather folds, hand pose, and stance.
- Let leather straps and forest accent shapes read clearly while avoiding pouch overload.
- Add fine seam detail, small stitching, painterly edge highlights, and restrained leather sheen only on existing garments/gear.
- Push lighting, value grouping, and silhouette rhythm for a key visual read.
- Maintain full-body clarity for cutout use, but avoid rigid mannequin posture.

Forbidden:
- No external IP costume motifs, no specific franchise/artist imitation.
- No shield, buckler, bow, axe, staff, secondary blade, animal ears, tail, wolf transformation, face tattoo, scarification, bone/feather totems, or giant cloak.
- No exact copy of REF 2 costume or pose; REF 2 is style-only.

=== EXACT COLOR ANCHORS (P09 asset material override hex) ===
Use these hex values when the ref's lighting obscures the canonical color. The ref shape + these hex = the canonical character.

- Hair: #15120F (near-black with cool deep tone). NOT brown, NOT auburn, NOT chestnut. Dark and cool, almost black, slightly windswept wild shag style.
- P09 slots: Head none / Armor_004_Chest / Armor_010_Arm / Armor_010_Waist / Armor_004_Leg.
- Chest leather zone: #A96F36 main, #D08A2E secondary, #C9B58A accent.
- Arm forest/leather zone: #8B5A2E main, #2F4A2F moss secondary, #D08A2E accent.
- Waist forest/leather zone: #5A3A25 main, #D08A2E secondary, #2F4A2F moss accent.
- Leg leather zone: #6B472D main, #3F2A1E secondary, #A96F36 accent.
- Sword: #73543A main, #C9B58A blade/highlight, #2D2016 dark grip. Muted hunting sword, not shiny heroic gold.
- Eyes: warm brown/amber iris. NOT green, NOT blue.
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
