---
slug: skill_X--default
kind: skill_icon                         # skill_icon | passive_icon | equipment_icon
subject_id: skill_X
variant: default
emotion: default
refs: []                                 # icon은 보통 ref 없이 prompt만으로 충분
aspect: "1:1"
output_size: "1024x1024"
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
- 1:1 square, subject CENTERED
- Subject takes ~70% of canvas (clear margin)
- Hero rim-light upper-left, deep shadow lower-right
- NO multi-object, NO scene, NO character

## Mood
{element / archetype / element family — e.g., 회상 결사 기억, element_shadow_soul, 솔라룸 정화 신성}
```
