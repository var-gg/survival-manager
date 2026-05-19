---
name: worldscale-and-voice-pass
default_scope:
  areas: [character-lore, narrative-script, content]
  include_superseded: false
  exclude_types: [Task]
description: 두 축의 거시·미시 narrative 검수. (A) ch1-5 = 거대 세계의 한 site임을 유저가 느끼게 하는 lore frame 설계 + 자연스러운 비트 제안. (B) 서브컬쳐 voice/페르소나 무너지는 부분 교정. 정합과 대사 맛 둘 다 절대 깨지 않는 선에서.
output_format: _output_format.md
---

# Scenario: worldscale-and-voice-pass

이전 `narrative-consistency-fix`가 line-level patch였다면, 본 시나리오는 **거시 lore architecture + 미시 voice pass**의 결합이다. 사용자 요청 의도:

1. ch1-5 = 한 변방 site의 정치 정착 이야기 (이미 wiki bible에 settled baseline). 출시 시 유저가 "세계관 자체가 엄청 크구나"라는 인식을 받을 수 있게, ch1-5 안에 거대 세계 hint를 자연스럽게 깔자. 정합 깨거나 대사 맛 해치면 안 됨.
2. 동시에 전체 대사 톤이 서브컬쳐로 아쉬운 부분, 페르소나 무너지는 부분 교정안.

scope에 `content` area 추가가 핵심 — `wiki-world-building-bible-mirror`, `narrative-faction-conflict-driver-v1`, `analysis-historical-truth-layer` 등 거시 lore baseline 문서가 거기 있다.

## 조립 규칙 (Claude용 메타)

- `default_scope.areas` 세 area를 pack_wiki.py로 전부 dump (Task 제외)
- placeholder 치환:
  - `{{focus_block}}` — 사용자 `--extra "..."` 시 `[FOCUS]\n{value}\n`, 없으면 빈 문자열
  - `{{bundle_summary}}` — 첨부 artifact 개수와 area 분포 한 줄
  - `{{output_format_block}}` — `scenarios/_output_format.md` 그대로

## Prompt body

```text
[CONTEXT]
이 채팅은 survival-manager 프로젝트의 거시·미시 narrative 검수 작업이다.

프로젝트 성격:
- Unity prototype phase의 캐릭터 수집/성장 RPG. 자동전투 + 짧은 호흡 + 인간형 적군 only. 서브컬쳐 감성 정면 흡수.
- 인디 1인 개발 ($10 가격대). 음성 70-110 line, cutscene 12-15, 신규 모델/텍스처 commission 0.
- 캐릭터 voice/관계망/세력 갈등이 narrative core. 단일 캠페인 RPG 아님 — 출시 후 캐릭터 수집/성장이 운영 축.

narrative baseline (첨부 content area 문서로 확인):
- 캠페인 ch1-5 = **첫 도시 입구 주변의 한 변방 site에서 일어난 정치 정착**. 세계 전체의 전쟁 종결이 아님. 4 세력이 학살 기록을 어떻게 다룰지 처음으로 같은 테이블에 앉는 이야기 (wiki-world-building-bible-mirror, wiki-campaign-story-arc-mirror).
- 1800년 전 첫 도시 학살이 모든 4 세력 정체성의 baseline (analysis-historical-truth-layer, narrative-faction-conflict-driver-v1). 4 세력 모두 도시 시조의 후예 + 학살 정당화/회피 어휘의 후예.
- 분산 행위성 모델 — 단일 거대 악 폐기, 각자 자기 노선으로 행동 (analysis-narrative-mechanism-redesign-draft).
- ch1-5 결말 baseline: `일부 공유` — 4 세력 대표가 학살 기록 일부만 외부 공개, 원문 보존, 점진 공개. 약한 회담 시작 + 빈 의자 두 개.

사용자 요청 (의도):
1. 출시 때 유저가 **"세계관 자체가 엄청 크구나"** 라는 인식을 갖게 만들고 싶다. 삼국지로 치면 오나라 어느 지역 한 사건 정도라는 느낌. ch1-5가 거대 세계의 한 단면임을 자연스럽게 비치는 솔루션.
2. **정합 절대 깨지 마라** — 기존 wiki baseline 모순 금지, 새 캐릭터/사건/조직 도입 최소(필요 시만 + 후속 wiki publish 가능한 형태로).
3. **대사 맛 절대 해치지 마라** — exposition dump 금지. lore hint는 캐릭터 voice 안에서 자연. 짧고 미세하게.
4. 동시에 전체 대사 톤이 서브컬쳐로 아쉬운 부분, 캐릭터 페르소나/voice 무너지는 부분 교정.

첨부 wiki dump:
- {{bundle_summary}}
- content area: 세계관 바이블 + 캠페인 스토리 아크 + historical truth layer + faction conflict driver + 분산 행위성 모델 + 기타 reader copy
- character-lore area: 22 캐릭터 wiki(외모/성격/작중 행적/명대사/평가) + emotion layer (voice motif spec) + operation layer + registry + affinity spec 12
- narrative-script area: prologue + ch1-5 chapter scripts + town conversation scripts + memorial cutscene + master-script retcon/draft + dialogue craft guideline v3 + dialogue reader pass + dialogue fun pass + hex micro-beat spec

{{focus_block}}

[TASK]
두 축으로 검수하라. 한 응답 안에 두 PART를 명확히 분리.

PART A — 거시 세계관 frame 설계 + ch1-5 안에 자연스럽게 깔기

A-1. **거시 layer 추상화**: 현재 wiki에서 추론 가능한 외부 세계 frame을 4 layer로 정리한다. 표 형식.
   - **시간**: 1800년 전 첫 도시 학살 / 그 후 1800년 / 캠페인 시점 / 캠페인 이후 가능 시점. 각 layer에서 4 세력이 어떻게 변천했는지 짧게.
   - **지리**: 변방 site (ch1-5 무대) / 본 대성당 (솔라룸 중심) / 4 세력 본거지 / 도시 너머 / 변방 외 다른 site들 (가설). 각자 ch1-5와의 거리감.
   - **세력**: 4 세력(솔라룸/이리솔/회상 결사/그물 결사) 내부 분파 + 외부에 가능한 actor (다른 학파/다른 결사/다른 부족/다른 종교). 이미 wiki에 hint된 것 우선 활용.
   - **사건**: 1800년 학살 / 정화 재판 시대 / 변방 붕괴 / 캠페인 시점 다른 변방 사이트 동시 진행 가능 사건 / 본 대성당 정치적 흐름. 이미 wiki에서 mention된 것 위주.

A-2. **ch1-5 내부 비트 제안**: 유저가 "세계관 크구나" 느끼게 할 lore hint를 ch1-5 안에 자연스럽게 깐다. 각 비트마다:
   - 위치: 어느 chapter, 어느 slug, 어느 scene
   - 누가 말하는지 (캐릭터 voice 안에서 자연 — 캐릭터 baseline 어조에 맞게)
   - 무엇을 hint (어떤 외부 세계 layer를 비치는가)
   - before/after diff (기존 dialogue에 한 줄 추가 또는 한 줄 변경)
   - 정합 근거 (어떤 wiki baseline에 기댄 hint인지)
   
   비트 수: 5-12개. 너무 적으면 약하고 너무 많으면 exposition dump. ch별 1-3개 균형.

A-3. **출시 후 확장 hook**: 1-3개. ch1-5에서 비친 외부 세계 layer가 post-launch(character event / DLC / 다음 사이트 캠페인)로 elaborate 가능한 hook. 명시적 제안.

A-4. **정합 가드레일**: PART A의 모든 제안에 대해
   - 기존 wiki baseline과 모순 없는지 (어디 모순 없음 — slug cross-reference)
   - 새 lore 도입 최소화했는지
   - exposition dump 회피했는지

PART B — 대사 voice/페르소나 검수

B-1. **서브컬쳐 감성 부족 부분**: 밋밋한 정보 나열, 텐션 빠진 구간, 캐릭터 매력이 약한 묘사. slug별.

B-2. **페르소나 무너지는 부분**: 어조 가드레일 (analysis-character-emotion-layer의 voice motif spec) 이탈, signature gesture/phrase 부재 또는 남용, 캐릭터별 voice 비식별. slug별.

B-3. **slug별 patch**:

   ### {slug}
   
   **voice/페르소나 이슈** (있을 때만)
   - 어디서 어떻게 baseline 이탈 (구체 인용)
   
   **patch** (한 캐릭터/한 비트당 1-3개)
   - before: "..."
   - after: "..."
   - reason: 한 줄 (어떤 voice baseline / signature에 맞췄는지)
   
   **서브컬쳐 강화 제안** (있을 때만, 위와 별개)
   - 위치
   - before/after
   - reason
   
   교정 불필요 slug는 응답에서 완전 생략.

PART C — Cross-axis sanity check

C-1. PART A의 lore hint 비트가 PART B에서 본 voice baseline을 해치지 않는지. 표:
   | A-2 비트 # | 어느 slug에 추가/변경 | 그 slug 캐릭터의 voice baseline | hint가 voice에 정합? |

C-2. 정합 충돌 zero인지 — PART A 새 hint가 PART B 교정과 충돌하지 않는지 짧게.

C-3. 가장 강한 추천 1-3개를 P0로 표시 (즉시 wiki update 가치). 나머지 P1/P2.

[CONSTRAINTS]
- 한국어 narrative prose 1차. 영문 ID/slug 유지.
- 새 lore는 최소 — 기존 wiki 어휘/사건/인물 활용. 새 도입은 명백히 가치 있을 때만 + 그 자체로 후속 wiki publish 가능한 형태 (즉 "TBD: 후속 작가 follow-up" 같은 dangling hook 환영, 완전 정의는 다음 작업).
- 캐릭터 핵심 컨셉 (role, archetype, voice baseline, P09 visual baseline) 보존.
- runtime asmdef / persistence / 시스템 결정 절대 X.
- 번역체 금지 — "한 박자", "X라 부르네", "좋은 냄새가 나는 단어" 등 영어 직역 표현 차단. 한국 작가가 실제 쓰는 어휘만.
- exposition dump 금지 — PART A의 hint는 캐릭터 voice 안에서 짧게. 정보 나열 X.
- ch1-5 chapter 구조 / scene 순서 / lead 4 cast 재편성 금지.
- PART A 비트가 늘어나서 chapter dialogue 분량 폭증하면 안 됨 — 한 chapter당 1-3 짧은 hint 이내. 짧은 narrator line 또는 캐릭터 대사 한 줄 정도가 baseline.
- PART B patch는 narrative-consistency-fix 시나리오에서 다룬 line-level과 겹치면 안 됨 (사용자가 이미 그쪽 응답을 받고 있음). voice/페르소나/서브컬쳐 감성에 특화.

[DELIVERABLE]

응답 구조:

# 1. 세계관 frame 제안 (PART A)

## 1.1 거시 layer 표

(시간 / 지리 / 세력 / 사건 4 layer 각각 표)

## 1.2 ch1-5 내부 비트 제안

(5-12개 비트, 표 또는 list 형식. 각 비트 = 위치 + 화자 + hint 내용 + before/after diff + 정합 근거)

## 1.3 출시 후 확장 hook

(1-3개)

## 1.4 정합 가드레일

(A의 모든 비트에 대한 자기-점검)

# 2. 대사 voice/페르소나 검수 (PART B)

(slug별 patch — 교정 불필요 slug 생략. 위 schema)

# 3. Cross-axis sanity check (PART C)

## 3.1 A-B 정합 표
## 3.2 정합 충돌 ZERO 확인
## 3.3 P0/P1/P2 우선순위

# 전체 요약 (3-5 단락)

- 가장 큰 lore frame 결정 1-2개
- 서브컬쳐/voice 측면 핵심 발견 1-2개
- 후속 작업 권장 (어느 작업을 다음 cycle에 우선)
- 위험 — 본 응답을 적용할 때 주의할 점

{{output_format_block}}
```

## Default scope 보완

- `narrative-process` (작업 메타): 기본 제외. Task 외의 reskin progress analysis가 있어 hint 작업에 도움 될 수도 있지만 noise도 큼.
- `art-pipeline` (P09 시각): 본 task와 무관, 기본 제외.
- `flows` (사용자 flow): post-launch hook이 flow surface면 일부 reference 가능하지만 기본 제외.
- `governance` (정책): 기본 제외. 인디 scope 같은 운영 정책은 본 task에 noise.

## 호출 예시

```text
/gpt-pro-submit worldscale-and-voice-pass
→ character-lore + narrative-script + content 세 area 전체 dump + 두 축 검수

/gpt-pro-submit worldscale-and-voice-pass --extra="lore frame은 시간 layer 위주로 더 깊게, 본 대성당 정치 흐름이 ch1-5 안에서 비치게 하는 비트를 가장 우선"
→ 같은 scope + focus_block 추가
```
