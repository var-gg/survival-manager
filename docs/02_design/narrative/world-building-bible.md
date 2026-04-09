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
- **톤 키워드**: 먼지, 재, 녹슨 철, 희미한 인광, 오래된 돌. 세계는 이미 한 번 끝났고, 그 잔해 위에서 살아가는 느낌이어야 한다.

## 연대기

사건 순서를 절대연표로 정리한다. chapter beat 문서는 이 연표를 다시 쓰지 않고 참조만 한다.

| chronology_id | era | 연대 (대략) | public_truth | hidden_truth | reveal_window |
|---|---|---|---|---|---|
| `chrono_first_peoples` | 태초기 | ~3000년 전 | 네 종족이 대륙 곳곳에 정착했다 | 최초에는 적대감 없이 공존했다. Heartforge가 아직 가동 전이었다 | `chapter_3` |
| `chrono_heartforge_creation` | 창조기 | ~2500년 전 | 고대 문명이 세계의 심장부에 위대한 유물을 남겼다 | Heartforge는 기억을 에너지로 변환하는 장치이며, 부작용으로 적대감을 증폭한다 | `chapter_3` |
| `chrono_first_war` | 첫 전쟁기 | ~2000년 전 | 종족 간 첫 대규모 전쟁이 발발했다 | Heartforge 가동이 적대감을 처음 증폭한 결과다 | `chapter_3_site_1` |
| `chrono_relicborn_sealing` | 봉인기 | ~1800년 전 | 유적은 비어 있다 | Relicborn는 Heartforge 봉인 관리자이며, 격자를 완성한 뒤 자발적 휴면에 들어갔다 | `chapter_3_site_2` |
| `chrono_long_peace` | 장기 평화기 | ~1800~200년 전 | 봉인 이후 종족 간 긴장이 줄었다 | 격자가 Heartforge 출력을 억제하고 있었다. 종족들은 이유를 몰랐다 | — |
| `chrono_seal_decay` | 격자 풍화기 | ~200년 전 | 변경 지대에 이상한 진동과 분쟁이 늘어났다 | 봉인 격자가 풍화되기 시작하면서 Heartforge 출력이 미세하게 누출되었다 | `chapter_2` |
| `chrono_kingdom_abuse` | 왕국 오용기 | ~80년 전 | 왕국이 강력한 방벽 기술을 개발했다 | 왕국 학자들이 격자 파편을 발견하고 방어 장치로 전용했다. 이것이 격자 풍화를 가속했다 | `chapter_2_site_2` |
| `chrono_rising_tension` | 긴장 고조기 | ~80~5년 전 | 전선 곳곳에서 교전이 빈번해졌다 | Heartforge 누출이 강해지면서 모든 종족의 적대감이 증폭되었다. 왕국 오용이 근본 원인이지만 아무도 모른다 | `chapter_2` |
| `chrono_fall_of_gate` | 붕괴기 | ~5년 전 | 국경문이 외부 침공으로 무너졌다 | Heartforge 파편 공명이 세 세력의 적대감을 증폭해 붕괴를 촉발했다. 외부 침공은 구실일 뿐이다 | `chapter_3` |
| `chrono_expedition_begins` | 원정기 | 현재 | 혼성 원정대가 원인 조사를 위해 파견된다 | 원정의 진짜 목적은 순환 종결이 된다 | `chapter_4~5` |

## 세력 정의

### Kingdom Remnant (`faction_kingdom_remnant`)

| 항목 | 내용 |
|---|---|
| 공식 이름 | Kingdom Remnant |
| 세계관 | 질서와 재건 |
| 즉각 필요 | 국경 통제와 생존 |
| 숨겨진 진실 | 봉인을 오용한 책임이 있다 |
| 게임플레이 연결 | `bastion_front`, `protect_carry` |

- **지도자 계층**: Steward Council. 국왕은 전사했고, 5인 집정관이 잔여 영토를 분할 통치한다. 최고 집정관 직위는 공석이며, 군사/행정/신앙/교역/정보 각 한 명이 합의로 결정한다.
- **사회 구조**: 주둔지 기반 위계. 군인-장인-사제-민간인 4단 계급. 전쟁 이후 계급 간 이동이 강제로 열렸지만, 사제 계급의 정화 권한이 나머지를 억누른다.
- **신앙**: '영원한 질서(Eternal Order)'. 세계에는 정해진 자리가 있으며, 이탈은 오염이라 본다. 봉인 격자 파편을 신성한 유물로 숭배하지만, 그것이 실제로 무엇인지는 모른다.
- **금기**: 사제의 정화 재판에 이의를 제기하는 것. 결과적으로 왕국 내부의 이견은 지하로 숨는다.
- **어조/언어 색채**: 명령형, 격식체. 칭호와 직위를 빠뜨리면 무례로 간주한다. 색채는 강철 회색, 백금, 곤색.
- **오해받기 쉬운 점**: 플레이어가 처음에는 왕국을 '질서의 편'으로 인식하지만, chapter 2에서 정화 재판과 격자 오용이 드러나면서 가해자이기도 했음을 알게 된다.

### Beastkin Clans (`faction_beastkin_clans`)

| 항목 | 내용 |
|---|---|
| 공식 이름 | Beastkin Clans |
| 세계관 | 자유와 영역 수호 |
| 즉각 필요 | 잃어버린 사냥터 탈환 |
| 숨겨진 진실 | Heartforge 공명에 본능적으로 반응한다 |
| 게임플레이 연결 | `weakside_dive`, `tempo_swarm` |

- **지도자 계층**: Pack Council. 각 씨족(pack)의 수장이 모여 계절마다 합의한다. 합의가 깨지면 개별 씨족이 독자 행동하고, 이것이 외부에서는 약탈로 보인다.
- **사회 구조**: 씨족 단위의 수평 연대. 혈연과 사냥 성과가 지위를 결정한다. 노약자를 돌보는 의무가 강하고, 이를 어기는 자는 추방된다.
- **신앙**: '순환의 숨결(Breath of the Cycle)'. 생사는 순환이며, 사냥과 먹힘은 같은 고리의 양면이라 본다. 의례에서 사냥감의 이름을 부르고 감사를 표한다.
- **금기**: 사냥감을 낭비하는 것, 아이의 첫 사냥 전에 피를 보여주는 것.
- **어조/언어 색채**: 직설적이고 감각적 비유가 많다. 바람, 피, 이빨, 흙 같은 구체적 명사를 선호한다. 색채는 적갈색, 호박색, 이끼 녹색.
- **오해받기 쉬운 점**: 외부에서는 야만적 약탈자로 보지만, 실제로는 영역이 침범당해 반격하는 것이다. Heartforge 공명이 본능을 자극해 반응이 과격해지는 것을 본인들도 인지하지 못한다.

### Undead Remnant (`faction_undead_remnant`)

| 항목 | 내용 |
|---|---|
| 공식 이름 | Undead Remnant |
| 세계관 | 기억의 보존과 항구 |
| 즉각 필요 | 존재 지속과 기억 회수 |
| 숨겨진 진실 | 과거 봉인 실패의 직접적 후손이다 |
| 게임플레이 연결 | `summon_pressure`, `sustain_grind` |

- **지도자 계층**: Memory Conclave. 가장 오래된 기억을 보유한 자가 발언권을 갖는다. 의사결정은 기억 투표(memory ballot)로 이루어지며, 각자가 보유한 기억의 양과 질이 표의 무게를 결정한다.
- **사회 구조**: 기억 계층. 깨어난 시기가 아니라 보유 기억의 깊이로 서열이 나뉜다. 기억이 소실된 자(Hollow)는 최하층이며, 이들의 복원이 사회적 의무다.
- **신앙**: '영원의 기록(Eternal Ledger)'. 죽음은 전환이지 소멸이 아니며, 기억이 남아 있는 한 존재는 지속된다. 기억의 완전 소실만이 진정한 죽음이다.
- **금기**: 타인의 기억을 허락 없이 읽거나 소거하는 것. 이를 어기면 Conclave에서 추방된다.
- **어조/언어 색채**: 과거형과 회고가 많다. 시간 감각이 살아있는 자와 다르며, 수백 년 전 사건을 어제 일처럼 말한다. 색채는 골회색, 창백한 청색, 인광 녹색.
- **오해받기 쉬운 점**: 외부에서는 죽은 것들의 침략으로 보지만, 실제로는 자신들의 기억과 존재를 지키려는 것이다. 소환 압력(summon_pressure)은 잃어버린 동료를 되찾으려는 본능적 행동이다.

### Relicborn (`faction_relicborn`)

| 항목 | 내용 |
|---|---|
| 공식 이름 | Relicborn |
| 세계관 | 보존과 봉인 |
| 즉각 필요 | 파손된 격자 수복 |
| 숨겨진 진실 | 침략자가 아니라 수문장이다 |
| 게임플레이 연결 | `control_cleanse`, barrier |

- **지도자 계층**: Lattice Chorus. 격자(lattice) 노드마다 한 명의 수문장(Warden)이 배치되어 있으며, 합의는 격자를 통한 공명으로 이루어진다. 휴면 중에는 합의가 정지되고, 각성한 수문장이 독자 판단한다.
- **사회 구조**: 기능별 분담. 수문장(방어/정화), 기록관(기억 보관), 직공(격자 수복), 관측자(외부 감시). 계급이라기보다 역할이며, 역할 전환이 자유롭다.
- **신앙**: '그물의 법칙(Law of the Lattice)'. 세계는 하나의 격자이며, 모든 존재는 그물의 매듭이다. 매듭이 끊어지면 전체가 약해진다. Heartforge는 격자의 심장이며, 제어해야 할 대상이지 파괴할 대상이 아니다.
- **금기**: 격자를 사적 목적으로 사용하는 것. 왕국이 격자 파편을 방어 장치로 전용한 행위는 Relicborn에게 최악의 모독이다.
- **어조/언어 색채**: 공명, 진동, 파장 같은 물리적 비유를 쓴다. 감정 표현이 절제되어 있고, 필요한 말만 한다. 색채는 수정 백색, 프리즘 무지개빛, 심연 보라.
- **오해받기 쉬운 점**: 다른 세 세력은 Relicborn를 유적의 이질적 침입자로 보지만, 실제로는 격자가 무너지는 것을 막으려는 수문장이다. 각성 후 보이는 장벽과 정화 능력이 공격으로 오인된다.

## 지역 및 사이트

### 상위 지역

| id | category | description | narrative_rule |
|---|---|---|---|
| `region_ashen_frontier` | region | Ashen Gate 주변의 불탄 변경지대. 검게 그을린 들판과 무너진 초소가 이어진다. 바람에 재가 섞여 시야가 흐리다. | chapter 1 무대. 원정의 출발점이며, 세계의 위협을 처음 체감하는 곳. |
| `region_sunken_bastion` | region | 지반 침하로 반쯤 가라앉은 왕국 요새 지대. 돌 위에 이끼와 녹이 번지고, 지하수가 복도를 적신다. | chapter 2 무대. 왕국의 위선과 내부 균열이 드러나는 곳. |
| `region_ruined_crypts` | region | 고대 묘역과 유골 과수원이 펼쳐진 지하 세계. 창백한 인광이 천장에서 흘러내리고, 기억의 잔향이 공기처럼 떠돈다. | chapter 3 무대. 언데드의 진실과 Relicborn 각성이 일어나는 곳. |
| `region_glass_forest` | region | 고대 폭발로 유리화된 숲. 나무가 투명한 결정이 되어 빛을 굴절시키고, 발 아래에서 유리 파편이 부서진다. | chapter 4 무대. 모든 세력이 피해를 입은 중립 지대. |
| `region_worldscar` | region | 전선 아래로 열린 상처형 대지. 지표가 갈라진 협곡이 심장부를 향해 하강하며, 깊이 들어갈수록 Heartforge의 맥동이 직접 느껴진다. | chapter 5 무대. 최종 하강과 결전의 장소. |

### 사이트별 환경 묘사

| site_id | region | 환경 묘사 | 핵심 분위기 |
|---|---|---|---|
| `site_ashen_gate` | `region_ashen_frontier` | 무너진 국경문의 잔해 사이로 원정대가 진입한다. 철문 파편이 바닥에 박혀 있고, 포대 위에 까마귀가 앉아 있다. 원거리에서 간헐적 교전음이 들린다. | 긴장된 출발, 전쟁의 흔적 |
| `site_wolfpine_trail` | `region_ashen_frontier` | 소나무 숲길. 야수족의 영역 표식이 나무에 새겨져 있고, 발자국과 갈퀴 자국이 교차한다. 안개가 낮게 깔리고, 먼 곳에서 울음소리가 들린다. | 추적과 조우, 첫 오해 |
| `site_sunken_bastion` | `region_sunken_bastion` | 지반 아래로 기울어진 왕국 요새. 대리석 기둥이 비스듬히 서 있고, 물이 차오른 지하실에서 녹슨 무기가 떠다닌다. 사제들의 정화 의식 흔적이 벽에 남아 있다. | 권위의 부패, 가라앉는 질서 |
| `site_tithe_road` | `region_sunken_bastion` | 십일조 행렬이 다니던 포장 도로. 도로 양편에 처형대와 고해소가 번갈아 서 있다. 깨진 성물함과 피 묻은 집행 기록이 흩어져 있다. | 신앙의 폭력, 통제의 민낯 |
| `site_ruined_crypts` | `region_ruined_crypts` | 고대 묘역의 입구. 석관이 층층이 쌓여 있고, 일부는 열려 있다. 인광 이끼가 통로를 희미하게 밝히며, 기억의 속삭임이 간헐적으로 들린다. | 죽은 자의 진실, 서서히 열리는 비밀 |
| `site_bone_orchard` | `region_ruined_crypts` | 유골이 뿌리처럼 자라난 석화 과수원. 나무 형태의 골격 구조물 사이로 Relicborn의 봉인 격자 조각이 빛난다. 중심부에서 각성의 맥동이 느껴진다. | 중반 반전, Relicborn 각성 |
| `site_glass_forest` | `region_glass_forest` | 유리화된 거목 사이의 개활지. 빛이 결정을 통과하면서 무지개 파편이 사방에 흩어진다. 발밑에서 유리가 부서지는 소리가 울린다. 모든 세력의 전투 흔적이 결정 안에 얼어 있다. | 아름다움과 파괴의 공존, 공감 |
| `site_starved_menagerie` | `region_glass_forest` | 유리 숲 깊은 곳의 동물 우리 잔해. 과거 야수족이 기르던 생물들이 유리화되거나 변이했다. 굶주린 변이체들이 영역을 지킨다. | 지속 압박, 혼성 위협 |
| `site_heartforge_gate` | `region_worldscar` | Worldscar 입구의 거대한 석조 관문. 네 세력의 문양이 각각 새겨져 있으며, 격자의 잔재가 문을 봉인하고 있다. 맥동이 가슴팍까지 울린다. | 결전 직전의 서약, 수렴 |
| `site_worldscar_depths` | `region_worldscar` | Heartforge가 있는 심부. 벽면에 모든 종족의 기억이 빛의 형태로 투사되어 있다. 중앙의 Heartforge는 거대한 심장처럼 뛰고 있으며, 그 주위로 파손된 격자가 부유한다. | 최종 진실, 순환의 종결 |

## 유물 / 장소 taxonomy

| id | category | owner | description | narrative_rule |
|---|---|---|---|---|
| `relic_heartforge_core` | relic | `faction_relicborn` | Worldscar 심부에 있는 기억-에너지 변환 장치. 심장 형태로 맥동하며, 격자가 제어하지 않으면 적대감을 무한 증폭한다. | main conflict와 직접 연결된다. 파괴하면 기억이 전부 소실되므로, 정화/봉인/정지 중 선택해야 한다. |
| `relic_lattice_shards` | relic | `faction_relicborn` (원래) / 여러 세력이 산포 | Heartforge를 제어하는 격자의 파편. 왕국이 일부를 방어 장치로 전용했고, 이것이 격자 풍화를 가속했다. 각 사이트에서 하나씩 발견된다. | 수집/복원 서브 목표. 모든 파편을 모으면 격자 복원이 가능해진다. |
| `relic_memory_vessels` | relic | `faction_undead_remnant` | 기억을 물리적으로 저장하는 용기. 깨지면 기억이 주변에 투사된다. 묘역 곳곳에 산재해 있다. | chapter 3에서 언데드의 진실을 플레이어에게 보여주는 장치. |
| `relic_pack_totems` | relic | `faction_beastkin_clans` | 씨족 영역의 경계를 표시하는 토템. 야수족의 본능과 영역 기억이 새겨져 있다. Heartforge 공명에 반응해 진동한다. | chapter 1에서 야수족 영역 침범의 물리적 증거. |
| `location_execution_grounds` | location | `faction_kingdom_remnant` | 사제 계급이 정화 재판을 집행하는 장소. 십일조 도로(Tithe Road) 곁에 있다. | chapter 2에서 왕국의 폭력성을 보여주는 핵심 장소. |
| `location_conclave_chamber` | location | `faction_undead_remnant` | Memory Conclave의 의사결정 장소. 기억 투표가 이루어지는 반구형 석실. | chapter 3에서 언데드의 사회 구조를 보여주는 장소. |
| `location_lattice_nexus` | location | `faction_relicborn` | 격자 노드가 수렴하는 중심점. Heartforge 바로 위에 있다. 수문장들이 합의를 이루던 곳. | chapter 5에서 최종 정화/봉인을 시도하는 장소. |

## 명명 규칙

- **세력**: `faction_` 접두사, snake_case
- **지역**: `region_` 접두사
- **사이트**: `site_` 접두사
- **유물**: `relic_` 접두사
- **장소**: `location_` 접두사
- **연대기**: `chrono_` 접두사
- **임시 작명**: `[WIP]` 접두사를 붙이고, 확정 시 제거
- **캐릭터 호칭**: 일반 대화에서는 display_name을 사용한다. 공식 문서에서는 hero_id를 병기한다.
- **지역 어휘 규칙**: 왕국 관련은 건축/금속/종교 어휘, 야수족은 자연/동물/감각 어휘, 언데드는 기억/시간/기록 어휘, Relicborn는 격자/파장/공명 어휘를 우선한다.

## retcon 금지 규칙

- Heartforge는 기억을 에너지로 변환하는 장치다. 이 기능을 변경하지 않는다.
- Relicborn는 침략자가 아니라 수문장이다. 이 정체성을 뒤집지 않는다.
- 네 세력 모두 Heartforge의 영향을 받는다. 한 세력만 면역으로 만들지 않는다.
- main conflict는 순환의 종결로 닫힌다. 지배/정복형 엔딩으로 바꾸지 않는다.
- 세계는 단일 대륙이며, 외부 대륙은 후속작 hook으로만 암시한다.

## 작성 지침

- chapter-level 사건은 여기서 서술하지 말고 `campaign-story-arc.md`에 둔다.
- hero personal bio는 여기서 길게 쓰지 말고 `docs/02_design/deck/character-lore-registry.md`로 보낸다.
- 모든 ID는 영어 snake_case를 유지한다.
