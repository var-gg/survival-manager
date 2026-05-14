---
slug: bg_X--day
kind: environment_site                   # town BG / UI BG / loading screen
subject_id: bg_X
variant: day                             # day | night | dusk | special
emotion: default
refs: []                                 # 후속 cycle에서 prior output 가능
aspect: "16:9"
output_size: "1920x1080"
chroma: false                            # background도 chroma OFF
status: idea
---

# Background template — replace placeholders below
용도: town 메인 화면 BG, 가챠/시설 BG, UI overlay BG, 로딩 화면.

```prompt
# Background: {bg_id} ({한국어 표시명} / {English name})
Use case: {town_main | town_facility:{시설명} | gacha | loading_screen | UI_overlay}

## Subject (environment)
{환경 묘사 — 예: 솔라룸 임시 main hall 내부, 이리솔 부족 회의 천막, 가챠 의식장}

## Composition
- 16:9 wide
- Camera: {eye-level interior / wide establishing / front view static}
- Foreground: {props 묘사 — 화로, 책상, 갑옷 거치대}
- Mid-ground: {주요 architectural elements}
- Background: {window / wall / sky / distant view}

## Vendor asset alignment
- Forest pack 발췌: {asset_set_camp_baseline / asset_set_lighting_props / asset_set_ruins_structure 일부}
- 양식화 필요한 부분: {vendor에 없는 요소 — 일러로 흡수}

## Mood
{narrative tone — 예: 솔라룸 잔당의 무거운 결의 / 이리솔 부족의 거친 활기 / 회상 결사의 침잠}

## Lighting
- Time: {day | dusk | night | morning}
- Source: {warm hearth / cool moonlight / candle ambient / window streak}
- Color grading: {chapter LUT — pindoc://analysis-cutscene-medium-mix}

## Empty space policy
- UI overlay 영역에 무거운 detail 금지 (left 30% / right 30% 또는 bottom 25% empty zone 명시)
- Subject prompt에 empty zone 위치 명시 권장
```
