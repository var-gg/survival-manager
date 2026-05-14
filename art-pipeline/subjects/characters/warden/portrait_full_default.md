---
slug: warden--portrait_full_default
kind: character_portrait_full
subject_id: warden
variant: portrait_full
emotion: default
refs:
- warden
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '2:3'
output_size: 1024x1536
chroma: '#FF00FF'
status: rendered
---
# 철위 (鐵衛) / Iron Warden — Portrait Full Default

## 생성 의도

첫 번째 reference는 이 캐릭터의 P09 canon이다. 두 번째 reference `portrait_full_style_seed_best`는 단린 best full-body illustration seed이며 작가감, 선 밀도, 얼굴 polish, painterly rendering, clean silhouette만 맞추는 style-only seed다. 단린의 의상, 무기, 색상, 포즈, 사제 motif는 가져오지 않는다.

## prompt 명세

```prompt
Create one full-body polished JRPG character illustration of 철위 (鐵衛) / Iron Warden, using the attached references with strict priority:

REFERENCE PRIORITY
1. The attached P09 model reference for warden is CANON for identity, outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The attached Dawn Priest full-body illustration is STYLE ONLY. Match its professional 2D illustration quality, elegant face polish, confident hand-painted linework, layered cloth/armor rendering, clean silhouette outline, and refined character-art finish. Do NOT copy Dawn Priest costume, sword/shield setup, pose, colors, face, or priest motifs.

The illustration is a high-resolution painterly production version of the exact P09 character. Preserve canon, but do not make a rigid 3D-to-2D conversion.

DO NOT add garments, props, ornaments, scars, tattoos, pouches, capes, hoods, religious symbols, tribal decorations, or extra weapons that are not visible in the REF. Do not replace the P09 outfit with a genre-standard class costume.

=== CHARACTER IDENTITY CONTRACT ===
- Character: 철위 (鐵衛) / Iron Warden
- Pipeline subject_id: warden
- Faction / job lane: solarum / iron_warden
- Uniform group: solarum_iron_warden
- Silhouette family: p09_h6_c6_a6_w5_l6_wp3_sh5
- Intent: Armor_006 full plate에 field waist와 royal blue shield를 둔 veteran warden.
- Natural-language identity lock: adult male veteran shield soldier, not a young knight, not feminine. Keep visible short facial hair / trimmed beard-moustache signal from the P09 reference. Face should read as seasoned and guarded while still polished.

=== P09 CURRENT IDS ===
- SexId 1 / FaceTypeId 2 / HairStyleId 5 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 4 / BustSizeId 2
- Outfit A6/W3/S5 / Head 6 / Chest 6 / Arm 6 / Waist 5 / Leg 6

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when the capture lighting obscures the canonical color. The REF shape plus these color anchors is the character identity.
- Hair: #C0C0B8
- Chest / top color zone: #9AA0A6
- Waist / lower color zone: #4A5160
- Leg / lower color zone: #7A828C
- Weapon: #B0B4B8
- Shield: #4A6FA8

=== VARIANT: portrait_full / default emotion ===
Expression: neutral default state that fits the character identity above. Keep the face readable and distinct from same-faction or same-job characters. Make the face appealing and professionally illustrated, not a literal 3D model face.
For this character specifically: restrained veteran calm, older than the other Solarum frontliners, with visible facial hair. Do not smooth him into a youthful androgynous face.

Pose and composition:
- Full body, single subject, vertical 2:3.
- Three-quarter front view, slight low camera angle, both feet visible.
- Keep the weapon and shield presence exactly as the REF shows.
- - Shield presence: render the shield exactly as shown in the P09 reference.
- Head-to-feet visible with small margin at top and bottom.

OUTPUT: 1024 x 1536 PNG.

=== HARD CONSTRAINTS ===
- Background: solid #FF00FF magenta, flat fill, no gradient, no cast shadow.
- 1-2 px clean dark outline around the entire subject silhouette for chroma cleanup.
- No magenta tint on the subject.
- No companions, no environment, no UI frame.

=== STYLE TARGET ===
- Same internal house style as the Dawn Priest full-body seed: polished hand-painted JRPG character art, elegant face rendering, crisp costume zone separation, painterly folds and metal highlights, controlled cinematic rim light.
- Color zones must remain readable at thumbnail size; avoid monochrome blobs.
- Keep only this character's P09 weapon and shield state.
```
