---
name: p09-costume-coordination
default_scope:
  areas: [character-lore, art-pipeline]
  include_superseded: false
  exclude_types: [Task]
description: 22명 캐릭터의 P09 외형 슬롯/색상 coordination — 정복 baseline 유지하되 미세 변주로 monotone 탈피 + 진영/직책별 식별성 확보. 머신리더블 YAML 산출 → 에이전트가 P09 asset 자동 셋업.
output_format: _output_format.md
---

# Scenario: p09-costume-coordination

22명 캐릭터(lead 4 + sub-antagonist 4 + supporting 8 + background 8 — wiki registry 기준)의 P09 외형이 현재 monotonic 적용 상태(예: Armor_010 슬롯 5개를 그대로 일괄 사용 + 단톤 색 override)다. GPT Pro에 통합 재산출을 위임해서 진영/직책/voice baseline은 유지하되 캐릭터별 미세 파츠 변주 + 색감 다양화로 디자인 매력과 시각 식별성을 동시에 챙긴다. 응답은 머신리더블 YAML이며, 별도 셋업 에이전트가 그대로 P09 `*.asset`에 적용한다.

## 조립 규칙 (호출자용 메타)

- `default_scope.areas` 두 area를 pack_wiki.py로 전부 dump (Task 제외)
- `character-lore`: 캐릭터 wiki(각자 "외모" + "P09 visual spec" 섹션에 현재 P09 적용값 + 색 override 텍스트 명세) + `wiki-character-p09-appearance-presets`(전체 캐릭터 preset JSON) + registry/emotion/operation layer
- `art-pipeline`: `data-p09-appearance-customization-catalog`(슬롯/필드 spec) + `analysis-p09-visual-morphology-atlas`(형태 태그 atlas) + `analysis-p09-visual-baseline`(자산 spec + 매핑) + `analysis-p09-character-spec-agent-guide`(에이전트 가이드)
- placeholder 치환:
  - `{{focus_block}}` — 사용자 `--extra "..."` 시 `[FOCUS]\n{value}\n`, 없으면 빈 문자열
  - `{{bundle_summary}}` — 첨부 artifact 개수와 area 분포 한 줄
  - `{{output_format_block}}` — `scenarios/_output_format.md` 그대로

## Prompt body

```text
[CONTEXT]
이 채팅은 survival-manager 프로젝트의 P09 캐릭터 외형 coordination 작업이다.

프로젝트 성격:
- Unity prototype phase의 캐릭터 수집/성장 RPG. 자동전투 + 짧은 호흡 + 인간형 적군 only. 서브컬쳐 감성
- 인디 1인 개발 ($10 가격대) — P09 Modular Humanoid 자산 한 세트로 22 캐릭터 외형을 모두 cover. 신규 모델/텍스처 도입 금지.
- 캐릭터 식별이 narrative 매력의 절반 — 같은 P09 baseline 안에서 슬롯/색 변주로 캐릭터별 silhouette·color palette를 만들어내야 한다.

P09 시스템 baseline (첨부 art-pipeline 문서 참고):
- 기본 baseline 필드: gender(M/F) / Hair style(1-N) / HairColorId(1-N) / SkinId(1-N) / EyeColorId(1-N) / FacialHair(M only) / 가슴 크기(F only)
- 장비 슬롯 5개: head / chest / arm / waist / leg — 각각 Armor_XXX_{Slot} 형태로 카탈로그 번호 지정
- 무기 슬롯: Sword_XXX / Bow_XXX / Staff_XXX / Spear_XXX 등 클래스별
- 방패 슬롯: Shield_XXX
- 색상 오버라이드 시스템 — 각 슬롯별 1개 entry. 필드:
  - Label (사람 읽기용)
  - Use Part Target (boolean, 보통 true)
  - Target Part (HairColor / Skin / EyeColor 등 enum)
  - Target Contains (substring match, e.g., "Hair")
  - Enabled (boolean)
  - Main Color (#RRGGBB)
  - Second Color (#RRGGBB) — 의상 이중 톤 / pattern accent
  - Third Color (#RRGGBB) — trim / 작은 accent
  - Use Emission Color (boolean)
  - Emission Color (#RRGGBB)
- 보통 head/chest/arm/waist/leg + weapon + shield + hair = 8 entries per character

현재 문제 (사용자 직접 지적):
- Armor 번호가 한 캐릭터당 5 슬롯 모두 동일 번호(예: Armor_010_Head + Armor_010_Chest + Armor_010_Arm + Armor_010_Waist + Armor_010_Leg)로 일괄 적용 — P09 디자이너가 "_010 세트로 어울리게" 만들었으니 기본은 OK이지만 캐릭터별 변주 부재
- 색상이 단톤 — Main/Second/Third가 거의 유사한 색이라 입체감/식별성 부족
- 결과: 같은 진영 안에서 캐릭터 silhouette/palette 식별이 어렵고, 디자인이 성의 없게 느껴짐

목표:
- 정복(진영 + 직책) baseline은 살리기 — 솔라룸 정규군 trio는 여전히 정규군처럼 보여야 하고, 회상 결사원은 결사답게 보여야 함
- 단 정복 안에서도 미세 슬롯 변주 + 색 accent로 캐릭터별 식별성 확보 (head 모자가 다르다거나 leg가 같은 계열 다른 번호라거나, second color가 캐릭터 voice motif 색이라거나)
- 같은 progressive cluster(같은 진영, 같은 직책)도 1-2 슬롯 차별화 + main/second/third 색 톤 mixing으로 visual unity 안의 individual 살리기
- 카탈로그 안의 가용 번호만 사용 (없는 번호 절대 ban)

첨부 wiki dump:
- {{bundle_summary}}
- 각 character-lore wiki의 "외모" / "P09 visual spec (atlas 인용)" 섹션에 현재 P09 적용값 + 색 override 텍스트로 명세되어 있음
- art-pipeline의 catalog/atlas/baseline 문서에 가용 번호 + 형태 태그 + 시각 baseline + 에이전트 입력 규칙 모두 명시
- 본문은 pindoc artifact body 원문 그대로

{{focus_block}}

[TASK]
22명 캐릭터에 대해 P09 외형 spec을 통합 재산출하라. 각 캐릭터에 대해:

1. **슬롯 조합** — head/chest/arm/waist/leg + weapon + shield. 카탈로그 안의 가용 번호만. 한 캐릭터 안에서 5 armor 슬롯이 모두 동일 번호일 필요 없음 (변주 가능). 단 P09 디자이너가 만든 default 매칭(예: _010 세트끼리)이 어울리는 baseline임을 인지하고, 변주 시 의도가 있어야 함 — e.g., "사제+호위 이중 정체성을 표현하기 위해 chest는 priest 계열 _007, leg는 가벼운 정찰 _004 mix".

2. **기본 baseline** — Hair style / HairColorId / SkinId / EyeColorId / FacialHair / 가슴 크기. 캐릭터 voice motif + 인종 baseline에 맞게.

3. **색상 baseline** — 각 슬롯 entry의 main/second/third + emission. 캐릭터 voice/모티프/직책에 의미 있는 색. 같은 진영 안에서도 캐릭터별 신호색이 다르게.

4. **진영/직책 식별성** — 같은 진영(솔라룸/이리솔/회상 결사/그물 결사) 안에서 visual unity 유지하되, 직책 차이는 슬롯/색 변주로 즉시 구분 가능. 같은 직책(예: 솔라룸 정규군 trio) 안에서도 미세 변주.

5. **rationale + deviation** — 각 캐릭터에 대해 한국어 2-4줄로 "왜 이 조합인지" + 한국어 1-2줄로 "현재 wiki spec과 어떻게 달라졌는지(예: 같은 _010 세트 → mix로 바꾼 의도, 단톤 → 신호색 도입)".

[CONSTRAINTS]
- 카탈로그 안 가용 파츠 번호만 사용 — 카탈로그에 없는 번호 절대 금지. 카탈로그가 첨부 art-pipeline 문서에 명시되어 있음.
- 캐릭터 핵심 컨셉 (role, archetype, narrative voice, P09 visual baseline의 진영 정체성) 보존 — 시각 spec만 재산출.
- runtime asmdef / persistence schema / 시스템 결정 절대 X. 이 출력은 P09 *.asset 데이터만 바꾼다.
- 신규 모델/텍스처/material 도입 금지. P09 카탈로그 자산 안에서만.
- HEX 색은 #RRGGBB. lowercase or uppercase 둘 다 OK이지만 한 응답 안에서 통일.
- emission은 기본 OFF (`enabled: false` 또는 `#000000`). 캐릭터 특별 모티프(예: 격자 인광, 신성 백금 빛)일 때만 ON + 적당히 dim.
- 같은 진영 4-5명이 모두 monotone하게 똑같으면 안 됨 — 직책별 1 신호색 차별화 필수.
- 머리 색 + 눈 색 + 의상 색 사이에 색 충돌(예: 강한 보색 동시 사용) 없어야 함 — 캐릭터 magnetism을 위한 harmony.
- 인디 자산 한계 존중 — 카탈로그가 작아도 그 안에서 변주 가능. 카탈로그 확장 요청 금지.

[DELIVERABLE]
머신리더블 YAML 1개로 통합 출력. 응답 마지막에 sanity-check 표.

YAML 구조 (정확히 이 schema 사용):

~~~yaml
schema_version: 1
generated_for: survival-manager
generated_by: gpt-pro / p09-costume-coordination
characters:
  - id: hero_dawn_priest
    label: 단린
    faction: solarum
    role: priest_paladin
    baseline:
      gender: F
      hair_style: 4
      hair_color_id: 6
      skin_id: 1
      eye_color_id: 1
      facial_hair: 0
      bust: M
    armor_slots:
      head:  Armor_007_Head
      chest: Armor_007_Chest
      arm:   Armor_007_Arm
      waist: Armor_007_Waist
      leg:   Armor_007_Leg
    weapon: Sword_003
    shield: Shield_004
    color_overrides:
      hair:
        label: "머리"
        use_part_target: true
        target_part: HairColor
        target_contains: Hair
        enabled: true
        main: "#9B643F"
        second: "#9B643F"
        third: "#9B643F"
        use_emission: false
        emission: "#000000"
      head:
        label: "머리 장비"
        use_part_target: false
        target_part: null
        target_contains: null
        enabled: true
        main: "#D8C8A8"
        second: "#C9A24E"
        third: "#D8C8A8"
        use_emission: false
        emission: "#000000"
      chest: { ... }
      arm:   { ... }
      waist: { ... }
      leg:   { ... }
      weapon: { ... }
      shield: { ... }
    rationale: |
      솔라룸 사제+호위 이중 정체성을 chest(priest 계열 _007 robe)와 leg(_007의 brown trouser)로 표현.
      이전 _010 단조 일괄 적용을 탈피해 신성 ivory + 따뜻한 brown wrap의 2-zone 정체성을 유지.
      hair는 warm copper-auburn(#9B643F)로 단린 voice motif(따뜻한 사제)와 정합.
    deviation_from_current: |
      현재 spec(Armor_007 + #9B643F hair + #D8C8A8 tunic)을 baseline으로 두고, head/chest는 ivory main + gold second로 trim 강조해 단톤 탈피.
      leg는 동일 brown wrap이지만 second에 약한 ochre accent를 추가해 시각 흐름.
  - id: hero_pack_raider
    label: 이빨바람
    ...
~~~

각 캐릭터 entry는 위 schema 정확히 따른다. 슬롯 값이 없으면 `null` 또는 entry 자체 생략 가능 (e.g., 방패 없는 캐릭터는 `shield: null`).

응답 마지막에 sanity-check 표 1개:

| 캐릭터 | 진영 | 직책 | 같은 진영 다른 캐릭터와 silhouette 식별? | monotone 탈피? | 변경 우선순위 |
|---|---|---|---|---|---|
| 단린 | 솔라룸 | priest_paladin | O — chest ivory + waist brown 2-zone | O — main/second 분리 | P1 |
| ... | ... | ... | ... | ... | ... |

마지막에 **전체 패턴 요약** 2-3 단락:
- 진영별 baseline palette 결정 (e.g., 솔라룸=백금+적색trim / 이리솔=갈색leather+호박accent / 회상 결사=골회색+청록인광 / 그물 결사=라벤더+프리즘시안)
- 가장 큰 변화 cluster (어떤 캐릭터 group이 가장 많이 바뀌었나)
- 후속 작업 권장 (예: 추가 portrait 작업 시 어떤 캐릭터를 우선)

{{output_format_block}}
```

## Default scope 보완

P09 작업의 cross-reference 필요시 호출자가 추가:

- `narrative-script` 일부 (캐릭터의 chapter 등장 비트가 외형 결정에 영향 줄 때 — e.g., 캠페인 후반 단린 인장 풀린 모습) — 기본 미포함
- `flows` 의 `flow-p09-character-preset` — preset 고정/재수정 운영 flow — 기본 미포함, 사용자 hint 있을 때만
- `decision-*` 의 visual baseline 관련 결정 — 기본 미포함

기본은 character-lore + art-pipeline 두 area로 충분.

## 호출 예시

```text
/gpt-pro-submit p09-costume-coordination
→ character-lore + art-pipeline 전체 dump + 22 캐릭터 통합 spec 재산출

/gpt-pro-submit p09-costume-coordination --extra="이리솔 부족 4-5명 위주로 더 깊게 — 무리 분열 전후 시각 차이도 표현하면 좋겠다"
→ 같은 scope + focus_block 추가
```
