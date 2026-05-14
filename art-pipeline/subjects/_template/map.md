---
slug: site_X--concept_thumbnail
kind: map_concept                        # map_concept (시안 1장 default) | map_painted (선택적 variant)
subject_id: site_X
variant: concept_thumbnail               # concept_thumbnail | painted_dusk / painted_rain / painted_boss_intro 등
emotion: default
refs: []                                 # map_concept=[] | map_painted=[site_X:reference_screenshot]
aspect: "16:9"
output_size: "1920x1080"
chroma: false                            # map kind는 항상 chroma OFF
status: idea
---

# Map template — site당 시안 1장 (concept_thumbnail) + 선택적 painted variant
정책 baseline: `pindoc://map-concept-cycle-and-edge-treatment-v1` (4-layer + 분지 + atmospheric perspective).

ChatGPT는 vendor ID를 모르므로 prompt는 visual language only로 작성한다. vendor 매핑은 사용자 + AI가 시안 결과를 보고 mental model로 처리. third-party IP 이름(Granblue Fantasy, Honkai Star Rail 등) 금지.

```prompt
# {Site name humanized — e.g., 늑대소나무길 / Wolfpine Trail}

A {one-line scene mood — peaceful pine forest path at golden hour}. Stylized painterly cartoon environment in the tradition of premium mobile JRPG world maps and high-end console anime fantasy game environment splash art — anime-leaning fantasy game aesthetic, NOT photoreal, NOT concept-art realism. Quarter-view tilt-down perspective (about 33° elevation, slight 12° rotation), wide cinematic 16:9. No characters in the scene — environment as subject.

## Four-layer composition (near to far)

1. **Inner playable ground (foreground center)** — STRICTLY CLEAN. {path/floor/road description}. 캐릭터 활동 surface, props 없음, 짧은 edge 디테일만.

2. **Buffer zone (around the playable, NOT on it)** — soft natural scatter blending the playable into surroundings: {ferns / grass tufts / mossy rocks / stumps / fallen logs / mushroom clusters — site별 적절히}.

3. **Frame zone (LEFT and RIGHT flanks only — proscenium)** — tall silhouette objects framing the stage: {large trees / large boulders / standing stones / ruin pillars / statues — site별}. Their silhouettes arch slightly inward to create theater proscenium feel. Defines stage edge without invading middle.

4. **Backdrop layer (distant)** — {distant hills / city silhouette / mountains}, fading into atmospheric haze. Layered silhouettes separated by haze. {Sky description — sky type, sun/moon position, cloud bank, light shafts}.

## Natural basin form
The playable ground sits low. Buffer rises gently. Frame zone is taller still. Backdrop crests highest. Eye flows from inner → flanking frame → distant backdrop, never hitting an empty edge.

## Atmospheric perspective
Foreground full-saturated and detailed. Middle slightly desaturated. Frame zone slightly hazy. Backdrop strongly desaturated and fog-tinted toward {warm/cool} {haze color} per scene mood.

## Reproduction feasibility (CRITICAL)
The illustration must look like a stylized low-poly toon-shaded 3D environment — recreatable with simple stylized polygon trees / rocks / foliage / props + toon shader + painterly post-process.
- Tree silhouettes: simple bold shapes, NOT individual painted leaves.
- Rocks: bold faceted stylized shapes with stylized moss patches, NOT photoreal micro-detail.
- Standing stones / ruin walls: rectangular rough-hewn slabs with simple flat low-relief carvings, NOT illuminated runes, NOT intricate gothic carving.
- Foliage: painterly clumps, NOT individual blades.

## Mood
{narrative beat — site의 감정적 무게}

## Palette
- {2-3 color descriptions per layer}

## Camera
- Quarter-view tilt-down, about 33° elevation, 12° rotation
- Wide 16:9 cinematic
- Frame proportions: foreground (path + buffer) ~50%, frame zone ~30%, backdrop + sky ~20%

## What this is NOT
- NOT a character close-up
- NOT photoreal terrain or rock micro-detail
- NOT a top-down map view
- NOT a battle screen with HUD / health bars / minimap
- NOT cluttered (path strictly clean)
- NOT lens blur on backdrop (atmospheric fade OK)
- NOT illuminated/glowing rune carvings
- NOT individually rendered leaves
```
