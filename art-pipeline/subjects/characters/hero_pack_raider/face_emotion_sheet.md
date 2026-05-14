---
slug: hero_pack_raider--face_emotion_sheet
kind: face_emotion_sheet
subject_id: hero_pack_raider
variant: face_emotion_sheet
refs:
  - hero_pack_raider
  - hero_pack_raider:portrait_full
aspect: "2:1"
output_size: "3168x1568"
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

# 이빨바람 — Face Emotion Sheet (8 emotion frontal)

## 산출물

이 페이지 1번 호출로 8 emotion 얼빡 portrait를 한 sheet에 동시 생성.
이후 `sheet_split.py`로 8개 individual PNG로 분리:
- `portrait_face_default.png`
- `portrait_face_smile.png`
- `portrait_face_serious.png`
- `portrait_face_shock.png`
- `portrait_face_anger.png`
- `portrait_face_sad.png`
- `portrait_face_cry.png`
- `portrait_face_quiet.png`

## 단린과 차이점

emotion 표현의 baseline이 priest restraint가 아니라 **hunter restraint**다. 단린이 "사제 격식이 무너지지 않는 자리"라면 이빨바람은 "사냥꾼 감각이 무너지지 않는 자리". signature gesture는 코를 살짝 들어올리는 sense-driven 동작이며, 가드레일 무너짐 비트(quiet, sad)에서는 은유가 사라지고 직설만 남는 — 손이 멈춘 상태.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: wild shag style, near-black with cool deep tone (#15120F). NOT brown, NOT auburn, NOT chestnut. Slightly windswept, hand-drawn strand definition.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family). NOT brown, NOT green, NOT blue.
- Skin: light tone, slightly tanned (#E0CFB4 family), soft idealized — masculine.
- Outfit collar (visible at neck/upper chest area): warm ochre leather (#A96F36) tunic top with subtle bone/amber accent stitching. NOT ivory, NOT white, NOT priest cloth.
- Face: sharp masculine silhouette, no facial hair.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Reinterpret the character into a different archetype (priest, knight, ranger, etc.).
- Use long hair, copper-auburn hair, brown eyes, or ivory/white collar in any cell.
- Add facial hair (stubble, beard, mustache).
- Add face tattoos, scarification, bone hair accessories, or totem ornaments — they are NOT visible in the ref.

=== GRID SHEET LAYOUT (CRITICAL) ===
This image is a 4-column × 2-row sprite sheet of 8 separate face close-up portraits.

CELL SIZE: each cell exactly 768 × 768 px (square close-up).
GAP: solid #FF00FF magenta separator, exactly 32 px wide between adjacent cells (horizontal and vertical).
OUTSIDE GRID: solid #FF00FF magenta filling the canvas margin.

CELL POSITIONS (column index, row index — 0-based, top-left to bottom-right):
- (col=0, row=0): emotion = default
- (col=1, row=0): emotion = smile
- (col=2, row=0): emotion = serious
- (col=3, row=0): emotion = shock
- (col=0, row=1): emotion = anger
- (col=1, row=1): emotion = sad
- (col=2, row=1): emotion = cry
- (col=3, row=1): emotion = quiet

CELL CONTENT — face close-up (per cell):
- Crop: face only — top of head visible at top edge, neck/collar at bottom edge, ears within frame, eyes positioned on upper-third line.
- Frontal view with subtle 5-10° rotation. Face fills most of the cell.
- HAIR fully framed within the cell (wild shag, no overflow).
- Background of each cell: solid #FF00FF magenta (same as grid gap).
- 1-2 px clean dark outline along the character silhouette in each cell — for clean chroma cutout.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in a regular 4×2 grid.
- NO overlap between cells.
- NO crooked angles.
- Same scale/zoom across all 8 cells (face roughly same size in each).
- Same head height position within each cell.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: alert hunter at rest — composed but coiled, sense-driven. Steady forward gaze with very subtle nose-lift (~3°), as if reading wind. Lips closed, jaw relaxed but ready. Amber eyes sharp focus, NOT smiling, NOT scowling — the resting state of someone whose senses are always half on.

(1,0) SMILE: rare warmth — never broad. Very slight lip-corner lift on one side (asymmetric, almost a smirk), eye softening with subtle crinkle. The hunter's restrained smile — either dry humor at someone's expense, or quiet relief after a companion's safety is confirmed. NOT a friendly grin.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line. Direct eye contact, intent. Hunter's full attention mode — when the wind has changed and the prey/threat is identified.

(3,0) SHOCK: eyes widening (whites visible above iris), pupils dilated, lips parted with breath caught, brow lifted. Internal stop, NOT panic, NOT theatrical scream. The "회조 결별" first-crack moment — sense reading something the mind hasn't accepted yet. Hunter restraint preserved — body has not moved, only the face has registered.

(0,1) ANGER: controlled hunter restraint — eyes narrowed sharply, jaw tightened, lips pressed thin or slightly drawn back to reveal teeth subtly (animal restraint). Brow gathered. NOT explosive, NOT loud. Fury held inside hunter discipline — the "정화 재판 처형 유적 직면" beat. The kind of anger that stays quiet because words aren't worth spending.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin. Slight downward angle of head. Quiet grief, not weeping — the "같은 인간끼리도 이렇게 하느냐" beat. Hunter's metaphors have stopped; what remains is the plain weight of seeing.

(2,1) CRY: a single tear traced down one cheek, lips pressed hard, jaw subtly trembling but controlled. Eyes still open but gaze distant. Used very sparingly — the ch5 회조 흉터 만지는 비트 directly after. Hunter dignity preserved even in tears — no sob, no shake, just one tear acknowledging what cannot be undone.

(3,1) QUIET: no expression — the deepest emotional state. Gaze far away, eyes distant, lips relaxed but lifeless. Hands NOT visible (frame is face only) — but the emptiness is in the eyes alone. The "...형제가 날 버렸어" beat — the moment metaphors disappear and only direct fact remains. Stillness, not blankness — alive but emptied.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere — hair, skin, eyes, collar, all avoid #FF00FF and adjacent fuchsia.
- Output: 3168 × 1568 PNG, 4-column × 2-row grid sheet.
```
