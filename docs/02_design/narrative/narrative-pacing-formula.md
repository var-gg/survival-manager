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
|---|---|---|
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

```
T_site = [28, 36, 44, 53, 63, 74, 83, 89, 94, 98][S]
T_node = {1: 0, 2: 5, 3: 12, 4: 22, 5: -18}[n]
T(S,n) = clamp(T_site + T_node, 0, 100)
```

- site 진행에 따라 기저 긴장 상승. node4=boss에서 국부 peak, node5=extract에서 즉시 하강.

### Relief

```
R_site = [69, 66, 62, 58, 54, 49, 45, 42, 40, 38][S]
R_node = {1: 0, 2: -6, 3: -15, 4: -29, 5: +34}[n]
R(S,n) = clamp(R_site + R_node, 0, 100)
```

- **규칙: 모든 boss 뒤 1노드 이내에 `R >= 70`인 이완 구간이 반드시 존재해야 한다.**

### Humor

```
H_site = [8, 8, 6, 6, 4, 4, 2, 2, 0, 0][S]
H_node = {1: 0, 2: 2, 3: 1, 4: -2, 5: +10}[n]
H(S,n) = clamp(H_site + H_node, 0, 30)
```

- grim tone을 깨지 않는 `gallows wit`만 허용. final 20%에서 humor는 거의 0.

### Catharsis

```
if n in {1,2}: C(S,n) = 2 + S
if n == 3:     C(S,n) = 8 + 2*S
if n == 4:     C(S,n) = C_boss[S]
if n == 5:     C(S,n) = C_release[S]

C_boss    = [32, 60, 40, 62, 48, 72, 56, 80, 86, 100]
C_release = [20, 38, 24, 40, 28, 56, 32, 64, 72, 92]
```

- final boss=`100`, final extract=`92`로 peak-end anchor 고정.

### 난이도

```
D_site = [1.4, 2.0, 2.7, 3.3, 4.0, 4.6, 5.3, 5.9, 6.6, 7.2][S]
D_node = {1: 0.0, 2: 0.3, 3: 0.8, 4: 1.5, 5: -0.2}[n]
D(S,n) = D_site + D_node
```

### 신규 콘텐츠 도입 밀도

```
NoveltyDensity(P) =
  1.0  if 0.00 <= P < 0.40   # 매 사이트마다 새 얼굴/새 규칙
  0.5  if 0.40 <= P < 0.80   # 1~2사이트마다 새 메카닉
  0.0  if 0.80 <= P <= 1.00  # 새 base system 금지
```

### 영웅 합류

```
HeroJoin(S) = 1 if S in {2,3,4,5,6,7,8,9} else 0
```

- start roster 12명 + site 2~9에서 1명씩 합류 = 총 20명.

## 공식-근거 매핑

| 공식/규칙 | 설계 이유 |
|---|---|
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
|---|---|---|---|---:|---:|---:|
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
|---|---:|---|
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

```
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

```
Resolved(main_conflict)         = 1.00
Resolved(surface_faction_wars)  >= 0.80
Resolved(setting_mystery)       ~= 0.70

OpenSecondaryConflicts = 2
OpenExternalHooks      = 1
OpenHooksTotal         = 3
```

- 메인 갈등은 Heartforge 봉인/정화/정지로 완결한다.
- 금지: "진짜 최종보스는 DLC/후속작"형 엔딩.

## 작성 지침

- 모든 수치는 `chapter-beat-sheet.md`와 일치해야 한다.
- 정성적 문장보다 정량 표를 우선한다.
- 모든 공식은 최소 1개의 external evidence를 참조한다.
