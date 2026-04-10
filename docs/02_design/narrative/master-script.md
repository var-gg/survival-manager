# 마스터 스크립트

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/master-script.md`
- 관련문서:
  - `docs/02_design/narrative/dialogue-event-schema.md`
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/deck/character-lore-registry.md`

## 목적

캠페인 전체 대사의 원문 SoT. 작가가 이 파일 하나만 열어 전체 대사를 읽고 수정할 수 있다.
dialogue-event-schema.md는 트리거/스키마를, 이 문서는 실제 대사 텍스트를 소유한다.

## 연출 유형

| 유형 | 용도 | 라인 수 |
|---|---|---|
| `dialogue-scene` | 핵심 스토리 장면. Narrator + 복수 화자. | 8~14 |
| `dialogue-overlay` | 전투 전후 bark, 짧은 반응. | 2~4 |
| `story-card` | 챕터/사이트 전환 카드 (대사 없음, 여기선 미사용) | — |

## 화자 어조 요약

| 화자 | 어체 | 핵심 특징 |
|---|---|---|
| Dawn Priest | 합니다/습니다 | 젊은 여성 사제. ch1-2 교리적 확신, ch3+ 문장이 끊기고 질문이 나옴, ch5 조용한 결의 |
| Pack Raider | 해/다 | 직설, 감각적, 빈정거림. 냄새/소리/질감으로 사고. 같은 비유 반복 금지 |
| Grave Hexer | 했지/이었네 | 건조한 유머가 정체성. 수백 년을 최근 취급. 절대 시적이지 않음 |
| Echo Savant | 한다/이다 | 최소 단어. 신체 감각. 1800년 잠에서 깸. 혼란스러움, 권위적이지 않음 |
| Aldric Sternholt | 했소/이오 | 고풍 격식. 차분하고 합리적. 자기가 옳다고 확신. 악당이 아니라 설계자 |
| Narrator | — | 인용 부호 없음. 현재형. 감각 묘사 1~2문장. 행동과 반응 묘사 |

---

## Chapter 1: Ashen Gate — 재의 문

---

### `dialogue_scene_ashen_gate_intro` — 재의 문 진입

> **컨텍스트**: 원정대가 무너진 국경문을 넘어 Ashen Gate에 처음 발을 딛는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 재가 바람에 실려 온다. 국경문의 철 파편이 들판에 박혀 있고, 그 위로 까마귀 한 마리가 움직이지 않는다. |
| 1 | Dawn Priest | resolute | "집정관 회의의 명에 따라, 이 문 너머를 조사합니다. 영원한 질서의 이름으로." |
| 2 | Pack Raider | skeptical | "너희가 세운 문이잖아. 너희 손으로 닫았고, 너희 손으로 부쉈어." |
| 3 | Dawn Priest | grim | "안에서부터 부서진 흔적입니다. 외부 침공의 양상이 아닙니다." |
| 4 | Narrator | — | Dawn Priest가 철 파편에 손을 댄다. 손끝에 재가 묻고, 그 아래 금속이 아직 따뜻하다. |
| 5 | Pack Raider | bitter | "재 냄새가 사흘째 바람을 타고 왔다. 코가 먹먹해질 정도로." |
| 6 | Dawn Priest | resolute | "까마귀가 낮게 앉아 있습니다. 먹을 것이 많다는 뜻입니다." |
| 7 | Narrator | — | Pack Raider가 코를 찡그린다. 그의 눈이 들판을 훑는다 — 시체가 아니라 냄새를 읽는 것이다. |
| 8 | Pack Raider | defiant | "시체 썩는 냄새가 아니야. 돌이 탄 냄새다. 땅 밑에서 뭔가가 올라왔어." |
| 9 | Dawn Priest | grim | "기록하겠습니다. 문은 밖에서가 아니라 안에서 무너졌다고." |
| 10 | Narrator | — | 바람이 방향을 바꾼다. 재가 원정대의 등 뒤로 흘러간다. 돌아갈 길이 회색으로 묻힌다. |
| 11 | Pack Raider | sardonic | "기록은 잘 하시네. 살아서 읽을 수 있으면 좋겠다." |

---

### `dialogue_scene_priest_ashen_gate` — Dawn Priest 독백: 재의 문 앞에서

> **컨텍스트**: Dawn Priest가 무너진 국경문 앞에서 홀로 기도를 올린다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Dawn Priest가 무릎을 꿇는다. 재가 내려앉아 기도하는 손등을 덮는다. |
| 1 | Dawn Priest | solemn | "영원한 질서의 이름으로, 이 문 너머의 오염을 정화하겠습니다." |
| 2 | Narrator | — | 그녀의 손이 멈춘다. 문 안쪽 벽에 찍힌 자국 — 밖에서 들이친 흔적이 아니다. 안에서 밀어낸 것이다. |
| 3 | Dawn Priest | grim | "이 문은 우리가 세웠습니다. 우리가 지켰습니다. 그런데... 안에서부터..." |
| 4 | Narrator | — | 기도를 마치지 못한다. 그녀가 일어선다. 무릎에 재가 묻어 있다. |
| 5 | Dawn Priest | resolute | "눈을 감지 않겠습니다. 그것이 사제에게 허락된 유일한 용기입니다." |

---

### `dialogue_overlay_foreshadow_lattice` — 탑 내벽의 격자 문양

> **컨텍스트**: 엘리트 전투 후 탑 내벽에서 인간/야수족 어느 쪽 것도 아닌 격자 문양을 발견한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 탑 내벽에 깊이 새겨진 문양이 있다. 인간의 성물 도안도, 야수족의 갈퀴 자국도 아니다. |
| 1 | Dawn Priest | grim | "이 문양은... 어디서도 본 적이 없습니다. 성전 기록에도 없는 양식이에요." |
| 2 | Pack Raider | skeptical | "우리 것도 아니야. 냄새가 다르거든. 돌보다 오래된 냄새." |

---

### `dialogue_overlay_boss_bark_ashen_gate` — 재의 문지기 조우

> **컨텍스트**: 자동 방어 골렘 '재의 문지기'가 기동한다. 가슴판에서 성유물과 같은 빛이 난다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | shock | "저 빛은... 성유물의 빛입니다. 왜 무기에 깃들어 있습니까?" |
| 1 | Pack Raider | defiant | "빛나든 말든. 부수면 꺼져." |
| 2 | Narrator | — | 골렘의 가슴판이 갈라지고, 안쪽에서 낯익은 빛이 쏟아진다. |

---

### `dialogue_overlay_boss_defeat_ashen_gate` — 재의 문지기 격파

> **컨텍스트**: 골렘이 쓰러지고, 내부에서 첫 번째 Heartforge 파편이 드러난다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 골렘의 몸체가 갈라진다. 가슴 안쪽에 주먹 크기의 파편이 박혀 있다. 성물함 안에 있어야 할 빛이다. |
| 1 | Dawn Priest | shock | "이것이... 골렘의 심장에..." |
| 2 | Pack Raider | sardonic | "네 신앙의 빛이 돌덩어리 주먹 안에 들어 있었다. 축복이라, 참 쓸모 있는 말이야." |

---

### `dialogue_scene_wolfpine_trail_intro` — 늑대소나무 길 진입

> **컨텍스트**: 원정대가 소나무 숲길에 진입한다. 나무마다 갈퀴 자국. 안개가 낮게 깔린다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 소나무 껍질에 갈퀴 자국이 깊게 파여 있다. 다섯 개. 손톱이 아니라 발톱이다. |
| 1 | Dawn Priest | grim | "야수족의 영역 표식입니다. 경계를 늦추지 마십시오." |
| 2 | Pack Raider | solemn | "경고가 아니야. 바람에게 올리는 기도다." |
| 3 | Narrator | — | 안개 사이로 노란 눈이 반짝인다. 하나, 둘, 셋 — 눈을 깜빡이면 사라진다. |
| 4 | Dawn Priest | tense | "움직이지 마십시오. 포위 징후가 보입니다." |
| 5 | Pack Raider | bitter | "포위가 아니야. 구경하는 거야. 너희가 얼마나 시끄러운지를." |
| 6 | Narrator | — | Pack Raider가 코를 들어올린다. 솔잎 아래로 젖은 흙과 털 냄새가 겹친다. |
| 7 | Pack Raider | solemn | "새끼들이 가까이 있어. 여기서부턴 네 식대로 하면 안 돼." |
| 8 | Dawn Priest | resolute | "당신의 영역이라는 것은 인정합니다. 하지만 우리의 명분은 변하지 않습니다." |
| 9 | Pack Raider | sardonic | "명분. 좋은 냄새가 나는 단어야. 숲에선 냄새가 안 나는 것만 위험하거든." |

---

### `dialogue_scene_raider_first_contact` — Pack Raider 첫 접촉

> **컨텍스트**: Pack Raider가 원정대와 처음 합류한다. 적대적 포로이거나 강제 안내자.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Pack Raider의 손이 묶여 있다. 하지만 코는 쉬지 않는다 — 연신 바람을 읽고 있다. |
| 1 | Pack Raider | defiant | "뒤에 스물, 앞에 다섯. 바람 냄새로 다 안다. 도망칠 생각은 없어." |
| 2 | Dawn Priest | solemn | "당신의 불신을 탓하지 않겠습니다." |
| 3 | Pack Raider | bitter | "불신? 너희가 우리 사냥터에 철을 박았잖아. 그걸 불신이라고 불러?" |
| 4 | Narrator | — | Dawn Priest의 표정이 굳는다. 대꾸하지 않는다. |
| 5 | Pack Raider | bitter | "사제가 입 열면 바람이 멈춰. 약속은 흙에 심어야 뿌리가 나지. 말로는 안 돼." |
| 6 | Dawn Priest | resolute | "같은 길을 걷는 동안만이라도 등을 맡기십시오." |
| 7 | Pack Raider | sardonic | "등을 맡기라고? 등에 칼을 꽂는 건 너희 전통이잖아." |
| 8 | Narrator | — | 침묵이 내려앉는다. 솔잎 사이로 바람이 분다. Pack Raider의 코가 움찔한다 — 뭔가를 감지한 것이다. |
| 9 | Pack Raider | wary | "...좋아. 근데 앞에서 걸어. 등은 네가 보여." |

---

### `dialogue_scene_raider_totem_explanation` — Pack Raider 토템 설명

> **컨텍스트**: Pack Raider가 소나무에 새겨진 Pack Totem의 의미를 설명한다. 토템에서 이상한 진동.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 소나무 한 그루가 다른 나무보다 깊이 새겨져 있다. 갈퀴 자국 위로 수지가 굳어 호박색 줄을 이루고 있다. |
| 1 | Pack Raider | solemn | "이건 무기가 아니야. 사냥터의 경계이고, 새끼들에게 남기는 약속이야." |
| 2 | Dawn Priest | curious | "어떤 약속입니까?" |
| 3 | Pack Raider | solemn | "여기까지는 안전하다. 여기서부터는 네 발로 서라." |
| 4 | Narrator | — | Pack Raider가 나무에 손을 댄다. 손바닥 아래에서 진동이 울린다. 그의 표정이 변한다. |
| 5 | Pack Raider | shock | "토템이 미쳐 날뛰고 있어. 안에서 뭔가가 울려." |
| 6 | Dawn Priest | shock | "이 진동... 성물함 안의 유물과 같은 파장입니다. 같은 물질이란 말입니까?" |
| 7 | Pack Raider | bitter | "너희가 훔쳐간 것의 무게를 이놈이 기억하고 있는 거야." |
| 8 | Narrator | — | 토템의 진동이 발밑으로 전해진다. 아래로. 더 아래로. 무언가와 공명하듯. |
| 9 | Dawn Priest | grim | "...기록을 수정해야겠습니다." |

---

### `dialogue_overlay_boss_bark_wolfpine_trail` — 회색 송곳니 조우

> **컨텍스트**: 팩 우두머리 '회색 송곳니'가 원정대를 시험한다. Pack Raider를 배신자로 부른다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | bitter | "회색 송곳니. 형제야. 날 배신자로 부르겠지." |
| 1 | Narrator | — | 숲 깊은 곳에서 낮은 울음소리가 울려온다. 분노가 아니다. 더 오래된 것이다. |
| 2 | Pack Raider | wary | "근데 저놈 냄새가 달라. 분노가 이렇게 오래 가는 건 자연스럽지 않아." |

---

### `dialogue_scene_boss_defeat_wolfpine_trail` — 회색 송곳니 격파 후

> **컨텍스트**: 회색 송곳니 패배 후, 파편이 공명하자 그가 마지막 말을 남긴다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 회색 송곳니가 쓰러진다. 몸에서 빛이 새어나온다 — Heartforge 파편이 그의 살 안에 박혀 있었다. |
| 1 | Pack Raider | shock | "...형제가 마지막에 말했어. '이 분노가 내 것이 아닐 수도 있다'고." |
| 2 | Narrator | — | Pack Raider가 형제의 몸에서 파편을 뽑는다. 손이 떨린다. |
| 3 | Pack Raider | bitter | "바람이 아니야. 땅 밑에서 올라온 거야. 이놈을 미치게 만든 것이." |
| 4 | Dawn Priest | gentle | "적대가 조작된 것이라면... 처음으로, 함께 걸을 이유가 생겼습니다." |
| 5 | Pack Raider | bitter | "이유 따위 필요 없어. 형제를 미치게 만든 놈이 밑에 있다면 가는 거야." |
| 6 | Narrator | — | Pack Raider가 파편을 주머니에 넣는다. Dawn Priest가 손을 뻗지만 — 그가 눈으로 거절한다. |
| 7 | Dawn Priest | solemn | "...알겠습니다. 당신의 것입니다." |

---

## Chapter 2: Sunken Bastion — 가라앉은 보루

---

### `dialogue_scene_sunken_bastion_intro` — 가라앉은 보루 진입

> **컨텍스트**: 지반 침하로 기울어진 왕국 요새에 도착한다. 대리석 기둥이 비스듬히 서 있고 지하수가 복도를 적신다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 대리석 기둥이 비스듬히 서 있다. 물이 복도를 발목까지 채우고 있고, 벽의 문양이 반쯤 물에 잠겨 있다. |
| 1 | Dawn Priest | shock | "이 양식은... 제가 수련받던 곳과 같습니다." |
| 2 | Narrator | — | 그녀의 목소리가 가라앉는다. 기둥이 기울어진 각도를 보고 있다 — 구조가 아니라 상징을 읽는 것이다. |
| 3 | Dawn Priest | weary | "질서가... 가라앉고 있습니다. 물리적으로." |
| 4 | Pack Raider | sardonic | "자기가 지은 것도 못 지키는군. 돌담이 물에 빠지는 꼴이라니." |
| 5 | Narrator | — | Pack Raider가 물속에 발을 담근다. 바닥에 녹슨 무기가 잠겨 있다. |
| 6 | Pack Raider | skeptical | "녹 냄새. 오래된 피 냄새. 여기서 자기들끼리 싸웠어." |
| 7 | Dawn Priest | grim | "같은 질서의 신도끼리 칼을 겨눴다는 겁니까?" |
| 8 | Narrator | — | 물 위로 기름 막이 번진다. 색이 이상하다 — 무지개가 아니라, 격자 파편이 녹아내린 색이다. |
| 9 | Pack Raider | bitter | "그러게. 질서라는 게 원래 이런 맛이야?" |
| 10 | Dawn Priest | resolute | "...안으로 들어갑니다. 여기서 무슨 일이 있었는지 알아야 합니다." |
| 11 | Narrator | — | 두 사람이 기울어진 복도를 걷는다. 물이 한쪽으로 흐르고, 그들의 발소리가 울린다. |

---

### `dialogue_overlay_boss_bark_sunken_bastion` — 침묵의 집정관 조우

> **컨텍스트**: 요새 최심부에서 '침묵의 집정관'이 격자 파편으로 강화된 갑옷을 입고 가로막는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | grim | "집정관 각하. 질서를 지키겠다고 맹세한 분이 왜 이 자리에 서 계십니까." |
| 1 | Pack Raider | defiant | "갑옷에서 토템이랑 같은 빛이 나. 직접 벗겨줄게." |
| 2 | Narrator | — | 집정관이 대답하지 않는다. 갑옷의 빛만이 응답한다. |

---

### `dialogue_overlay_boss_defeat_sunken_bastion` — 침묵의 집정관 격파

> **컨텍스트**: 집정관의 갑옷에서 빠져나온 격자 파편이 주변 물에 기억을 투사한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 파편이 물에 닿자, 수면 위로 80년 전의 영상이 떠오른다. 누군가가 땅을 파고 있다. |
| 1 | Dawn Priest | shock | "성유물이... 도굴품이었습니다." |
| 2 | Pack Raider | bitter | "80년. 그동안 새끼들이 서로 물어뜯은 이유가 여기 있었어." |

---

### `dialogue_scene_aldric_journal` — Aldric의 일지 발견

> **컨텍스트**: 요새 기록실에서 Dawn Priest가 "A. Sternholt" 서명의 연구 일지를 발견한다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 기록실 바닥에 물이 차 있다. 선반 위쪽에만 책이 남아 있다. Dawn Priest가 한 권을 꺼낸다. 표지에 서명. |
| 1 | Dawn Priest | shock | "A. Sternholt... 이 이름을 알고 있습니다. 교회 창설 문서에..." |
| 2 | Pack Raider | skeptical | "누군데." |
| 3 | Dawn Priest | grim | "초대 학자-수장입니다. 영원한 질서의 교리를 설계한 사람. 제가 배운 모든 기도의... 근원입니다." |
| 4 | Narrator | — | 그녀의 손이 페이지를 넘긴다. 필체가 점점 거칠어진다. 초반의 정돈된 글씨가 후반에는 갈겨 쓴 것처럼 변한다. |
| 5 | Pack Raider | bitter | "네 기도를 만든 놈이 땅을 파고 있었다고? 대단한 신이야." |
| 6 | Dawn Priest | weary | "일지에 격자 파편의 군사 전용 절차가... 이것이 방벽 기술의 기반이었습니다." |
| 7 | Narrator | — | Dawn Priest가 일지를 닫는다. 잠시 동안 아무 말도 하지 않는다. 물이 발목을 적시고 있다. |
| 8 | Dawn Priest | grim | "이 사람이 모든 것을 설계했습니다. 교리도. 방벽도. 전부." |
| 9 | Pack Raider | sardonic | "이제야 네 집 기둥이 도둑질로 세워진 걸 알았군." |
| 10 | Narrator | — | 저널 마지막 페이지에 다른 잉크로 쓴 메모가 있다. '제4수문 — 격자가 우리가 생각한 것보다 더 많은 것을 기억하고 있다. 네 번째 봉인 뒤에 무언가 잠들어 있다.' |
| 11 | Dawn Priest | grim | "제4수문? 영원한 질서의 교리에는 수문이 세 개뿐입니다. 네 번째라니..." |
| 12 | Pack Raider | skeptical | "세 개라고 가르치고 네 개째를 숨겼다? 너희 신앙답다." |

---

### `dialogue_scene_tithe_road_intro` — 십일조 길 진입

> **컨텍스트**: 처형대와 고해소가 번갈아 서 있는 포장 도로. 깨진 성물함과 마른 핏자국.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 포장 도로 양편에 처형대와 고해소가 번갈아 서 있다. 돌바닥에 검은 얼룩이 규칙적으로 찍혀 있다. |
| 1 | Dawn Priest | grim | "정화 재판의 현장입니다." |
| 2 | Pack Raider | shock | "자기 동족한테도 이런 짓을?" |
| 3 | Narrator | — | Pack Raider가 처형대 위의 쇠고리를 만진다. 손가락 크기에 맞게 닳아 있다. |
| 4 | Pack Raider | bitter | "짐승은 최소한 먹으려고 죽이기라도 하지." |
| 5 | Dawn Priest | weary | "정화라는 이름으로 행해진 일들입니다." |
| 6 | Narrator | — | 바닥에 깨진 성물함이 있다. 안이 비어 있다 — 원래부터 비어 있었던 것처럼. |
| 7 | Dawn Priest | bitter | "빈 성물함 위에 피를 뿌렸습니다. 이것을 축복이라 불렀습니다." |
| 8 | Narrator | — | Dawn Priest의 목소리가 흔들린다. 처음이다. |
| 9 | Pack Raider | quiet | "...사제. 안색이 안 좋아." |
| 10 | Dawn Priest | weary | "안색이 좋을 수가 없습니다." |
| 11 | Narrator | — | 바람이 도로를 따라 분다. 재가 아니라 먼지다. 오래된 먼지. |

---

### `dialogue_scene_priest_faith_question` — Dawn Priest의 의문

> **컨텍스트**: Dawn Priest가 기록실의 진실 앞에서 처음으로 자신의 신앙에 의문을 품는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Dawn Priest가 기록부를 손에 들고 서 있다. 잉크가 아직 마르지 않은 페이지. |
| 1 | Dawn Priest | weary | "제가 섬긴 질서... 그 밑바닥에 이런 것이 있었습니다." |
| 2 | Dawn Priest | grim | "이걸 아는 자들이 있었고, 그들은 침묵을 택했습니다." |
| 3 | Narrator | — | 기록부의 서명란에 "A. Sternholt"가 또 있다. 같은 필체. 같은 사람. |
| 4 | Pack Raider | gentle | "의심하는 것도 용기야, 사제." |
| 5 | Dawn Priest | surprise | "...당신에게 그런 말을 들을 줄은 몰랐습니다." |
| 6 | Pack Raider | solemn | "바람을 거스르는 짐승은 무리에서 쫓겨나. 근데 가끔은 그놈이 길을 찾거든." |
| 7 | Narrator | — | Dawn Priest가 기록부를 배낭에 넣는다. 손이 떨리지만, 놓지 않는다. |

---

### `dialogue_overlay_boss_bark_tithe_road` — 대심문관 조우

> **컨텍스트**: 대심문관 '정결의 불꽃'이 원정대를 이단으로 선고한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | defiant | "대심문관. 그 불꽃이 태운 것은 이단이 아니라 진실이었습니다." |
| 1 | Pack Raider | sardonic | "진짜 불꽃 색이 궁금했는데. 금색이 아니라 핏빛이었네." |
| 2 | Narrator | — | 대심문관의 손에서 격자 빛이 타오른다. Dawn Priest와 같은 빛이다. |

---

### `dialogue_scene_boss_defeat_tithe_road` — 대심문관 격파 후

> **컨텍스트**: 대심문관이 쓰러진다. Dawn Priest가 자신의 축복과 이 파괴가 같은 원리임을 깨닫는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 대심문관이 쓰러진다. 손에서 꺼지는 빛이 Dawn Priest의 손끝에서도 같은 색으로 깜빡인다. |
| 1 | Dawn Priest | bitter | "축복과 정화가 같은 원리였습니다. 제 기도와 저 자의 불꽃은... 방향만 달랐습니다." |
| 2 | Narrator | — | Dawn Priest가 자기 손을 내려다본다. 오래도록. |
| 3 | Pack Raider | solemn | "사제 얼굴이 처음으로 무너졌어." |
| 4 | Dawn Priest | bitter | "무너진 게 맞습니다." |
| 5 | Narrator | — | 물이 대심문관의 몸 위로 차오른다. 빛이 완전히 꺼진다. |
| 6 | Pack Raider | quiet | "믿던 게 부서지는 소리가 이렇게 크더라." |
| 7 | Dawn Priest | weary | "...예." |
| 8 | Narrator | — | 대심문관의 갑옷에서 격자 파편이 빠져나오며 짧은 음성이 울린다. 오래된 말투. 교리처럼 들리지만 무기에서 나온다. |
| 9 | npc_aldric (voice fragment) | — | "...통제할 수 있소. 질서는 도구에서 시작하오..." |
| 10 | Dawn Priest | shock | "이 목소리... 경전의 서문과 같은 어조입니다. 왜 무기에서..." |

---

### `dialogue_scene_priest_faith_crack` — Dawn Priest 신앙 균열

> **컨텍스트**: 대심문관 격파 후. Dawn Priest의 신앙이 결정적으로 균열한다. 사제 인장을 손에 쥐고 있다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Dawn Priest가 사제 인장을 손에 쥐고 서 있다. 오랫동안 말이 없다. |
| 1 | Dawn Priest | bitter | "도굴품 위에 세운 신앙입니다." |
| 2 | Dawn Priest | weary | "성물함 안에 든 것이 신의 축복이 아니라 남의 뼈였다면..." |
| 3 | Narrator | — | 문장이 끊긴다. 그녀가 입을 다문다. 다시 연다. |
| 4 | Dawn Priest | breaking | "우리가 올린 기도는... 누구에게..." |
| 5 | Narrator | — | 끝맺지 못한다. 인장이 손에서 미끄러질 뻔한다. 그녀가 움켜쥔다 — 아직 놓지는 못한다. |
| 6 | Pack Raider | gentle | "무너진 것도 흙이 돼." |
| 7 | Dawn Priest | weary | "...흙이요?" |
| 8 | Pack Raider | solemn | "흙에서 뿌리가 나는 법이야. 네가 심을 차례다, 사제." |
| 9 | Narrator | — | Dawn Priest가 인장을 주머니에 넣는다. 버리지는 않는다. 아직은. |

---

### `dialogue_scene_raider_kingdom_anger` — Pack Raider 왕국 분노

> **컨텍스트**: Pack Raider가 왕국의 격자 오용에 대해 분노를 표출한다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Pack Raider가 벽을 발톱으로 긁는다. 대리석에 다섯 줄의 자국이 생긴다. |
| 1 | Pack Raider | furious | "80년이야. 80년 동안 우리 땅 밑을 파갔어." |
| 2 | Pack Raider | bitter | "새끼들이 서로 물어뜯는 동안, 너희는 그걸로 벽돌을 쌓고 있었지." |
| 3 | Dawn Priest | solemn | "부정하지 않겠습니다." |
| 4 | Pack Raider | furious | "부정 안 한다고? 그게 뭐 대단한 건 줄 알아?" |
| 5 | Narrator | — | Pack Raider의 손이 떨린다. 분노와 슬픔이 섞인 떨림이다. |
| 6 | Dawn Priest | solemn | "왕국이 저지른 일의 무게를 제가 대신 질 수는 없습니다. 하지만 외면하지도 않겠습니다." |
| 7 | Pack Raider | bitter | "말은 잘 하시네. 항상." |

---

## Chapter 3: Ruined Crypts — 무너진 묘역

---

### `dialogue_scene_ruined_crypts_intro` — 무너진 묘역 진입

> **컨텍스트**: 고대 묘역으로 하강. Grave Hexer가 어둠 속에서 합류한다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 석관이 층층이 쌓여 있다. 인광 이끼가 통로를 희미하게 밝힌다. 공기가 차갑고 축축하다. |
| 1 | Dawn Priest | grim | "죽음의 땅이라 들었습니다." |
| 2 | Narrator | — | 어둠 속에서 발소리. 느릿하고 조심성 없는 걸음. 누군가가 걸어 나온다. |
| 3 | Grave Hexer | casual | "어, 손님이네. 오래간만이야. 한 300년? 400년? ...뭐, 상관없지만." |
| 4 | Pack Raider | wary | "죽은 것이 걸어. 코에 아무 냄새도 안 와." |
| 5 | Grave Hexer | amused | "냄새가 안 나면 위험하다고 했지? 좋은 코야." |
| 6 | Dawn Priest | tense | "당신은 누구십니까?" |
| 7 | Grave Hexer | casual | "기억 관리자. 여기 있는 속삭임 들려? 공격이 아니야. 잠든 기억이 손님을 맞이하는 거지." |
| 8 | Narrator | — | 벽 틈에서 희미한 빛이 맥동한다. 속삭임처럼. |
| 9 | Grave Hexer | dry | "좀 시끄럽긴 한데. 수백 년 만의 손님이라 흥분한 모양이야." |
| 10 | Dawn Priest | grim | "이 빛은... 죽음보다는 기다림에 가깝군요." |
| 11 | Grave Hexer | gentle | "맞아. 기다림이야. 꽤 오래된." |

---

### `dialogue_scene_hexer_memory_intro` — Grave Hexer 정체

> **컨텍스트**: Grave Hexer가 자신의 역할과 시간 감각의 괴리를 드러낸다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | casual | "400년 전 전쟁? 아, 그거. 화요일이었나. 수요일이었나." |
| 1 | Pack Raider | incredulous | "400년을 요일로 기억해?" |
| 2 | Grave Hexer | dry | "비가 왔거든. 비 오는 날은 기억이 잘 흘러나와서 정리하기 좋아." |
| 3 | Dawn Priest | solemn | "산 자보다 오래 기억하시는 분이군요." |
| 4 | Grave Hexer | casual | "기억은 못 내려놓아. 팔이 길어서 많이 드는 게 아니라, 내려놓는 법을 잊은 거야." |
| 5 | Narrator | — | Grave Hexer가 석관 하나를 손가락으로 툭 친다. 먼지가 일어난다. |
| 6 | Grave Hexer | dry | "이 사람은 270년 전에 죽었어. 빵을 좋아했지. 호밀빵." |
| 7 | Pack Raider | disturbed | "...그런 것까지 기억해?" |

---

### `dialogue_overlay_boss_bark_ruined_crypts` — 침묵의 기록관 조우

> **컨텍스트**: 묘역 깊은 곳의 '침묵의 기록관'. 기억을 지키는 존재.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | grim | "침묵의 기록관. 나보다 오래된 기억을 품고 있어. 열쇠를 안 넘기면 아무것도 못 꺼내." |
| 1 | Dawn Priest | weary | "진실에 다가가는 길은 매번 이렇습니까." |
| 2 | Grave Hexer | dry | "쉬웠으면 진실이라고 안 불렀겠지." |

---

### `dialogue_overlay_boss_defeat_ruined_crypts` — 침묵의 기록관 격파

> **컨텍스트**: 기록관이 쓰러지며 기억의 열쇠를 넘긴다. 마지막 행위는 공격이 아니라 인계.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 기록관이 무릎을 꿇는다. 가슴에서 빛이 새어나와 Dawn Priest의 손으로 흘러간다. |
| 1 | Grave Hexer | gentle | "열쇠를 넘겼어. 마지막 순간에 우릴 인정한 거야." |
| 2 | Dawn Priest | solemn | "이 무게를 받을 자격이... 있을까요." |

---

### `dialogue_scene_bone_orchard_intro` — 유골 과수원 진입

> **컨텍스트**: 유골이 뿌리처럼 자란 석화 과수원. 기억의 밀도가 극도로 높다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 뼈가 나무처럼 자라 있다. 가지마다 인광이 매달려 있고, 공기가 두꺼워 숨쉬기가 힘들다. |
| 1 | Grave Hexer | grim | "여기는 기억의 우물이 아니라 바다야. 밀도가 묘역의 수십 배." |
| 2 | Pack Raider | uneasy | "뼈가 뿌리처럼 자랐어. 숲이랑은 다른 냄새야. 돌 냄새하고... 슬픔 냄새." |
| 3 | Dawn Priest | solemn | "격자 조각이 빛나고 있습니다. 무언가가 깨어나려 하고 있습니다." |
| 4 | Grave Hexer | warning | "조심해. 여기서 빠지면 못 나와. 기억이 삼켜." |
| 5 | Narrator | — | 바닥의 격자문이 희미하게 맥동한다. 심장박동 같은 리듬. |
| 6 | Pack Raider | wary | "밑에서 뭔가가 뛴다. 심장 소리 같아." |
| 7 | Dawn Priest | grim | "심장이 아닙니다. 더 오래된 것입니다." |
| 8 | Narrator | — | 격자문의 빛이 강해진다. 석화 나무 사이에서, 무언가가 눈을 뜨고 있다. |
| 9 | Grave Hexer | shock | "...이건 기억의 잔향이 아니야. 살아 있는 거야." |

---

### `dialogue_scene_relicborn_awakening` — Relicborn 각성

> **컨텍스트**: 석화 과수원 중심부에서 Relicborn이 각성한다. Echo Savant 첫 등장.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 격자문이 푸른 빛으로 타오른다. 석화 나무 사이에서 한 존재가 천천히 일어선다. 관절이 움직일 때마다 돌 부스러기가 떨어진다. |
| 1 | Echo Savant | disoriented | "...시끄럽다." |
| 2 | Narrator | — | 그의 첫 마디. 1800년 만에. 눈이 초점을 못 잡는다. |
| 3 | Dawn Priest | shock | "봉인의 수호자가... 살아 있었습니까?" |
| 4 | Pack Raider | wary | "새 냄새야. 바람에도 없던 종류." |
| 5 | Grave Hexer | curious | "어머나. 격자 안에서 진짜로 누가 나왔네." |
| 6 | Echo Savant | disoriented | "머리가... 울린다. 이 소리가 뭐다." |
| 7 | Narrator | — | Echo Savant가 손을 들어 올린다. 천천히. 자기 손을 보는 것 — 1800년 전에 마지막으로 본 손이다. |
| 8 | Dawn Priest | shock | "우리가 침략자라 부른 존재가... 이 세계를 지키고 있었다니." |
| 9 | Grave Hexer | dry | "1800년 동안 자고 일어났으니 컨디션이 좀 그렇겠네." |
| 10 | Echo Savant | disoriented | "...여기가 어디다." |
| 11 | Pack Raider | blunt | "긴 얘기야. 앉아." |
| 12 | Narrator | — | Echo Savant가 앉지 않는다. 서 있다. 다리가 떨리지만. |
| 13 | Echo Savant | grim | "뼈가 울린다. 격자가... 닳았다." |

---

### `dialogue_scene_aldric_face` — Aldric의 얼굴

> **컨텍스트**: 기억 투사 속에서 Aldric Sternholt의 살아있는 얼굴이 나타난다. 이름에 얼굴이 붙는 순간.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 기억 투사가 벽에 펼쳐진다. 한 남자가 땅에서 격자 파편을 뜯어내고 있다. 차분한 얼굴. 확신에 찬 손. |
| 1 | Dawn Priest | shock | "저 얼굴... 교회 초상화에서 본 적이 있습니다. Aldric Sternholt." |
| 2 | Grave Hexer | grim | "아, 저 사람. 기억해. 꽤 똑똒한 인간이었지. 80년 전이면... 최근이야." |
| 3 | Echo Savant | dark | "이 자의 의지가... 아직 안에 있다." |
| 4 | Narrator | — | Echo Savant의 몸이 경직된다. 무언가를 감지한 것이다 — 격자 안쪽에서. |
| 5 | Dawn Priest | dread | "살아 있다는 겁니까? 40년 전에 죽었다고 기록되어 있는데..." |
| 6 | Echo Savant | grim | "죽었다. 하지만 의지가 남았다. 심장이 먹었다." |
| 7 | Grave Hexer | dry | "기억을 먹는 기계 안에서 죽었으면, 기억이 남는 건 당연하지." |

---

### `dialogue_scene_hexer_ancient_coexistence` — 고대 공존의 기억

> **컨텍스트**: 기억 투사가 네 종족이 공존했던 시대를 보여준다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 빛의 환영이 펼쳐진다. 네 종족이 같은 물가에 서 있다. 무기가 없다. |
| 1 | Grave Hexer | gentle | "이 기억 봐. 전쟁 이전이야. 꽤 좋은 시절이었지." |
| 2 | Pack Raider | solemn | "토템이 기억하고 있었어. 진동 속에 옛 냄새가 남아 있었는데... 이거였구나." |
| 3 | Dawn Priest | shock | "공존의 증거가 있었습니까? 왜 아무도..." |
| 4 | Grave Hexer | bitter | "이 기억을 지운 게 왕국의 첫 번째 죄야. 공존의 증거를 묻고 적을 만들었어." |
| 5 | Narrator | — | 환영이 흐려진다. 물가의 네 종족이 서서히 사라진다. |
| 6 | Pack Raider | quiet | "...좋은 냄새였을 거야. 그 물가." |
| 7 | Dawn Priest | weary | "우리가 묻은 것이 이렇게나 많았습니까." |

---

### `dialogue_overlay_boss_bark_bone_orchard` — 뿌리의 관망자 조우

> **컨텍스트**: 석화 과수원 중심의 보스 '뿌리의 관망자'.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | grim | "뿌리의 관망자. 1800년 동안 이 자리를 지킨 문지기." |
| 1 | Dawn Priest | resolute | "이해를 위한 싸움입니다. 정복이 아닙니다." |
| 2 | Echo Savant | grim | "동족이다. 가볍게 하지 마라." |

---

### `dialogue_overlay_boss_defeat_bone_orchard` — 뿌리의 관망자 격파

> **컨텍스트**: 관망자가 쓰러지고, 세 세력 모두가 피해자였음이 드러난다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | bitter | "몰라서 그랬던 거야. 우리 모두. 서로를 적이라 믿은 건 무지였지." |
| 1 | Dawn Priest | weary | "인간도, 야수족도, 언데드도... 같은 것에 당하고 있었습니다." |
| 2 | Narrator | — | 관망자의 몸이 빛으로 흩어진다. 먼지가 아니라 기억이 된다. |

---

### `dialogue_scene_savant_awakening` — Echo Savant 완전 각성

> **컨텍스트**: Echo Savant가 휴면에서 완전히 깨어나며 격자 상태를 감지한다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Echo Savant가 일어선다. 이번에는 다리가 떨리지 않는다. 눈에 초점이 잡힌다. |
| 1 | Echo Savant | grim | "심장이 기억을 먹고 있다. 뱉어내는 것은 분노." |
| 2 | Grave Hexer | curious | "네 공명과 내 기억이 같은 파장이군. 1800년을 사이에 두고. 재밌네." |
| 3 | Echo Savant | solemn | "격자가 닳았다. 분노가 새어나온다." |
| 4 | Dawn Priest | shock | "우리 모두가... 조종당한 겁니까?" |
| 5 | Echo Savant | flat | "조종이 아니다. 누수다." |
| 6 | Pack Raider | bitter | "누수든 조종이든. 형제가 미쳐 죽은 건 똑같아." |
| 7 | Narrator | — | Echo Savant가 Pack Raider를 본다. 오래. 대답하지 않는다. |
| 8 | Echo Savant | quiet | "...미안하다." |
| 9 | Pack Raider | surprised | "네가 한 짓이 아니잖아." |

---

### `dialogue_scene_midpoint_reveal` — 중반 반전

> **컨텍스트**: 네 시점 캐릭터가 처음으로 한 자리에 선다. 원정의 목표가 전환되는 순간.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 격자 빛이 네 사람을 동시에 비춘다. 처음으로 같은 빛 아래 서 있다. |
| 1 | Echo Savant | grim | "격자가 40% 이하다. 심장의 출력이 억제 없이 퍼지고 있다." |
| 2 | Dawn Priest | shock | "신앙도, 전쟁도... 전부 이것의 부산물이었다는 겁니까?" |
| 3 | Grave Hexer | dry | "부산물이라니. 꽤 비싼 부산물이네. 수천 년치 전쟁이잖아." |
| 4 | Pack Raider | defiant | "그럼 밑에 뭐가 있든 부수러 가면 되잖아." |
| 5 | Echo Savant | flat | "부수면 기억이 전부 사라진다." |
| 6 | Narrator | — | 침묵. Grave Hexer의 표정이 처음으로 굳는다. |
| 7 | Grave Hexer | quiet | "...그건 안 돼." |
| 8 | Dawn Priest | resolute | "파괴가 아니라 다른 방법을 찾아야 합니다. 아래로 내려갑시다." |
| 9 | Pack Raider | bitter | "명분이 바뀌었네. 조사에서 수리로." |
| 10 | Echo Savant | flat | "순환의 종결이다. 수리가 아니다." |
| 11 | Narrator | — | 네 사람이 아래를 본다. 어둠이 올라온다. |

---

### `dialogue_scene_priest_midpoint_shock` — Dawn Priest 중반 충격

> **컨텍스트**: 중반 반전 직후. Dawn Priest가 진실의 무게에 짓눌린다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 나머지 셋이 앞서 걷는다. Dawn Priest가 뒤처져 있다. |
| 1 | Dawn Priest | broken | "제가 올린 기도는... 기계의 부산물 위에 세운 것이었습니다." |
| 2 | Dawn Priest | breaking | "Aldric Sternholt가 설계한 교리를 제가... 아무 의심 없이..." |
| 3 | Narrator | — | 문장이 또 끊긴다. 세 번째다. 그녀의 어법이 변하고 있다. |
| 4 | Grave Hexer | gentle | "의심 없이 믿은 건 네 잘못이 아니야. 그렇게 설계된 거니까." |
| 5 | Dawn Priest | bitter | "그게 더 나쁩니다. 설계된 걸 모르고 살았다는 게." |
| 6 | Narrator | — | Dawn Priest가 걸음을 옮긴다. 다시 걷는다. 느리지만 멈추지는 않는다. |
| 7 | Grave Hexer | dry | "아직 걷네. 그러면 됐어." |

---

### `dialogue_scene_echo_grave_bonding` — Echo Savant와 Grave Hexer의 조용한 유대

> **컨텍스트**: 중반 반전 직후, 원정대가 유골 과수원을 빠져나오는 길. Echo Savant와 Grave Hexer가 뒤에 처져 걷는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 과수원 출구. 뼈나무 사이로 인광이 스러진다. 앞서 걷는 세 사람과 떨어져, 둘이 나란히 걷는다. |
| 1 | Echo Savant | solemn | "기억을 어떻게 견디나." |
| 2 | Grave Hexer | — | "...뭐?" |
| 3 | Echo Savant | solemn | "그 무게를. 수백 년. 어떻게." |
| 4 | Grave Hexer | gentle | "농담을 해. 400년 전 일을 어제 일처럼 말하면 사람들이 웃거든. 웃기면 견딜 만해." |
| 5 | Echo Savant | — | "..." |
| 6 | Narrator | — | Echo Savant가 웃지 않는다. Grave Hexer는 그것을 알아차린다. |
| 7 | Grave Hexer | solemn | "너는 안 웃는구나. 1800년 동안 혼자 있으면 그렇게 되나." |
| 8 | Echo Savant | solemn | "웃는 법을 잊었다. 격자 안에는 웃을 이유가 없었다." |
| 9 | Grave Hexer | gentle | "그럼 내가 대신 웃어 줄게. 네 몫까지." |

---

## Chapter 4: Glass Forest — 유리의 숲

---

### `dialogue_scene_glass_forest_intro` — 유리의 숲 진입

> **컨텍스트**: 결정이 된 거목들이 빛을 굴절시킨다. 아름답지만, 결정 안에 얼어붙은 전사체가 보인다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 유리 나무가 빛을 굴절시킨다. 무지개 파편이 사방에 흩어진다. 아름답다 — 발 아래서 유리가 부서지기 전까지. |
| 1 | Dawn Priest | grim | "결정 안에... 사람이 있습니다. 갑옷을 입은 채로." |
| 2 | Pack Raider | disturbed | "야수족도 있어. 달리다가 굳은 자세야. 도망치고 있었어." |
| 3 | Grave Hexer | quiet | "내 동족도 보여. 여기서 모두가 동시에 죽었어." |
| 4 | Narrator | — | 결정 나무 사이로 빛이 굴절된다. 네 가지 색 — 강철 회색, 호박색, 인광 녹색, 프리즘 보라. |
| 5 | Narrator | — | Echo Savant가 고개를 든다. 유리 수관 사이로 하늘이 보인다. 1800년 만에 처음. |
| 6 | Narrator | — | 평가도, 분석도 아닌 표정이 그의 얼굴에 떠오른다. 원정대가 처음 보는 얼굴이다. |
| 7 | Echo Savant | flat | "폭주 흔적이다. 심장이 한 번 터졌다. 오래전에." |
| 8 | Dawn Priest | grim | "모든 세력의 전사가... 같은 순간에..." |
| 9 | Pack Raider | bitter | "여기가 중립 지대라는 거야. 아무도 이기지 못한 곳." |
| 10 | Narrator | — | 유리를 밟을 때마다 소리가 난다. 뼈가 부러지는 소리와 비슷하다. |
| 11 | Grave Hexer | dry | "적어도 공평하긴 했네. 모두 같이 죽었으니까." |
| 12 | Echo Savant | grim | "같이 죽은 게 아니다. 같이 얼어붙은 거다." |
| 13 | Dawn Priest | resolute | "다시 이런 일이 없도록. 그것이 이유입니다." |

---

### `dialogue_scene_method_debate` — 정화/봉인/파괴 논쟁

> **컨텍스트**: 유리 숲 진입 후 Echo Savant가 세 선택지를 설명한다. lead 4명의 입장이 갈린다. 정화, 봉인, 파괴 — 각각의 대가가 다르고, 누구도 쉽게 양보하지 않는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "세 가지 길이 있다. 정화, 봉인, 파괴." |
| 1 | Narrator | — | 짧은 침묵. 유리 나뭇가지에서 빛이 굴절되어 네 사람의 얼굴에 서로 다른 색을 입힌다. |
| 2 | Dawn Priest | resolute | "정화해야 합니다. 왕국이 진 빚을 갚으려면 순환을 완전히 끝내야 해요. 대가가 있더라도." |
| 3 | Pack Raider | defiant | "대가? 누구 대가? 네가 치를 거야, 사제? 아니면 저 친구를 격자 안에 가둘 거야?" |
| 4 | Dawn Priest | weary | "그건..." |
| 5 | Pack Raider | bitter | "봉인이면 아무도 안 죽어. 아무도 안 갇혀. 200년이든 500년이든 그때 가서 또 생각하면 되잖아." |
| 6 | Grave Hexer | solemn | "200년 후에 같은 전쟁이 반복돼. 나는 그 전쟁을 기억하고 있거든. 똑같은 전쟁을." |
| 7 | Pack Raider | bitter | "그래서 기억을 바치겠다고? 네 기원을? 그건 네 존재 자체잖아." |
| 8 | Grave Hexer | — | "..." |
| 9 | Narrator | — | Grave Hexer가 처음으로 말을 잇지 못한다. 농담도, 과거형도 나오지 않는다. |
| 10 | Echo Savant | solemn | "봉인은 전례가 있다. 우리가 1800년 전에 했다. 하지만..." |
| 11 | Echo Savant | grim | "격자가 다시 풍화되면 다음 세대가 우리와 같은 자리에 선다." |
| 12 | Pack Raider | weary | "...그래도 지금 여기서 친구를 묻을 순 없어." |
| 13 | Narrator | — | 네 사람이 서로 다른 방향을 보고 있다. 합의는 없다. 유리가 바람에 울린다. |

---

### `dialogue_scene_aldric_whisper` — Aldric의 속삭임

> **컨텍스트**: 유리 숲 전투 후, 결정을 통해 Aldric의 목소리가 각 lead에게 다르게 속삭인다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 유리 파편이 울리기 시작한다. 같은 소리가 아니다. 각자 다른 주파수로. |
| 1 | npc_aldric | — | (to Dawn Priest) "그대의 속죄가 이것을 요구하오. 정화만이 그대가 진 빚을 갚는 길이오." |
| 2 | Dawn Priest | shock | "이 목소리... 저널의 필체와 같은 사람. 왜 제게..." |
| 3 | npc_aldric | — | (to Pack Raider) "봉인하시오. 무리가 안전해지오. 지금, 당장. 희생은 필요 없소." |
| 4 | Pack Raider | bitter | "꺼져. 네 목소리가 달콤할수록 썩은 냄새가 나." |
| 5 | Narrator | — | Pack Raider가 코를 찡그린다. 하지만 손이 떨린다. 봉인이라는 말에 흔들렸다. |
| 6 | Dawn Priest | grim | "이 자는 우리가 듣고 싶은 말을 하고 있어요. 그게 가장 위험한 거예요." |
| 7 | Pack Raider | weary | "...알아. 알고 있어. 그래도 반은 맞는 말이잖아." |

---

### `dialogue_overlay_boss_bark_glass_forest` — 유리의 숲 보스 조우

> **컨텍스트**: 유리 숲 심부의 결정 생물체. Aldric의 의지가 조종하고 있다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | grim | "격자가 꼬여 있다. 저 안에 의지가 있다." |
| 1 | Dawn Priest | resolute | "Aldric의 것입니다. 알고 있습니다." |
| 2 | Pack Raider | defiant | "의지든 뭐든. 부수면 조용해지겠지." |

---

### `dialogue_overlay_boss_defeat_glass_forest` — 유리의 숲 보스 격파

> **컨텍스트**: 결정 생물체가 부서진다. Aldric의 영향이 일시적으로 약해진다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 결정이 부서지면서 빛이 사방으로 흩어진다. 잠시, 속삭임이 멈춘다. |
| 1 | Dawn Priest | grim | "잠시뿐입니다. 본체는 아래에 있습니다." |
| 2 | Grave Hexer | dry | "잠시라도 조용하면 고마운 거야." |

---

### `dialogue_scene_starved_menagerie_intro` — 굶주린 동물원 진입

> **컨텍스트**: 유리 숲 너머의 야수 서식지. 생물들이 격자 오염에 의해 변이되어 있다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 울타리가 있다. 유리가 아니라 뼈로 만든 울타리. 안에서 움직이는 것들이 보인다. |
| 1 | Pack Raider | horror | "이건... 야수가 아니야. 야수였던 거야." |
| 2 | Narrator | — | 변이된 생물들이 울타리 안을 배회한다. 눈이 없다. 귀만 남아 있다. |
| 3 | Dawn Priest | grim | "격자 오염이 이 정도까지..." |
| 4 | Grave Hexer | quiet | "동물원이라기보다 감옥이야. 스스로 들어온 감옥." |
| 5 | Pack Raider | bitter | "울음소리가 안 나. 울 줄도 잊은 거야." |
| 6 | Echo Savant | flat | "격자가 생명을 비틀었다. 오래 방치된 곳이다." |
| 7 | Narrator | — | Pack Raider가 울타리에 손을 댄다. 안의 생물이 다가온다 — 적대가 아니라, 기억하는 것처럼. |
| 8 | Pack Raider | quiet | "...형제의 사냥개랑 같은 냄새가 나." |
| 9 | Dawn Priest | solemn | "끝내야 합니다. 이 고통을." |

---

### `dialogue_scene_clan_split` — 씨족 분열

> **컨텍스트**: 굶주린 우리 진입 직전, Ember Runner가 숨을 헐떡이며 도착한다. 씨족 분열 소식을 가져왔다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 유리 숲 경계에서 작은 그림자가 달려온다. 야수족 소녀. 다리에 피가 묻어 있다. |
| 1 | npc_ember_runner | shock | "형... 오빠! 장로들이... Grey Fang이 서약을 철회했어요." |
| 2 | Pack Raider | shock | "뭐라고?" |
| 3 | npc_ember_runner | weary | "인간과 손잡은 건 배신이래요. 무리의 반이 떠났어요. Grey Fang이 이끌고." |
| 4 | Narrator | — | Pack Raider가 아무 말도 하지 않는다. 분노가 아니다. 비어 있다. |
| 5 | Dawn Priest | gentle | "Pack Raider..." |
| 6 | Pack Raider | — | "...형제가 날 버렸어." |
| 7 | Narrator | — | 그가 처음으로 냄새도 바람도 이야기하지 않는다. 감각이 꺼진 것 같다. |
| 8 | Grave Hexer | gentle | "400년 전에도 같은 일이 있었지. 무리가 쪼개지고, 한쪽이 돌아오지 않았어." |
| 9 | Pack Raider | bitter | "위로야? 농담이야?" |
| 10 | Grave Hexer | solemn | "사실이야. 그리고 돌아오지 않은 쪽도 살았어. 다만 다른 이름으로." |

---

### `dialogue_scene_hexer_memory_debt` — Grave Hexer의 대가

> **컨텍스트**: Grave Hexer가 정화의 대가가 자신의 기억임을 깨닫는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 격자 조각이 Grave Hexer의 손에서 반응한다. 빛이 그녀의 가장 오래된 기억 쪽으로 흐른다. |
| 1 | Grave Hexer | realization | "...아." |
| 2 | Dawn Priest | concerned | "무슨 일입니까?" |
| 3 | Grave Hexer | bitter | "정화의 열쇠가 기억이야. 가장 오래된 기억. 그러니까..." |
| 4 | Grave Hexer | dry | "내 기원 기억이야. 내가 누구였는지의 처음. 그걸 먹이로 줘야 한다는 뜻이지." |
| 5 | Pack Raider | shock | "그러면 넌..." |
| 6 | Grave Hexer | forced-casual | "뭐, 400년치 다른 기억은 남으니까. 호밀빵 좋아했던 사람 기억이라든가." |
| 7 | Narrator | — | 농담을 하고 있다. 하지만 손이 격자 조각을 꽉 쥐고 있다. |
| 8 | Echo Savant | quiet | "...무겁다." |

---

### `dialogue_scene_savant_resonance_adj` — Echo Savant 공명 조율

> **컨텍스트**: Echo Savant가 격자의 현재 상태를 몸으로 느끼며 조율을 시도한다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Echo Savant가 바닥에 손을 댄다. 눈을 감는다. 프리즘 보라빛이 손끝에서 퍼진다. |
| 1 | Echo Savant | strained | "격자가... 비명을 지르고 있다." |
| 2 | Dawn Priest | concerned | "위험한 겁니까?" |
| 3 | Echo Savant | flat | "위험하지 않은 것이 없다." |
| 4 | Grave Hexer | dry | "1800년 자고 일어나서 긍정적이면 오히려 무서울 거야." |
| 5 | Narrator | — | Echo Savant의 몸이 미세하게 떨린다. 격자와 동기화되는 중이다. |
| 6 | Echo Savant | grim | "갈 수 있다. 아래로." |
| 7 | Pack Raider | blunt | "그럼 가자. 여기 더 있으면 유리가 될 것 같아." |

---

### `dialogue_overlay_boss_bark_starved_menagerie` — 굶주린 동물원 보스 조우

> **컨텍스트**: 변이된 야수 군주와의 전투.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | pained | "저건 왕이었어. 사냥터의 왕. 지금은..." |
| 1 | Echo Savant | grim | "격자가 만든 왕이다. 본래 모습이 아니다." |
| 2 | Dawn Priest | resolute | "해방시켜 줍시다." |

---

### `dialogue_overlay_boss_defeat_starved_menagerie` — 굶주린 동물원 보스 격파

> **컨텍스트**: 변이 야수 군주가 쓰러진다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 야수 군주가 쓰러진다. 몸이 풀리면서 — 한 순간 — 원래 모습이 비친다. 크고 아름다운 짐승이었다. |
| 1 | Pack Raider | quiet | "잘 가라." |
| 2 | Grave Hexer | gentle | "기억해 줄게. 원래 모습으로." |

---

### `dialogue_scene_raider_glass_forest_grief` — Pack Raider의 슬픔

> **컨텍스트**: 씨족 분열 소식 이후. Pack Raider가 혼자 있는 시간.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 밤. Pack Raider가 씨족 토템을 손에 쥐고 앉아 있다. 유리 나무의 빛이 그의 얼굴을 비춘다. |
| 1 | Dawn Priest | careful | "...잠이 안 옵니까?" |
| 2 | Pack Raider | hollow | "토템이 울리지 않아. 반쪽이 갈라지면 진동이 반으로 줄어." |
| 3 | Narrator | — | Dawn Priest가 옆에 앉는다. 말하지 않는다. |
| 4 | Pack Raider | bitter | "올바른 선택을 했다고 생각했어. 형제를 미치게 만든 것이 뭔지 알아내면, 씨족이 이해할 줄 알았어." |
| 5 | Dawn Priest | quiet | "저도 그랬습니다. 진실을 밝히면 교회가 고칠 줄 알았습니다." |
| 6 | Pack Raider | sardonic | "우리 둘 다 순진했네." |
| 7 | Narrator | — | 유리가 달빛에 울린다. 맑은 소리. 슬픈 소리. |
| 8 | Dawn Priest | gentle | "순진한 것과 옳은 것은 다릅니다." |
| 9 | Pack Raider | quiet | "...그래. 그래야지." |

---

### `dialogue_scene_costs_accepted` — 대가의 수용

> **컨텍스트**: 네 리드 캐릭터가 각자의 대가를 직시하고, 불완전하게나마 받아들인다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 유리 숲의 가장자리. Worldscar 하강로가 보인다. 네 사람이 서 있다. |
| 1 | Dawn Priest | resolute | "저는 Aldric이 만든 교리를 놓아야 합니다. 사제 인장이 보증하는 것이... 더 이상 제 것이 아닙니다." |
| 2 | Pack Raider | bitter | "난 이미 씨족 반을 잃었어. 더 잃을 것도 없어." |
| 3 | Grave Hexer | quiet | "난 가장 오래된 기억을 줘야 해. 내가 누구였는지의 처음을." |
| 4 | Narrator | — | Grave Hexer가 웃는다. 평소의 건조한 웃음이 아니다. 약간 더 얇다. |
| 5 | Grave Hexer | forced-casual | "뭐, 첫 기억 없이도 400년은 살았잖아. 얼마나 중요하겠어." |
| 6 | Echo Savant | solemn | "안은 너무 조용하다." |
| 7 | Grave Hexer | skeptical | "뭐가?" |
| 8 | Echo Savant | solemn | "격자 안. 1800년. 아무 소리도 없었다." |
| 9 | Narrator | — | Grave Hexer가 그 말의 무게를 이해하기 전에, Echo Savant가 입을 연다. |
| 10 | Echo Savant | quiet | "나는 남는다. 격자 안에." |
| 11 | Narrator | — | 가장 짧은 문장. 가장 무거운 문장. |
| 12 | Pack Raider | shock | "...뭐라고?" |
| 13 | Echo Savant | flat | "영구 수호자가 필요하다. 자원한다." |
| 14 | Dawn Priest | pained | "다른 방법이..." |
| 15 | Echo Savant | calm | "1800년 전에도 선택했다. 이번에는 눈을 뜨고 한다." |

---

## Chapter 5: Heartforge Descent — 심장로 하강

---

### `dialogue_scene_heartforge_gate_intro` — Heartforge 관문

> **컨텍스트**: Worldscar 입구의 거대한 석조 관문. 네 세력의 문양이 새겨져 있다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 석조 관문이 솟아 있다. 네 세력의 문양이 각 기둥에 새겨져 있다. Heartforge의 맥동이 가슴팍까지 울린다. |
| 1 | Dawn Priest | solemn | "네 문양이 있습니다. 네 세력이 함께 있을 때만 열리는 것 같습니다." |
| 2 | Pack Raider | grim | "문도 우리보다 먼저 알고 있었네. 함께 와야 한다는 걸." |
| 3 | Grave Hexer | dry | "3000년 전 문이 우리보다 똑똑하다니. 자존심 상하네." |
| 4 | Echo Savant | flat | "문이 아니다. 시험이다." |
| 5 | Narrator | — | 관문의 문양이 빛나기 시작한다. 각자의 색으로. |
| 6 | Dawn Priest | resolute | "문이든 시험이든. 열겠습니다." |
| 7 | Pack Raider | defiant | "그래. 충분히 기다렸어." |
| 8 | Grave Hexer | casual | "기다린 건 나야. 400년." |
| 9 | Echo Savant | flat | "1800년." |
| 10 | Grave Hexer | amused | "...졌네." |
| 11 | Narrator | — | 관문이 열린다. 안에서 어둠이 아니라 빛이 올라온다. 오래된 빛. |
| 12 | Dawn Priest | grim | "내려갑시다." |
| 13 | Pack Raider | solemn | "돌아올 수 있는 거지?" |

---

### `dialogue_scene_oath_ritual` — 서약의 의식

> **컨텍스트**: 관문 앞에서 각자가 자신의 봉헌물을 내려놓는다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 관문 앞에 네 개의 홈이 있다. 각 세력의 문양 모양. 무언가를 내려놓아야 한다. |
| 1 | Dawn Priest | solemn | "기도서를 놓겠습니다. Aldric이 쓴 원문입니다. 더 이상 이 기도를 올리지 않겠다는 뜻으로." |
| 2 | Narrator | — | Dawn Priest가 기도서를 홈에 놓는다. 손이 떨리지만, 놓는다. |
| 3 | Pack Raider | solemn | "형제의 파편을 놓을게. Wolfpine에서 뽑은 거야." |
| 4 | Narrator | — | 파편이 홈에 닿자 진동이 울린다. Pack Raider가 이를 악문다. |
| 5 | Grave Hexer | quiet | "나는... 이것." |
| 6 | Narrator | — | Grave Hexer가 빛나는 구슬을 꺼낸다. 가장 오래된 기억이 담긴 것. 아직 놓지 않는다. |
| 7 | Grave Hexer | forced-casual | "아직은 안 놓을 거야. 마지막에." |
| 8 | Echo Savant | flat | "나는 몸을 놓는다." |
| 9 | Narrator | — | 아무도 대꾸하지 않는다. 관문의 빛이 더 강해진다. |
| 10 | Dawn Priest | quiet | "...갑시다." |
| 11 | Narrator | — | 네 사람이 관문을 넘는다. 빛이 그들을 삼킨다. |

---

### `dialogue_overlay_boss_bark_heartforge_gate` — Heartforge 관문 보스 조우

> **컨텍스트**: 관문 수호자와의 전투.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | grim | "관문 수호자다. 나와 같은 시대의." |
| 1 | Dawn Priest | resolute | "길을 열겠습니다." |
| 2 | Pack Raider | defiant | "역사 수업은 나중에. 먼저 지나가자." |

---

### `dialogue_overlay_boss_defeat_heartforge_gate` — Heartforge 관문 보스 격파

> **컨텍스트**: 관문 수호자를 넘어서 심부로 진입한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 수호자가 빛으로 흩어진다. 길이 열린다. 아래에서 열기가 올라온다. |
| 1 | Grave Hexer | dry | "문지기 치고 예의 바르더라. 죽으면서도 길을 열어주다니." |
| 2 | Echo Savant | quiet | "예의가 아니다. 인계다." |

---

### `dialogue_scene_worldscar_depths_intro` — Worldscar 심부

> **컨텍스트**: 3000년의 기억이 벽면에 투사되어 있다. 하강이 깊어진다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 벽면이 빛나고 있다. 3000년의 기억이 지층처럼 쌓여 있다. 걸을수록 시대가 깊어진다. |
| 1 | Grave Hexer | awe | "이건... 내 기억 보관소보다 크네. 훨씬." |
| 2 | Dawn Priest | grim | "맨 아래 층에 Aldric의 시대가 있을 겁니다." |
| 3 | Narrator | — | 벽에 전투 장면이 재생된다. 오래된 것부터. 첫 전쟁, 봉인, 긴 평화, 그리고 풍화. |
| 4 | Pack Raider | quiet | "야수족이 보여. 옛날 야수족. 무기가 없어." |
| 5 | Echo Savant | grim | "나도 보인다. 1800년 전의 나." |
| 6 | Narrator | — | Echo Savant가 벽에 손을 댄다. 자신의 과거와 마주한다. |
| 7 | Grave Hexer | gentle | "기분 이상하지?" |
| 8 | Echo Savant | flat | "이상한 게 아니다. 아프다." |
| 9 | Dawn Priest | solemn | "모두의 기억입니다. 이것을 지켜야 합니다." |
| 10 | Pack Raider | resolute | "그래. 부수는 건 답이 아니야." |
| 11 | Narrator | — | 아래에서 맥동이 강해진다. 심장에 가까워지고 있다. |

---

### `dialogue_scene_priest_final_prayer` — Dawn Priest의 마지막 기도

> **컨텍스트**: Dawn Priest가 사제 인장을 내려놓는다. 더 이상 사제가 아니다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Dawn Priest가 멈춘다. 주머니에서 사제 인장을 꺼낸다. 오래 쥐고 있었다. 손자국이 남아 있다. |
| 1 | Dawn Priest | quiet | "이 인장은 Aldric이 설계한 교리의 증명입니다." |
| 2 | Dawn Priest | resolute | "더 이상 이것으로 스스로를 정의하지 않겠습니다." |
| 3 | Narrator | — | 인장을 바닥에 놓는다. 금속이 돌에 닿는 소리가 울린다. |
| 4 | Dawn Priest | breaking | "다만... 기도는 계속하겠습니다. Aldric의 기도가 아니라, 제 기도를." |
| 5 | Narrator | — | 그녀의 눈이 젖어 있다. 하지만 울지 않는다. |
| 6 | Dawn Priest | quiet | "영원한 질서의 이름이 아니라... 그냥. 제 이름으로." |
| 7 | Narrator | — | 인장이 바닥에서 희미하게 빛난다. 그리고 꺼진다. |

---

### `dialogue_scene_raider_oath_swear` — Pack Raider의 서약

> **컨텍스트**: Pack Raider가 이빨을 드러내며 맹세한다. 씨족이 아닌 자신의 의지로.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Pack Raider가 토템을 허리에서 풀어 앞에 놓는다. 반쪽짜리 씨족의 표식. |
| 1 | Pack Raider | solemn | "씨족 맹세가 아니야. 반쪽밖에 안 남았으니까." |
| 2 | Narrator | — | 그가 이빨을 드러낸다. 야수족의 서약 자세. |
| 3 | Pack Raider | fierce | "내 이빨로 맹세한다. 여기서 끝내고, 돌아가서, 남은 씨족에게 진실을 전한다." |
| 4 | Dawn Priest | moved | "Pack Raider..." |
| 5 | Pack Raider | fierce | "내 이름은 씨족에서 받은 거야. 반쪽이 떨어져 나갔지만 이름은 내가 지킨다." |
| 6 | Narrator | — | 이빨이 빛을 받아 번쩍인다. 위협이 아니라 약속이다. |
| 7 | Grave Hexer | gentle | "꽤 멋지네." |

---

### `dialogue_scene_hexer_memory_offering` — Grave Hexer의 기억 봉헌

> **컨텍스트**: Grave Hexer가 가장 오래된 기억을 정화의 열쇠로 바친다. 유머가 멈추는 순간.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Grave Hexer가 빛나는 구슬을 양손에 쥐고 있다. 안에서 빛이 맥동한다. 누군가의 웃음소리가 들리는 것 같다. |
| 1 | Grave Hexer | quiet | "이게 내 첫 기억이야." |
| 2 | Dawn Priest | gentle | "어떤 기억입니까?" |
| 3 | Grave Hexer | quiet | "...햇빛이었어. 그리고 누군가의 목소리. 이름은 기억 안 나. 근데 따뜻했지." |
| 4 | Narrator | — | 평소의 건조함이 없다. 처음이다. |
| 5 | Pack Raider | pained | "놓지 마. 다른 방법이 있을 거야." |
| 6 | Grave Hexer | sad | "400년 동안 이것만은 지켰어. 다른 기억은 흐려져도 이건... 이건 선명했거든." |
| 7 | Narrator | — | 손이 떨린다. 그녀의 손이 떨리는 것을 처음 본다. |
| 8 | Grave Hexer | resolute | "근데 이것보다 지켜야 할 게 더 많아." |
| 9 | Narrator | — | 구슬을 격자에 밀어넣는다. 빛이 퍼진다. 웃음소리가 사라진다. |
| 10 | Grave Hexer | empty | "..." |
| 11 | Narrator | — | Grave Hexer가 한동안 서 있다. 손이 허공을 쥔다. 아까까지 무엇을 쥐고 있었는지 모르겠다는 얼굴로. |
| 12 | Grave Hexer | hollow | "...뭐였더라." |

---

### `dialogue_scene_savant_seal_decision` — Echo Savant의 결정

> **컨텍스트**: Echo Savant가 격자의 영구 수호자로 남기로 선택한다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 격자의 중심이 보인다. 빈자리. 수호자가 들어갈 자리. |
| 1 | Echo Savant | calm | "여기다." |
| 2 | Dawn Priest | pained | "정말로... 다른 방법은 없습니까?" |
| 3 | Echo Savant | flat | "없다." |
| 4 | Pack Raider | frustrated | "네가 안 들어가면 안 되나." |
| 5 | Echo Savant | calm | "격자를 읽을 수 있는 것은 나뿐이다." |
| 6 | Narrator | — | 사실이다. 아무도 반박하지 못한다. |
| 7 | Echo Savant | quiet | "의무가 아니다. 선택이다." |

---

### `dialogue_overlay_boss_bark_worldscar_depths` — Aldric 의지체 대면

> **컨텍스트**: Worldscar 심부에서 Aldric의 의지체가 나타난다. 괴물이 아니라 한 남자의 형상.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Aldric | calm | "내가 Heartforge를 통제했소. 내가 혼돈에 질서를 가져왔소." |
| 1 | Aldric | reasonable | "너희는 내가 세운 모든 것을 파괴하고 있소." |
| 2 | Dawn Priest | resolute | "당신이 세운 것은 도굴 위의 성전이었습니다." |
| 3 | Echo Savant | grim | "끝이다, 설계자." |

---

### `dialogue_scene_final_confrontation` — 최종 대면

> **컨텍스트**: Aldric 의지체와의 최종 전투 전후. 논쟁이 전투만큼 중요하다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Aldric이 서 있다. 괴물이 아니다. 한 남자의 형상. 학자의 가운을 입고 있다. 눈이 차분하다. |
| 1 | Aldric | calm | "80년이오. 80년 동안 내가 만든 질서가 이 세계를 유지했소." |
| 2 | Dawn Priest | defiant | "유지가 아닙니다. 착취였습니다." |
| 3 | Aldric | patient | "착취라 하시오? 내가 오기 전에 너희는 서로를 죽이고 있었소. 이유도 모른 채." |
| 4 | Narrator | — | 틀린 말이 아니다. Dawn Priest가 대꾸하지 못한다. |
| 5 | Pack Raider | bitter | "그래, 이유는 몰랐지. 근데 네가 이유를 알고도 이용한 거잖아." |
| 6 | Aldric | measured | "이용이 아니라 관리였소. 혼돈에 구조를 부여한 것이오." |
| 7 | Grave Hexer | cold | "구조. 내 동족의 기억을 연료로 쓰면서 그걸 구조라고 불렀군." |
| 8 | Aldric | sad | "나는... 최선을 택했소. 불완전한 세계에서 불완전한 질서를." |
| 9 | Narrator | — | 한 순간, Aldric이 진심으로 슬퍼 보인다. 그래서 더 무섭다. |
| 10 | Echo Savant | flat | "최선이 이거였다면. 최선이 끝날 때가 됐다." |
| 11 | Dawn Priest | resolute | "당신이 만든 질서가 아닌, 우리가 선택한 질서를 세우겠습니다." |
| 12 | Aldric | resigned | "그래도... 기억해 주시오. 내가 먼저 불을 켰다는 것을." |
| 13 | Narrator | — | Aldric의 형상이 빛나기 시작한다. 전투가 시작된다. |

---

### `dialogue_scene_sacrifice_sequence` — 정화 시퀀스

> **컨텍스트**: Aldric의 의지를 꺾은 후. 정화가 시작된다. 각자가 대가를 치른다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | Aldric의 형상이 흩어진다. 분노도 원한도 아닌, 그저 사라지는 한 남자의 표정. |
| 1 | Narrator | — | 격자가 반응한다. 정화의 순서가 시작된다. |
| 2 | Dawn Priest | resolute | "시작합니다." |
| 3 | Narrator | — | Dawn Priest의 손에서 빛이 난다. 사제의 빛이 아니다. 그녀 자신의 빛이다. |
| 4 | Narrator | — | Grave Hexer가 빈 손을 들어올린다. 아까 놓은 구슬이 있던 자리. |
| 5 | Grave Hexer | quiet | "...끝났어. 놨어." |
| 6 | Narrator | — | Pack Raider가 이빨을 드러낸다. 서약 자세. 울부짖지 않는다. |
| 7 | Pack Raider | quiet | "돌아가면 남은 씨족에게 전한다. 전부." |
| 8 | Narrator | — | Echo Savant가 격자의 중심으로 걸어간다. 느리게. 한 걸음씩. |
| 9 | Echo Savant | calm | "괜찮다." |
| 10 | Narrator | — | 격자가 그를 감싼다. 프리즘 보라빛이 맥동하기 시작한다. |
| 11 | Dawn Priest | breaking | "Echo Savant..." |
| 12 | Echo Savant | calm | "잘 걸어라. 위로." |
| 13 | Narrator | — | 빛이 강해진다. Heartforge가 멈춘다. 심장이 뛰지 않는다. 고요. |

---

### `dialogue_scene_echo_farewell` — Echo Savant의 작별

> **컨텍스트**: Echo Savant가 격자 안에서 마지막으로 말한다. 대부분 침묵.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 격자 안에서 프리즘 보라빛이 일정한 리듬으로 맥동한다. Echo Savant의 형상이 희미하게 보인다. |
| 1 | Dawn Priest | tearful | "감사합니다." |
| 2 | Narrator | — | 대답이 없다. 한참 동안. |
| 3 | Pack Raider | quiet | "...들리나?" |
| 4 | Narrator | — | 맥동이 한 번 강해진다. 대답인 것 같다. |
| 5 | Grave Hexer | quiet | "기억해 줄게." |
| 6 | Narrator | — | Grave Hexer가 멈춘다. 그녀가 무엇을 기억할 수 있는지, 이제 본인도 확실하지 않다. |
| 7 | Grave Hexer | sad | "...기억할 수 있는 만큼." |
| 8 | Narrator | — | 맥동이 또 한 번. 이번에는 약하다. 멀어지고 있다. |
| 9 | Echo Savant | distant | "...시끄럽지 않다. 처음으로." |
| 10 | Narrator | — | 그의 마지막 말이다. 격자가 고요해진다. |

---

### `dialogue_scene_campaign_complete` — 캠페인 완결

> **컨텍스트**: 상승. Worldscar를 빠져나온다. 빛이 내려온다.
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 상승이 시작된다. 긴 통로. 발소리가 세 사람분이다. |
| 1 | Narrator | — | Worldscar의 좁은 틈 사이로 하늘이 보인다. 재가 아니라 빛이 내려온다. |
| 2 | Dawn Priest | quiet | "빛입니다." |
| 3 | Narrator | — | 그녀의 손에 사제 인장이 없다. 대신 Aldric의 일지가 있다. 증거를 가져간다. |
| 4 | Pack Raider | quiet | "따뜻해. 바람이." |
| 5 | Narrator | — | Pack Raider가 코를 들어올린다. 재 냄새가 아니다. 흙 냄새. 살아 있는 흙. |
| 6 | Grave Hexer | confused | "...여기 처음 온 기분이야. 이상하네. 분명 아는 곳인데." |
| 7 | Narrator | — | 그녀가 잃어버린 것의 흔적. 익숙함의 부재. |
| 8 | Dawn Priest | gentle | "괜찮습니다. 새로운 기억을 만들면 됩니다." |
| 9 | Narrator | — | 세 사람이 밖으로 나온다. 하늘이 열려 있다. |
| 10 | Pack Raider | quiet | "한 명이 빠졌어." |
| 11 | Narrator | — | 아무도 대꾸하지 않는다. 빛이 세 사람의 얼굴을 비춘다. 지치고, 상처 입고, 줄어들었다. 하지만 서 있다. |

---

## Town Returns — 마을 귀환

---

### `dialogue_overlay_town_return_ch1` — 1장 귀환

> **컨텍스트**: Ashen Gate 탐사 후 마을 복귀.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | resolute | "보고할 것이 있습니다. 문은 밖에서가 아니라 안에서 무너졌습니다." |
| 1 | Pack Raider | sardonic | "보고는 좋은데. 믿을 사람이 있으려나." |
| 2 | Narrator | — | 마을의 불빛이 보인다. 작고 불안하다. |

---

### `dialogue_overlay_town_return_ch2` — 2장 귀환

> **컨텍스트**: Sunken Bastion 탐사 후. Dawn Priest의 신앙이 흔들린 상태.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | weary | "교회에 보고해야 합니다. 하지만... 무엇을 보고해야 할지 모르겠습니다." |
| 1 | Pack Raider | bitter | "진실을 보고해. 너희가 뭘 했는지." |
| 2 | Narrator | — | Dawn Priest가 대답하지 않는다. 사제 인장을 만지작거리고 있다. |

---

### `dialogue_overlay_town_return_ch3` — 3장 귀환

> **컨텍스트**: Relicborn 진실 공개 후. 세계관이 뒤집힌 상태.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | dry | "마을이 좁아 보이네. 지하에 3000년치 기억을 보고 오니까." |
| 1 | Echo Savant | flat | "시끄럽다. 여기도." |
| 2 | Dawn Priest | solemn | "이제 시작입니다. 모든 것이." |

---

### `dialogue_overlay_town_return_ch4` — 4장 귀환

> **컨텍스트**: 대가를 수용한 후. 마지막 준비.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | grim | "마을에 와도 쉬는 기분이 안 나." |
| 1 | Grave Hexer | dry | "쉬는 건 끝나고 해. 만약 끝나면." |
| 2 | Dawn Priest | resolute | "끝냅니다. 반드시." |
| 3 | Narrator | — | 네 사람이 마을을 등진다. 마지막으로. |

---

### `dialogue_overlay_town_return_ch5` — 5장 귀환 (에필로그)

> **컨텍스트**: 캠페인 완료 후 마을 복귀.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | quiet | "끝났습니다." |
| 1 | Pack Raider | quiet | "...끝났어." |
| 2 | Narrator | — | 세 사람이 마을로 걸어 들어간다. 빛이 등 뒤에서 비추고 있다. 한 명이 빠져 있다. 하지만 서 있다. |
