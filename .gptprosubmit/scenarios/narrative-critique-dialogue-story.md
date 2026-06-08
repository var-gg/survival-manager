---
name: narrative-critique-dialogue-story
default_scope:
  areas: [narrative, characters]
  include_superseded: false
  exclude_types: [Task]
  exclude_templates: true
output_format: _output_format.md
description: 대사처리 문제 + 스토리 문제 비판적 검수. 번역체/voice 일관성/자연스러움(대사) + "알맹이 없음" 원인/stake/conflict warrant/pacing/캐릭터 동기/구캐넌 잔재(스토리). 1독자 시각 + 구조 분석, 가장 약한 지점 우선.
---

# Scenario: narrative-critique-dialogue-story

프로젝트 오너 겸 V1 authority가 직접 narrative를 읽고 "문법도 어색하고 내용도 알맹이 없는, 뭔지 모를 스토리 같다"고 느꼈다. 이 검수는 그 직감이 맞는지·어디가 약한지·무엇을 고치면 가장 크게 나아지는지를 GPT Pro 시각으로 냉정하게 받는다. 칭찬 금지, 약점·반례·구체 인용 위주, 가장 약한 지점부터.

## 조립 규칙 (호출자용 메타)

- `default_scope.areas` 세 area(narrative-script / character-lore / content)를 pack_wiki.py로 dump (Task·template 제외)
- placeholder: `{{focus_block}}`(--extra), `{{bundle_summary}}`(자동 카운트), `{{output_format_block}}`(_output_format.md)

## Prompt body

```text
[CONTEXT]
survival-manager는 직군(아키타입) 고용 + 4인 배치 자동전투(플레이어는 배치·택틱·로스터만 정하고 전투 중 직접 조작 없음 — 관전) + 짧은 호흡 루프의 캐릭터 수집/성장 RPG다. 서브컬쳐 감성을 정면으로 흡수한다. 단일 캠페인 클리어가 목표가 아니라 출시 후 캐릭터 수집/성장이 운영 축이다.

narrative 성격:
- 적군은 전부 인간형 세력. 캠페인 ch1-5 = 첫 도시 입구 주변 한 변방 site의 정치 정착극이다. 세계 전체 전쟁 종결이 아니라, 4 인간 세력(솔라룸 / 이리솔 부족 / 회상 결사 / 그물 결사)이 1800년 전 첫 도시 학살 기록을 어떻게 다룰지 처음으로 같은 테이블에 앉는 이야기.
- 4 세력 모두 도시 시조의 후예이자 학살 정당화/회피 어휘의 후예. 단일 거대 악 없음 — 분산 행위성(각자 자기 노선으로 행동).
- narrative SoT 1차 언어 = 한국어. 명칭은 영문 ID + 한국어 표시명.
- **자동전투 + 짧은 호흡 + 인간형 적군 only**라는 형식 때문에 "왜 이 세력과 싸우는가(conflict warrant)"가 narrative의 중심축이다. 관전 플레이어에게 전투의 당위가 안 서면 전투 자체가 공허해진다.

사용자(프로젝트 오너)의 직접 체감 — 이 검수의 동기:
"문법도 어색하고 내용 자체도 매력 없는(알맹이 없는), 뭔지 모를 스토리 같다."
이게 실제 약점인지, 어디가 약한지, 무엇을 고치면 가장 크게 나아지는지 냉정하게 받고 싶다.

첨부 wiki dump (현행 new-canon SoT):
- {{bundle_summary}}
- narrative-script: prologue + ch1-5 chapter scripts + town conversation + memorial cutscene + dialogue craft guideline + reader/fun pass + master-script
- character-lore: 캐릭터 wiki(외모/성격/작중 행적/명대사/평가) + emotion·voice motif spec + affinity
- content: 세계관 바이블 + 캠페인 스토리 아크 + historical truth layer + faction conflict driver + 분산 행위성 모델

{{focus_block}}

[TASK]
칭찬 최소화. 가장 약한 지점부터, 구체 slug/scene/대사를 직접 인용하며 비판적으로. 두 축으로 나눠라.

축 1 — 대사처리 문제 (dialogue)
1-1. 번역체/어색한 한국어 — 한국 작가가 실제로 안 쓰는 직역투(예: "한 박자", "X라 부르네", "좋은 냄새가 나는 단어" 류). slug + 인용 + 자연스러운 한국어 before/after.
1-2. 화자 voice 일관성/식별성 — 캐릭터별 어조가 구분되나, 아니면 다 같은 목소리인가. emotion/voice motif spec 이탈, signature 부재/남용.
1-3. 자연스러움·호흡·분량 — exposition dump, 정보 나열, 텐션 없는 구간, 관전 게임의 짧은 루프에 안 맞는 길이.
1-4. 서브컬쳐 감성 — 매력이 약한 부분, 캐릭터가 "기억에 남는 한 줄"을 못 만드는 지점.

축 2 — 스토리 문제 (story)
2-1. [가장 깊게] "알맹이 없음/뭔지 모를" 느낌의 실제 원인 진단 — 사용자 체감을 구조로 환원하라. 인물 동기 불명? stake 추상? 정보가 늦거나 안 옴? 시점·화자 혼란? POV 부재? 무엇이 "뭔지 모름"을 만드나 — 가장 강한 원인 1-3개를 짚고 근거 인용.
2-2. stake 구체성 — 각 chapter에서 "지면 무엇을 잃는가"가 구체적으로 서 있나, 추상적 명분뿐인가.
2-3. conflict warrant — 자동전투+인간형 적군 형식에서 "왜 이 세력과 싸우나"가 매 site/chapter마다 서 있나. 안 서는 지점.
2-4. pacing & 정보 설계 — 프롤로그~ch5에서 정보가 풀리는 속도, 전환, climax 배치. 늘어지거나 비는 곳.
2-5. 캐릭터 동기·성장 — lead 4인 + 주요 세력 인물의 동기가 읽히나, arc가 있나.
2-6. 일관성 — 폐기된 구캐넌(Relicborn / Heartforge / 야수족 / 격자 파편 잠식 / Eternal Order / 단현 직접 대면 등 SF 버전) 잔재가 new-canon 본문에 남아 모순/혼란을 만드는 곳. 발견 시 slug + 인용.

[프레이밍]
- 한 명의 플레이어/독자가 처음 읽었을 때의 시각 + 작법 구조 분석, 둘 다.
- 구조 체크리스트에 매몰되지 말고 "이게 재밌나 / 마음이 움직이나"를 1독자로서 먼저 답하고, 그 다음 구조로 환원하라.

[CONSTRAINTS]
- 한국어 narrative prose 1차. 영문 ID/slug 유지.
- 번역체 금지 — 1-1 기준을 너 자신의 제안 문장에도 적용. 한국 작가가 실제 쓰는 어휘만.
- 대사 patch는 before/after + 한 줄 reason. 캐릭터 핵심 컨셉(role/voice baseline) 보존.
- 시스템/asmdef/persistence/runtime 결정 금지 — narrative 품질만.
- ch1-5 구조·scene 순서·lead 4 cast 재편성을 요구하는 큰 수술은 P2로 분리(당장 적용 X 표시).

[DELIVERABLE]
# 0. 1독자 평결 (3-5 문장)
처음 읽은 플레이어로서: 재밌나? 마음이 움직이나? "뭔지 모를" 느낌이 실재하나? 가장 강한 인상과 가장 큰 실망 각 1개.

# 1. 가장 약한 지점 Top 5 (우선순위)
각: [축] 문제 한 줄 + 근거 slug + 고치면 얻는 것 + P0/P1/P2.

# 2. 대사처리 검수 (축 1)
1-1~1-4, slug별 인용 + before/after.

# 3. 스토리 검수 (축 2)
2-1~2-6. 특히 2-1("알맹이 없음" 원인 진단)을 가장 길고 깊게.

# 4. 다음 한 수
지금 단 하나만 고친다면 무엇을, 왜. 그리고 "지금 하지 마라" 1개.

{{output_format_block}}
```
