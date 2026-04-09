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

---

## Chapter 1: Ashen Gate — 재의 문

---

### `dialogue_seq_ashen_gate_intro` — 재의 문 진입

> **컨텍스트**: 원정대가 무너진 국경문의 잔해를 넘어 Ashen Gate에 처음 발을 딛는다. 철문 파편 사이로 연기가 피어오르고, 재가 바람에 실려 목구멍 안쪽까지 긁는다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | resolute | "집정관 회의의 명에 따라 이 문을 넘습니다. 재의 들판 너머에 무엇이 있든, 영원한 질서의 이름으로 정화하겠습니다." |
| 1 | Pack Raider | skeptical | "너희가 세운 문이잖아. 너희 손으로 부순 거고. 재 냄새가 사흘째 바람을 타고 왔다." |
| 2 | Dawn Priest | grim | "까마귀가 낮게 날고 있습니다. 이 들판은 이미 한 번 끝난 세계입니다." |

---

### `dialogue_seq_priest_ashen_gate` — Dawn Priest 독백: 재의 문 앞에서

> **컨텍스트**: Dawn Priest가 무너진 국경문 앞에서 홀로 기도를 올리며 원정의 명분을 되새긴다. 손끝에 재가 묻는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | resolute | "영원한 질서의 이름으로, 이 문 너머의 오염을 정화하겠습니다." |
| 1 | Dawn Priest | grim | "이 문은 우리가 세운 것입니다. 우리가 지킨 것입니다. 그런데... 안에서부터 부서진 흔적이 있습니다." |
| 2 | Dawn Priest | solemn | "문 너머에 무엇이 있든, 눈을 감지 않겠습니다. 그것이 사제에게 허락된 유일한 용기입니다." |

---

### `dialogue_seq_boss_bark_ashen_gate` — 재의 문지기 조우

> **컨텍스트**: 국경문의 자동 방어 골렘 '재의 문지기'가 기동한다. 봉인 격자 에너지로 작동하는 고대 장치. 가슴판에서 성유물과 같은 빛이 난다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | shock | "저 빛은... 성유물의 빛입니다. 왜 무기에 깃들어 있는 겁니까?" |
| 1 | Pack Raider | defiant | "빛나든 말든. 부수면 꺼지겠지." |

---

### `dialogue_seq_boss_defeat_ashen_gate` — 재의 문지기 격파

> **컨텍스트**: 골렘이 쓰러지고, 내부에서 첫 번째 Heartforge 파편이 드러난다. Dawn Priest가 파편을 손에 쥐자 짧은 환시가 밀려온다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | shock | "성물함을 열었을 때... 안에 있어야 할 것이 없었습니다. 대신 이 파편이... 골렘의 심장에..." |
| 1 | Pack Raider | skeptical | "네 신앙의 빛이 돌덩어리 주먹 안에 들어 있었다. 축복이라, 참 쓸모 있는 말이야." |

---

### `dialogue_seq_wolfpine_trail_intro` — 늑대소나무 길 진입

> **컨텍스트**: 원정대가 소나무 숲길에 진입한다. 나무마다 갈퀴 자국으로 새긴 야수족 영역 표식. 안개가 낮게 깔리고, 솔잎 사이로 노란 눈이 반짝인다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | solemn | "이 갈퀴 자국은 경고가 아니야. 바람에게 올리는 기도다. 여기서부턴 씨족의 땅이야." |
| 1 | Dawn Priest | grim | "야수족의 영역입니다. 경계를 늦추지 마십시오." |

---

### `dialogue_seq_raider_first_contact` — Pack Raider 첫 접촉

> **컨텍스트**: Pack Raider가 원정대와 처음 합류하는 시점. 적대적 포로이거나 강제 안내자로, 원정대와 강한 마찰을 보인다. 눈빛이 사납고 코를 연신 찡그린다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | defiant | "바람 냄새로 다 안다. 뒤에 스물, 앞에 다섯. 도망칠 생각은 없어." |
| 1 | Pack Raider | bitter | "사제가 입을 열면 바람이 멈춰. 약속은 흙에 심어야 뿌리가 나지. 말로는 안 돼." |
| 2 | Dawn Priest | solemn | "당신의 불신을 탓하지 않겠습니다. 다만, 같은 길을 걷는 동안만이라도 등을 맡기십시오." |

---

### `dialogue_seq_raider_totem_explanation` — Pack Raider 토템 설명

> **컨텍스트**: Pack Raider가 소나무에 새겨진 Pack Totem의 의미를 설명한다. 토템에서 이상한 진동이 울리고 있다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | solemn | "이 토템은 무기가 아니야. 바람에게 올리는 기도이고, 사냥터의 경계이고, 새끼들에게 남기는 약속이야." |
| 1 | Pack Raider | bitter | "근데 토템이 미쳐 날뛰고 있어. 안에서 뭔가가 울려. 너희가 가져간 것의 무게를 이놈이 기억하는 거야." |
| 2 | Dawn Priest | shock | "이 진동... 성물함 안의 유물과 같은 파장입니다. 성유물과 토템이 같은 물질이란 말입니까?" |

---

### `dialogue_seq_boss_bark_wolfpine_trail` — 회색 송곳니 조우

> **컨텍스트**: 소나무 숲 깊은 곳에서 팩 우두머리 '회색 송곳니'가 원정대를 시험한다. Pack Raider를 배신자로 부른다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | bitter | "회색 송곳니. 같은 피를 나눈 형제야. 날 배신자로 부르겠지. 하지만 저놈의 분노... 뭔가 냄새가 달라." |
| 1 | Dawn Priest | resolute | "시험이라면 받아들이겠습니다. 이 칼은 토템과 같은 빛으로 벼려진 것이니." |

---

### `dialogue_seq_boss_defeat_wolfpine_trail` — 회색 송곳니 격파

> **컨텍스트**: 회색 송곳니가 패배 후, Heartforge 파편이 공명하자 자신의 분노가 외부에서 증폭된 것일 수 있음을 자각한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | shock | "...형제가 마지막에 말했어. '이 분노가 내 것이 아닐 수도 있다'고. 바람이 아니야. 땅 밑에서 올라온 거야." |
| 1 | Dawn Priest | gentle | "적대가 조작된 것이라면... 처음으로, 함께 걸을 이유가 생겼습니다." |

---

## Chapter 2: Sunken Bastion — 가라앉은 보루

---

### `dialogue_seq_sunken_bastion_intro` — 가라앉은 보루 진입

> **컨텍스트**: 원정대가 지반 침하로 기울어진 왕국 요새에 도착한다. 대리석 기둥이 비스듬히 서 있고, 지하수가 복도를 발목까지 적신다. 벽에 영원한 질서의 문양이 반쯤 물에 잠겨 있다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | shock | "이 양식은... 제가 훈련받던 곳과 같습니다. 기둥이 기울고, 회랑이 물에 잠겨... 질서가 가라앉고 있습니다." |
| 1 | Pack Raider | skeptical | "자기가 지은 것도 못 지키는군. 돌담이 물에 빠지는 꼴이라니, 코웃음이 나와." |
| 2 | Dawn Priest | weary | "같은 질서의 신도끼리 칼을 겨눠야 한다니. 이 요새 안에서 대체 무슨 일이..." |

---

### `dialogue_seq_boss_bark_sunken_bastion` — 침묵의 집정관 조우

> **컨텍스트**: 요새 최심부에서 타락한 지휘관 '침묵의 집정관'이 격자 파편으로 강화된 갑옷을 입고 원정대를 가로막는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | grim | "집정관 각하. 질서를 지키겠다고 맹세한 분이 왜 진실을 매장하고 계십니까." |
| 1 | Pack Raider | defiant | "갑옷에서 우리 토템이랑 같은 빛이 나. 직접 벗겨줄게." |

---

### `dialogue_seq_boss_defeat_sunken_bastion` — 침묵의 집정관 격파

> **컨텍스트**: 집정관의 갑옷에서 빠져나온 격자 파편이 주변 물에 기억을 투사한다. 80년 전 격자 파편 채굴과 오용의 증거가 물 위에 영상처럼 떠오른다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | shock | "성유물이... 도굴품이었습니다. 80년 동안 다른 존재의 뼈를 깎아서 방벽을 세운..." |
| 1 | Pack Raider | bitter | "80년. 그 세월 동안 사냥터가 미쳐 날뛰고 새끼들이 서로를 물어뜯은 이유가 여기 있었어." |

---

### `dialogue_seq_tithe_road_intro` — 십일조 길 진입

> **컨텍스트**: 십일조 행렬이 다니던 포장 도로. 처형대와 고해소가 번갈아 서 있고, 깨진 성물함과 집행 기록이 바닥에 흩어져 있다. 마른 핏자국이 돌바닥에 검게 배어 있다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | grim | "처형대와 고해소가 번갈아 서 있습니다. 이것이... 정화 재판의 현장입니다." |
| 1 | Pack Raider | shock | "자기 동족에게도 이런 짓을? 허. 짐승은 최소한 먹으려고 죽이기라도 하지." |
| 2 | Dawn Priest | weary | "정화라는 이름으로 행해진 일들입니다. 이것을 축복이라 불렀던 제가... 부끄럽습니다." |

---

### `dialogue_seq_priest_faith_question` — Dawn Priest 신앙 질문

> **컨텍스트**: Dawn Priest가 기록실에서 발견한 진실 앞에서 처음으로 자신의 신앙에 의문을 품는다. 손에 들린 기록부가 떨린다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | weary | "제가 섬긴 질서... 그 밑바닥에 이런 것이 있었습니다." |
| 1 | Dawn Priest | grim | "기록실의 잉크가 아직 마르지 않았습니다. 이걸 아는 자들이 있었고, 그들은 침묵을 택했습니다." |
| 2 | Pack Raider | gentle | "의심하는 것도 용기야, 사제. 바람을 거스르는 짐승은 무리에서 쫓겨나지만... 가끔은 그놈이 옳거든." |

---

### `dialogue_seq_boss_bark_tithe_road` — 대심문관 조우

> **컨텍스트**: 십일조 도로 종점에서 대심문관 '정결의 불꽃'이 원정대를 이단으로 선고한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | defiant | "대심문관. 그 불꽃이 태운 것은 이단이 아니라 진실이었습니다. 오늘 제가 그 불을 되돌리겠습니다." |
| 1 | Pack Raider | defiant | "네 신이 좋아하는 색이 금색인 줄 알았는데, 빨간색이었나 보지?" |

---

### `dialogue_seq_boss_defeat_tithe_road` — 대심문관 격파

> **컨텍스트**: 대심문관이 쓰러지고, Dawn Priest는 자신이 수행한 축복 의식과 이 파괴적 정화가 같은 원리임을 깨닫는다. 손을 내려다본다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | bitter | "축복과 정화가 같은 원리였습니다. 제가 올린 기도와 저 자가 휘두른 불꽃의 차이는... 방향뿐이었습니다." |
| 1 | Pack Raider | solemn | "...사제 얼굴이 처음으로 무너졌어. 믿던 게 부서지는 소리는 뼈가 부러지는 것보다 크더라." |

---

### `dialogue_seq_priest_faith_crack` — Dawn Priest 신앙 균열

> **컨텍스트**: 대심문관 격파 후, Dawn Priest의 신앙이 결정적으로 균열한다. 사제 인장을 손에 쥐고 오랫동안 말이 없다가 입을 연다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | bitter | "도굴품 위에 세운 신앙. 성물함 안에 든 것이 신의 축복이 아니라 남의 뼈였다면... 우리가 올린 기도는 누구에게..." |
| 1 | Dawn Priest | weary | "사제 인장이 이렇게 무거운 줄 몰랐습니다. 이 인장이 보증한 것이 정의가 아니라 은폐였다니." |
| 2 | Pack Raider | gentle | "무너진 것도 흙이 돼. 흙에서 새 뿌리가 나는 법이야. 네가 심을 차례다, 사제." |

---

### `dialogue_seq_raider_kingdom_anger` — Pack Raider 왕국 분노

> **컨텍스트**: Pack Raider가 요새에서 확인한 왕국의 격자 오용에 대해 분노를 표출한다. 발톱으로 벽을 긁으며, 이가 드러나도록 입술을 걷는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | defiant | "80년 동안 우리 땅 밑의 것을 파갔어. 새끼들이 미쳐 날뛰고 사냥터가 말라붙는 동안, 너희는 요새 벽돌을 쌓고 있었지." |
| 1 | Pack Raider | bitter | "이 돌담에 피 냄새가 배어 있다. 너희 피만이 아니야. 우리 것도 섞여 있어." |
| 2 | Dawn Priest | solemn | "부정하지 않겠습니다. 왕국이 저지른 일의 무게를 제가 대신 질 수는 없지만, 외면하지도 않겠습니다." |

---

## Chapter 3: Ruined Crypts — 무너진 묘역

---

### `dialogue_seq_ruined_crypts_intro` — 무너진 묘역 진입

> **컨텍스트**: 원정대가 고대 묘역으로 하강한다. 석관이 층층이 쌓여 있고 인광 이끼가 통로를 희미하게 밝힌다. Grave Hexer가 이 시점에서 합류한다. 어둠 속에서 느릿하게 걸어 나온다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | casual | "어, 돌아왔네. 몇백 년 만이지? 몇십 년? ...뭐, 상관없지만." |
| 1 | Grave Hexer | gentle | "이 속삭임 들려? 공격이 아니야. 잠든 기억이 손님을 맞이하는 거지. 좀 시끄럽긴 한데." |
| 2 | Dawn Priest | grim | "죽음의 땅이라 들었습니다만... 이 빛은 죽음보다는 기다림에 가깝군요." |

---

### `dialogue_seq_hexer_memory_intro` — Grave Hexer 기억 소개

> **컨텍스트**: Grave Hexer가 자신의 기억 관리자 역할과 시간 감각의 괴리를 원정대에게 드러낸다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | casual | "400년 전 전쟁? 아, 그거. 화요일이었나. 아니, 수요일. ...어차피 상관없지만." |
| 1 | Grave Hexer | solemn | "기억은 내려놓을 수가 없어. 죽은 자가 지는 짐이야. 팔이 길어서 많이 드는 게 아니라, 내려놓는 법을 잊은 거지." |
| 2 | Dawn Priest | solemn | "산 자보다 오래 기억하는 분이시군요. 저희가 잊은 것을 당신은 아직 지고 계신 겁니까." |

---

### `dialogue_seq_boss_bark_ruined_crypts` — 침묵의 기록관 조우

> **컨텍스트**: 묘역 깊은 곳에서 '침묵의 기록관'이 가장 오래된 기억을 지키고 있다. 원정대가 기록에 접근하려면 기록관을 넘어야 한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | grim | "침묵의 기록관. 나보다 오래된 기억을 품고 있지. 열쇠를 안 넘기면 아무것도 못 꺼내." |
| 1 | Dawn Priest | weary | "기록을 지키는 자와 싸워야 한다니. 진실에 다가가는 길은 매번 이렇습니까." |

---

### `dialogue_seq_boss_defeat_ruined_crypts` — 침묵의 기록관 격파

> **컨텍스트**: 기록관이 쓰러지며 기억의 열쇠를 원정대에 넘긴다. 기록관의 마지막 행위는 공격이 아니라 인계였다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | gentle | "열쇠를 넘겼어. 마지막 순간에 우릴 인정한 거야. 기억을 넘길 자격이 있다고." |
| 1 | Dawn Priest | solemn | "수복자로 인정받은 것입니까. 이 무게를 받을 자격이 우리에게... 있을까요." |

---

### `dialogue_seq_bone_orchard_intro` — 유골 과수원 진입

> **컨텍스트**: 유골이 뿌리처럼 자라난 석화 과수원에 진입한다. 기억의 밀도가 묘역의 수십 배에 달하며, 봉인 격자 조각이 빛나기 시작한다. 공기가 두꺼워서 숨쉬기가 힘들다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | grim | "여기는 기억의 우물이 아니라 바다야. 밀도가 묘역의 수십 배. 조심해, 빠지면 못 나와." |
| 1 | Pack Raider | defiant | "뼈가 뿌리처럼 자란 나무라고? 코를 찡그리게 만드는 곳이군." |
| 2 | Dawn Priest | solemn | "격자 조각이 빛나고 있습니다. 무언가가 깨어나려 하고 있습니다." |

---

### `dialogue_seq_relicborn_awakening` — Relicborn 각성

> **컨텍스트**: 석화 과수원 중심부에서 봉인 격자 조각이 공명하고, Relicborn이 각성한다. Echo Savant가 처음 등장한다. 바닥의 격자문이 푸른 빛으로 타오르고, 석화된 나무 사이에서 한 존재가 천천히 눈을 뜬다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | shock | "격자가 깨어나고 있어. 이건 기억의 잔향이 아니야. 살아 있는 거야." |
| 1 | Echo Savant | disoriented | "...시끄럽다." |
| 2 | Dawn Priest | shock | "봉인의 수호자가 있었단 말입니까? 우리가 침략자라 부른 존재가... 이 세계를 지키고 있었다니." |
| 3 | Pack Raider | skeptical | "또 새 냄새야. 바람에도 없던 종류인데. 어깨를 으쓱할 건지, 경계할 건지 좀 고민되네." |

---

### `dialogue_seq_hexer_ancient_coexistence` — Grave Hexer 고대 공존

> **컨텍스트**: 기억 투사를 통해 고대에 네 종족이 적대감 없이 공존했던 시대의 기억이 드러난다. 빛의 환영 속에 네 종족이 나란히 물을 마시는 장면이 보인다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | gentle | "이 기억 봐. 네 종족이 같은 물을 마시고 있어. 전쟁 이전의 기억이야. 꽤 좋은 시절이었지." |
| 1 | Grave Hexer | bitter | "이 기억을 지운 게 왕국의 첫 번째 죄야. 공존의 증거를 묻고 적을 만들었어." |
| 2 | Pack Raider | solemn | "토템이 기억하고 있었어. 진동 속에 옛 냄새가 남아 있었는데... 이거였구나." |

---

### `dialogue_seq_boss_bark_bone_orchard` — 뿌리의 관망자 조우

> **컨텍스트**: 석화 과수원 중심의 보스 '뿌리의 관망자'. 1800년간 격자를 지켜온 존재다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | grim | "뿌리의 관망자. 1800년 동안 이 자리를 지킨 문지기야. 기억으로 무장한 존재지." |
| 1 | Dawn Priest | resolute | "설득이 안 된다면 쓰러뜨리겠습니다. 하지만 이 싸움은 정복이 아니라 이해를 위한 것입니다." |

---

### `dialogue_seq_boss_defeat_bone_orchard` — 뿌리의 관망자 격파

> **컨텍스트**: 관망자가 쓰러지고, 기억 투사를 통해 세 세력이 모두 Heartforge의 피해자였음이 드러난다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | bitter | "몰라서 그랬던 거야. 우리 모두. 서로를 적이라 믿은 건 진실이 아니라 무지였지." |
| 1 | Dawn Priest | weary | "인간도, 야수족도, 언데드도... 같은 상처를 안고 있었습니다. 같은 것에 당하고 있었습니다." |

---

### `dialogue_seq_savant_awakening` — Echo Savant 각성

> **컨텍스트**: Echo Savant가 긴 휴면에서 완전히 깨어나며, 격자의 상태를 처음으로 감지한다. 1800년 만에 뜬 눈이 초점을 못 잡는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | disoriented | "뼈가 울린다. 낮게. 1800년 전에 심은 것이 자라고 있다." |
| 1 | Echo Savant | grim | "나무였던 것이 뼈가 되었다. 1800년이면... 충분한 시간이지." |
| 2 | Grave Hexer | curious | "네 공명과 내 기억이 같은 파장이군. 1800년을 사이에 두고. 재밌네." |

---

### `dialogue_seq_savant_lattice_assessment` — Echo Savant 격자 진단

> **컨텍스트**: Echo Savant가 격자 네트워크의 현재 상태를 읽어내고, Heartforge의 본질을 설명한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | grim | "심장이 기억을 먹고 있다. 뱉어내는 것은 분노. 격자가 그것을 막고 있었다." |
| 1 | Echo Savant | solemn | "격자가 닳았다. 이제 분노가 새어나온다. 너희가 느낀 적대감... 너희 것이 아니었다." |
| 2 | Dawn Priest | shock | "우리 모두가 기계의 부산물에 조종당한 것입니까? 신앙도, 전쟁도... 전부?" |

---

### `dialogue_seq_midpoint_reveal` — 중반 반전

> **컨텍스트**: 네 시점 캐릭터가 처음으로 한 자리에 서서, 원정의 목표가 조사에서 순환의 종결로 전환되는 순간. 격자 빛이 네 사람을 동시에 비추고 있다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | resolute | "목적이 바뀌었다. 심장을 멈춰야 한다." |
| 1 | Dawn Priest | resolute | "왕국의 죄를 인정합니다. 그 위에서 도망치지 않겠습니다. 정화가 아니라 복원을 택하겠습니다." |
| 2 | Pack Raider | solemn | "씨족의 분노 너머를 보겠어. 땅만 되찾아서는 이 바람이 멈추지 않으니까." |
| 3 | Grave Hexer | resolute | "묻힌 것을 꺼내고, 빚을 갚아야지. 그게 내 몫이야." |

---

### `dialogue_seq_priest_midpoint_shock` — Dawn Priest 중반 충격

> **컨텍스트**: 중반 반전 직후, Dawn Priest가 개인적 충격을 토로한다. 무릎이 꺾일 듯 비틀거린다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | shock | "교리가... 기계의 배기가스 위에 세워져 있었습니다. 영원한 질서가... 영원한 기만..." |
| 1 | Dawn Priest | bitter | "사제로서 무엇을 지켜왔습니까. 정화라는 이름으로 자행한 것들을 생각하면... 손이 떨립니다." |
| 2 | Pack Raider | gentle | "부서진 것에서 새 뿌리가 나. 흙은 원래 죽은 것들로 만들어지거든. 사제, 네 흙도 나쁘지 않을 거야." |

---

## Chapter 4: Glass Forest — 유리의 숲

---

### `dialogue_seq_glass_forest_intro` — 유리의 숲 진입

> **컨텍스트**: 유리화된 숲에 도착한다. 결정이 된 거목들이 빛을 굴절시키고, 발 아래에서 유리가 서걱거리며 부서진다. 결정 안에 모든 세력의 전투 흔적이 얼어 있다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "격자가 폭발한 자리다. 전쟁이 유리가 되었다. 모든 것이 얼어 있다." |
| 1 | Pack Raider | bitter | "결정 안에 씨족 전사가 있어. 장례도 못 치른 채. 200년이 넘었는데 아직 여기 있다." |
| 2 | Dawn Priest | weary | "아름답고 잔혹합니다. 빛이 파편을 통과할 때마다 누군가의 마지막 표정이 비칩니다." |

---

### `dialogue_seq_boss_bark_glass_forest` — 프리즘 수호자 조우

> **컨텍스트**: 유리 숲의 보스 '프리즘 수호자'. 결정화된 격자 에너지로 이루어진 존재다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | grim | "폭발 때 격자가 응고된 존재다. 불안정하다. 건드리면 깨진다." |
| 1 | Pack Raider | defiant | "유리 속 전사들의 이름으로. 200년 묵은 장례를 치러줄게." |

---

### `dialogue_seq_boss_defeat_glass_forest` — 프리즘 수호자 격파

> **컨텍스트**: 수호자가 쓰러지며 격자 도관이 열린다. 도관은 왕국이 격자 파편을 빼간 통로였다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "도관이 열렸다. 파편이 여기서 빠져나갔다. 이것이 도난의 통로였다." |
| 1 | Dawn Priest | bitter | "왕국의 방어 장치가 이 숲에서 훔친 파편으로 만들어진 것이었습니다. 방어라는 이름의 약탈입니다." |

---

### `dialogue_seq_starved_menagerie_intro` — 굶주린 우리 진입

> **컨텍스트**: 유리 숲 깊은 곳의 동물 우리 잔해. 야수족이 기르던 생물들이 유리화되거나 변이했다. 우리 안에서 긁는 소리가 난다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | bitter | "이건... 우리가 돌보던 것들이었어. 먹이를 주고, 이름을 불러주던. 유리 속에서 아직 주인을 찾고 있어." |
| 1 | Grave Hexer | gentle | "변이체에 잔류 기억이 남아 있어. 주인 손길을 기억하고 있지. 오래 된 기억일수록 선명한 법이야." |
| 2 | Echo Savant | grim | "심장의 오염이 여기까지 왔다. 자연의 순환까지 뒤틀렸다." |

---

### `dialogue_seq_raider_glass_forest_grief` — Pack Raider 유리숲 애도

> **컨텍스트**: Pack Raider가 유리 속에 얼어붙은 옛 씨족 전사를 발견하고 애도한다. 결정에 손을 대고 오랫동안 아무 말도 하지 않다가 입을 연다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | bitter | "이름을 알아. 장로가 불러주던 이름이야. 200년 전에 죽었는데... 장례도 못 치렀어." |
| 1 | Pack Raider | solemn | "바람이 결정을 울려. 곡소리 같다. 씨족의 노래를 부르려는 건지." |
| 2 | Grave Hexer | gentle | "기억이 남아 있으면 장례는 아직 끝나지 않은 거야. 내가 증인이 되어줄게." |

---

### `dialogue_seq_hexer_memory_debt` — Grave Hexer 기억의 빚

> **컨텍스트**: 유리 숲에서 Grave Hexer가 왕국의 기억 소거 행위가 언데드 최대의 금기임을 밝힌다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | bitter | "기억을 지우는 것. 우리에겐 그게 가장 큰 죄야. 존재의 근본을 없애는 거니까." |
| 1 | Grave Hexer | grim | "왕국의 정화 의식이 딱 그 짓을 했어. 기억을 소거해서 이견을 없앴지. 우리의 금기를 너희가 무기로 쓴 거야." |
| 2 | Dawn Priest | weary | "부정할 수 없습니다. 정화 재판의 기록을 직접 보았으니까요." |

---

### `dialogue_seq_savant_resonance_adj` — Echo Savant 공명 조율

> **컨텍스트**: Echo Savant가 야수족 토템의 주파수를 격자에 맞추어 간섭을 제거한다. Pack Raider가 처음으로 맑은 의식을 체험한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "토템에 손을 대겠다. 잡음을 걷어낸다. 움직이지 마라." |
| 1 | Echo Savant | gentle | "간섭이 사라지면 증폭된 적대감도 사라진다. 본래 감각이 돌아올 것이다." |
| 2 | Pack Raider | shock | "...머릿속이 조용해졌어. 이게 본래 감각인 거야? 분노 없이 바람을 맡는 게... 이런 느낌이었어?" |

---

### `dialogue_seq_boss_bark_starved_menagerie` — 기아의 관리자 조우

> **컨텍스트**: 굶주린 우리의 보스 '기아의 관리자'. 변이된 거대 짐승으로, 주변의 생명력을 빨아들인다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | defiant | "우리가 기르던 것들의 이름으로. 이 울타리 안에서 끝내주마." |
| 1 | Echo Savant | grim | "주변이 차가워진다. 빨아들이고 있다. 빨리 끝내야 한다." |

---

### `dialogue_seq_boss_defeat_starved_menagerie` — 기아의 관리자 격파

> **컨텍스트**: 관리자가 쓰러지고 공명석이 드러난다. 순환 종결의 실마리가 된다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | solemn | "공명석이 나왔어. 이 돌이 울리는 소리가... 토템과 같다. 같은 뿌리에서 온 거야." |
| 1 | Echo Savant | solemn | "순환을 끊을 열쇠의 조각이다. 심부에서 필요할 것이다." |

---

## Chapter 5: Heartforge Descent — 심장로 하강

---

### `dialogue_seq_heartforge_gate_intro` — 심장로 관문 진입

> **컨텍스트**: Worldscar 입구의 거대한 석조 관문. 네 세력의 문양이 새겨져 있고, Heartforge의 맥동이 가슴팍까지 울린다. 심장박동처럼 규칙적이고, 점점 빨라진다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "관문에 네 세력의 문양이 있다. 이 문은 합의로만 열린다." |
| 1 | Dawn Priest | resolute | "왕국의 죄를 인정합니다. 도굴한 격자를 되돌리겠습니다. 이것이 제 참회입니다." |
| 2 | Pack Raider | solemn | "파편만 되찾아서는 안 돼. 격자 전체가 나아야 바람이 맑아져." |
| 3 | Grave Hexer | resolute | "기억의 보존과 정화가 양립하는지, 이 문 너머에서 확인해야지." |

---

### `dialogue_seq_boss_bark_heartforge_gate` — 영원의 대문장 조우

> **컨텍스트**: 관문의 보스 '영원의 대문장'. Heartforge가 자기 방어를 위해 만들어낸 기억의 무기다. 원정대의 가장 고통스러운 기억을 꺼내 환각으로 던진다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | resolute | "영원의 대문장. 이 순환이 만든 마지막 방벽입니다. 넘겠습니다." |
| 1 | Grave Hexer | grim | "기억을 무기로 쓰는 놈이야. 가장 아픈 기억을 꺼내 던질 거야. 마음 단단히 먹어." |

---

### `dialogue_seq_boss_defeat_heartforge_gate` — 영원의 대문장 격파

> **컨텍스트**: 대문장이 쓰러지고, 가장 고통스러운 기억을 넘어선 원정대 앞에 정화 코드가 노출된다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | weary | "가장 아픈 기억을 넘었습니다. 신앙의 무너짐도, 동료의 죽음도... 전부 직면했습니다." |
| 1 | Echo Savant | solemn | "길이 열렸다. 마지막 하강을 시작한다." |

---

### `dialogue_seq_worldscar_depths_intro` — 세계상흔 심부 진입

> **컨텍스트**: Heartforge가 있는 심부. 벽면에 3000년의 기억이 빛의 형태로 투사되어 있다. 중앙의 Heartforge가 심장처럼 뛴다. 맥동이 발바닥을 타고 올라온다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "3000년의 기억이 벽에 있다. 태초부터 지금까지. 전부." |
| 1 | Dawn Priest | weary | "가한 피해와 받은 피해가 동시에 보입니다. 왕국이 무엇을 했는지, 무엇을 당했는지... 전부." |
| 2 | Pack Raider | solemn | "바람의 기억도 여기 있어. 씨족이 자유로웠던 시절 냄새가 나. 이걸 되찾아야 해." |

---

### `dialogue_seq_priest_final_prayer` — Dawn Priest 최후의 기도

> **컨텍스트**: Heartforge 앞에서 Dawn Priest가 마지막 기도를 올린다. 영원한 질서의 기도가 아니라, 새로운 형태의 기도. 사제 인장을 내려다보며 시작한다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | solemn | "영원한 질서는 없었습니다. 위에서 내리는 질서는 언제나 누군가의 등뼈 위에 서 있었습니다." |
| 1 | Dawn Priest | resolute | "그래도 기도할 수 있습니다. 질서란 서로가 서로를 지탱하는 것이라면, 이 격자가 바로 그 기도입니다." |
| 2 | Dawn Priest | gentle | "부서진 신앙 위에 새 질서를 세우겠습니다. 이번에는 위에서 내리는 것이 아니라, 함께 세우는 것으로." |

---

### `dialogue_seq_raider_oath_swear` — Pack Raider 맹세

> **컨텍스트**: Pack Raider가 씨족의 전통에 따라 이빨을 보이며 서약한다. 야수족에게 이빨을 보이는 것은 신뢰의 최고 표현이다. 천천히 입술을 걷어 송곳니를 드러낸다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | resolute | "이빨을 보인다. 씨족에서 이건 가장 높은 서약이야. 이 순환을 끝내겠다고, 피와 바람에 맹세해." |
| 1 | Pack Raider | solemn | "바람이 다시 깨끗해질 때까지. 사냥터에 피가 아니라 이슬이 맺힐 때까지." |
| 2 | Pack Raider | gentle | "끝나면 씨족에 돌아가서 전할 거야. 적이라 믿었던 것들이 같은 상처를 안고 있었다고." |

---

### `dialogue_seq_hexer_final_testimony` — Grave Hexer 최후의 증언

> **컨텍스트**: Grave Hexer가 자신의 가장 오래된 기억이 봉인 실패의 순간 것임을 확인하고, 기억의 빚에 대한 답을 찾는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | solemn | "이 벽면에 내 첫 기억이 있어. 봉인이 실패한 순간. 내 존재의 시작이 바로 그 파국이었어." |
| 1 | Grave Hexer | gentle | "기억은 이제 빚이 아니야. 유산이지. 잊어야 할 게 아니라 전해야 할 거야." |
| 2 | Grave Hexer | resolute | "모든 세력에 전하겠어. 묻힌 진실을, 공존의 기억을. 최근 일이니까. 400년 정도 됐나? 뭐, 최근이야." |

---

### `dialogue_seq_savant_seal_decision` — Echo Savant 봉인 결단

> **컨텍스트**: Echo Savant가 Heartforge 정화/봉인의 결단을 내린다. 파괴가 아닌 정화를 선택한다. 격자에 손을 대고 눈을 감는다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | solemn | "정화하면 기억은 남는다. 파괴하면 전부 사라진다. 선택은 하나뿐이다." |
| 1 | Echo Savant | resolute | "심부에 남겠다. 봉인을 유지한다. 수문장의 역할이다." |
| 2 | Echo Savant | gentle | "공명은 끝나지 않는다. 형태만 바뀐다. 적대의 공명이 멈추면, 다른 것이 시작된다." |

---

### `dialogue_seq_boss_bark_worldscar_depths` — 순환의 핵 조우

> **컨텍스트**: Heartforge가 자기 방어를 위해 축적한 적대감의 결정체. 3000년의 증오가 응축된 최종 적대체다. 형체가 없고, 검은 빛만 맥동한다. 네 사람의 심장박동이 동시에 빨라진다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Echo Savant | grim | "3000년의 적대감이 응축된 것이다. 모든 전쟁의 씨앗이다." |
| 1 | Dawn Priest | resolute | "이 순환을 끝냅니다. 신앙이 아니라 의지로." |
| 2 | Pack Raider | defiant | "발톱을 세운다. 씨족만을 위해서가 아니야. 모든 사냥터를 위해서다." |
| 3 | Grave Hexer | resolute | "3000년의 빚을 오늘 갚겠어. 여기서." |

---

### `dialogue_seq_final_boss_defeat` — 최종 보스 격파

> **컨텍스트**: 순환의 핵이 쓰러진다. Heartforge의 맥동이 느려지고, 격자가 복원되기 시작한다. 벽면의 기억 투사가 적대에서 공존으로 바뀐다. 공기가 달라진다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | weary | "멈추었습니다. 심장이 고요해지고 있습니다. 3000년의 전쟁이... 끝났습니다." |
| 1 | Echo Savant | solemn | "격자가 이어지고 있다. 매듭이 하나씩. 봉인이 다시 작동한다." |
| 2 | Pack Raider | gentle | "바람이 맑아졌어. 피 냄새가 아니라 이슬 냄새가 나. 이게... 본래의 바람이었구나." |
| 3 | Grave Hexer | gentle | "벽면의 기억이 바뀌고 있어. 전쟁 위에 우리가 함께 싸운 기억이 덧씌워지고 있지. 새 유산이야." |

---

### `dialogue_seq_campaign_complete` — 캠페인 완료

> **컨텍스트**: Worldscar를 빠져나온 원정대. 좁은 틈 사이로 하늘이 보이고, 재가 아니라 빛이 내려온다. 네 사람이 나란히 서서 하늘을 올려다본다.
> **연출**: story-card

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | resolute | "부서진 신앙 위에 새 의미를 세우겠습니다. 질서란 격자처럼 서로 지탱하는 것임을 전하겠습니다." |
| 1 | Pack Raider | gentle | "씨족에 돌아간다. 같은 상처를 안고 있었다고, 이빨을 보이며 전할 거야." |
| 2 | Grave Hexer | resolute | "묻힌 진실을 전하겠어. 무덤에 둘 게 아니라 다리로 만들어야 하니까." |
| 3 | Echo Savant | solemn | "심부에 남는다. 공명은 끝나지 않는다. 수문장의 역할도." |

---

## Town Returns — 마을 귀환

---

### `dialogue_seq_town_return_ch1` — 1장 이후 귀환

> **컨텍스트**: Ashen Gate와 Wolfpine Trail을 마치고 마을에 돌아온 직후. 야수족과의 첫 동맹이 성립된 상태.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | weary | "문 밖의 세계는 예상과 달랐습니다. 성유물이 무기였고, 적이라 부른 자들은 기도하고 있었습니다." |
| 1 | Pack Raider | skeptical | "인간의 마을이라. 콧바람을 불고 싶지만, 지금은 참아볼게." |

---

### `dialogue_seq_town_return_ch2` — 2장 이후 귀환

> **컨텍스트**: Sunken Bastion과 Tithe Road를 마치고 마을에 돌아온 직후. 왕국의 격자 오용과 정화 재판의 실체가 밝혀진 상태.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | bitter | "같은 신도가 같은 신도를 매장했습니다. 이 마을의 성물함도 확인해야 합니다." |
| 1 | Pack Raider | solemn | "요새 돌담에서 피 냄새가 났어. 여기서도 날지 모르지만, 지금은 입꼬리를 접어둘게." |

---

### `dialogue_seq_town_return_ch3` — 3장 이후 귀환

> **컨텍스트**: Ruined Crypts와 Bone Orchard를 마치고 마을에 돌아온 직후. Relicborn 각성과 중반 반전이 일어난 상태.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Grave Hexer | casual | "기억 보관소를 열었어. 3000년치가 쏟아졌지. 이 마을 사람들도 알아야 할 이야기야. 좀 길겠지만." |
| 1 | Echo Savant | grim | "격자가 닳고 있다. 시간이 많지 않다." |
| 2 | Dawn Priest | resolute | "목적이 바뀌었습니다. 조사에서 순환 종결로. 마을에 보고하겠습니다." |

---

### `dialogue_seq_town_return_ch4` — 4장 이후 귀환

> **컨텍스트**: Glass Forest와 Starved Menagerie를 마치고 마을에 돌아온 직후. 네 세력의 공감 기반이 형성된 상태.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Pack Raider | solemn | "유리 숲의 전사들을 기억한다. 200년 묵은 장례를 치렀어. 바람에게 이름을 불러줬다." |
| 1 | Echo Savant | resolute | "공명석을 얻었다. 최종 하강 준비를 시작해야 한다." |

---

### `dialogue_seq_town_return_ch5` — 5장 이후 귀환 (캠페인 완료)

> **컨텍스트**: Heartforge 정화 후 마을에 돌아온 직후. 순환이 종결되고 세계의 수습이 시작되는 단계. 바람이 다르다.
> **연출**: dialogue-overlay

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Dawn Priest | gentle | "순환이 끝났습니다. 하지만 수습은 이제부터입니다. 부서진 것을 함께 세우는 일이 남았습니다." |
| 1 | Pack Raider | gentle | "바람이 달라졌어. 깨끗하다. 돌아가면 새끼들에게 이 바람을 맡게 해줄 거야." |
| 2 | Grave Hexer | resolute | "기억의 유산을 전할 준비가 됐어. 묻힌 진실이 다리가 되는 날이 왔지. 드디어." |
