# Game Image Style Anchor — Common (모든 kind 공통)

> `assemble_prompt.py`가 모든 kind 호출에 prepend한다.
> 그림체 baseline + NEGATIVE 보편 항목.
> kind별 sub-anchor (`style-anchor-{character,map,icon,cutscene}.md`)가 추가 정책을 더한다.

```text
=== STYLE BASELINE (모든 kind 공통) ===
Stylized Japanese-style fantasy game art baseline:
- Premium mobile JRPG / gacha banner art tradition 80% — painterly base with rich color washes, magnetic glamorous appeal, vivid harmonious palette.
- High-end console anime fantasy game splash & environment art tradition 20% — cinematic atmosphere, story-moment lighting, painterly soft transitions.
- 일본 정통 painterly + cel mix — painterly gradient dominates, cel-shading sparingly only at hard edges.
- 명암 처리: 닫힐 땐 하드 (jaw under, fold creases, weapon edge), 풀릴 땐 부드러운 그라데이션 (skin, hair, fabric drape).
- Hand-drawn brushwork feel — intentional line weight, purposeful brush direction, NOT generic AI smoothing, NOT plastic vector look.

=== NEGATIVE COMMON (모든 kind 공통) ===
General avoidance:
- Watermarks, signatures, text artifacts in the image.
- Real-world brand logos. Use stylized fantasy sigils only.
- Plastic / waxy texture, melted shapes.
- Generic AI illustration smoothing (no purposeful brush direction).
- Symmetric "AI" composition (eyes too symmetric, layout too symmetric).
- Oversaturated neon palette.
- Photorealistic rendering of any element (this is FANTASY GAME ART).
- Extra fingers, malformed hands.
```
