---
slug: rift_stalker--battle_stance_sheet
kind: battle_stance_sheet
subject_id: rift_stalker
variant: battle_stance_sheet
refs:
- rift_stalker
- rift_stalker:portrait_full
- rift_stalker:portrait_face_default
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
# 틈사냥꾼 / Rift Stalker — Battle Stance Sheet

## prompt 명세

```prompt
Create a 2-column x 2-row full-body battle stance sprite sheet for 틈사냥꾼 / Rift Stalker.

=== REFERENCE PRIORITY ===
1. The P09 anchor for rift_stalker is canon for outfit slots, silhouette, hair, weapon, shield state, and color zoning.
2. The rift_stalker:portrait_full prior output is canon for the current 2D face polish, house style, and translated costume rendering.
3. The portrait_face_default prior output locks face proportions and should be matched closely.
4. If a Dawn Priest style seed is attached, it is style-only. Do not copy Dawn Priest costume, colors, shield, pose, face, or priest motifs.


=== CHARACTER IDENTITY CONTRACT ===
- Character: 틈사냥꾼 / Rift Stalker
- Pipeline subject_id: rift_stalker
- Faction / job lane: irisol / rift_stalker
- Uniform group: irisol_rift_stalker
- Silhouette family: p09_h0_c11_a4_w10_l11_wp1_sh0
- Intent: Head를 비운 Armor_011 rogue에 leather/forest clan mark와 cyan rift accent를 더한 stalker.

=== P09 CURRENT IDS ===
- SexId 2 / FaceTypeId 3 / HairStyleId 1 / HairColorId 1
- SkinId 1 / EyeColorId 4 / FacialHairId 0 / BustSizeId 1
- Outfit A11/W1/S0 / Head 0 / Chest 11 / Arm 4 / Waist 10 / Leg 11
- WeaponId 1 (short sword / hunting blade) / ShieldId 0

=== EXACT COLOR ANCHORS FROM P09 MATERIAL OVERRIDES ===
Use these values when capture lighting or prior illustrations obscure the canonical color.
- Hair: #2A1F1A
- Head gear: #D1C7B2
- Chest / upper outfit: #5A4A62
- Arm / sleeve: #7A4A32
- Waist / sash: #5A3A25
- Leg / lower outfit: #3F304D
- Weapon: #5B5A58
- Shield: none in P09 preset


=== WEAPON / SHIELD STANCE RULES ===
- WeaponId 1: short sword / hunting blade.
- ShieldId 0: no shield, no buckler, no mirror shield, no defensive prop in any cell.
- attack: sword lunge or diagonal strike, clear blade silhouette, no bow or staff.
- guard: sword parry or evasive low guard; off-hand empty, no shield or buckler.
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
