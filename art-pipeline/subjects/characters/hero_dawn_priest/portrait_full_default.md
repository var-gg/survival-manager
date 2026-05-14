---
slug: hero_dawn_priest--portrait_full_default
kind: character_portrait_full
subject_id: hero_dawn_priest
variant: portrait_full
emotion: default
refs:
  - hero_dawn_priest
  - hero_dawn_priest:portrait_full_style_seed_best
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: rendered
---

# 단린 — Dawn Priest (v8 — P09 canon + house style key visual)

## v8 변경 사유

V1~V5: spec block에 "WHO / OUTFIT / paladin-priest / Vanguard / Armor 007 paladin set" 같은 archetype 어휘가 들어가면 ChatGPT가 그 어휘를 일반 archetype으로 매핑(=Catholic priest, fantasy paladin)하고 ref를 보조 reference로 strip해버림. 결과: V1 cape+hood, V5 paladin plate armor — 둘 다 ref와 다름.

V6 핵심 재설계: spec block에서 **archetype 묘사를 모두 제거**하고, **REF가 캐릭터 BLUEPRINT 전체**임을 강제. spec block은 archetype 정의가 아니라 ref가 못 잡는 색 hex + 미세 painterly enhancement만 담당. "이 캐릭터는 X archetype이다" 어휘 자체가 prompt에서 사라짐 → ChatGPT가 외부 archetype 매핑 못하고 ref만 따름.

V7 핵심 갱신: 2026-05-13 P09 통합 외형 재산출 기준을 반영한다. 현재 wiki 기준은 Armor_007 head/chest/waist + Armor_005 arm + Armor_004 leg + Sword_003 + Shield_004다. 일러스트 생성 시 `외모`와 `P09 visual spec (atlas 인용)`이 1차 기준이며, wiki의 능력/여담에 있는 사제 인장·성물·후광류 lore motif는 기본 portrait에 새 visible asset으로 추가하지 않는다.

V8 핵심 전환: P09 anchor를 tracing target으로 쓰지 않고 identity/costume canon으로만 쓴다. `portrait_full_raw.backup-1778676949.png`에서 복사한 `portrait_full_style_seed_best.png`를 style-only seed로 첨부해 얼굴 매력, brush density, cloth rhythm, professional key visual 느낌을 회수한다. 목표는 3D-to-2D 변환이 아니라, P09 canon을 유지한 survival-manager house style key visual이다.

## prompt 명세

```prompt
=== REFERENCE ROLE SPLIT (highest authority) ===
Two reference images are attached.

REF 1 — P09 model anchor:
- Canon for identity, costume family, slot layout, weapon/shield presence, color zones, and silhouette readability.
- Use it as the costume truth, NOT as a line-for-line tracing target.
- Preserve the big readable facts: warm copper hair, ivory/gold priest upper garment, brown lower gear, sword, shield, compact priest-guard silhouette.

REF 2 — prior internal illustration:
- Style-only seed for survival-manager house style.
- Borrow its professional key visual qualities: face appeal, brush density, painterly cloth rhythm, elegant asymmetry, confident line economy, premium JRPG character art finish.
- Do NOT copy its outdated costume details when they conflict with REF 1 or the P09 slot list below.

Final image goal:
Translate the P09 costume canon into an original professional fantasy game key visual. It should feel drawn by a senior character illustrator interpreting the model, not like a direct 3D-to-2D conversion.

=== HARD CANON LOCKS ===
Keep these exact identity/costume facts:
- Female young priest-guard, calm and composed.
- Warm copper-auburn short hair, soft side strands, amber/brown eyes.
- Armor_007 priest head/chest/waist identity with Armor_005 arm guards and Armor_004 leather lower gear.
- Sword_003 in right hand and Shield_004 on left side. She is not a staff caster.
- Ivory/gold/rose/brown palette. Do not shift her to cold violet, black plate, pure white cleric robe, or generic knight armor.

=== PROFESSIONAL ILLUSTRATOR TRANSLATION (allowed and desired) ===
Use artistic judgment to make the character feel like premium original game art:
- Improve facial appeal and expression subtlety beyond the low-poly model.
- Use tasteful asymmetry in hair strands, cloth folds, hand pose, and body line.
- Let the cloak/drape and fabric folds flow naturally as an illustrator would compose them, while keeping the P09 color zones and no oversized new cape.
- Add fine fabric trim, small seam detail, painterly edge highlights, and restrained engraving only as surface detail on existing garments/gear.
- Push lighting, value grouping, and silhouette rhythm for a key visual read.
- Maintain full-body clarity for cutout use, but avoid rigid mannequin posture.

Forbidden:
- No external IP costume motifs, no specific franchise/artist imitation, no cross-shaped Christian chest symbol.
- No staff, book, halo, wings, large rosary, crown, giant new cloak, plate knight redesign, or extra companion.
- No exact copy of REF 2 costume if it conflicts with the P09 2026-05-13 slot list.

=== EXACT COLOR ANCHORS (P09 asset material override hex) ===
Use these hex values when the ref's lighting obscures the canonical color. The ref shape + these hex = the canonical character.

- P09 slots: Armor_007_Head / Armor_007_Chest / Armor_005_Arm / Armor_007_Waist / Armor_004_Leg.
- Weapon and shield: Sword_003 + Shield_004. Do not substitute with a staff, mace, book, cross, banner, or larger fantasy shield.
- Hair: #9B643F (warm copper-auburn). NOT dark chestnut. NOT navy-tinted. NOT black.
- Head + chest priest cloth zone: #D8C8A8 (warm ivory).
- Arm guard zone: #CFC7B8 main, #8E7F72 secondary, #C9A24E accent.
- Waist zone: #8E6A45 main, #5B4430 secondary, #C9A24E accent.
- Leg/leather lower zone: #7A5635 main, #4D3828 secondary, #D8C8A8 accent.
- Sword blade: #C9A24E (warm gold), with restrained metallic sheen only.
- Shield body: #D8C8A8 (warm ivory) with #C9A24E (warm gold) trim. Simple round emblem — NOT a Christian cross design.
- Cape / shoulder drape (visible at shoulders in ref): keep the soft rose/purple cloth accent from the ref, restrained and not oversized.
- Eyes: warm brown/amber iris, matching the ref and P09 eye color family.
- Skin: light ivory tone (#E8DDC8 family), soft idealized.

=== VARIANT: portrait_full / default emotion ===

EXPRESSION: composed, calm — the resting state of someone whose duty is her identity. Eyes amber-bright with sharp focus. Lips closed gently. Faithful but quietly carrying questions. Not smiling, not severe.

POSE (ref-derived, with portrait composition framing):
- Three-quarter front view (~20° body rotation toward camera-left), calm guard stance.
- Sword in right hand, held low and readable with the tip angled downward or diagonally down. It should not become an attack swing.
- Shield on left side near hip/forearm, readable but not covering the torso.
- Cloak/drape hangs from shoulders with illustrator-grade cloth rhythm, restrained scale.
- Slight low camera angle (~5–10°) for subject presence.
- Both hands visible.
- Natural contrapposto is allowed; avoid stiff mannequin symmetry.

COMPOSITION: full body portrait. Head to feet fully visible with small margin top (~8%) and bottom (~6%). Vertical 2:3.

OUTPUT: 1024 × 1536 PNG.

=== HARD CONSTRAINTS ===
- Single illustration, single subject (no companions, no environment).
- Background: solid #FF00FF magenta, flat fill, no gradient, no shadow on background.
- 1–2 px clean dark outline along the entire subject silhouette for clean chroma cleanup.
- No magenta tint on subject anywhere — clothing, skin, hair, weapon, shield, cape, all must avoid #FF00FF and adjacent fuchsia.
```
