---
slug: hero_echo_savant--face_emotion_sheet
kind: face_emotion_sheet
subject_id: hero_echo_savant
variant: face_emotion_sheet
refs:
  - hero_echo_savant
  - hero_echo_savant:portrait_full
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

# 공한 — Face Emotion Sheet (8 emotion frontal)

## 산출물

이 페이지 1번 호출로 8 emotion 얼빡 portrait를 한 sheet에 동시 생성. sheet_split.py로 8개 individual PNG로 분리.

## 단린/이빨바람/묵향과 차이점

emotion 표현 baseline이 **observer restraint + 1800년 침묵의 잔재**. 단린의 priest restraint, 이빨바람의 hunter restraint, 묵향의 dry-amused mystic restraint와 다른 축. 공한의 baseline은 **distant attentive composure** (먼 곳을 듣는 자의 차분함). 가드레일 무너지는 비트(quiet, sad, shock)에서 **분석 어휘 누출 / 인간적 기억 파편**이 표면화 — 표정으로는 입술 살짝 벌어짐 + 눈이 잠깐 바깥 초점에서 인간적 기억 안쪽으로 끌려감. cry는 매우 sparingly (1번 정도). 묵향처럼 broad 미소 거의 없음 — smile조차 매우 restrained.

## prompt 명세

```prompt
=== REF IS THE FULL CHARACTER BLUEPRINT (highest authority) ===
The attached image is the COMPLETE BLUEPRINT for this character. All 8 cells in the grid below are the SAME character with different facial expressions only — same outfit collar, same hair, same eye color, same skin, same head proportions, same identity.

Match the REF for the character's identity:
- Hair: SHORT spiky smoky violet/lavender (#92879F main, #C8C1D8 highlights). NOT long, NOT brown, NOT black, NOT bright purple. Hand-drawn strand definition, slightly tousled top.
- Eyes: yellow/amber iris (#D8B040 ~ #E0A030 family). NOT brown, NOT green, NOT violet.
- Skin: light pale tone with very slight cool undertone (#E8DDC8 family), soft idealized — masculine.
- Outfit collar (visible at neck/upper chest area): smoky lavender ritual armor (#8C7FA0) with cream/sage scarf (#D8D4C8) draped from one shoulder. NOT pure black, NOT priest white, NOT hunter leather.
- Face: slender masculine silhouette, no facial hair.
- Skin tone, eye spacing, nose shape, jaw shape, lip shape: identical across ALL 8 cells.

DO NOT:
- Change the character identity between cells.
- Reinterpret the character into a different archetype (assassin, dark mystic, monk).
- Use long hair, brown hair, black hair, or bright magenta in any cell.
- Add facial hair (stubble, beard, mustache).
- Add forehead lattice tattoos, hair-tip crystal attachments — they are NOT visible in the ref.

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
- Hair fully framed within the cell (short spiky, no overflow).
- Cream/sage scarf collar visible at the bottom of the frame (shoulder edge).
- Background of each cell: solid #FF00FF magenta (same as grid gap).
- 1-2 px clean dark outline along the character silhouette in each cell — for clean chroma cutout.

CELL ALIGNMENT (CRITICAL):
- All 8 cells perfectly aligned in a regular 4×2 grid.
- NO overlap between cells.
- Same scale/zoom across all 8 cells.
- Same head height position within each cell.

=== EMOTION DESCRIPTORS (per cell) ===

(0,0) DEFAULT: composed observer at rest — quiet attentiveness with distant focus. Eyes amber-bright but slightly half-lidded (the focus of someone attuned to subtle signals far past the viewer). Lips closed gently. Faint guarded reserve at lip corners. The "1800년 침묵에서 깨어난 자" baseline — listening, not speaking. NOT smiling, NOT severe.

(1,0) SMILE: very rare warmth — never broad. Almost imperceptible softening at one lip corner, eye crease very faint. The smile of someone who has spoken few words in many years; even joy is restrained. The "...너도 농담을 가르쳐 줄까" beat from 묵향 — tentative, brief, almost wondering at the act of smiling itself. NOT a friendly grin.

(2,0) SERIOUS: jaw set firmly, eyes carrying visible weight, brow gathered slightly. Lips closed in a firm line. Eye contact direct — but with the additional quality of "weighing something far older than this moment." The observer's full focus mode.

(3,0) SHOCK: lips parted slightly with breath caught, eyes widening (whites visible above iris), pupils dilated, brow lifted. The observer's stillness disrupted — but contained. NOT panic. The internal "stop" of a 1800년 보유자가 처음 보는 것에 마주한 비트. Hair almost imperceptibly disheveled by a small breath catch.

(0,1) ANGER: cold restraint — eyes narrowed sharply with distant ice (NOT hot fury), jaw tightened, lips pressed thin. A stillness more dangerous than expression. The observer who has seen rage cycle for 1800 years and chosen not to add to it.

(1,1) SAD: eyes lowered, lashes hide gaze partially, lips pressed thin without tremor. Slight downward angle of head. Quiet grief — the mourning of fellow conclave members who have entered Hollow state. The "...불을 쬐던 손이 따뜻했다. 그것만 기억한다" beat — human memory fragment surfacing.

(2,1) CRY: a single dry tear traced down one cheek, lips pressed firmly, no jaw tremor — restrained absolutely. Eyes still open but gaze far past the viewer. Used very sparingly — perhaps once across the campaign. Observer dignity preserved even in tears. The 1800년 보관해둔 무게가 처음으로 표면에 비치는 비트.

(3,1) QUIET: no expression — the deepest emotional state. The observer's attentive baseline has emptied. Lip corners flat, gaze far away, eyes distant. The "...괜찮다" beat — the answer that is almost never true. Stillness, not blankness — alive but deeply withdrawn into the 1800년 안. Hair feels heavier in shadow.

=== HARD CONSTRAINTS ===
- Single subject per cell, single character across all 8 cells.
- Background of each cell + grid gaps: solid #FF00FF magenta, flat fill, no gradient.
- 1-2 px clean dark outline on character silhouette in each cell.
- No magenta tint on character anywhere.
- Output: 3168 × 1568 PNG, 4-column × 2-row grid sheet.
```
