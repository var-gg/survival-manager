# 내러티브 페이싱 공식

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/narrative/narrative-pacing-formula.md`
- 관련문서:
  - `docs/02_design/narrative/campaign-story-arc.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`
  - `docs/02_design/meta/story-gating-and-unlock-rules.md`

## 목적

캠페인 전체와 site/node 단위의 감정 곡선, reveal timing, reward cadence를 수식과 표로 고정한다. 이 문서는 narrative pacing의 유일한 source of truth다.

## 채택 모델

`Freytag macro + Ryan epic plot premise + 5-node mini arc`를 선택했다.

- **Freytag macro**: 5챕터에 exposition→rising action→climax→falling action→denouement를 자연스럽게 대응할 수 있다.
- **epic plot premise**: hostile world survival/expedition이라는 게임 전제가 Ryan의 interactive narrative 분류와 맞는다.
- **5-node mini arc**: 기존 site rhythm(5노드 1사이트)을 그대로 활용해 site마다 국부적 긴장-이완을 만든다.

비채택 모델: Hero's Journey(ensemble squad와 맞지 않음), 기승전결(grim expedition의 지속 conflict/boss cadence에 약함), 3막 구조(falling action/denouement가 약해짐).

## 변수 정의

| 기호 | 의미 | 범위 |
| --- | --- | --- |
| `N_total` | 전체 스토리 노드 수 | `50` |
| `S` | site index | `1..10` |
| `n` | node index within site | `1..5` |
| `P` | 전체 진행률 `cleared_nodes / 50` | `0.00..1.00` |
| `T` | Tension | `0..100` |
| `R` | Relief | `0..100` |
| `H` | Humor | `0..30` |
| `C` | Catharsis | `0..100` |
| `D` | 전투 난이도 지수 | `1.0..9.0` |

## 공식 목록

수식과 테이블이 충돌하면 **테이블이 우선**한다.

### Tension

```text
T_site = [28, 36, 44, 53, 63, 74, 83, 89, 94, 98][S]
T_node = {1: 0, 2: 5, 3: 12, 4: 22, 5: -18}[n]
T(S,n) = clamp(T_site + T_node, 0, 100)
```

- site 진행에 따라 기저 긴장 상승. node4=boss에서 국부 peak, node5=extract에서 즉시 하강.

### Relief

```text
R_site = [69, 66, 62, 58, 54, 49, 45, 42, 40, 38][S]
R_node = {1: 0, 2: -6, 3: -15, 4: -29, 5: +34}[n]
R(S,n) = clamp(R_site + R_node, 0, 100)
```

- **규칙: 모든 boss 뒤 1노드 이내에 `R >= 70`인 이완 구간이 반드시 존재해야 한다.**

### Humor

```text
H_site = [8, 8, 6, 6, 4, 4, 2, 2, 0, 0][S]
H_node = {1: 0, 2: 2, 3: 1, 4: -2, 5: +10}[n]
H(S,n) = clamp(H_site + H_node, 0, 30)
```

- grim tone을 깨지 않는 `gallows wit`만 허용. final 20%에서 humor는 거의 0.

### Catharsis

```text
if n in {1,2}: C(S,n) = 2 + S
if n == 3:     C(S,n) = 8 + 2*S
if n == 4:     C(S,n) = C_boss[S]
if n == 5:     C(S,n) = C_release[S]

C_boss    = [32, 60, 40, 62, 48, 72, 56, 80, 86, 100]
C_release = [20, 38, 24, 40, 28, 56, 32, 64, 72, 92]
```

- final boss=`100`, final extract=`92`로 peak-end anchor 고정.

### 난이도

```text
D_site = [1.4, 2.0, 2.7, 3.3, 4.0, 4.6, 5.3, 5.9, 6.6, 7.2][S]
D_node = {1: 0.0, 2: 0.3, 3: 0.8, 4: 1.5, 5: -0.2}[n]
D(S,n) = D_site + D_node
```

### 신규 콘텐츠 도입 밀도

```text
NoveltyDensity(P) =
  1.0  if 0.00 <= P < 0.40   # 매 사이트마다 새 얼굴/새 규칙
  0.5  if 0.40 <= P < 0.80   # 1~2사이트마다 새 메카닉
  0.0  if 0.80 <= P <= 1.00  # 새 base system 금지
```

### 영웅 합류

```text
HeroJoin(S) = 1 if S in {2,3,4,5,6,7,8,9} else 0
```

- start roster 12명 + site 2~9에서 1명씩 합류 = 총 20명.

## 공식-근거 매핑

| 공식/규칙 | 설계 이유 |
| --- | --- |
| `Freytag macro` | 선형 완결 캠페인에 가장 자연스럽다 |
| `epic plot premise` | Darkest Dungeon 계열 루프와 전제가 맞는다 |
| `oscillating T/R` | 단조 상승선보다 긴장 유지와 회복이 낫다 |
| `major reversal = 56~62%` | curiosity와 suspense를 재점화한다 |
| `boss 후 1노드 이내 relief` | peak-end와 대비 기억을 강화한다 |
| `final boss=100 / final extract=92` | peak와 ending이 경험 평가에 크게 작동한다 |
| `site 30±5분` | run-sized expedition chunk로 적절 |
| `첫 15~20분 내 첫 payoff` | first-session retention 상관 |
| `NoveltyDensity 감소` | mastery와 challenge-skill balance 유지 |

## 진행률 구간 표

| band_id | progress_range | freytag_stage | must_have_beat | T_target | R_target | C_target |
| --- | --- | --- | --- | ---: | ---: | ---: |
| `band_hook` | `0.00~0.10` | Exposition | 세계 위협 제시 | 28~50 | 40~100 | 0~20 |
| `band_promise` | `0.10~0.20` | Promise | 야수족 첫 오해/동맹, specialist unlock | 36~58 | 37~100 | boss>=60 |
| `band_rising_1` | `0.20~0.40` | Rising Action I | 인간 culpability 노출 | 44~75 | 감소 | — |
| `band_rising_2` | `0.40~0.56` | Rising Action II | 언데드 기억, 유물 신호 | 63~88 | — | — |
| `band_midpoint` | `0.56~0.62` | Major Reversal | Relicborn reveal | 80~96 | 강제 | 급상승 |
| `band_crisis` | `0.62~0.82` | Crisis | full roster mastery test | 88~94+ | — | — |
| `band_convergence` | `0.82~0.92` | Convergence | 4세력 수렴, 결전 서약 | ~99 | — | 72~86 |
| `band_climax` | `0.92~0.98` | Climax | 최종 적대체 직전 | 100 | 최소 | — |
| `band_denouement` | `0.98~1.00` | Denouement | main conflict closure | — | 72 | 92 |

## 세션 / 보상 공식

| 항목 | 목표값 | 규칙 |
| --- | ---: | --- |
| 첫 앉은자리 길이 | `15~20분` | tutorial half-site + first extraction까지 |
| 표준 site 세션 | `30±5분` | 5노드 1사이트를 1회 플레이 단위로 본다 |
| 노드당 체류 | `6±1분` | auto-battle 기준 pre/post UI 포함 |
| minor reward 간격 | `<= 1노드` | 전투 후 즉시 loot/offer/codex/toast 중 최소 1개 |
| major reward 간격 | `= 1사이트` | extract 시 hero unlock, augment, story card 중 최소 1개 |
| 신규 영웅 간격 | `1사이트` | site 2~9에서 1명씩 |
| 신규 메카닉 간격 | 초반 1사이트, 중반 1~2사이트, 후반 0 | `NoveltyDensity(P)` 적용 |
| 신규 base system 종료점 | `P >= 0.80` | final mastery 보존 |

## 캐릭터 서사 비중 공식

총 로스터 20명 기준, character-authored beat budget `56`.

```text
BeatBudget(hero) =
  6 if tier == lead
  3 if tier == support
  1 if tier == background
```

- `lead = 4명 x 6 = 24`
- `support = 8명 x 3 = 24`
- `background = 8명 x 1 = 8`
- 총합 `56`

선정 규칙:
1. 네 종족에서 최소 1명의 lead를 둔다.
2. lead tier는 chapter 1~5 전체에 재등장한다.
3. specialist는 대부분 background/support에 머문다.
4. Relicborn는 midpoint 이후 급부상하므로 최소 1명의 lead가 필요하다.

권장 lead: `hero_dawn_priest`, `hero_pack_raider`, `hero_grave_hexer`, `hero_echo_savant`.

## DLC / 후속작 확장 공식

```text
Resolved(main_conflict)         = 1.00
Resolved(surface_faction_wars)  >= 0.80
Resolved(setting_mystery)       ~= 0.70

OpenSecondaryConflicts = 2
OpenExternalHooks      = 1
OpenHooksTotal         = 3
```

- 메인 갈등은 Heartforge 봉인/정화/정지로 완결한다.
- 금지: "진짜 최종보스는 DLC/후속작"형 엔딩.

## 장면 문법 공식

T/R/H/C는 장면의 **온도**를 설계한다. 하지만 좋은 장면은 온도가 아니라 **벡터**로 살아난다 — 누가 누구에게 다가가고, 밀어내고, 포기하고, 역전하는지. 아래 공식은 대사 생성 시 LLM이 장면을 "정보 전달 단위"가 아닌 "상태 변화 단위"로 쓰게 강제하는 규칙이다.

### ArcStep — 리드 캐릭터 아크 상태 머신

BeatBudget은 등장 분량을 관리하지만, 변화 밀도는 관리하지 않는다. ArcStep은 각 리드가 **어떻게 변하는가**를 모델링한다.

```text
ArcStep(hero, chapter) ∈ { defense, crack, confession, redefine, commit }

규칙:
  - 각 리드는 chapter 1개당 최소 1회 상태 이동
  - 이전 단계를 건너뛸 수 없음 (defense → commit 직행 금지)
  - 같은 단계에 2챕터 이상 머물면 WARNING
```

| hero | ch1 | ch2 | ch3 | ch4 | ch5 |
| --- | --- | --- | --- | --- | --- |
| Dawn Priest | defense (교리적 확신) | crack (도굴품 발견) | confession (교리가 배기가스) | redefine (질서 없는 기도) | commit (새 의미의 사제) |
| Pack Raider | defense (인간 불신) | defense→crack (인간도 피해자) | crack (본능이 조작) | redefine (영역 너머를 봄) | commit (분열된 씨족으로 귀환) |
| Grave Hexer | — | — | defense→crack (기억 보존의 한계) | confession (기억이 빚) | commit (기원 기억을 바침) |
| Echo Savant | — | — | defense (수문장 사명) | crack→confession (봉인이 누수) | commit (심부 잔류) |

### RelationDelta — 사이트별 관계 변화

```text
RelationDelta(pair, site) ∈ { -2, -1, 0, +1, +2 }

규칙:
  - 사이트 종료 시 최소 1개 리드 pair의 값이 ±1 이상 변해야 함
  - 0인 pair가 3사이트 연속이면 WARNING — 관계가 정체됨
  - 변화의 원인은 정보 습득이 아니라 상호 행동이어야 함
```

### SceneTurn — 장면 내부 문법

모든 `dialogue-scene`은 아래 4슬롯을 반드시 포함해야 한다. 슬롯이 빠지면 장면이 "요약"으로 수축한다.

```text
SceneTurn(scene) = {
  hook:       누군가가 무언가를 원한다 (비난/고백/경고/조롱/부탁/유혹)
  resistance: 다른 누군가가 그것을 막는다 (거부/반박/침묵/회피)
  proof:      새 증거/상처/비밀이 들어와 균형을 깨뜨린다
  changed:    장면 시작과 끝에서 적어도 하나의 관계/믿음/상태가 다르다
}

규칙:
  - 첫 대사는 요약 금지. hook 행위로 시작
  - 마지막 대사는 결론문 금지. changed state로 종료
  - resistance가 없으면 갈등이 아니라 브리핑
  - proof가 없으면 정보 교환이 아니라 수다
```

### RevealCost — 발견에는 반드시 비용이 따른다

```text
RevealCost(site) =
  major reveal이 있는 사이트는 같은 사이트 안에
  { 맹세 | 상실 | 관계 균열 | 희생 | 자기고발 } 중 1개 이상 포함

규칙:
  - 정보와 비용이 같은 사이트에 존재해야 함
  - 정보만 전달하고 비용이 다음 사이트로 밀리면 reveal의 무게가 증발
  - 비용은 추상적 선언이 아니라 구체적 행동/상실이어야 함
```

### SubtextRatio — 설명 대사 제한

```text
SubtextRatio(scene) =
  설정 핵심명사를 직접 말하는 줄 <= 40%
  몸/사물/감각/개인 상처로 말하는 줄 >= 60%

핵심명사: Heartforge, 격자, 정화, 봉인, 순환, 질서, 공명, 기억
```

- 이 비율을 지키면 thesis-speech가 줄고 subtext가 늘어난다.
- LLM은 제한 없으면 가장 안전한 형식(설정 설명형 문장)으로 수렴한다. 이 규칙이 그 수렴을 차단한다.

### ObjectAnchor — 장면의 물건

```text
ObjectAnchor(scene) =
  모든 dialogue-scene은 구체적 사물 최소 1개를 장면 중심에 둔다

허용 사물: 묵주, 토템, 파편, 성물함, 기억 용기, 격자 문양,
          재, 피, 뼈, 갈퀴 자국, 인장, 기도문 조각, 일지,
          유리 결정, 공명석, 잔향 장치
```

- 사물이 없으면 장면이 추상 공간에서 부유한다. 플레이어의 기억에 남지 않는다.
- 좋은 대사는 대체로 사물에 붙어 있고, 나쁜 대사는 추상 명사에 떠 있다.

### QuestionDebt — 미스터리 부채 관리

```text
QuestionDebt(chapter) =
  새 큰 질문을 2개 열면, 기존 질문 1개는 반드시 상환

규칙:
  - 열린 질문이 5개를 넘으면 플레이어가 추적을 포기한다
  - 상환은 완전 해명이 아니어도 됨 — 부분 답변도 1회 상환으로 인정
  - 질문 목록은 chapter-beat-sheet.md에서 관리
```

### NarratorConstraint — 나레이터 제한

```text
NarratorConstraint =
  narrator는 감정 해설 금지
  narrator는 관찰 가능한 동작/사물/환경만 묘사
  금지어: "무겁다", "무섭다", "슬프다", "가장 ~한", "~인 문장", "~의 의미"

규칙:
  - narrator가 장면의 무게를 선포하면 캐릭터의 감정이 약해진다
  - "가장 무거운 문장"이 아니라 "아무도 바로 대답하지 않는다"가 낫다
  - narrator의 역할: 물리적 현재를 보여주는 것. 의미를 해석하는 것이 아님
```

## 대사 생성 지침

`master-script.md` 생성 시 LLM에게 반드시 전달해야 하는 작문 규칙이다. 장면 문법 공식이 "무엇을 넣어라"를 말한다면, 이 지침은 "어떻게 쓰라"를 말한다.

1. **설계 문서에 그대로 들어가도 자연스러운 문장은 금지.** 문장이 결론문처럼 들리면 삭제 후 재작성.
2. **장면 첫 대사는 요약 금지.** 첫 줄은 `비난 | 고백 | 경고 | 조롱 | 유혹 | 부탁` 중 하나로 시작.
3. **모든 dialogue-scene에는 ObjectAnchor 규칙 적용.** 구체적 사물 1개 이상 필수.
4. **narrator는 NarratorConstraint 적용.** 감정 해설 금지, 관찰 가능한 동작만.
5. **같은 장면에서 같은 인물이 두 번 연속 정답 설명 금지.** 한 번 설명했으면 다음 줄은 방어/회피/반격/침묵.
6. **장면 마지막 줄은 요약 금지.** `상태 변화 | 침묵 | 맹세 | 떠남 | 물건 변화` 중 하나로 종료.
7. **설정 핵심명사 반복 제한.** SubtextRatio 규칙 적용. 한 scene 내 핵심명사 직접 언급 3회 이하.
8. **major reveal 직후에는 후유증 1비트 필수.** 바로 다음 exposition으로 넘어가지 않는다.
9. **각 사이트마다 "스크린샷될 한 줄" 1개 목표.** 기억에 남을 line density 강제.
10. **연속 lore scene 금지.** reveal 장면 다음에는 reaction 비트를 둔다.

### 리드별 음성 제한

| hero | 허용 어휘군 | 금지 패턴 | 감정 붕괴 시 변화 |
| --- | --- | --- | --- |
| Dawn Priest | 사물/의식어: 묵주, 성반, 인장, 성물함, 재, 기도문 | 범용 결의문 ("끝내야 합니다"), reveal 결론을 직접 선언 | 문장이 부서짐. self-correction, 끊김, 말의 실패 |
| Pack Raider | 감각어: 바람, 흙, 냄새, 이빨, 피, 발톱 | 냄새/바람 은유 남용 (같은 비유 반복), 사이트당 동일 감각 은유 2회 초과 | 은유가 사라짐. 직설적이고 짧아짐 |
| Grave Hexer | 기억 관리어: 분류, 퇴적, 잔향, 오기, 누락, 보관 | 순수 gag (상황 무게를 해제하는 웃음), 유머 연속 2회 | 유머가 사라지고 건조함만 남음. 전문가의 패배 |
| Echo Savant | 기술/감각어: 누수, 잔향, 파장, 공명, 진동, 열기 | 기술설명 연쇄 (한 scene 내 기술 은유 2회 초과), lore terminal 역할 | 인간적 기억 파편이 누출. 촉감, 열기, 오래된 목소리 |
| Aldric | 관리/유지어: 통제, 유지, 설계, 작동, 균형 | 분노 독백, 광기 표현 | 변하지 않음. 끝까지 calm, clerical. 이것이 공포의 원천 |

## 작성 지침

- 모든 수치는 `chapter-beat-sheet.md`와 일치해야 한다.
- 정성적 문장보다 정량 표를 우선한다.
- 모든 공식은 최소 1개의 external evidence를 참조한다.
- 장면 문법 공식(ArcStep, SceneTurn, RevealCost 등)은 대사 생성 시 T/R/H/C와 동등한 구속력을 갖는다.
