# 챕터 비트 시트

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/chapter-beat-sheet.md`
- 관련문서:
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/narrative/narrative-pacing-formula.md`
  - `docs/02_design/narrative/dialogue-event-schema.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`

## 목적

chapter/site/node 단위의 서사 비트, 감정값, reveal, 영웅 합류 시점을 표로 고정한다. 이 문서는 node-level narrative truth의 기준이다.

## 비트 작성 규칙

- 각 node는 반드시 `type`, `beat_label`, `emotion_targets(T/R/H/C)`, `difficulty`, `unlock`을 가져야 한다.
- topology나 encounter lane 자체는 `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`를 참조하고 여기서는 반복하지 않는다.
- 모든 감정값은 `narrative-pacing-formula.md`의 수식과 일치해야 한다.

## 사이트 서사 유형

| site_id | narrative_type | 의미 |
|---|---|---|
| site_ashen_gate | 발견형 | node3에서 핵심 단서 발견 |
| site_wolfpine_trail | 추격형 | node2에서 관계 충돌이 먼저 터짐 |
| site_sunken_bastion | 재판형 | extract가 relief가 아닌 불편한 결론 |
| site_tithe_road | 강하형 | boss 후 질문이 더 커짐 |
| site_ruined_crypts | 발견형 | node3에서 핵심 단서 발견 |
| site_bone_orchard | 강하형 | boss 후 세계가 뒤집힘 |
| site_glass_forest | 재판형 | extract가 합의 실패와 비용 확인 |
| site_starved_menagerie | 추격형 | node1에서 개인 관계 충격이 먼저 |
| site_heartforge_gate | 재판형 | extract가 서약의 무게 |
| site_worldscar_depths | 강하형 | 모든 것이 수렴 |

## 50노드 구조표

### Chapter 1: Ashen Gate

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_ashen_gate` | 1 | `skirmish` | 경계 돌파와 징후 확인 | 28 | 69 | 8 | 3 | 1.4 | — |
| `site_ashen_gate` | 2 | `skirmish` | 초기 교전 규칙 확립 | 33 | 63 | 10 | 3 | 1.7 | — |
| `site_ashen_gate` | 3 | `elite` | 엘리트 경비 격파 | 40 | 54 | 9 | 10 | 2.2 | — |
| `site_ashen_gate` | 4 | `boss` | 문지기 처치 | 50 | 40 | 6 | 32 | 2.9 | — |
| `site_ashen_gate` | 5 | `extract` | 첫 단서 회수 | 10 | 100 | 18 | 20 | 1.2 | — |
| `site_wolfpine_trail` | 1 | `skirmish` | 짐승길 추적 | 36 | 66 | 8 | 4 | 2.0 | — |
| `site_wolfpine_trail` | 2 | `skirmish` | 오해에서 동맹으로 | 41 | 60 | 10 | 4 | 2.3 | — |
| `site_wolfpine_trail` | 3 | `elite` | 매복 진실 파악 | 48 | 51 | 9 | 12 | 2.8 | — |
| `site_wolfpine_trail` | 4 | `boss` | 팩 우두머리 격파 | 58 | 37 | 6 | 60 | 3.5 | — |
| `site_wolfpine_trail` | 5 | `extract` | 정찰 동맹 확보 | 18 | 100 | 18 | 38 | 1.8 | `hero_rift_stalker` |

#### Chapter 1 서사 상세

| site_id | n | narrative_detail |
|---|---:|---|
| `site_ashen_gate` | 1 | Dawn Priest가 무너진 국경문의 잔해를 넘으며 원정대를 이끌고 첫 진입한다. 포대 잔해 위의 까마귀 떼와 재로 뒤덮인 들판이 전쟁의 규모를 체감시키고, 원정대원들은 철문 파편 사이에서 격자 파편의 희미한 맥동을 처음 감지한다. 플레이어는 이 세계가 이미 한 번 끝났다는 사실을 시각적으로 확인한다. |
| `site_ashen_gate` | 2 | 국경문 안쪽 초소에서 잔존 왕국 경비병들과 첫 교전이 벌어진다. Dawn Priest는 영원한 질서의 사제로서 통과 허가를 요청하지만 거부당하고, 경비병들은 원정대를 탈영병으로 취급한다. 원정대는 왕국 자체의 질서가 이미 무너져 있음을 깨닫는다. |
| `site_ashen_gate` | 3 | 국경문 주 탑에 주둔한 엘리트 경비 부대가 원정대의 진행을 차단한다. 이들은 Heartforge 공명의 영향으로 과잉 적대적이며, Dawn Priest는 같은 왕국 출신의 병사가 왜 이토록 광기에 가까운 적의를 보이는지 의문을 품기 시작한다. 격전 끝에 탑 내부에서 기이하게 진동하는 파편 하나를 발견한다. 동시에 탑 벽면에 인간도 야수족도 아닌 설계의 격자 문양이 새겨져 있는 것을 발견한다. Dawn Priest는 이 문양이 어떤 교단 기록에도 없다며 당혹해하고, Pack Raider는 "우리 발톱 자국도 아니다"라고 말한다. 네 번째 존재가 이곳에 있었다는 최초의 암시다. |
| `site_ashen_gate` | 4 | 국경문의 자동 방어 골렘 '재의 문지기'가 기동한다. 문지기는 봉인 격자 에너지로 작동하는 고대 장치이며, Dawn Priest는 '영원한 질서'가 숭배하는 성유물의 힘이 실제로는 무기로 전용된 것임을 목격한다. 원정대가 골렘을 쓰러뜨리자 내부에서 첫 번째 Heartforge 파편이 드러난다. |
| `site_ashen_gate` | 5 | 원정대가 골렘 잔해에서 Heartforge 파편을 회수하며 첫 단서를 확보한다. Dawn Priest가 파편을 손에 쥐자 짧은 환시가 밀려오고, 과거 국경문이 건설되기 전 이 땅에 네 종족이 함께 살았던 한 순간의 기억이 투사된다. Pack Raider가 합류 후 이 파편의 맥동이 야수족 영역 토템과 동일한 진동임을 지적하며, 양 세력의 유물이 같은 근원에서 나왔다는 첫 실마리가 열린다. |
| `site_wolfpine_trail` | 1 | 원정대가 소나무 숲길에 진입하자 나무마다 갈퀴 자국으로 새긴 야수족 영역 표식이 나타난다. Pack Raider가 이 표식의 의미를 Dawn Priest에게 설명하며 "이건 경고가 아니라 기도다"라고 말한다. 안개 속에서 들려오는 울음소리가 단순한 짐승이 아닌 씨족의 순찰 신호임이 밝혀진다. |
| `site_wolfpine_trail` | 2 | 야수족 정찰대와 조우하지만, Pack Raider의 중재로 전면 충돌이 피해진다. Dawn Priest가 Ashen Gate에서 회수한 파편을 보여주자 야수족 정찰대장이 동요하며 "우리 토템이 미쳐 날뛰는 이유가 그것 때문이냐"고 묻는다. Pack Raider가 토템의 구조를 설명하면서 Dawn Priest는 '영원한 질서'의 성유물과 야수족 토템이 동일 물질임을 확인한다. |
| `site_wolfpine_trail` | 3 | 숲 깊은 곳에서 원정대가 야수족 매복에 걸린다. 그러나 교전 도중 매복을 지시한 것이 씨족의 수장이 아니라 Heartforge 공명에 자극받은 하위 전사들의 독단임이 드러난다. Pack Raider가 흥분한 전사들을 제지하며 본능의 폭주와 진짜 의지를 구분해야 한다고 역설한다. |
| `site_wolfpine_trail` | 4 | 소나무 숲의 가장 깊은 곳에서 Grey Fang이 원정대를 가로막는다. 그는 Pack Raider의 피형제이자 씨족의 장로 — 같은 날 피의 서약을 나눈 사이다. Grey Fang은 Pack Raider를 배신자로 부르지만, 그 목소리에는 분노만큼이나 상처가 섞여 있다. 형제끼리 이빨을 세우는 것은 씨족법에서 가장 무거운 금기이며, 둘 다 그것을 알고 있다. 전투 중 Heartforge 파편이 공명하자 Grey Fang의 격분이 자신의 의지를 넘어서는 것을 자각한다. 패배 후 그는 무릎을 꿇고 "이 분노가 내 것이 아닐 수 있다"고 인정한다 — 평생의 유대에 금이 가는 순간이다. 적대가 끝난 것이 아니라 시작된 균열이며, 인간과 야수족 사이의 증오가 조작된 것일 가능성을 피형제 사이의 싸움이 처음으로 열어둔다. |
| `site_wolfpine_trail` | 5 | 회색 송곳니가 씨족의 정찰 전문가 Rift Stalker를 원정대에 배속시킨다. Pack Raider와 Rift Stalker가 야수족 전통에 따라 피의 서약을 나누고, Dawn Priest는 이 의식이 영원한 질서의 사제 서임과 얼마나 닮았는지에 경악한다. 원정대는 첫 종족간 동맹을 확보하고, Worldscar를 향한 다음 구간의 길잡이를 얻는다. |

### Chapter 2: Sunken Bastion

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_sunken_bastion` | 1 | `skirmish` | 주둔지 진입 | 44 | 62 | 6 | 5 | 2.7 | — |
| `site_sunken_bastion` | 2 | `skirmish` | 호위-보호 패턴 확인 | 49 | 56 | 8 | 5 | 3.0 | — |
| `site_sunken_bastion` | 3 | `elite` | 보급 기록 탈취 | 56 | 47 | 7 | 14 | 3.5 | — |
| `site_sunken_bastion` | 4 | `boss` | 타락 지휘관 처치 | 66 | 33 | 4 | 40 | 4.2 | — |
| `site_sunken_bastion` | 5 | `extract` | 요새 배신 증거 확보 | 26 | 96 | 16 | 24 | 2.5 | `hero_bastion_penitent` |
| `site_tithe_road` | 1 | `skirmish` | 십일조 행렬 습격 | 53 | 58 | 6 | 6 | 3.3 | — |
| `site_tithe_road` | 2 | `skirmish` | 표식-처형 위협 학습 | 58 | 52 | 8 | 6 | 3.6 | — |
| `site_tithe_road` | 3 | `elite` | 정화 의식의 실체 확인 | 65 | 43 | 7 | 16 | 4.1 | — |
| `site_tithe_road` | 4 | `boss` | 심문관 격파 | 75 | 29 | 4 | 62 | 4.8 | — |
| `site_tithe_road` | 5 | `extract` | 신앙 균열이 표면화 | 35 | 92 | 16 | 40 | 3.1 | `hero_pale_executor` |

#### Chapter 2 서사 상세

| site_id | n | narrative_detail |
|---|---:|---|
| `site_sunken_bastion` | 1 | 원정대가 지반 침하로 기울어진 왕국 요새에 진입한다. 비스듬히 서 있는 대리석 기둥과 물이 차오른 복도를 보며 Dawn Priest는 자신이 훈련받던 요새와 같은 양식임을 알아본다. 요새 수비대가 원정대를 적으로 분류하고 즉각 교전하며, Dawn Priest는 같은 영원한 질서의 신도끼리 싸우는 현실에 직면한다. |
| `site_sunken_bastion` | 2 | 요새 내부에서 수비대가 지하 보급창고를 호위하는 패턴이 포착된다. Grave Hexer가 기억 감응으로 벽에 남은 잔향을 읽으며 이 보급창고가 단순한 군수품이 아니라 격자 파편 가공 시설임을 의심한다. Dawn Priest는 사제 계급만 접근 가능한 구역의 존재에 의문을 품기 시작한다. |
| `site_sunken_bastion` | 3 | 원정대가 침수된 기록실에서 왕국의 비밀 보급 기록을 탈취한다. 기록에는 80년 전부터 학자들이 봉인 격자 파편을 채굴하여 방어 장치로 전용한 내역이 상세히 적혀 있다. 기록 더미 속에서 Dawn Priest가 "A. Sternholt"라는 서명이 적힌 연구 일지를 발견한다 — 적대자의 이름이 처음으로 등장하는 순간이다. 일지의 한 항목에 "제4수문(the Fourth Gate)"이라는 언급이 있고, 그 옆에 "격자가 우리가 생각한 것보다 더 많은 것을 기억하고 있다. 네 번째 봉인 뒤에 무언가가 잠들어 있다"는 메모가 적혀 있다. Dawn Priest는 이 언급을 이해하지 못하지만 기록에 남겨둔다. Dawn Priest는 자신이 숭배해온 성유물이 도굴품이었다는 사실에 충격을 받고, Pack Raider는 야수족 영역이 왜 불안정해졌는지의 직접적 원인을 확인한다. |
| `site_sunken_bastion` | 4 | 요새 최심부에서 타락한 지휘관 '침묵의 집정관'이 격자 파편으로 강화된 갑옷을 입고 원정대를 막는다. 집정관은 Aldric의 수제자였으며, 갑옷에는 Aldric의 연구 문장이 새겨져 있다. 그는 왕국의 격자 오용을 알면서도 은폐한 장본인이며, "질서를 지키려면 진실은 매장해야 한다"고 선언한다. 패배 후 그의 갑옷에서 빠져나온 격자 파편이 주변의 물에 기억을 투사하며 요새 건설 당시의 착취가 영상으로 재생된다. |
| `site_sunken_bastion` | 5 | 원정대가 요새의 배신 증거 전체를 확보하고, 감옥에 갇혀 있던 Bastion Penitent를 해방한다. Bastion Penitent는 진실을 알고 내부 고발을 시도하다 투옥된 전직 왕국 기사이며, "우리 왕국이 지키겠다고 맹세한 것을 우리 스스로 부수고 있었다"고 증언한다. Dawn Priest의 신앙에 첫 번째 균열이 생기며, 원정대는 왕국이 피해자인 동시에 가해자임을 받아들인다. |
| `site_tithe_road` | 1 | 원정대가 십일조 행렬이 다니던 포장 도로에 진입하자, 도로 양편의 처형대와 고해소가 일정한 간격으로 늘어서 있다. Dawn Priest는 이 시설들이 '정화 재판'용임을 알아보고 얼굴이 굳는다. Pack Raider가 "인간은 자기 동족에게도 이렇게 하느냐"고 물으며, 야수족과 인간의 잔혹함이 본질적으로 다르지 않다는 사실이 부각된다. |
| `site_tithe_road` | 2 | 도로 위에서 왕국 사제단의 순찰대와 조우한다. 사제단은 원정대원들의 이마에 '오염 표식'을 찍으려 하며, 표식이 찍히면 다음 고해소에서 정화 처형이 집행된다고 경고한다. Dawn Priest가 같은 사제 계급의 권위로 저지하지만, 순찰대장은 "문 밖에 나간 자는 이미 오염되었다"며 거부한다. 플레이어는 신앙이 얼마나 폭력적 통제 도구로 변질되었는지를 직접 체험한다. |
| `site_tithe_road` | 3 | 도로 중심부의 대형 고해소에서 '정화 의식'의 실체가 드러난다. 사제들이 격자 파편 가루를 피험자에게 주입하여 기억을 소거하는 의식이 진행 중이며, 이것이 왕국이 내부 이견을 처리하는 방식이었다. Dawn Priest가 정화 공식의 원본 문서를 조사하자, 저자가 Aldric Sternholt임을 발견한다 — 자신의 신앙이 떠받드는 창립 문서가 실은 무기 매뉴얼이었다. Grave Hexer가 기억 소거의 잔해를 읽고 분노하며, 이 방식이 언데드가 가장 금기시하는 '타인의 기억 무단 소거'와 동일함을 지적한다. |
| `site_tithe_road` | 4 | 십일조 도로의 종점에서 대심문관 '정결의 불꽃'이 원정대를 이단으로 선고하고 최종 전투가 벌어진다. 대심문관은 격자 파편으로 강화된 정화의 불을 다루며, Dawn Priest는 자신이 수행해온 축복 의식과 이 파괴적 정화가 같은 원리임을 깨닫는다. 대심문관의 패배와 함께 십일조 도로의 정화 체계가 붕괴하며, Dawn Priest는 영원한 질서의 사제로서의 정체성이 근본부터 흔들린다. 대심문관이 쓰러지는 순간, 그의 갑옷에서 고풍스러운 음성 파편이 흘러나온다 — Aldric의 완전한 말이 아니라, 격자에 각인된 어조의 잔향이다. "...통제할 수 있소..."라는 했소/이오 체의 한 마디. Dawn Priest가 얼어붙는다. 이 말투는 경전의 어조와 같지만, 무기에서 흘러나오고 있다. |
| `site_tithe_road` | 5 | 대심문관의 집행 기록에서 정화 재판의 전체 규모가 드러나며, 도로에서 해방된 Pale Executor가 원정대에 합류한다. Pale Executor는 대심문관 휘하에서 처형을 집행하다가 양심의 가책으로 반란을 일으킨 전직 집행자이다. Dawn Priest는 "내가 믿던 신앙이 도굴품 위에 세워진 폭력이었다"고 고백하며, 원정대 내부에서 왕국 출신 세 사람의 신앙 균열이 표면화된다. |

### Chapter 3: Ruined Crypts

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_ruined_crypts` | 1 | `skirmish` | 묘역 하강 | 63 | 54 | 4 | 7 | 4.0 | — |
| `site_ruined_crypts` | 2 | `skirmish` | 언데드 기억 회수 | 68 | 48 | 6 | 7 | 4.3 | — |
| `site_ruined_crypts` | 3 | `elite` | 소환 압력 대응 | 75 | 39 | 5 | 18 | 4.8 | — |
| `site_ruined_crypts` | 4 | `boss` | 묘지 수문장 처치 | 85 | 25 | 2 | 48 | 5.5 | — |
| `site_ruined_crypts` | 5 | `extract` | 유물 수호 신호 감지 | 45 | 88 | 14 | 28 | 3.8 | `hero_aegis_sentinel` |
| `site_bone_orchard` | 1 | `skirmish` | 유골 과수원 진입 | 74 | 49 | 4 | 8 | 4.6 | — |
| `site_bone_orchard` | 2 | `skirmish` | 왕국의 흡수 계획 폭로 | 79 | 43 | 6 | 8 | 4.9 | — |
| `site_bone_orchard` | 3 | `elite` | Relicborn 각성 | 86 | 34 | 5 | 20 | 5.4 | — |
| `site_bone_orchard` | 4 | `boss` | 중심 수문장 격파 | 96 | 20 | 2 | 72 | 6.1 | — |
| `site_bone_orchard` | 5 | `extract` | 중반 반전 정리 | 56 | 83 | 14 | 56 | 4.4 | `hero_echo_savant` |

#### Chapter 3 서사 상세

| site_id | n | narrative_detail |
|---|---:|---|
| `site_ruined_crypts` | 1 | 원정대가 고대 묘역의 입구로 하강한다. 층층이 쌓인 석관 사이로 인광 이끼가 희미한 빛을 드리우고, 공기 중에 기억의 속삭임이 떠돈다. Grave Hexer가 이 속삭임을 해독하며 "이들은 공격하는 것이 아니라 잃어버린 동료를 부르는 것"이라고 설명한다. 원정대는 언데드 영역이 공포의 땅이 아니라 기억의 보관소임을 처음 이해하기 시작한다. |
| `site_ruined_crypts` | 2 | 묘역 깊은 곳에서 첫 번째 기억 용기(memory vessel)가 발견되고, Grave Hexer가 의식적으로 용기를 열자 고대 공존 시대의 기억이 쏟아져 나온다. 기억 속에서 네 종족이 적대감 없이 같은 땅에 살던 장면이 투사되며, Dawn Priest와 Pack Raider는 전쟁 이전의 세계를 직접 목격한다. Grave Hexer는 "모든 세력이 같은 기억을 잃었다"며, 기억의 정의를 찾겠다는 결의를 밝힌다. |
| `site_ruined_crypts` | 3 | 묘역 중심부에서 언데드 소환 압력이 급격히 증가한다. Memory Conclave의 하급 수문장들이 잃어버린 동료의 기억을 복원하려 Heartforge 잔류 에너지를 끌어당기며, 이 과정에서 공간 자체가 불안정해진다. Grave Hexer가 Conclave의 기억 투표 방식을 설명하며, 소환 압력이 침략이 아니라 복원 본능임을 원정대에게 납득시킨다. |
| `site_ruined_crypts` | 4 | 묘역 최심부를 지키는 고대 수문장 '침묵의 기록관'과 전투가 벌어진다. 침묵의 기록관은 가장 오래된 기억을 보유한 언데드이며, 전투 중 그가 가진 기억이 파편적으로 쏟아져 나와 Heartforge가 처음 가동된 순간의 진실이 투사된다. 패배 후 기록관이 "우리가 지킨 것의 이름을 기억하는 자가 왔다"며 적대를 멈추고, Grave Hexer에게 묘역의 열쇠를 넘긴다. |
| `site_ruined_crypts` | 5 | 묘역 최심부에서 봉인 격자의 미약한 신호가 감지되고, 그 신호를 추적하던 Aegis Sentinel이 원정대 앞에 모습을 드러낸다. Aegis Sentinel은 Relicborn 수문장이 아닌 언데드 출신의 유물 수호자로, 격자 파편을 무단 사용하는 왕국에 대한 분노와 격자를 수복해야 한다는 의무감 사이에서 갈등해왔다. 그의 합류로 원정대는 Relicborn의 존재를 처음 인지하며, "유적의 이질적 침입자"라는 기존 인식에 의문을 품게 된다. |
| `site_bone_orchard` | 1 | 원정대가 유골 과수원에 진입한다. 뼈가 뿌리처럼 자라난 석화 나무들 사이로 봉인 격자 조각이 희미하게 빛나고, 중심부에서 미지의 맥동이 전해온다. Grave Hexer가 이곳의 기억 밀도가 묘역보다 수십 배 높다며 경고하고, Dawn Priest는 격자 조각이 왕국의 성유물과 같은 파장으로 진동하는 것을 확인한다. 원정대 전원이 무언가 거대한 것이 깨어나려 한다는 불안감을 공유한다. |
| `site_bone_orchard` | 2 | 과수원 내부에서 왕국의 비밀 채굴 기록이 격자 조각에 새겨진 형태로 발견된다. 왕국이 이 과수원에서 대량의 격자 파편을 채굴하여 방어 장치와 정화 도구로 전용한 이력이 드러나며, 이것이 봉인 격자 전체의 풍화를 결정적으로 가속한 원인이었음이 확인된다. Pack Raider는 "인간이 빼앗은 것이 우리 모두의 울타리였다"며 분노하고, Dawn Priest는 왕국의 죄가 야수족과 언데드 양쪽에 동시 피해를 주었음을 인정한다. |
| `site_bone_orchard` | 3 | 과수원 중심의 가장 큰 석화 나무에서 봉인 격자가 임계점에 도달하고, Relicborn의 첫 번째 각성이 발생한다. 격자 조각들이 공명하며 나무에서 빛의 형체가 떠오르고, 수천 년간 휴면했던 Relicborn 수문장이 깨어난다. 각성의 여파로 기억 투사가 발생하며, 투사 속에서 Aldric Sternholt의 살아있던 시절 얼굴이 나타난다 — 그가 직접 격자 파편을 물리적으로 추출하는 장면이다. Echo Savant가 "이 자의 의지가 아직 심장부 안에 남아 있다"고 경고한다. 각성한 수문장은 원정대를 격자 침범자로 인식하여 공격하지만, 전투 중 Grave Hexer의 기억 감응이 수문장의 격자 기억과 접촉하며 "적이 아니라 수복자가 왔다"는 인식이 전달된다. 중반 반전의 시작점이다. |
| `site_bone_orchard` | 4 | 과수원의 중심 수문장 '뿌리의 관망자'가 각성하여 격자의 마지막 방어 프로토콜을 가동한다. 뿌리의 관망자는 1800년간 이 과수원을 지켜온 Relicborn이며, 그가 가동한 방어막이 인간/야수족/언데드의 기억을 동시에 투사하여 세 세력이 서로에게 가한 피해의 전체 그림이 전장에 펼쳐진다. 패배 후 관망자는 "그물이 찢어진 것은 침략이 아니라 무지 때문이었다"며, Relicborn이 침략자가 아니라 봉인의 수문장임을 직접 선언한다. |
| `site_bone_orchard` | 5 | 중반 반전이 정리되며 Echo Savant가 원정대에 합류한다. Echo Savant는 각성한 Relicborn 미스틱으로, 격자의 전체 구조와 Heartforge의 작동 원리에 대한 고대의 진실을 기억하고 있다. 그가 원정대에게 "Heartforge는 기억을 에너지로 변환하는 장치이며, 제어하지 않으면 적대감을 무한 증폭한다"는 핵심 진실을 밝히면서, 원정의 목적이 조사에서 순환 종결로 전환된다. Echo Savant는 격자가 무너질수록 Aldric의 잔류 의지가 강해지고 있다고 경고한다 — 진정한 적은 기계만이 아니라 그 안에 남은 사람이다. Dawn Priest, Pack Raider, Grave Hexer 세 사람은 각자의 세력이 모두 Heartforge의 피해자였음을 인정하고, 4세력 공동 목표가 형성된다. 회의가 끝난 뒤 조용한 순간 — Echo Savant가 Grave Hexer에게 묻는다. "그 많은 기억을 어떻게 지고 가느냐." 그녀가 농담으로 답하지만, 그는 웃지 않는다. 1800년 만에 처음으로 누군가에게 진심으로 질문한 순간이며, 둘 사이의 첫 진정한 연결이다. |

### Chapter 4: Glass Forest

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_glass_forest` | 1 | `skirmish` | 유리 숲 도달 | 83 | 45 | 2 | 9 | 5.3 | — |
| `site_glass_forest` | 2 | `skirmish` | 야수족 피해 확인 | 88 | 39 | 4 | 9 | 5.6 | — |
| `site_glass_forest` | 3 | `elite` | 복합 편성 시험 | 95 | 30 | 3 | 22 | 6.1 | — |
| `site_glass_forest` | 4 | `boss` | 도관 수호자 격파 | 100 | 16 | 0 | 56 | 6.8 | — |
| `site_glass_forest` | 5 | `extract` | Relicborn 전사 합류 | 65 | 79 | 12 | 32 | 5.1 | `hero_shardblade` |
| `site_starved_menagerie` | 1 | `skirmish` | 굶주린 우리 진입 | 89 | 42 | 2 | 10 | 5.9 | — |
| `site_starved_menagerie` | 2 | `skirmish` | 지속 피해 압박 | 94 | 36 | 4 | 10 | 6.2 | — |
| `site_starved_menagerie` | 3 | `elite` | 혼성 보스 전조 | 100 | 27 | 3 | 24 | 6.7 | — |
| `site_starved_menagerie` | 4 | `boss` | 사육장 관리자 격파 | 100 | 13 | 0 | 80 | 7.4 | — |
| `site_starved_menagerie` | 5 | `extract` | Relicborn 사수 합류 | 71 | 76 | 12 | 64 | 5.7 | `hero_prism_seeker` |

#### Chapter 4 서사 상세

| site_id | n | narrative_detail |
|---|---:|---|
| `site_glass_forest` | 1 | 원정대가 유리화된 숲에 도달한다. 결정이 된 거목 사이로 무지개 빛 파편이 흩어지고, 발밑에서 유리가 부서지는 소리가 울린다. Echo Savant가 결정화된 수관 위로 올려다본다. 1800년 만에 처음 보는 하늘이다. 그는 아무 말도 하지 않지만, 내레이터가 그의 얼굴을 묘사한다 — "원정대가 그에게서 처음으로 본, 분석이 아닌 표정." Echo Savant가 이 숲이 고대 격자 폭발의 결과물이며 모든 세력의 전투 흔적이 결정 안에 얼어 있다고 설명한다. 이 자리에서 Heartforge 정화의 세 가지 선택지 — 정화(purify), 봉인(seal), 파괴(destroy) — 가 처음 제시되고, 즉시 삼파전이 벌어진다. Dawn Priest는 정화를 주장한다 — 순환을 끊으려면 속죄의 대가를 치러야 한다. Pack Raider는 봉인을 요구한다 — 아무도 가두거나 죽이지 않고 지금 당장 모두가 안전해질 수 있는데 왜 누군가를 희생시키느냐. 둘이 정면으로 충돌한다. Pack Raider가 "네 속죄를 위해 내 동료를 가두겠다는 거냐"고 묻고, Dawn Priest가 "200년 뒤 네 후손이 같은 전쟁을 치르게 놔두겠다는 거냐"고 받아친다. 서로를 미워하는 것이 아니라 아끼기에 더 치열한 충돌이다. Grave Hexer는 정화 쪽이다 — 기억이 보존되는 유일한 길이므로. Echo Savant는 처음에 봉인의 합리성을 인정하지만(Relicborn은 1800년 전에도 휴면을 택했으니까), 격자 풍화 데이터를 분석하며 침묵에 빠진다. |
| `site_glass_forest` | 2 | 유리 결정 내부에 야수족 전사들의 마지막 순간이 얼어붙어 있는 것을 발견한다. Pack Raider가 결정 속 전사들의 씨족 문양을 읽으며 "이들은 200년 전에 여기서 죽었고 아무도 장례를 치르지 못했다"고 말한다. 이때 부식된 결정체를 통해 Aldric의 의지가 간섭을 시작한다. 그는 각 리드에게 다른 말을 속삭인다. Dawn Priest에게는 "네 속죄가 이것을 요구한다. 이것이 진정한 정화다"라고 말하여 죄책감을 부추기고, Pack Raider에게는 "봉인이 네 팩을 지금 당장 지켜준다. 미래는 미래가 알아서 할 일이다"라고 말하여 보호 본능을 자극한다. Aldric은 논쟁을 격화시키고 있다 — 각자가 가장 듣고 싶은 말을 해주어 자기 입장에 더 강하게 매달리게 만든다. Dawn Priest는 자신의 신앙 창시자가 아직 살아있는 의지로 간섭하고 있다는 사실에 전율하면서도, 그의 말이 자신의 확신과 겹치는 것에 흔들린다. |
| `site_glass_forest` | 3 | Echo Savant가 정화의 구체적 비용을 밝힌다. 수문장 한 명이 격자에 영구 속박되어야 하고, 정화의 열쇠로 Grave Hexer의 기원 기억이 필요하다. Echo Savant가 동시에 재봉인의 풍화 시뮬레이션 결과를 공유한다 — 200-500년 안에 격자가 다시 무너져 순환이 재개된다. 그는 봉인 지지에서 정화 지지로 입장을 바꾼다. Grave Hexer가 조용해진다. 지금까지 모든 상황에서 농담이나 비꼬는 말로 분위기를 조절하던 그녀가, 처음으로 아무 말도 하지 않는다. 기억을 지키는 것이 존재 이유인 그녀에게, 자신의 기억을 바치라는 요구는 자기 존재의 근거를 지우라는 것과 같다. 유리 숲 깊은 곳에서 왕국, 야수족, 언데드, Relicborn 출신 적대체가 혼합된 복합 편성과 처음 맞닥뜨린다. Echo Savant가 격자 공명 분석을 통해 이들이 Heartforge의 잔류 에너지로 기억과 적대감이 뒤섞인 환영-실체 혼합물임을 밝힌다. 원정대는 4세력 혼성 편성으로 대응하며, 서로 다른 전술 언어를 하나로 엮는 최초의 시험을 치른다. |
| `site_glass_forest` | 4 | 유리 숲 중심의 거대한 격자 도관을 지키는 '프리즘 수호자'와 전투가 벌어진다. 프리즘 수호자는 Aldric의 간섭을 직접 중계하는 도관 역할을 하며, Aldric의 영향력과의 첫 직접 전투다. 전투 중 도관을 통해 Heartforge의 맥동이 직접 전달되어 원정대원 전원이 자신의 가장 고통스러운 기억과 직면한다. Echo Savant가 왕국이 이 도관에서 격자 파편을 빼낸 것이 Heartforge 누출의 직접적 통로가 되었음을 밝히고, 왕국의 방어 장치가 사실은 도난당한 격자 파편으로 만들어진 것임이 최종 확인된다. |
| `site_glass_forest` | 5 | 프리즘 수호자의 잔해에서 Relicborn 전사 Shardblade가 결정 휴면에서 깨어나 원정대에 합류한다. Shardblade는 봉인 격자의 물리적 수복을 담당하는 Relicborn 직공 계급이며, 격자 파편을 검의 형태로 재구성하는 능력을 가졌다. 원정대는 정화가 유일한 길이라는 데 합의하지만, 그 대가를 아직 수용하지 못한 상태다. Shardblade가 왕국이 빼앗은 격자 파편의 공명 패턴을 추적할 수 있다고 밝히면서, 원정대는 남은 파편을 수복하여 Heartforge를 제어할 구체적 경로를 확보한다. |
| `site_starved_menagerie` | 1 | 유리 숲 깊은 곳의 동물 우리 잔해에 진입한다. Ember Runner가 숲에서 뛰어나온다 — 숨이 차고, 눈이 젖어 있다. "장로들이 피형제를 버렸어요. Grey Fang이 서약을 철회했어요." Pack Raider가 멈춘다. 분노가 아니다. 텅 빈 표정. 아무 말도 하지 않는다. Ember Runner는 씨족이 둘로 갈라졌고, Grey Fang이 반대편의 중심에 섰다고 전한다. Pack Raider에게 피형제의 배반은 씨족의 분열보다 더 깊은 상처다 — 적이 된 것이 아니라, 같은 길을 걸을 수 없게 된 것이다. 과거 야수족이 기르던 생물들이 유리화되거나 Heartforge 잔류 에너지로 변이하여 영역을 지키고 있다. Pack Raider가 변이된 생물들의 고통을 감지하며 "이것들은 우리 씨족이 돌보던 짐승들이었다"고 비통해한다. 원정대는 Heartforge 오염이 인간과 야수족뿐 아니라 자연 자체까지 변질시켰음을 직면한다. |
| `site_starved_menagerie` | 2 | 사육장 내부에서 변이 생물들의 지속적 공격이 이어지며 원정대에 소모전이 강제된다. Grave Hexer가 자신의 가장 오래된 기억이 정화의 열쇠로 필요하다는 것을 깨닫는다. Pack Raider는 야수족 진정 의식으로 일부 생물의 적대감을 잠시 가라앉힌다. 그러나 Heartforge의 맥동이 다시 적대감을 자극하며, 순환을 끊지 않는 한 일시적 구제가 무의미함을 원정대가 절감한다. |
| `site_starved_menagerie` | 3 | 자원을 선언하기 전, Echo Savant가 Grave Hexer에게 말한다: "안은 너무 조용하다." 그녀는 아직 이 말의 의미를 이해하지 못한다. 이것이 그의 모티프가 된다 — ch5에서 그의 마지막 말 "밖이 시끄러울 테니"가 이 쌍을 완성한다. Echo Savant가 영구 수문장으로 자원한다. 다른 원정대원들이 반발하지만, 그는 "내가 남아야 격자가 유지된다"고 말한다. 사육장 심부에서 변이 정점에 달한 혼성 적대체가 출현한다. 왕국의 갑옷 파편, 야수족의 발톱, 언데드의 기억 잔향, Relicborn의 격자 조각이 하나의 몸체에 뒤섞인 이 존재는 Heartforge가 모든 세력의 고통을 뒤섞어 만든 결과물이다. Echo Savant가 "이것이 순환이 끝나지 않으면 우리 모두가 될 미래"라고 경고하며, 최종 전투를 앞둔 절박함이 극대화된다. |
| `site_starved_menagerie` | 4 | 사육장의 최심부를 지배하는 '기아의 관리자'와 최종 전투가 벌어진다. 기아의 관리자는 Aldric의 직접적 의지가 동력원이며, 지금까지 가장 가혹한 전투다. Heartforge 에너지를 직접 끌어당겨 주변의 모든 생명력을 흡수한다. 가장 가혹한 소모전 끝에 관리자가 쓰러지며, 그 내부에서 격자의 핵심 주파수가 담긴 공명석이 발견된다. Prism Seeker가 이 공명석을 감지하고 사육장 벽 너머에서 원정대를 향해 신호를 보낸다. |
| `site_starved_menagerie` | 5 | 공명석의 신호를 추적하여 Relicborn 관측자 Prism Seeker가 원정대에 합류한다. 각 지도자가 자신의 대가를 수용한다. Pack Raider는 온전한 몸으로 씨족에 돌아갈 수 없음을 받아들이고, Grave Hexer는 자신의 기원 기억을 바쳐야 함을 인정하며, Echo Savant는 돌아오지 못할 잔류를 확약한다. Prism Seeker는 격자의 주파수를 조율하는 전문가이며, 격자 복원이 야수족 본능 완화에 실질적으로 도움됨을 증명한다. 원정대는 순환 종결의 가능성을 확신하며 Heartforge를 향한 최종 하강을 결의한다. |

### Chapter 5: Heartforge Descent

| site_id | n | type | beat | T | R | H | C | diff | unlock |
|---|---:|---|---|---:|---:|---:|---:|---:|---|
| `site_heartforge_gate` | 1 | `skirmish` | 심장로 진입 | 94 | 40 | 0 | 11 | 6.6 | — |
| `site_heartforge_gate` | 2 | `skirmish` | 4세력 이해관계 수렴 | 99 | 34 | 2 | 11 | 6.9 | — |
| `site_heartforge_gate` | 3 | `elite` | 결전 전 맹세 | 100 | 25 | 1 | 26 | 7.4 | — |
| `site_heartforge_gate` | 4 | `boss` | 대문 수문장 격파 | 100 | 11 | 0 | 86 | 8.1 | — |
| `site_heartforge_gate` | 5 | `extract` | 최종 정화 코드 확보 | 76 | 74 | 10 | 72 | 6.4 | `hero_mirror_cantor` |
| `site_worldscar_depths` | 1 | `skirmish` | 심부 하강 | 98 | 38 | 0 | 12 | 7.2 | — |
| `site_worldscar_depths` | 2 | `skirmish` | 핵심 진실 수용 | 100 | 32 | 2 | 12 | 7.5 | — |
| `site_worldscar_depths` | 3 | `elite` | 돌이킬 수 없는 선택 | 100 | 23 | 1 | 28 | 8.0 | — |
| `site_worldscar_depths` | 4 | `boss` | 최종 적대체 격파 | 100 | 9 | 0 | 100 | 8.7 | — |
| `site_worldscar_depths` | 5 | `extract` | 종결과 엔드게임 해금 | 80 | 72 | 10 | 92 | 7.0 | `mode_endless_cycle` |

#### Chapter 5 서사 상세

| site_id | n | narrative_detail |
|---|---:|---|
| `site_heartforge_gate` | 1 | 원정대가 Worldscar 입구의 거대한 석조 관문에 도달한다. 관문에는 인간/야수족/언데드/Relicborn 네 세력의 문양이 나란히 새겨져 있으며, 격자의 잔재가 문을 봉인하고 있다. Echo Savant가 이 문양이 적대의 기록이 아니라 고대 공존 협약의 증거임을 밝히며, Heartforge의 맥동이 가슴팍까지 전해지는 이 장소에서 원정대는 최종 하강의 긴장을 체감한다. |
| `site_heartforge_gate` | 2 | 관문 앞에서 각 지도자가 자신이 무엇을 포기하는지를 선언한다. 연설이 아니라 조용한 고백이다. Dawn Priest는 왕국의 죄를 인정하고 격자 복원에 동의하며, Pack Raider는 야수족의 영역이 격자 수복으로만 안정화됨을 받아들인다. Grave Hexer는 기억의 보존이 Heartforge 정화와 양립 가능한지를 Echo Savant에게 확인한다. 네 사람이 각자의 세력을 대표하여 "순환을 끝내겠다"는 공동 선언을 하며, 플레이어는 여정 전체가 이 한 순간을 위한 것이었음을 느낀다. |
| `site_heartforge_gate` | 3 | 관문을 열기 위해 네 세력의 대표가 각자의 유물을 관문에 바치는 서약 의식이 진행된다. Dawn Priest는 영원한 질서의 사제 인장을 내려놓고, Pack Raider는 씨족 토템 조각을 바친다 — 씨족이 그를 온전히 받아들이지 않을 것을 알면서. Grave Hexer는 기억 용기를, Echo Savant는 격자 열쇠를 관문의 네 홈에 맞춘다. 관문이 열리면서 격자 에너지가 폭주하고, 이를 저지하려는 엘리트 격자 수호체와 전투가 벌어진다. 서약은 돌이킬 수 없으며, 원정대는 이제 전진만 가능하다. |
| `site_heartforge_gate` | 4 | 관문 안쪽에서 최종 방벽을 지키는 '영원의 대문장'과 전투가 벌어진다. 영원의 대문장은 Aldric의 기억-무기로, 각 지도자의 희생과 관련된 가장 큰 두려움을 투사한다. Dawn Priest는 정화 재판의 기억을, Pack Raider는 씨족이 학살당한 기억을, Grave Hexer는 기억이 소거당하는 공포를, Echo Savant는 동료 수문장이 무너지는 장면을 각각 직면하고 극복한다. |
| `site_heartforge_gate` | 5 | 대문장의 붕괴와 함께 최종 정화 코드가 담긴 격자 핵이 노출되고, 그 안에서 Relicborn 기록관 Mirror Cantor가 마지막 휴면에서 깨어나 원정대에 합류한다. Mirror Cantor는 Heartforge 정화 의식의 전체 절차를 기억하고 있는 유일한 존재이며, "정화하면 기억은 보존되지만 Heartforge의 에너지 변환은 영구 정지된다"고 설명한다. 원정대는 순환을 끝낼 열쇠를 마침내 손에 넣고 심부로 향한다. |
| `site_worldscar_depths` | 1 | 원정대가 Heartforge가 있는 심부로 하강한다. 벽면에 모든 종족의 기억이 빛의 형태로 투사되어 있으며, 3000년간의 공존과 전쟁과 봉인의 역사가 끊임없이 재생된다. Echo Savant가 이 기억들이 Heartforge가 변환하지 못한 잔여분이라고 설명하며, 원정대원들은 자신들의 세력이 가한 피해와 받은 피해를 동시에 목격한다. |
| `site_worldscar_depths` | 2 | Heartforge의 맥동이 직접 느껴지는 거리에서 핵심 진실이 최종적으로 수용된다. Mirror Cantor가 Heartforge의 전체 작동 원리를 시연하며, 기억이 에너지로 변환될 때 부산물로 적대감이 방출되는 메커니즘을 보여준다. Dawn Priest는 영원한 질서의 교리 자체가 이 적대감 증폭의 산물이었음을 깨닫고, "내 신앙은 기계의 배기가스 위에 세워진 것이었다"고 인정한다. 네 세력 모두가 피해자이자 가해자였다는 진실이 최종 확정된다. |
| `site_worldscar_depths` | 3 | 돌이킬 수 없는 순간이 온다. Grave Hexer가 자신의 가장 오래된 기억을 Heartforge에 투입한다. 기억이 소멸되는 순간, 그녀는 자신의 기원을 잊는다. Mirror Cantor가 정화, 봉인, 정지 세 가지 선택지를 최종 제시하고, 각 선택의 대가를 설명한다. 정화는 기억을 보존하지만 Heartforge의 에너지를 영구 상실하고, 봉인은 다시 풍화될 위험이 있으며, 정지는 기억마저 소실될 수 있다. 원정대가 정화를 선택하면 격자가 최대 출력으로 가동되며, 이를 저지하려는 Heartforge의 최종 방어체가 출현한다. |
| `site_worldscar_depths` | 4 | Heartforge의 심장부에서 Aldric의 의지-실체와 결전이 벌어진다. 괴물이 아니라 사람이다. Aldric은 "나는 혼돈을 통제했다. 너희는 질서를 파괴하고 있다"고 주장하며, Dawn Priest는 자신의 스승의 스승에게 답해야 한다. 전투 중 Aldric의 의지가 원정대원 각자의 적대감마저 증폭시키지만, Dawn Priest의 신앙 잔해에서 나온 의지, Pack Raider의 씨족 유대, Grave Hexer의 기억 정의, Echo Savant의 고대 봉인 지식이 결합하여 Aldric의 의지를 제압한다. 캠페인의 모든 갈등이 이 한 전투에서 수렴한다. |
| `site_worldscar_depths` | 5 | Heartforge가 정화되고 순환이 종결된다. Echo Savant가 심부에 남는다. 그의 마지막 말: "밖이 시끄러울 테니" — ch4에서 Grave Hexer에게 했던 "안은 너무 조용하다"의 완결. 조용한 작별. 나머지가 올라간다. 격자가 완전히 복원되면서 벽면의 기억 투사가 평화로운 장면으로 바뀌고, 3000년 전 네 종족이 공존하던 태초의 풍경이 마지막으로 재생된다. Dawn Priest는 더 이상 사제가 아니다. Pack Raider는 분열된 씨족으로 돌아간다 — Ember Runner의 선택은 아직 알 수 없다. Pack Raider는 묻지 않았다. Grave Hexer는 자신의 것이었는지조차 기억하지 못하는 기억들을 짊어지고 간다. 순환은 끝났지만 세계의 수습은 이제 시작이며, endless cycle이 해금된다. |

## 대사 앵커 표

| node_ref | mandatory_event_id | optional_event_id | note |
|---|---|---|---|
| `chapter_ashen_gate/site_ashen_gate/1` | `story_event_site_intro_ashen_gate` | — | 캠페인 첫 진입 |
| `chapter_ashen_gate/site_wolfpine_trail/5` | `story_event_unlock_rift_stalker` | `story_event_beastkin_alliance` | 첫 specialist 전환 |
| `chapter_sunken_bastion/site_sunken_bastion/5` | `story_event_unlock_bastion_penitent` | — | 요새 배신 증거 |
| `chapter_sunken_bastion/site_tithe_road/5` | `story_event_unlock_pale_executor` | `story_event_faith_crack` | 신앙 균열 |
| `chapter_ruined_crypts/site_ruined_crypts/5` | `story_event_unlock_aegis_sentinel` | `story_event_relicborn_teaser` | Relicborn 첫 접촉 |
| `chapter_ruined_crypts/site_bone_orchard/3` | `story_event_relicborn_awakening` | — | Relicborn 각성 (midpoint) |
| `chapter_ruined_crypts/site_bone_orchard/5` | `story_event_midpoint_reveal` | `story_event_unlock_echo_savant` | midpoint extraction |
| `chapter_glass_forest/site_glass_forest/5` | `story_event_unlock_shardblade` | — | Relicborn 전사 합류 |
| `chapter_glass_forest/site_starved_menagerie/5` | `story_event_unlock_prism_seeker` | — | Relicborn 사수 합류 |
| `chapter_heartforge_descent/site_heartforge_gate/5` | `story_event_unlock_mirror_cantor` | `story_event_final_code` | 최종 specialist |
| `chapter_heartforge_descent/site_worldscar_depths/4` | `story_event_final_boss` | — | 최종 적대체 |
| `chapter_heartforge_descent/site_worldscar_depths/5` | `story_event_campaign_complete` | `story_event_endless_open` | 종결과 endless 해금 |

## 영웅 합류 / 해금 규칙

| hero_id | join_site | join_node | gate_type |
|---|---|---:|---|
| `hero_rift_stalker` | `site_wolfpine_trail` | 5 | extract commit |
| `hero_bastion_penitent` | `site_sunken_bastion` | 5 | extract commit |
| `hero_pale_executor` | `site_tithe_road` | 5 | extract commit |
| `hero_aegis_sentinel` | `site_ruined_crypts` | 5 | extract commit |
| `hero_echo_savant` | `site_bone_orchard` | 5 | extract commit |
| `hero_shardblade` | `site_glass_forest` | 5 | extract commit |
| `hero_prism_seeker` | `site_starved_menagerie` | 5 | extract commit |
| `hero_mirror_cantor` | `site_heartforge_gate` | 5 | extract commit |

## 작성 지침

- 모든 감정값은 `narrative-pacing-formula.md`의 수식과 일치해야 한다.
- 모든 unlock은 `story-gating-and-unlock-rules.md`와 양방향으로 확인한다.
- 한 node에 너무 많은 reveal을 몰아넣지 않는다.
