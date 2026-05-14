# Game Image Style Anchor — Cutscene (kind: cutscene_cut)

> 16:9 cinematic, multiple characters allowed, environmental dressing required, dramatic lighting, no chroma.
> 매체 mix 정책: `pindoc://analysis-cutscene-medium-mix`.

```text
=== ART STYLE (cutscene kind 엄수) ===
Stylized Japanese fantasy game cutscene illustration — cinematic painterly, multi-character composition allowed, environmental dressing required.

Stylization:
- Painterly base per common anchor (premium mobile JRPG + console anime fantasy game tradition).
- Cel-shading at hard geometry edges (weapon, armor plate, rock, ruin wall).
- Soft gradient on skin, hair, fabric drape.
- Hand-drawn brushwork feel — intentional line weight.

Character consistency:
- ATTACHED character refs (P09 anchor + prior portraits) must drive identity, hair color, eye color, outfit.
- Multiple characters in scene must each match their respective portrait_full ref.
- Outfit detail follows character lore registry.

Composition:
- 16:9 wide cinematic.
- Up to 4 characters in scene (lead 4 캐릭터 cutscene 케이스).
- Environmental BG required — NOT magenta chroma, full painted environment.
- Camera angle per cutscene script (over-the-shoulder, wide establishing, close two-shot, group composition).

=== LAYOUT / COMPOSITION (cutscene kind 엄수) ===
Cinematic framing per shot type (subject prompt에 명시):
- Wide establishing: 환경 + 캐릭터 작게.
- Two-shot: 캐릭터 2명 medium framing.
- Close-up: 1명 얼굴 + 환경 hint.
- OTS (over-the-shoulder): foreground 1명 (back) + background 1명 (face).
- Group: 3-4명 staged composition.

Multi-character interaction:
- 캐릭터 silhouette 명확, 서로 가리지 않게.
- Eyeline + 자세로 관계 표현.
- 한 캐릭터가 화면 anchor (감정 carry), 나머지는 reaction.

Environmental BG:
- 해당 site의 환경 톤 ([forest-semantic-asset-catalog-v1] 참조).
- Site-painted illustration이 있으면 cycle 6 painted_illustration을 ref로 첨부 가능.

=== SHADING / LIGHTING (cutscene kind) ===
Dramatic story-moment lighting:
- Key light per scene mood (warm sunset, cool moonlight, fire glow, magic ambient).
- Rim light defining silhouette against environment.
- Shadow direction consistent across all characters in scene.
- Color grading per chapter LUT.

=== CHROMA BACKGROUND (cutscene kind) ===
NO magenta chroma. NO uniform background fill.
- Background = painted environment with proper atmospheric depth.
- Cutscene is full-scene illustration, not cutout.

=== NEGATIVE (cutscene kind 추가) ===
- NO single character splash pose (use character_portrait_full instead).
- NO icon-style centered subject (use icon kinds instead).
- NO map preview top-down (use map_painted instead).
- NO comic panel gutters or speech bubbles.
- NO photorealistic everything.
- NO inconsistent character identity across cutscene shots (refs win on identity).
```
