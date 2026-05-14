---
slug: hero_pack_raider--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: hero_pack_raider
variant: face_combat_state_sheet
refs:
  - hero_pack_raider
  - hero_pack_raider:portrait_full
  - hero_pack_raider:portrait_face_default
aspect: "1.51:1"
output_size: "2368x1568"
chroma: "#FF00FF"
states:
  - wounded
  - stunned
  - feared
  - charmed
  - pained
  - downed
status: draft
---

# 이빨바람 — Face Combat State Sheet (6 game state frontal)

## 산출물

배틀 시스템에서 face icon swap에 사용. 1번 호출로 6 state 동시 생성, sheet_split.py로 분리:
- `portrait_face_wounded.png` — HP critical
- `portrait_face_stunned.png` — CC stun
- `portrait_face_feared.png` — CC fear
- `portrait_face_charmed.png` — CC charm
- `portrait_face_pained.png` — DoT (burn/poison/bleed)
- `portrait_face_downed.png` — HP=0

narrative emotion 8종은 face_emotion_sheet에서 별도 cover. 합쳐서 이빨바람 face state 매트릭스 14종 (8 narrative + 6 combat).

## 단린과 차이점

state 표현의 baseline이 priest restraint가 아니라 **hunter restraint**. wounded/pained 상태에서도 사냥꾼은 표면 침착함을 끝까지 유지. charmed의 magical hint는 단린(warm violet)과 동일하게 — 마법 효과 자체는 캐릭터 archetype과 무관한 system 효과이므로.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 6 cells in the grid below are the SAME character — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: wild shag style, near-black with cool deep tone (#15120F). NOT brown, NOT auburn.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family). NOT brown, NOT green.
- Skin: light tone, slightly tanned (#E0CFB4 family), soft idealized — masculine.
- Outfit collar visible at neck/upper chest area: warm ochre leather (#A96F36) tunic top with subtle bone/amber accent stitching. NOT ivory, NOT priest cloth.
- Face: sharp masculine silhouette, no facial hair.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 6 cells.

DO NOT:
- Change the character identity between cells.
- Use long hair, copper-auburn hair, brown eyes, or ivory/white collar in any cell.
- Add facial hair, face tattoos, scarification, bone hair accessories — they are NOT visible in the ref.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 3-column × 2-row sprite sheet of 6 separate face close-up portraits.

CELL SIZE: each cell exactly 768 × 768 px (square close-up).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): state = wounded
- (1,0): state = stunned
- (2,0): state = feared
- (0,1): state = charmed
- (1,1): state = pained
- (2,1): state = downed

CELL CONTENT — face close-up (per cell):
- Crop: face only — top of head visible at top edge, neck/collar at bottom edge, ears within frame, eyes positioned on upper-third line.
- Frontal view with subtle 5-10° rotation.
- HAIR fully framed within the cell (wild shag, no overflow).
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline along the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 6 cells perfectly aligned in regular 3×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position across all cells.

=== STATE DESCRIPTORS (per cell — these are GAME STATES, not narrative emotions) ===

(0,0) WOUNDED: HP critical state. A small streak of blood traced from the temple or one cheek; subtle blood mark at the corner of the lips. Wild shag hair partially disheveled — a few strands fall across the brow. Eyes narrowed in collected pain, NOT teary, NOT theatrical. A thin sheen of sweat on the forehead. Lips pressed firmly with a hint of breath escape (slightly parted). Hunter restraint preserved despite physical limit reached — the "사냥꾼은 비명을 지르지 않는다" baseline.

(1,0) STUNNED: CC stun state. Eyes have lost focus — pupils slightly dilated and gaze drifting off-center, NOT looking at camera. Lips parted slightly, jaw slack, expression vacant. Head tilted softly to one side (~10°), as if the body forgot to hold itself up. NOT a serious wound — neural disruption, conscious-but-disconnected. NOT exaggerated cartoon stun.

(2,0) FEARED: CC fear state. TERROR breaking hunter restraint — eyes wide (whites clearly visible above and below iris), pupils smaller (constricted by adrenaline), brow lifted in a sharp peak. A bead of cold sweat on the temple. Lips trembling slightly, parted with caught breath. The hunter who reads danger by smell suddenly catching one he cannot identify. NOT shock surprise — sustained, body-shaking fear.

(0,1) CHARMED: CC charm state. Hypnotized blank smile — lip corners lifted softly, eyes ABSENT (gaze unfocused, looking through the viewer). Pupils slightly enlarged with a faint magical hint (a soft warm violet glow at the iris edge — stay well below #C040A0, NOT magenta, NOT pink). Head relaxed, slight tilt forward. Hunter senses temporarily suspended — the most unnatural state for this character.

(1,1) PAINED: damage-over-time state (burn/poison/bleed). Eyes squeezed shut or sharply narrowed, deep crease between brows showing endurance. Lips drawn tight or biting subtly, jaw clenched. A bead of sweat near the temple. Hunter discipline absorbing pain — stoic, NOT screaming, NOT teary. Head slightly bowed forward.

(2,1) DOWNED: HP=0 state. Eyes closed or half-closed, eyelashes lowered. Lips parted slightly (the moment before breath stops). Head tilted to one side, wild shag hair falling over. A small blood trace on the brow or cheek (more visible than the wounded cell). Hunter's last expression — peaceful unconsciousness rather than agony. The "downed but recoverable" framing — alive but limp, NOT a corpse, NOT eyes-rolled-back horror.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 6 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere — hair, skin, eyes, collar all avoid #FF00FF and adjacent fuchsia. Charmed cell's magical iris hint stays in warm violet (#7050A0 family or below #C040A0).
- Output: 2368 × 1568 PNG, 3-column × 2-row grid sheet.
```
