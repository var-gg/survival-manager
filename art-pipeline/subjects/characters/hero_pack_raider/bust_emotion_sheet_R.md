---
slug: hero_pack_raider--bust_emotion_sheet_R
kind: bust_emotion_sheet
subject_id: hero_pack_raider
variant: bust_emotion_sheet_R
direction: facing_right
refs:
  - hero_pack_raider
  - hero_pack_raider:portrait_full
  - hero_pack_raider:portrait_face_default
aspect: "1.36:1"
output_size: "3168x2336"
chroma: "#FF00FF"
emotions:
  - default
  - smile
  - serious
  - shock
  - anger
  - sad
  - cry
  - quiet
status: draft
---

# 이빨바람 — Bust Emotion Sheet (facing right, 8 emotion 흉상)

## 산출물 (VN 화면 왼쪽 배치용)

`portrait_bust_<emotion>_R.png` 8장. facing camera-right (face turned 15-20° toward right of frame, body rotated so right shoulder closer to camera) — VN dialogue에서 화면 왼쪽 위치 캐릭터로 사용.

bust_emotion_sheet_L은 별도 prompt 호출 없이 **이 R sheet의 split 결과를 코드 좌우반전** 하여 생성한다. 이빨바람의 lore-only 비대칭 디테일(오른 손목 흉터 등)은 P09 미표현이며 일러에 포함하지 않으므로 좌우반전 안전.

## 단린과 차이점

bust crop은 head + shoulders + upper chest까지라 무기/방패가 거의 안 보이는 영역이다. 이빨바람은 방패가 없고 sword만 있어서 bust crop에서는 어차피 안 보임 — 단린의 R/L 패턴과 동일하게 처리. 단, 어깨에 priest cape 대신 hunter leather (ochre) shoulder가 보인다.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same facing direction.

Match the REF for the character's identity:
- Hair: wild shag style, near-black with cool deep tone (#15120F) — hand-drawn strand definition, slightly windswept.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light tone, slightly tanned (#E0CFB4 family), soft idealized — masculine.
- Outfit (visible at chest/shoulders/arms): ochre leather hunter armor (#A96F36 dominant) with bone/amber accent (#C9B58A and #D08A2E) at seams. Chest tunic visible with subtle stitching detail. NOT ivory, NOT priest cloth, NOT cape.
- Face: sharp masculine silhouette, no facial hair.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape, hair shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Use long hair, copper-auburn hair, brown eyes, or ivory/white outfit in any cell.
- Add facial hair, face tattoos, scarification, bone hair accessories, totem ornaments — they are NOT visible in the ref.
- Add cape, hood, shoulder mantle, or layered priest vestments. The character wears utilitarian hunter leather only.
- Change the facing direction between cells.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 4-column × 2-row sprite sheet of 8 separate bust portraits of the SAME character.

CELL SIZE: each cell exactly 768 × 1152 px (vertical bust portrait).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells.
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (col, row — 0-based, top-left to bottom-right):
- (0,0): emotion = default
- (1,0): emotion = smile
- (2,0): emotion = serious
- (3,0): emotion = shock
- (0,1): emotion = anger
- (1,1): emotion = sad
- (2,1): emotion = cry
- (3,1): emotion = quiet

CELL CONTENT — bust portrait (per cell):
- Crop: head + shoulders + upper chest (down to collarbone area, just above the belt). Hair fully framed top.
- **FACING DIRECTION IS DEFINED BY THIS SPEC — REFERENCES ARE FOR IDENTITY ONLY.** The attached reference images (anchor, portrait_full, portrait_face_default) are for hair color, eye color, outfit colors, facial features only. **DO NOT copy the references' facing direction.** Render the facing direction strictly per the angles below.

GAZE ANGLE (precise — applies to ALL 8 cells):
- Eyes look toward the RIGHT side of the frame at approximately 25-30° from frontal axis. The gaze is clearly off-center to the RIGHT — NOT centered, NOT left-leaning.
- Pupils positioned RIGHT of center within each eye.
- The left side of each eye (toward viewer) shows slightly more sclera than the right side (because eyes are turned right).

HEAD ROTATION (precise):
- Head turned approximately 20-25° toward the RIGHT.
- Chin sweeps slightly toward the LEFT side of the frame; back of the head sweeps toward RIGHT.
- RIGHT cheek (camera-side closer) is more visible to viewer than left cheek.
- Wild shag hair falls forward over the RIGHT shoulder/cheek slightly.

BODY / SHOULDER ROTATION (precise):
- Body rotated approximately 20-25° so the RIGHT shoulder is closer to the camera (forward in 3/4 view).
- The left shoulder is angled away from camera (further back).
- Leather shoulder/collar drape asymmetry follows the rotation.

Mental anchor: "VN dialogue scene, character on LEFT side of screen, looking toward conversation partner on RIGHT."
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in regular 4×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position, same body rotation across all cells.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: alert hunter at rest — composed but coiled, sense-driven. Steady gaze toward right of frame with very subtle nose-lift (~3°), as if reading wind. Lips closed, jaw relaxed but ready. Amber eyes sharp focus, NOT smiling, NOT scowling. The "냄새가 다르다" baseline state.

(1,0) SMILE: rare warmth — never broad. Very slight asymmetric lip-corner lift (almost a smirk), eye softening with subtle crinkle. Hunter's restrained smile — dry humor or quiet relief after companion's safety confirmed. NOT a friendly grin.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line. Hunter's full attention mode — when the wind has changed and the threat is identified.

(3,0) SHOCK: eyes widening (whites visible above iris), pupils dilated, lips parted with breath caught, brow lifted. Internal stop, NOT panic. The "회조 결별" first-crack moment — sense reading something the mind hasn't accepted yet. Hunter restraint preserved.

(0,1) ANGER: controlled hunter restraint — eyes narrowed sharply, jaw tightened, lips pressed thin or slightly drawn back to reveal teeth subtly (animal restraint). NOT explosive. Fury held inside hunter discipline.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin. Slight downward angle of head. Quiet grief — hunter's metaphors stopped, plain weight of seeing.

(2,1) CRY: a single tear traced down one cheek, lips pressed hard, jaw subtly trembling but controlled. Eyes still open but gaze distant. Used very sparingly — hunter dignity preserved even in tears.

(3,1) QUIET: no expression — deepest emotional state. Gaze far away, eyes distant, lips relaxed but lifeless. The "...형제가 날 버렸어" beat — metaphors disappear, only direct fact remains. Stillness, not blankness — alive but emptied.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells, all FACING RIGHT.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 2336 PNG, 4-column × 2-row grid sheet.
```
