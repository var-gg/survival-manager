---
slug: hero_echo_savant--face_combat_state_sheet
kind: face_combat_state_sheet
subject_id: hero_echo_savant
variant: face_combat_state_sheet
refs:
  - hero_echo_savant
  - hero_echo_savant:portrait_full
  - hero_echo_savant:portrait_face_default
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

# 공한 — Face Combat State Sheet (6 game state frontal)

## 산출물

배틀 시스템 face icon swap용. 1번 호출로 6 state 동시 생성.

## 단린/이빨바람/묵향과 차이점

observer restraint baseline — 1800년 보관자 답게 통증/공포에서도 표면 침묵 유지. wounded는 "괜찮다" 어법 (dry, restrained). stunned는 1800년 시간 척도 깨짐 — 그가 외부 세계에 갑자기 너무 present해진 상태. feared는 narrative anchor — 1800년 동안 보지 못한 종류의 두려움 표면. charmed는 default의 distant attentiveness가 hypnotic blank stare로 대체.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 6 cells in the grid below are the SAME character — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: SHORT spiky smoky violet/lavender (#92879F). NOT long, NOT brown, NOT black.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light pale tone with very slight cool undertone (#E8DDC8 family), masculine.
- Outfit collar visible: smoky lavender ritual armor (#8C7FA0) with cream/sage scarf (#D8D4C8) draped from one shoulder.
- Face: slender masculine silhouette, no facial hair.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 6 cells.

DO NOT:
- Change the character identity between cells.
- Use long hair, brown/black hair, or replace short spiky cut with long/curly hair.
- Add facial hair, forehead tattoos, or hair-tip crystal attachments.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 3-column × 2-row sprite sheet of 6 separate face close-up portraits.

CELL SIZE: each cell exactly 768 × 768 px.
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
- Hair fully framed within the cell (short spiky).
- Cream/sage scarf visible at bottom edge.
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline along the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 6 cells perfectly aligned in regular 3×2 grid.
- Same scale, same head height position across all cells.

=== STATE DESCRIPTORS (per cell — these are GAME STATES, not narrative emotions) ===

(0,0) WOUNDED: HP critical state. A small streak of blood traced from temple or one cheek; subtle blood mark at lip corner. Hair partially disheveled — a few violet strands fall across the brow. Eyes narrowed in collected weariness, NOT teary. Thin sheen of sweat on forehead. Lips slightly parted with breath escape. The "...괜찮다" baseline — observer restraint preserved despite physical limit.

(1,0) STUNNED: CC stun state. Eyes have lost focus — pupils slightly dilated, gaze drifting off-center, NOT looking at camera. Lips parted slightly, jaw slack, expression vacant. Head tilted softly to one side (~10°). The 1800년 시간 척도가 일시적으로 깨진 상태 — without the long view he looks unusually present, jarringly here-and-now.

(2,0) FEARED: CC fear state. The narratively most powerful state — TERROR breaking the 1800년 baseline. Eyes wide (whites clearly visible above and below iris), pupils smaller, brow lifted in sharp peak. Bead of cold sweat on temple. Lips trembling slightly, parted with caught breath. The 1800년 동안 보지 못한 종류의 위협 — and that gap in his recognition is the threat's weight.

(0,1) CHARMED: CC charm state. Hypnotized blank stare replacing the distant attentiveness — eyes ABSENT (gaze unfocused, looking through the viewer). Pupils slightly enlarged with faint magical hint (a soft warm violet glow at the iris edge — stay well below #C040A0, NOT magenta). Head relaxed, slight tilt forward. Lip corners faintly lifted in vacant smile. Observer identity temporarily suspended.

(1,1) PAINED: damage-over-time state (burn/poison/bleed). Eyes squeezed shut or sharply narrowed, deep crease between brows. Lips drawn tight, jaw clenched. Bead of sweat near temple. Observer discipline absorbing pain — stoic, NOT screaming. Head slightly bowed forward.

(2,1) DOWNED: HP=0 state. Eyes closed or half-closed, eyelashes lowered. Lips parted slightly. Head tilted to one side, violet hair falling forward. A small blood trace on brow or cheek (more visible than wounded cell). Observer's last expression — peaceful unconsciousness. The "downed but recoverable" framing — alive but limp.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 6 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere — Charmed cell's magical iris hint stays in warm violet (#7050A0 family or below #C040A0).
- Output: 2368 × 1568 PNG, 3-column × 2-row grid sheet.
```
