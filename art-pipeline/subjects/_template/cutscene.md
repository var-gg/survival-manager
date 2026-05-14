---
slug: cutscene_X--shot_01
kind: cutscene_cut
subject_id: cutscene_X
variant: shot_01
emotion: default
refs:                                    # 등장 캐릭터 portrait_full prior outputs를 ref로 첨부
  - hero_dawn_priest:portrait_full
  - hero_pack_raider:portrait_full
aspect: "16:9"
output_size: "1920x1080"
chroma: false                            # cutscene은 chroma OFF (full painted scene)
status: idea
---

# Cutscene template — replace placeholders below
chapter LUT 정책: `pindoc://analysis-cutscene-medium-mix`.

```prompt
# Cutscene: {cutscene_id} — Shot {shot_number}
Chapter: {chapter_id} | Beat: {narrative_beat_id}

## Shot type
{wide_establishing | two_shot | close_up | OTS | group}

## Characters in scene (refs above must drive identity)
1. {hero_dawn_priest} — position: {foreground center / left / right}, pose: {standing / crouching / arms raised}, expression: {determined / hesitant / shocked}
2. {hero_pack_raider} — position: ..., pose: ..., expression: ...

## Environment (if site-specific)
- Site: {site_id} — refer to `forest-semantic-asset-catalog-v1` for vendor pool
- BG: {painted environment — site mood, time of day, weather, lighting source}
- Foreground props: {asset_set_camp_baseline / asset_set_lighting_props 발췌}

## Camera
- Angle: {eye-level / low-angle hero / high-angle vulnerable}
- Framing: {wide / medium / close}
- Focal point: {character_id / object / environment hint}

## Lighting
- Key light: {warm sunset / cool moonlight / fire glow / magic ambient}
- Rim light: {direction + color}
- Shadow direction: consistent across all characters

## Mood
{narrative beat — 예: chapter 2 단현 일지 발견 직후, 단린의 신앙 균열의 첫 순간}
```
