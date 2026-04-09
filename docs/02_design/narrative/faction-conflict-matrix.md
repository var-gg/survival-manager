# 세력 충돌 행렬

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/faction-conflict-matrix.md`
- 관련문서:
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/deck/hero-expansion-roadmap.md`

## 목적

세력 쌍별 오해, 실제 진실, 충돌축, encounter link를 한눈에 정리한다. 이 문서는 세력 갈등의 관계형 source of truth다.

## 왜 별도 문서가 필요한가

world bible은 각 세력의 단독 설명을 소유하고, campaign arc는 chapter-level 진행을 소유한다. faction pair의 충돌 관계는 별도 매트릭스로 분리해야 중복이 줄어든다.

## 충돌 행렬 표

| faction_a | faction_b | public_conflict | hidden_truth | encounter_link | first_resolution_window |
|---|---|---|---|---|---|
| `faction_kingdom_remnant` | `faction_beastkin_clans` | 국경 약탈과 보복 | 둘 다 Heartforge 파편에 반응해 오판하고 있다 | `weakside_dive`, `protect_carry` | `chapter_1_site_2` |
| `faction_kingdom_remnant` | `faction_undead_remnant` | 이단과 정화 | 왕국이 과거 봉인 오용의 주범이다 | `bastion_front`, `mark_execute` | `chapter_2_site_1` |
| `faction_kingdom_remnant` | `faction_relicborn` | 유적 침입자 탄압 | Relicborn는 왕국보다 먼저 이 땅을 지키고 있었다 | `protect_carry`, `control_cleanse` | `chapter_4_site_1` |
| `faction_beastkin_clans` | `faction_undead_remnant` | 사냥터 오염과 영역 분쟁 | 둘 다 Heartforge 공명에 다른 방식으로 반응할 뿐이다 | `tempo_swarm`, `sustain_grind` | `chapter_3_site_1` |
| `faction_beastkin_clans` | `faction_relicborn` | 본능적 적대 (이질적 존재) | Relicborn 격자가 야수족 본능 공명을 완화할 수 있다 | `weakside_dive`, `control_cleanse` | `chapter_4_site_2` |
| `faction_undead_remnant` | `faction_relicborn` | 유적 소유권 분쟁 | 둘 다 과거 봉인 실패의 후손이다 | `summon_pressure`, `control_cleanse` | `chapter_3_site_2` |

## chapter별 해소 상태

| conflict_pair | chapter_1 | chapter_2 | chapter_3 | chapter_4 | chapter_5 |
|---|---|---|---|---|---|
| kingdom-beastkin | seed | escalate | partial resolve | deepen empathy | mostly resolved |
| kingdom-undead | hidden | seed+escalate | partial resolve | negotiate | mostly resolved |
| kingdom-relicborn | hidden | hidden | teaser | confrontation->negotiate | stabilized |
| beastkin-undead | seed | hidden | partial resolve | mutual understanding | mostly resolved |
| beastkin-relicborn | hidden | hidden | teaser | first contact->ally | stabilized |
| undead-relicborn | hidden | hidden | reveal | negotiate | stabilized |

## 작성 지침

- 세력 단독 lore는 `world-building-bible.md`를 복제하지 않는다.
- gameplay link는 encounter family 혹은 answer lane ID로 연결한다.
- chapter resolution 표현은 동일한 어휘 세트를 유지한다: `hidden`, `seed`, `escalate`, `teaser`, `partial resolve`, `confrontation`, `negotiate`, `mutual understanding`, `deepen empathy`, `mostly resolved`, `stabilized`.
