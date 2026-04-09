# 세계관 바이블

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/world-building-bible.md`
- 관련문서:
  - `docs/02_design/narrative/faction-conflict-matrix.md`
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/deck/character-lore-registry.md`

## 목적

세계의 불변 진실과 세력/지리/연대기/명명 규칙을 하나로 고정한다. 이 문서는 retcon을 막는 기준점으로 사용한다.

## 세계의 전제

플레이어가 첫 30분 안에 이해해야 하는 표층 진실과 후반에 밝혀질 심층 진실을 분리해 서술한다.

- **표층 진실**: Ashen Gate가 붕괴했고, 인간/야수족/언데드의 전선이 무너졌다. 원정대가 파견되어 Worldscar를 따라 하강하며 원인을 조사한다.
- **심층 진실**: 전쟁은 매장된 Heartforge가 기억과 적대감을 재증폭하는 순환이다. Relicborn는 침략자가 아니라 오래된 봉인의 수문장이며, 최종 목표는 지배가 아니라 순환의 종결이다.

## 핵심 테마와 톤 가드레일

- **주제**: 오해에서 비롯된 전쟁, 봉인된 진실의 해방, 적과 동맹의 경계
- **톤**: grim expedition. 세계는 가혹하지만 희망이 완전히 부재하지는 않다.
- **허용 유머**: `gallows wit` 수준의 건조한 유머만 허용. 코미디 릴리프 캐릭터 금지.
- **금지 표현**: gratuitous gore, 아동 대상 폭력, 성적 폭력

## 연대기

사건 순서를 절대연표로 정리한다. chapter beat 문서는 이 연표를 다시 쓰지 않고 참조만 한다.

| chronology_id | era | public_truth | hidden_truth | reveal_window |
|---|---|---|---|---|
| `chrono_heartforge_creation` | 창조기 | 고대 문명이 세계의 심장부에 위대한 유물을 남겼다 | Heartforge는 기억을 에너지로 변환하는 장치이며, 부작용으로 적대감을 증폭한다 | `chapter_3` |
| `chrono_relicborn_sealing` | 봉인기 | 유적은 비어 있다 | Relicborn는 Heartforge 봉인 관리자이며 자발적 휴면에 들어갔다 | `chapter_3_site_2` |
| `chrono_fall_of_gate` | 붕괴기 | 국경문이 외부 침공으로 무너졌다 | Heartforge 파편 공명이 세 세력의 적대감을 증폭해 붕괴를 촉발했다 | `chapter_3` |
| `chrono_expedition_begins` | 원정기 | 혼성 원정대가 원인 조사를 위해 파견된다 | 원정의 진짜 목적은 순환 종결이 된다 | `chapter_4~5` |

## 세력 정의

| faction_id | public_name | worldview | immediate_need | hidden_truth | gameplay_link |
|---|---|---|---|---|---|
| `faction_kingdom_remnant` | Kingdom Remnant | 질서와 재건 | 국경 통제와 생존 | 봉인을 오용한 책임이 있다 | `bastion_front`, `protect_carry` |
| `faction_beastkin_clans` | Beastkin Clans | 자유와 영역 수호 | 잃어버린 사냥터 탈환 | Heartforge 공명에 본능적으로 반응한다 | `weakside_dive`, `tempo_swarm` |
| `faction_undead_remnant` | Undead Remnant | 기억의 보존과 항구 | 존재 지속과 기억 회수 | 과거 봉인 실패의 직접적 후손이다 | `summon_pressure`, `sustain_grind` |
| `faction_relicborn` | Relicborn | 보존과 봉인 | 파손된 격자 수복 | 침략자가 아니라 수문장이다 | `control_cleanse`, barrier |

## 지역 / 유물 / 금기

| id | category | owner | description | narrative_rule |
|---|---|---|---|---|
| `relic_heartforge_core` | relic | `faction_relicborn` | 전쟁 기억을 증폭하는 핵 | main conflict와 직접 연결된다 |
| `region_worldscar` | region | — | 전선 아래로 열린 상처형 대지 | 후반부 핵심 무대 |
| `region_ashen_gate` | region | `faction_kingdom_remnant` | 무너진 국경문과 인접 전선 | 캠페인 시작 무대 |
| `region_glass_forest` | region | — | 유리화된 숲, 모든 세력이 피해를 입은 중립 지대 | chapter 4 무대 |

## 명명 규칙

- **세력**: `faction_` 접두사, snake_case
- **지역**: `region_` 접두사
- **유물**: `relic_` 접두사
- **연대기**: `chrono_` 접두사
- **임시 작명**: `[WIP]` 접두사를 붙이고, 확정 시 제거

## retcon 금지 규칙

- Heartforge는 기억을 에너지로 변환하는 장치다. 이 기능을 변경하지 않는다.
- Relicborn는 침략자가 아니라 수문장이다. 이 정체성을 뒤집지 않는다.
- 네 세력 모두 Heartforge의 영향을 받는다. 한 세력만 면역으로 만들지 않는다.
- main conflict는 순환의 종결로 닫힌다. 지배/정복형 엔딩으로 바꾸지 않는다.
- 세계는 단일 대륙이며, 외부 대륙은 후속작 hook으로만 암시한다.

## 작성 지침

- chapter-level 사건은 여기서 서술하지 말고 `campaign-story-arc.md`에 둔다.
- hero personal bio는 여기서 길게 쓰지 말고 `character-lore-registry.md`로 보낸다.
- 모든 ID는 영어 snake_case를 유지한다.
