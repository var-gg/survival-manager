---
slug: hero_dawn_priest--battle_stance_sheet
kind: battle_stance_sheet
subject_id: hero_dawn_priest
variant: battle_stance_sheet
refs:
  - hero_dawn_priest
  - hero_dawn_priest:portrait_full
  - hero_dawn_priest:portrait_face_default
aspect: "1:1.49"
output_size: "1568x2336"
chroma: "#FF00FF"
stances:
  - idle
  - attack
  - guard
  - cast
status: rendered
---

# 단린 — Battle Stance Sheet (4 stance 풀바디)

## 산출물

`portrait_stance_<id>.png` 4장:
- portrait_stance_idle.png — 평상 자세 (전투 대기)
- portrait_stance_attack.png — 공격 모션 (검 raised)
- portrait_stance_guard.png — 방어 자세 (방패 forward)
- portrait_stance_cast.png — 시전 모션 (priest spell cast)

인게임 stance sprite + banner art용. portrait_full(V6 풀바디 idle)과 별도 — battle 측에서 더 dynamic한 pose.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 4 cells in the grid below are the SAME character with different combat stances only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same weapons.

Match the REF for the character's identity:
- Hair: chin-length, warm copper-auburn (#9B643F).
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light ivory tone, soft idealized.
- Outfit: layered priest clothing — purple/maroon shoulder cape on top, ivory tunic (#D8C8A8), brown leather belt + skirt-wrap (#8E6A45), brown trousers (#6B472D), brown boots. Match REF.
- Right hand: warm-gold blade single-handed sword (Sword_003, blade #C9A24E).
- Left hand: ivory + gold round shield (Shield_004, body #D8C8A8 with gold trim, simple round emblem NOT a Christian cross).

DO NOT:
- Change the character identity, outfit, or weapons between cells.
- Add cape (other than the purple/maroon shoulder cape from ref), hood, large pectoral cross, body-spanning rosary, or paladin plate armor.
- Use shoulder-length hair, dark chestnut hair, navy eyes.
- Substitute the sword/shield for different designs.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 2-column × 2-row sprite sheet of 4 separate FULL BODY portraits of the SAME character.

CELL SIZE: each cell exactly 768 × 1152 px (vertical full body).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): stance = idle
- (1,0): stance = attack
- (0,1): stance = guard
- (1,1): stance = cast

CELL CONTENT — full body portrait (per cell):
- Crop: full body, head to feet visible.
- Three-quarter front view (~20° rotation toward camera-left), matching ref orientation.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 4 cells perfectly aligned in regular 2×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same character size across all cells (each character takes roughly the same vertical span in the cell).

=== STANCE DESCRIPTORS (per cell) ===

(0,0) IDLE: standing relaxed-alert combat ready. Right hand: sword held at right side, tip pointed downward, blade close to leg. Left hand: shield held at left side resting near hip, slight forward angle. Both arms slightly bent. Weight even on both feet. Slight head tilt forward (~5°), eyes alert. The "before combat begins" posture.

(1,0) ATTACK: mid-swing offensive stance. Right hand: sword RAISED to high guard or mid-swing position, blade angled diagonally up-right (in motion, momentum visible). Left foot stepping forward. Left hand: shield raised slightly to chest level for protection. Body weight forward, momentum visible in cape and tunic motion. Eyes locked on imminent target with intent. NOT mid-impact — the moment before the strike connects.

(0,1) GUARD: defensive stance. Left hand: shield FORWARD at chest level, fully facing the camera (shield emblem clearly visible to viewer — round, ivory + gold). Right hand: sword pulled back, blade behind the shield, ready for counter-strike. Body weight slightly back, knees bent for stability. Head slightly lowered, eyes peering over shield rim with focused calm. The "I will absorb this blow for my companions" posture.

(1,1) CAST: priest spell casting stance. Right hand: sword grounded (blade tip in earth or held loosely at side, NOT actively raised). Left hand: open palm raised at chest height, glowing softly with warm gold/ivory holy light (subtle radiance, NOT bright magenta or pink — stay in warm tone family #FFD979 to #C9A24E). Shield held in left arm/hand or at side. Eyes half-closed in concentration, head slightly tilted upward. Cape fluttering subtly from light pressure of the spell. The priest's faith made manifest.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 4 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Spell light in CAST cell: warm gold/ivory only (#FFD979 family), NOT cool pink, NOT magenta.
- Output: 1568 × 2336 PNG, 2-column × 2-row grid sheet.
```
