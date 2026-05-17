---
slug: skill_X--default
kind: skill_icon                         # skill_icon | passive_icon | equipment_icon
subject_id: skill_X
variant: default
emotion: default
refs: []                                 # icon은 보통 ref 없이 prompt만으로 충분
aspect: "1:1"
output_size: "1568x1568"                 # sheet 작업 기본: 2x2, 768px cells + 32px gutter
chroma: "#FF00FF"
status: idea
---

# Icon template — replace placeholders below

```prompt
# Icon: {icon_id} ({한국어 표시명} / {English name})
Kind: {skill | passive | equipment}

## Subject
{subject 묘사 — 예: 보라색 영혼 불꽃, 부서진 격자 파편, 회색 책 펼친 모양, 검은 양각 검}

## Visual cue (single concept silhouette)
- Primary shape: {기본 도형 — e.g., 원형 마법진, 검 silhouette, 사각 부적}
- Detail: {정교 묘사 — emboss, glow, runes, edges}
- Color zone: primary {HEX}, accent {HEX}

## Composition
- For list work, generate a 2x2 sheet: 1568x1568 canvas, 768x768 cells, 32px pure #FF00FF gutters and outer margin
- For single-icon work, 1024x1024 is allowed
- 1:1 square per cell, subject CENTERED
- Subject takes ~70% of canvas (clear margin)
- Hero rim-light upper-left, deep shadow lower-right
- NO multi-object, NO scene, NO character

## Chroma / cutout contract
- Background must be flat pure #FF00FF from canvas edge to the subject's outer stroke
- Subject must have a continuous clean 2-4 px outer stroke
- No shadow, blur, glow, particles, or semi-transparent residue outside the outer stroke
- No magenta / fuchsia / hot pink on the subject itself
- No text, letters, numerals, frame, or border

## Mood
{element / archetype / element family — e.g., 회상 결사 기억, element_shadow_soul, 솔라룸 정화 신성}
```
