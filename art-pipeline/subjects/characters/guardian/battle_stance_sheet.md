---
slug: guardian--battle_stance_sheet
kind: battle_stance_sheet
subject_id: guardian
variant: battle_stance_sheet
refs:
- guardian
- guardian:portrait_full
- guardian:portrait_face_default
- hero_dawn_priest:portrait_full_style_seed_best
aspect: '1:1.49'
output_size: 1568x2336
chroma: '#FF00FF'
status: prompted
stances:
- idle
- attack
- guard
- cast
---
# 묘직 (墓直) / Crypt Guardian — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 묘직 (墓直) / Crypt Guardian.

=== REFERENCE PRIORITY ===
1. The P09 anchor for guardian is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The guardian:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 묘직 (墓直) / Crypt Guardian
- Pipeline subject_id: guardian
- Faction / job lane: pale_conclave / crypt_guardian
- Uniform group: pale_conclave_crypt_guardian
- Silhouette family: p09_h9_c5_a6_w9_l5_wp2_sh3
- Intent: Armor_005 chain에 robe head/waist와 heavy arm을 얹은 blue-grey/bone/amber 수호자.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 2 / HairStyleId 6 / HairColorId 5
- SkinId 1 / EyeColorId 2 / FacialHairId 0 / BustSizeId 2
- Outfit A5/W2/S3 / Head 9 / Chest 5 / Arm 6 / Waist 9 / Leg 5
- WeaponId 2 (straight sword) / ShieldId 3

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #D2CEC4
- Head gear: #B8B0A3
- Chest / upper outfit: #8D969A
- Arm / sleeve: #8A9096
- Waist / sash: #B8B0A3
- Leg / lower outfit: #6B7075
- Weapon: #7A7770
- Shield: #8D969A


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 2: straight sword.
- ShieldId 3: render the shield exactly as P09 shows in every stance where the shield arm is visible.
- attack: sword lunge or diagonal strike, clear blade silhouette, no bow or staff.
- guard: shield-forward defensive stance using the P09 shield, sword held ready.
- cast: class technique pose with weapon held low or used as focus; restrained effect, not a new weapon.


=== GRID SHEET LAYOUT ===
- Canvas: 1568 x 2336 PNG.
- 2 columns x 2 rows.
- Each cell: exactly 768 x 1152 px.
- Gap between cells: exactly 32 px, solid #FF00FF.
- Outside grid and each cell background: solid #FF00FF.
- Reading order:
  - row 0: idle, attack
  - row 1: guard, cast

=== CELL CONTENT ===
- Full body in every cell: head to feet visible, weapon and shield state readable.
- Same character, same outfit, same hair, same color zones, same weapon, same shield state across all 4 cells.
- Three-quarter front view with dynamic stance differences.
- Same scale and vertical footprint across all cells.
- Clean 1-2 px dark outline around the entire character silhouette for chroma cleanup.

=== STANCE SET ===
- idle: combat-ready rest posture, readable class identity.
- attack: active offensive motion as specified in weapon rules.
- guard: defensive posture as specified in weapon/shield rules.
- cast: class technique or support stance; restrained effect only, no environment.

=== HARD CONSTRAINTS ===
- Single subject per cell.
- No UI, captions, labels, environment, ground shadows, companions, extra weapons, or extra props.
- Do not change gender presentation, hair, face, outfit slots, weapon family, shield state, or palette between cells.
- Do not add capes, halos, animal features, scars, tattoos, jewelry, class symbols, or ornaments not visible in P09.
- No magenta tint on the character. Any effect must avoid #FF00FF/fuchsia and must not bridge cells.
```
