---
slug: hero_X--portrait_full_default
kind: character_portrait_full
subject_id: hero_X
variant: portrait_full
emotion: default
refs:
  - hero_X
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: idea
---

# Character template — replace placeholders below

```prompt
# Character: {hero_id} ({한국어 표시명} / {English alias})

## Identity
- Hair: color {HEX}, length, style (e.g., 중간 길이 웨이브 dawn-priest 스타일)
- Eyes: color {HEX}, expression
- Skin: tone (e.g., warm ivory, cool porcelain)
- Build: slim/athletic/etc.
- Age: visual age range

## Outfit (per character lore registry)
- Layer 1 (inner): description, color {HEX}
- Layer 2 (mid): description, color {HEX}
- Layer 3 (outer): description, color {HEX}
- Accessories: belt, jewelry, sigils

## Weapon / Prop
- Type: {weapon_type}
- Material/finish: stylized highlights
- Position: held in {hand}, pose context

## Composition
- 2:3 portrait, 3/4 view, character centered
- Pose: {default | combat ready | introspective | etc.}
- Eyeline: {straight to viewer | distant | down}

## Mood
- {emotion}: {감정 묘사 + 상황 hint}
```
