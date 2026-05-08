---
slug: hero_dawn_priest--bust_emotion_sheet_L
kind: bust_emotion_sheet
subject_id: hero_dawn_priest
variant: bust_emotion_sheet_L
direction: facing_left
refs:
  - hero_dawn_priest
  - hero_dawn_priest:portrait_full
  - hero_dawn_priest:portrait_face_default
  - hero_dawn_priest:portrait_bust_default_R
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
status: rendered
---

# 단린 — Bust Emotion Sheet (facing left, 8 emotion 흉상)

## 산출물 (VN 화면 오른쪽 배치용)

`portrait_bust_<emotion>_L.png` 8장. facing camera-left — VN dialogue에서 화면 오른쪽 위치 캐릭터로 사용.

bust_emotion_sheet_R와 짝을 이뤄 양방향 dialogue 매트릭스 완성.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit, same hair, same eye color, same skin, same proportions, same identity, same facing direction.

Match the REF for the character's identity:
- Hair: chin-length, warm copper-auburn (#9B643F).
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family).
- Skin: light ivory tone, soft idealized.
- Outfit (visible at chest/shoulders/arms): purple/maroon shoulder cape on top, ivory tunic underneath (#D8C8A8), brown leather belt at waist (just visible at frame bottom). Match REF.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape, hair shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Use shoulder-length hair, dark chestnut hair, navy eyes.
- Add cape (other than the purple/maroon shoulder cape from ref), hood, large pectoral cross, body-spanning rosary, or paladin plate armor.
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
- Crop: head + shoulders + upper chest (down to collarbone area). Hair fully framed top.
- **FACING DIRECTION IS DEFINED BY THIS SPEC — REFERENCES ARE FOR IDENTITY ONLY.** The attached reference images (anchor, portrait_full, portrait_face_default) are for hair color, eye color, outfit colors, facial features only. **DO NOT copy the references' facing direction.** Render the facing direction strictly per the angles below.

GAZE ANGLE (precise — applies to ALL 8 cells):
- Eyes look toward the LEFT side of the frame at approximately 25-30° from frontal axis. The gaze is clearly off-center to the LEFT — NOT centered, NOT right-leaning.
- Pupils positioned LEFT of center within each eye.
- The right side of each eye (toward viewer) shows slightly more sclera than the left side (because eyes are turned left).

HEAD ROTATION (precise):
- Head turned approximately 20-25° toward the LEFT.
- Chin sweeps slightly toward the RIGHT side of the frame; back of the head sweeps toward LEFT.
- LEFT cheek (camera-side closer) is more visible to viewer than right cheek.
- Hair falls forward over the LEFT shoulder/cheek.

BODY / SHOULDER ROTATION (precise):
- Body rotated approximately 20-25° so the LEFT shoulder is closer to the camera (forward in 3/4 view).
- The right shoulder is angled away from camera (further back).
- Cape/collar drape asymmetry follows the rotation.

If the reference images show the character right-leaning (right shoulder forward, eyes turned right) — render this sheet's pose as the OPPOSITE: left-leaning, left shoulder forward, eyes turned left.

Mental anchor: "VN dialogue scene, character on RIGHT side of screen, looking toward conversation partner on LEFT."
- Background of each cell: solid #FF00FF magenta.
- 1-2 px clean dark outline on the character silhouette in each cell.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in regular 4×2 grid.
- NO overlap, NO crooked angles.
- Same scale, same head height position, same body rotation across all cells.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: composed, calm — resting state of priest carrying duty as identity. Eyes forward (toward left of frame), lips closed gently. Warm-but-solemn, faithful but quietly carrying questions. NOT smiling, NOT severe.

(1,0) SMILE: quiet warmth, never broad. Small lift at lip corners only, eye softening. Restrained — the trust-formation moment.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line.

(3,0) SHOCK: eyes widening (whites visible above iris), pupils dilated, lips parted with breath caught, brow lifted. Internal stop, NOT panic.

(0,1) ANGER: controlled restraint — eyes narrowed sharply, jaw tightened, lips pressed thin. NOT explosive.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin with slight tremor. Slight downward angle of head.

(2,1) CRY: a single tear traced down one cheek, lips pressed hard, jaw subtly trembling but controlled. Eyes still open but gaze distant.

(3,1) QUIET: no expression — deepest emotional state. Gaze far away, eyes distant, lips relaxed but lifeless.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells, all FACING LEFT.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 2336 PNG, 4-column × 2-row grid sheet.
```
