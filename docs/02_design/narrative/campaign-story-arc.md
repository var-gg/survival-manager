# 캠페인 스토리 아크

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 리드 시점 캐릭터: Dawn Priest (인간 미스틱), Pack Raider (야수족 듀얼리스트), Grave Hexer (언데드 미스틱), Echo Savant (Relicborn 미스틱)
- 소스오브트루스: `docs/02_design/narrative/campaign-story-arc.md`
- 관련문서:
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`
  - `docs/02_design/deck/hero-expansion-roadmap.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`

## 목적

캠페인 전체의 로그라인, chapter 목적, 갈등 해소율, 엔딩과 후속작 hook를 고정한다. 이 문서는 chapter-level story truth의 기준이다.

## 로그라인

Ashen Gate 붕괴 이후 파견된 혼성 원정대가 전선 아래의 Worldscar를 따라 하강하면서, 인간/야수족/언데드의 전쟁이 매장된 Heartforge에 의해 재증폭되고 있음을 발견한다. 중반에 각성한 Relicborn는 침략자가 아니라 오래된 봉인의 수문장으로 드러나고, Heartforge 안에서 80년 전 왕국 학자-수장 Aldric Sternholt의 의지가 되살아나며 원정대를 방해한다. 최종 목표는 지배가 아니라 순환의 종결로 전환되며, 그 대가로 각 리드 캐릭터는 되돌릴 수 없는 것을 잃는다.

## 핵심 갈등과 결말 규칙

| 갈등 유형 | 내용 | 런치 해소율 |
|---|---|---:|
| **main conflict** | Heartforge의 기억 증폭 순환 + Aldric의 잔류 의지 | `100%` — 정화로 완결, Aldric 의지 소멸 |
| **surface faction wars** | 인간-야수족-언데드 전선 | `>= 80%` — 대부분 정리, 정치 재편 1개 남김 |
| **antagonist arc** | Aldric Sternholt(`npc_aldric`) — Heartforge에 흡수된 왕국 학자-수장의 의지 | `100%` — ch5에서 의지체 격파 및 소멸 |
| **setting mystery** | Heartforge 기원, Relicborn 존재 이유 | `~70%` — 핵심 해명, 외부 대륙 신호 미해명 |

금지: "진짜 최종보스는 DLC/후속작"형 엔딩.

## 챕터 구조표

| chapter_id | dramatic_function | gameplay_function | key_reveal | resolved_hook | open_hook |
|---|---|---|---|---|---|
| `chapter_ashen_gate` | Exposition | squad loop/site rhythm onboarding | 위협의 실재 | 첫 단서 확보, 정찰 동맹 | 야수족의 진짜 동기 |
| `chapter_sunken_bastion` | Rising Action I | protect/mark pressure 학습, answer lane 구별 | 인간 진영 culpability, Aldric 일지 발견 | 요새 배신 증거, 적대자에 이름 부여 | 신앙 균열의 폭, Aldric의 정체 |
| `chapter_ruined_crypts` | Midpoint Reversal | summon pressure + truth pivot | Relicborn reveal, 언데드 기억의 진실, Aldric 얼굴 최초 등장 | 언데드 오해 일부 해소 | Heartforge 전체 목적, Aldric 의지의 위협 |
| `chapter_glass_forest` | Crisis | mixed encounter mastery, full comp stress test | 정화/봉인/파괴 삼지선다, Aldric 직접 간섭 시작 | 정화로 수렴, 대가의 윤곽 확정 | 개인 희생의 구체적 형태 |
| `chapter_heartforge_descent` | Climax + Denouement | 신규 시스템 금지, 결산과 payoff 극대화 | Aldric 의지체 대면, 4인의 되돌릴 수 없는 상실 | main conflict closure | 정치 재편, Relicborn 분열, 외부 신호 |

## 챕터별 서사 목적 / 게임플레이 목적 매핑

| chapter_id | 서사적 목적 | 게임플레이 목적 | 신규 도입 | 챕터 종료 보상 |
|---|---|---|---|---|
| `chapter_ashen_gate` | 위협의 실재와 원정의 명분 제시. 인간/야수의 첫 오해를 완충 | 기본 squad synergy, site rhythm, reward cadence onboarding | 첫 specialist 전환 사례 | `hero_rift_stalker` |
| `chapter_sunken_bastion` | 인간 진영 culpability와 신앙/통제의 폭력을 드러냄. Aldric 일지(`relic_aldric_journal`) 발견으로 적대자에 이름 부여 | protect/mark pressure 학습, answer lane 구별 강화 | specialist 2종 추가 | `hero_bastion_penitent`, `hero_pale_executor` |
| `chapter_ruined_crypts` | 언데드를 단순 악으로 보던 시각을 뒤집고 Relicborn 진실을 공개. 기억 투사 속 Aldric 얼굴 최초 등장 | summon pressure, midpoint reversal, race 4 teaser | Relicborn 방어/증언 축 도입 | `hero_aegis_sentinel`, `hero_echo_savant` |
| `chapter_glass_forest` | 정화/봉인/파괴 삼지선다 논쟁과 Aldric의 직접 간섭. 올바른 선택의 대가를 직면 | mixed encounter mastery, full comp stress test | Relicborn 전열돌파/추적 축 완성 | `hero_shardblade`, `hero_prism_seeker` |
| `chapter_heartforge_descent` | Aldric 의지체와의 최종 대면. 4인의 되돌릴 수 없는 희생으로 main conflict를 닫음 | 신규 시스템 금지, 결산과 payoff 극대화 | 최종 specialist와 endless gate | `hero_mirror_cantor`, `mode_endless_cycle` |

## 챕터별 시놉시스

### Chapter 1: Ashen Gate — 재의 문

**감정 아크**: 의무감 → 불안 → 첫 번째 의심

Dawn Priest는 Steward Council의 명을 받아 Ashen Gate 붕괴 지점으로 향한다. 원정대는 정규군 잔여 병력과 지원자로 편성되었으나, 사기는 이미 바닥이다. 무너진 국경문에 도착했을 때, 철문 파편 사이에서 아직 연기가 오르고 까마귀가 잔해 위를 점유하고 있다. Dawn Priest는 이것이 외부의 침공이라고 믿으며 영원한 질서의 기도를 올리지만, 폐허에 남은 흔적은 외부 공격의 양상과 맞지 않는다. 무언가가 안에서부터 터져 나온 것처럼 보인다.

Wolfpine Trail로 진입하면서 야수족 영역 표식인 Pack Totem과 처음 조우한다. 소나무에 새겨진 갈퀴 문양을 Dawn Priest는 침공의 증거로 읽지만, 원정대 정찰 중 포획된 야수족 전사는 이 표식이 방어 경계임을 주장한다. 안개 속에서 벌어지는 첫 교전은 양쪽 모두에게 혼란이다. Pack Raider는 이 시점에서 적대적 포로이거나 강제 안내자로 등장하며, 원정대와 마찰을 일으킨다. 그의 직설적이고 감각적인 언어는 Dawn Priest의 격식체와 충돌하고, 둘 사이의 불신이 챕터 전반의 긴장을 만든다.

챕터의 전환점은 Wolfpine Trail 보스전 직후다. Pack Raider가 영역을 지키려 했을 뿐이라는 사실이 물리적 증거(Heartforge 파편에 반응하는 Pack Totem의 진동)로 뒷받침된다. Dawn Priest는 공식 보고에 이 사실을 기록할지 묻는 판단에 직면한다. 첫 specialist 전환인 `hero_rift_stalker`의 합류는 이 동맹의 물리적 결과다. 챕터를 빠져나올 때, 원정대는 위협이 실재함을 확인했지만 그 원인이 처음 상정한 것과 다를 수 있다는 불안을 안고 간다.

**마감 시퀀스**: 원정대가 Ashen Gate를 등지고 남쪽 하강로로 들어선다. Dawn Priest가 뒤를 돌아보면 검게 그을린 들판 위로 재가 바람에 실려 날린다. Pack Raider는 뒤를 돌아보지 않는다. 그에게 뒤는 잃어버린 사냥터일 뿐이다. 카메라가 당기면 Wolfpine Trail의 Pack Totem이 희미하게 진동하고 있고, 그 진동은 지하 깊은 곳을 향해 맥동을 전달한다.

---

### Chapter 2: Sunken Bastion — 가라앉은 보루

**감정 아크**: 신뢰 → 배신감 → 신앙의 균열

원정대가 지반 침하로 반쯤 가라앉은 왕국 요새 지대에 도착한다. Dawn Priest에게 이곳은 자신의 세력의 본거지이자 안전 지대가 되어야 하지만, 대리석 기둥이 비스듬히 기울고 녹슨 무기가 지하수에 떠다니는 광경은 왕국의 질서가 이미 무너졌음을 물리적으로 보여준다. 요새 내부 기록을 탈취하는 과정에서, 원정대는 80년 전 왕국 학자들이 봉인 격자 파편을 발견하고 방어 장치로 전용했다는 증거를 발견한다. Dawn Priest는 이 기록의 진위를 의심하지만, Pack Raider는 왕국의 위선을 조소한다.

Tithe Road는 챕터의 핵심 감정 무대다. 십일조 행렬이 다니던 포장 도로의 양편에 처형대와 고해소가 번갈아 서 있고, 깨진 성물함과 피 묻은 집행 기록이 바닥에 흩어져 있다. Dawn Priest는 이곳에서 자신이 봉사해온 Eternal Order의 정화 재판이 신앙의 이름으로 자행한 폭력이었음을 직시한다. 심문관 보스와의 전투는 단순한 전투가 아니라, Dawn Priest 자신의 과거 행위를 거울처럼 비추는 장면이다. 심문관은 Dawn Priest가 다른 시간, 다른 자리에서 되었을 수 있는 존재다.

전환점은 심문관 격파 후 발견되는 요새 심부의 격자 연구 기록이다. 왕국이 격자 파편을 체계적으로 수거하여 군사 방벽에 이식했고, 이것이 Heartforge 봉인의 풍화를 가속했다는 인과가 명확해진다. 기록 더미 속에서 Dawn Priest는 "A. Sternholt"라고 서명된 연구 일지(`relic_aldric_journal`)를 발견한다. Aldric Sternholt — 80년 전 왕국 최초의 학자-수장이자, 격자 파편의 군사 전용을 주도하고 영원한 질서의 교리 틀을 설계한 인물이다. Dawn Priest는 교회 창설 문서에서 이 이름을 본 적이 있다. 적대자가 처음으로 이름을 얻는 순간이다. 일지에는 격자 파편의 속성 분석, 봉인 해체 절차, 그리고 Heartforge에 대한 집착이 점점 강해지는 필체의 변화가 담겨 있다.

Dawn Priest의 신앙이 처음으로 균열을 보이는 순간이며, `hero_bastion_penitent`는 이 진실을 목격하고 참회를 선택한 왕국 내부자로, `hero_pale_executor`는 진실을 알면서도 질서를 택하다 환멸한 집행자로 합류한다. Pack Raider는 이 폭로에 분노하지만, 동시에 Dawn Priest가 자기 세력의 죄를 직시하려는 태도에 처음으로 존중의 기미를 보인다.

**마감 시퀀스**: 요새 최하층에서 물이 천천히 차오른다. Dawn Priest가 격자 파편이 박힌 방벽 앞에 서 있다. 파편은 신성한 빛이 아니라 도난당한 봉인의 잔해였다. 그가 기도의 손을 내리고, 처음으로 침묵 속에 서 있다. 물이 그의 발목을 적신다. 카메라가 올라가면 요새 전체가 서서히 침하하고 있고, 그것은 왕국의 권위가 물리적으로 가라앉는 것의 은유다.

---

### Chapter 3: Ruined Crypts — 무너진 묘역

**감정 아크**: 혐오 → 이해 → 인식의 전복 (MIDPOINT REVERSAL)

원정대가 고대 묘역으로 하강한다. 석관이 층층이 쌓여 있고 인광 이끼가 통로를 희미하게 밝히는 이 공간에서, 생자에게는 본능적인 거부감이 먼저 온다. Dawn Priest는 언데드를 질서의 이탈자로 보고, Pack Raider는 죽은 것이 움직이는 것에 감각적 혐오를 느낀다. 그러나 묘역 깊숙이 들어갈수록, 기억 용기(Memory Vessel)가 깨지면서 투사되는 기억들이 원정대의 편견을 흔든다. 투사된 기억 속에서, 언데드는 침략자가 아니라 과거 봉인 실패의 직접적 후손이며, 기억을 지키기 위해 존재를 연장하고 있었다. Grave Hexer가 이 시점에서 원정대와 접촉한다. 그녀는 Memory Conclave의 기억 관리자이며, 수백 년 전 사건을 어제 일처럼 말하는 시간 감각의 괴리가 생자들을 불안하게 만든다.

Bone Orchard에서 캠페인 전체의 최대 반전이 일어난다. 유골이 뿌리처럼 자라난 석화 과수원의 중심부에서, 봉인 격자 조각이 빛나기 시작하고 Relicborn이 각성한다. 모든 세력이 이 존재를 침략자로 예상했지만, 고대 기억 투사가 펼쳐지면서 진실이 드러난다. Heartforge가 가동되기 전, 네 종족이 적대감 없이 공존했던 시대의 기억이다. 그리고 기억 투사 속에서, 한 남자의 살아있는 얼굴이 나타난다 — Aldric Sternholt. 그는 땅에서 격자 파편을 물리적으로 뜯어내고 있다. ch2에서 서명으로만 존재했던 이름이 처음으로 얼굴을 갖는 순간이다. Echo Savant가 그를 알아본다. "이 자의 의지가 아직 안에 있다." 격자를 처음 해체한 인간의 의지가 Heartforge 깊은 곳에 흡수되어 남아 있다는 암시가 반전에 불길한 차원을 더한다.

Relicborn은 그 공존을 지키기 위해 Heartforge를 봉인하고 자발적 휴면에 들어갔던 수문장이었다. 지금 각성한 것은 격자가 임계치 이하로 풍화되었기 때문이며, 그들은 공격이 아니라 긴급 복구를 시도하고 있었다.

이 반전은 네 시점 캐릭터 모두에게 충격을 준다. Dawn Priest는 자신의 세력이 오용한 격자가 이 존재들의 몸이나 다름없었음을 깨닫는다. Pack Raider는 야수족의 본능 증폭이 자연적 현상이 아니라 Heartforge의 부작용이었음을 알게 된다. Grave Hexer는 자신의 존재 자체가 과거 봉인 실패의 산물임을 직면한다. Echo Savant는 이 노드에서 합류하며, 긴 휴면에서 깨어난 수문장으로서 격자 복원이라는 사명과 세계가 1800년 동안 변해버렸다는 현실 사이에서 당혹한다. 그의 절제된 언어와 공명/파장의 비유는 다른 세 인물의 감정적 언어와 대조를 이루며, 원정대의 대화 구조 자체가 변한다.

전환점은 Echo Savant가 격자 네트워크의 현재 상태를 읽어내는 장면이다. 격자는 전체의 40% 이하로 기능이 떨어져 있으며, Heartforge의 출력이 억제 없이 모든 종족의 적대감을 증폭하고 있다. 문제는 파괴가 아니라 복원이며, 적은 외부에 있는 것이 아니라 지하에 묻혀 맥동하고 있다. 원정의 목표가 전선 안정화에서 순환의 종결로 전환되는 순간이다.

**마감 시퀀스**: Bone Orchard의 중심에서 고대 기억 투사가 서서히 꺼진다. 네 종족이 함께 서 있던 기억의 잔상이 인광처럼 흩어진다. Echo Savant가 처음으로 눈을 뜬 뒤, 석화된 과수원을 둘러본다. 1800년 전에 심었던 것이 뼈가 되어 자라고 있다. 그가 말한다. 짧고 건조하게. 격자가 이 정도면, 남은 시간이 많지 않다고. 원정대 전원이 침묵 속에서 그 말의 무게를 받는다.

---

### Chapter 4: Glass Forest — 유리의 숲

**감정 아크**: 분열 → 대가의 무게 → 불완전한 결의

유리화된 숲에 도착한 원정대의 첫 인상은 아름다움이다. 결정이 된 거목들이 빛을 굴절시켜 무지개 파편이 사방에 흩어진다. 그러나 발 아래에서 유리가 부서지는 소리와 함께, 결정 안에 얼어붙은 과거 전투의 흔적이 보인다. 모든 세력의 전사, 야수, 격자 파편이 유리 속에 박혀 있다. 이 장소는 Heartforge 출력이 한 번 폭주했을 때 모든 것을 유리화시킨 고대 재앙의 현장이며, 네 세력 모두가 동시에 피해자였던 중립 지대다.

ch3의 Relicborn 진실 공개 이후, 원정대 앞에 세 가지 선택지가 놓인다. Heartforge를 어떻게 처리할 것인가.

| 선택지 | 대가 | 지지 세력 | 반대 세력 |
|---|---|---|---|
| **정화(Purification)** | 기억 보존되나, 수호자 1명이 격자에 영구 속박 | 언데드(기억 안전), 왕국(속죄) | 야수족(동맹 속박), 일부 Relicborn(동족 희생) |
| **봉인(Sealing)** | 안전하나 순환 재발 가능 | 야수족(즉각적 안전), Relicborn(휴면 경험) | 언데드(같은 실수 반복), 왕국(근본 해결 아님) |
| **파괴(Destruction)** | 적대감 영구 소멸, 그러나 모든 기억도 파괴 | 일부 왕국(새 출발) | 언데드(존재 삭제), Relicborn(격자 파괴) |

이 논쟁이 챕터 전체를 지배한다. 네 시점 캐릭터가 처음부터 서로 다른 쪽을 지지하면서 진짜 갈등이 벌어진다.

Dawn Priest는 정화를 옹호한다. 순환을 끊으려면 속죄가 필요하고, 대가가 크더라도 영원히 반복되는 것보다 낫다는 입장이다. 그러나 누군가를 격자에 영구히 가두는 것이 자신이 Sunken Bastion에서 목격한 왕국의 폭력과 구조적으로 다르지 않음을 괴로워한다. Pack Raider는 봉인을 주장한다. 즉각적으로 모든 세력이 안전해지고, 아무도 죽거나 갇히지 않는다. 그에게 동료를 영구 속박하는 것은 추상적 정의를 위해 친구를 버리는 짓이다. Dawn Priest와 Pack Raider가 정면으로 부딪힌다 — Pack Raider는 "네 속죄를 위해 내 동료를 가두겠다는 거냐"고 묻고, Dawn Priest는 "200년 뒤 네 후손이 같은 전쟁을 치르게 놔두겠다는 거냐"고 받아친다. 서로를 미워하는 것이 아니라 서로를 아끼기 때문에 더 치열한 충돌이다.

Grave Hexer는 처음에 정화를 지지한다. 기억이 보존되는 유일한 선택이기 때문이다. 그러나 Echo Savant가 정화의 구체적 비용을 밝히는 순간 — Grave Hexer 자신의 기원 기억이 열쇠로 필요하다는 사실이 드러나면서 — 그녀의 확신이 흔들린다. 남의 기억을 지키기 위해 자신의 기억을 바치는 것. 그녀는 침묵에 빠진다. Echo Savant는 초기에 봉인을 지지한다. Relicborn의 선례 — 1800년 전의 자발적 휴면 — 를 알고 있기에, 봉인이 시간을 버는 합리적 선택이라고 본다. 그러나 격자의 현재 풍화 속도를 분석한 뒤, 재봉인이 200-500년 안에 다시 무너져 같은 순환이 반복될 것임을 깨닫고, 정화 쪽으로 입장을 바꾼다. 그리고 정작 자신이 속박 후보임을 아직 말하지 않는다.

이 와중에 Aldric Sternholt의 의지가 처음으로 직접 간섭하기 시작한다. 격자 풍화가 깊어지면서 Heartforge 안에 흡수된 그의 인격이 강해졌다. 유리 숲의 부패한 결정 생물체를 통해 그의 목소리가 흘러나온다. Aldric의 간섭은 논쟁을 악화시킨다 — 그는 각 리드에게 그들이 가장 듣고 싶은 말을 속삭이기 때문이다. Dawn Priest에게는 "네 속죄가 이것을 요구한다. 이것이 진정한 정화다"라고 말하여 그녀의 죄책감을 부추기고, Pack Raider에게는 "봉인이 네 팩을 지금 당장 지켜준다. 미래는 미래가 알아서 할 일이다"라고 말하여 그의 보호 본능을 자극한다. 각자가 자기 입장에 더 강하게 매달리게 만들어, 합의를 불가능하게 하려는 것이다. Aldric은 Dawn Priest가 섬기는 교리 체계를 설계한 장본인이다. 그녀의 스승의 스승. 그의 말은 유혹이자 위협이며, Dawn Priest는 자신의 신앙적 기반 전체가 이 남자의 설계물이었다는 사실에 동요한다.

동시에, Pack Raider에게 야수족 영역에서 전령이 도착한다. 그의 씨족 장로들이 4세력 동맹을 배신으로 규정했다. 씨족의 일부가 영구 분리를 선언했다는 소식이다. Pack Raider는 올바른 선택을 한 대가로 자신의 씨족을 잃어가고 있다. 되돌릴 수 없는 균열이다.

전환점은 원정대가 정화를 선택하기로 수렴하는 장면이다. 봉인은 순환의 반복이고, 파괴는 기억의 학살이다. 정화만이 Heartforge를 멈추면서도 기억을 지킬 수 있다. 그러나 그 대가 — 누군가의 영구적 희생 — 는 여전히 미결이며, 이 무게가 챕터의 끝까지 원정대를 짓누른다. `hero_shardblade`와 `hero_prism_seeker`가 합류하면서 Relicborn의 전열돌파/추적 축이 완성되고, 원정대는 처음으로 네 세력 완전 편성으로 운용된다. 그러나 이전 챕터의 연대와 달리, 이번 결속은 희망이 아니라 각오에 기반한다.

**마감 시퀀스**: 유리 숲의 개활지에서 원정대가 Worldscar를 향해 하강로를 바라본다. 결정 나무 사이로 석양빛이 굴절되어 네 색깔의 빛이 원정대 위로 떨어진다. 강철 회색, 호박색, 인광 녹색, 프리즘 보라. 아무도 말하지 않는다. Pack Raider가 먼저 걷기 시작한다. 그의 씨족 토템이 허리에서 흔들린다 — 이제 반쪽짜리 씨족의 표식이다. Dawn Priest가 따르며 기도의 손을 올리려다 멈춘다. 그 기도를 설계한 남자의 속삭임이 아직 귓가에 남아 있기 때문이다. 유리가 부서지는 소리만이 그들의 행군을 알린다.

---

### Chapter 5: Heartforge Descent — 심장로 하강

**감정 아크**: 결의 → 대면 → 상실을 안은 종결

Worldscar 입구의 거대한 석조 관문에는 네 세력의 문양이 각각 새겨져 있다. 격자의 잔재가 문을 봉인하고 있으며, Heartforge의 맥동이 가슴팍까지 울린다. 이 챕터에서는 새로운 시스템이 도입되지 않는다. 원정대가 지금까지 배운 모든 것을 사용해 하강하는, 결산과 payoff의 공간이다.

하강이 깊어질수록 Aldric Sternholt의 의지가 강해진다. 80년 전 왕국 최초의 학자-수장이었던 그는 40년 전 Heartforge 심부에서 죽었고, 그의 기억과 의지는 Heartforge에 흡수되었다. 격자 풍화가 임계를 넘으면서 그의 인격이 Heartforge 안에서 완전히 되살아났다. 그는 부패한 격자 생물체를 통해 말하고, Dawn Priest의 정신에 직접 닿으며, 심부의 기억 투사를 왜곡하여 원정대의 결속을 흔든다.

Worldscar 심부에서 Aldric의 의지체(`npc_aldric`)와의 최종 전투가 벌어진다. 그는 괴물이 아니다. 한 남자의 형상이다 — 자신이 옳았다고 믿는 남자. "내가 Heartforge를 통제했다. 내가 혼돈에 질서를 가져왔다. 너희는 내가 세운 모든 것을 파괴하고 있다." 그의 말에는 광기가 아니라 확신이 있고, 그 확신이 Dawn Priest에게 가장 깊은 상처를 준다. Aldric은 Dawn Priest가 섬기는 교리 체계를 설계한 장본인이기 때문이다. 그녀의 스승의 스승. 그와 싸운다는 것은 자신을 만든 기반 전체를 부수는 것이다. 전투는 네 세력의 능력을 모두 요구하도록 설계되며, 어느 한 세력만으로는 돌파가 불가능하다.

Aldric의 의지를 꺾은 뒤, 정화 시퀀스가 시작된다. 이 과정에서 네 리드 캐릭터 각각이 되돌릴 수 없는 것을 잃는다.

**Dawn Priest**: 자발적으로 사제 휘장을 벗어 놓는다. 더 이상 사제가 아니다. Aldric이 설계한 교리에 기반한 신분을 내려놓는 것이 정화의 첫 번째 조건이다. 그녀는 자신을 정의하던 정체성 없이 의미를 찾아야 한다.

**Pack Raider**: 씨족 장로들이 그의 동맹을 배신으로 선언한 것은 ch4에서 이미 시작되었다. 정화가 완료된 후에도 분리된 씨족은 돌아오지 않는다. 그가 옳은 선택을 한 대가는 씨족의 영구적 균열이다. 되돌릴 수 없다.

**Grave Hexer**: 정화의 열쇠로 자신의 가장 오래된 기억을 바쳐야 한다. 이 기억은 그녀의 기원 — 그녀가 왜 존재하게 되었는지, 그녀가 누구였는지의 근원이다. 정화가 끝나면 그녀는 자신이 이전에 무엇이었는지 영원히 잊게 된다.

**Echo Savant**: 격자의 영구 수호자로 자원한다. 격자 안에 영원히 갇히는 것이다. 의무가 아니라 선택이다 — 나머지가 자유롭게 걸어 나갈 수 있도록. 그의 마지막 말은 짧고 건조하다. 1800년 전에도 봉인을 선택했고, 이번에는 눈을 뜨고 선택한다고.

Heartforge는 파괴되지 않는다. 파괴하면 모든 기억이 소실되기 때문이다. 정화를 통해 Aldric의 잔류 의지가 소멸하고, 적대감 증폭 순환이 정지된다. Echo Savant가 격자의 일부가 되면서 봉인이 안정화된다.

엔딩은 승리의 환호가 아니라 지친 안도와 상실의 고요다. 네 세력이 각자의 방식으로 세계를 수습하는 모습이 교차 편집된다. 왕국은 격자를 돌려주고 Steward Council이 재편을 시작하지만, Dawn Priest는 더 이상 사제로서가 아니라 한 개인으로서 그 자리에 서 있다. 야수족은 사냥터로 돌아가되, Pack Raider의 씨족은 이전보다 작다. 그는 반쪽짜리 씨족을 이끌고 본능의 증폭이 사라진 세계에서 처음으로 평온한 사냥을 한다. 언데드는 기억이 더 이상 왜곡되지 않는 묘역으로 돌아가지만, Grave Hexer는 자신의 가장 오래된 기억이 비어 있는 채로 돌아간다. Relicborn은 격자 수복을 계속하지만, Echo Savant는 격자 안에서 영원히 깨어 있다. 내부에서 노선 분열의 조짐이 보인다. 정치 재편, Relicborn 분열, 외부 대륙 신호가 후속작 hook으로 남지만, main conflict는 완결된다.

**마감 시퀀스**: Heartforge가 정지한 뒤, 심부의 벽면에서 기억 투사가 마지막으로 한 번 빛난다. 네 종족이 공존했던 태초기의 기억이 아니라, 방금 이 자리에서 네 세력이 함께 싸운 기억이다. 새로운 기억이 가장 오래된 기억 위에 겹쳐진다. Aldric의 얼굴이 기억 투사 속에서 희미해진다 — 분노도 원한도 아닌, 그저 사라지는 한 남자의 표정이다. Dawn Priest가 위를 올려다본다. Worldscar의 좁은 틈 사이로 하늘이 보인다. 재가 아니라 빛이 내려온다. 그녀의 손에 사제 휘장은 없다. 원정대가 상승을 시작한다. 길고 느린 상승이다. 한 명이 빠져 있다. 카메라가 마지막으로 Heartforge를 비추면, 심장은 뛰지 않는다. 격자가 조용히 빛나고, 그 안에서 Echo Savant의 프리즘 보라빛이 일정한 리듬으로 맥동한다. 고요하다. 그리고 아주 먼 곳에서, 격자가 감지하지 못하는 방향에서, 희미한 신호 하나가 깜빡인다.

## 엔딩 / 에필로그 / endless

- **ending beat**: Heartforge 정화/봉인 후 4세력이 각자의 방식으로 세계를 수습하는 장면
- **credits 직전 card**: 원정대의 귀환과 남은 과제 암시
- **endless framing**: main conflict 종료 후 남은 파편/왜곡/기억 수습 임무. `mode_endless_cycle`로 3-site heat loop 운영

### 엔드게임 전환 구조

| 항목 | 설계 |
|---|---|
| 모드 ID | `mode_endless_cycle` |
| 해금 시점 | `site_worldscar_depths` extract 직후 |
| 구조 | 3-site cycle, site당 5노드, 총 15노드 1사이클 |
| narrative framing | main conflict 종료 후 남은 파편/왜곡/기억 수습 임무 |
| rule set | 기존 site kit 재사용 + heat mutator + mixed encounter families |
| canonical status | 에필로그적 containment loop. main ending을 다시 열지 않음 |
| reward focus | 후반 augment, recruit conversion residue, codex completion, cosmetics |
| 금지 | main conflict 재개방, 진엔딩 분리 판매 |

## 후속작 / DLC hook 정책

| hook_id | category | status_at_launch_end | followup_media |
|---|---|---|---|
| `hook_surface_realignment` | secondary conflict | open — 정치 재편 미완 | DLC or sequel |
| `hook_relicborn_schism` | secondary conflict | open — Relicborn 내부 분열 | DLC or sequel |
| `hook_far_signal` | external hook | open — 외부 대륙/원거리 신호 | sequel |

## 작성 지침

- node 단위 비트는 여기서 쓰지 말고 `chapter-beat-sheet.md`에 둔다.
- 세계관 원문 설명은 여기서 반복하지 말고 `world-building-bible.md`를 참조한다.
- resolved/open hook 표는 narrative release scope 판단의 source of truth다.
