---
name: narrative-rereview-2
default_scope:
  areas: [narrative, characters]
  include_superseded: false
  exclude_types: [Task]
  exclude_templates: true
output_format: _output_format.md
description: 1차 검수 P0/P1 반영 후 2차 재검수. 수정 효과 재평가 + 남은 가장 약한 지점(잔여 시스템어·다음 레버리지·새 약점). 가장 약한 곳부터, 칭찬 최소.
---

# Scenario: narrative-rereview-2

1차 검수(narrative-critique-dialogue-story)의 P0/P1을 반영한 뒤 같은 narrative dump를 다시 올린다. 칭찬이 아니라, 수정이 실제로 약점을 닫았는지 + 다음 최고 레버리지를 냉정하게 받는다.

## Prompt body

```text
[CONTEXT]
survival-manager는 직군(아키타입) 고용 + 4인 배치 자동전투(관전, 전투 중 직접 조작 없음) + 짧은 호흡 루프의 캐릭터 수집/성장 RPG다. 적군은 전부 인간형 세력. 캠페인 ch1-5 = 첫 도시 입구 변방 site의 정치 정착극(4 인간 세력이 1800년 전 첫 도시 학살 기록을 처음 같은 테이블에 올린다). narrative SoT 1차 언어 = 한국어. **자동전투+인간형 적군 only**라 "왜 이 세력과 싸우나(conflict warrant)"가 중심축.

당신(GPT Pro)의 1차 검수가 지목한 핵심 약점과, 그에 대해 우리가 반영한 수정:

1. **Ch5 결말이 회의록처럼 닫힘 (P0, #1 레버리지)** → `dialogue_scene_final_record_decision`을 행동화. 정책 나열("선택지 셋… 일부 공유. 대표 기록. 점진 공개. 봉합")을 제거하고 명음이 빈 종이 세 장을 놓는 행동 + 단린의 "첫 도시는 있었다, 사람을 번호로 부르지 않는다" 첫 줄 + 네 손을 종이에 얹는 비트 + 묵향이 흑지 명단의 잎 한 장을 건네는 물건으로 닫게 바꿨다. 명음 확정도 "첫 줄은 나갑니다. 원문은 남깁니다. 빈 의자 둘은 지우지 않습니다."로 물건화.
2. **시스템어 유출 (P0)** → 대사/나레이터의 `region clear`·`dossier`·`ch1`·`ch4`·`memorial lobby`·`mode_endless_cycle` 제거. ch5(mirror_cantor/town_return/extract card) + pack-raider TC2/TC3에서 작중어로 교정.
3. **conflict warrant 약함 (P0/P1)** → Ch3 침묵의 기록관: "훔치러/읽으러 비슷해 보여"(약함) → "저 사람이 종을 치면 흑지의 명단이 먼저 와, 읽히기 전에 단죄문이 돼"(즉각 stake). Ch5 수문 지휘관: "자격 없는 자는 통과 불가"(기능적) → "불일치 기록은 즉시 백규 학자장에게 전송, 자격 없는 증언은 도착 전에 폐기"(즉각 stake).
4. **후반 voice 문서체 붕괴 (P1)** → 명음(massacre_record/boss_defeat_baekgyu)·백규("학자적으로 어색합니다"→"학문의 격에 맞지 않습니다") 어조 완화.

첨부 wiki dump (수정 반영된 현행 SoT):
- {{bundle_summary}}
- narrative: prologue + ch1-5 + town conversation + event-* + memorial cutscene + craft guideline
- characters: 캐릭터 wiki + emotion/voice spec

{{focus_block}}

[TASK] 칭찬 최소화. 가장 약한 지점부터, 구체 slug/scene/대사 인용.

1. [재평가] 위 1~4 수정이 실제로 약점을 닫았는지 냉정히. 특히 **Ch5 `final_record_decision`이 이제 '회의록'에서 벗어나 사건으로 읽히는가** — 닫혔으면 어디까지, 안 닫혔으면 가장 강한 반례. warrant 2건(기록관/수문장)이 관전 플레이어에게 "왜 지금 싸우나"를 세우는가.

2. [남은 가장 약한 지점 Top 5] 1차에서 안 다뤘거나 새로 보이는 것. 특히:
   - **잔여 시스템어**: event-*/memorial cutscene 등에 남은 narrator/stage-direction 유출(예: "회조의 말에서 자기 ch5 line을 듣는다", "ch4 사육장 비트의 같은 침묵" 류 ch1~5·시스템어). 전수로 짚어라.
   - **#1 다음 레버리지**: Ch5 결말을 손본 지금, "알맹이/몰입"을 가장 키울 다음 단 하나.
   - 그 외 대사/스토리 새 약점.

3. [다음 한 수] 지금 단 하나만 더 고친다면 무엇을, 왜. + "지금 하지 마라" 1개.

[CONSTRAINTS]
- 한국어 prose 1차, 영문 ID/slug 유지. 번역체 금지(너 자신의 제안에도). 시스템/asmdef/runtime 결정 금지.
- 대사 patch는 before/after + 한 줄 reason.

[DELIVERABLE]
# 0. 1차 수정 재평결 (3-5문장) — 닫혔나/덜 닫혔나, Ch5 결말 체감 변화
# 1. 남은 약한 지점 Top 5 (우선순위 + slug + P0/P1/P2)
# 2. 잔여 시스템어 전수 (slug + 인용 + before/after)
# 3. 대사/스토리 새 약점 (slug별)
# 4. 다음 한 수 + 지금 하지 마라

{{output_format_block}}
```
