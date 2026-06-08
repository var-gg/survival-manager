---
name: p2-warrant-systems-review
default_scope:
  slugs:
    - analysis-p2-warrant-system-design
    - analysis-ludonarrative-loop-implementation
    - analysis-narrative-engine-retrofit-jrpg
    - roster-expedition-combat-시스템지도
  include_superseded: false
  exclude_templates: true
output_format: _output_format.md
---

# Scenario: p2-warrant-systems-review

systems 엔지니어·게임 디자이너 관점에서 ludonarrative 루프 P2(Warrant) 설계 방향을 냉정하게 검증한다. P2b/P3로 더 쌓기 전에, 되돌리기 비싼 구조적 결정이 옳은지 외부 검수받는 게 목적이다. 칭찬이 아니라 약점·반례·대안을 받는다.

## Prompt body

```text
[CONTEXT]
survival-manager는 직군(아키타입) 고용 + 4인 배치 자동전투 + 짧은 호흡의 루프 게임이다. Town에서 roster를 구성하고 출격(sortie)하면 4인 squad가 자동전투를 관전(플레이어는 배치·택틱·로스터만 정하고 전투 중 직접 조작은 없음)하고, reward를 거쳐 다음 노드로 가는 작은 루프를 반복한다. 적군은 전부 인간형 세력(악당이 아니라 죽은 제도·맹세에 묶인 사람들)이고, 캠페인 스토리는 네 인간 세력의 공동 죄를 다루는 무겁고 비장한 정치극이다.

GPT Pro가 이전 검수에서 꼽은 '단 하나의 핵심 문제': 전투·고용이 '정치극 사이 콘텐츠'가 아니라 '정치극의 원인'이어야 한다(ludonarrative 루프). 이를 기존 아키텍처에 이식하는 설계가 첨부 analysis-ludonarrative-loop-implementation이고, 그중 P1(전투 결과 → Dossier 영구 기록 → 분기 대사)은 이미 구현·검증됐다.

이번 검수 대상은 루프의 앞 절반 P2(Warrant = 출격 전 서약)다. 첨부 analysis-p2-warrant-system-design이 전체 설계이고 구현 상태는 P2a(판정 spine) 완료:
- Warrant = 출격 전 squad가 한 세력 기준에 거는 약속. 슬라이스 1 두 종류 — 속전(Swift: 승리 + turn 수 임계 이하)·온전(Intact: 승리 + squad 손실 0).
- 전투 코드 0줄 변경. 판정(WarrantJudge)은 전투가 이미 내는 사실(승패·생존 수·turn 수)만으로 SM.Meta 순수 코어가 한다. 전투 시뮬은 사실만 산출하고 objective/narrative를 모른다(combat 순수성).
- 서약 id는 run overlay에 실려 settlement까지 운반되고, 결과는 Dossier(영구 ledger)에 기록되며 story flag로 stamp돼 세력 반응 대사로 분기된다.
- P2b(선택 UI + 세력 페이오프 대사), P3(전투 엔티티 objective: 민간인 유닛·증거 오브젝트·비살상 최종상태 — 전투 모델 변경 동반)로 단계 분리.

첨부 wiki dump: {{bundle_summary}}

[TASK] 칭찬은 최소화하고 약점·반례·대안 위주로, 첨부 설계의 실제 항목을 직접 인용하며 냉정하게:

1. [가장 중요] Swift(속전)/Intact(온전) 2축 warrant가 플레이어에게 진짜 '선택'인가, 같은 전투를 다르게 라벨링하는 lens인가? 자동전투에서 플레이어는 배치·택틱·로스터만 정하고 관전한다. 이때 'turn 수'와 'squad 손실'을 출격 전 결정으로 얼마나 가를 수 있나? 만약 결과가 주로 매치업 운이면 서약은 통제 가능한 선택이 아니라 도박이다 — 이 위험이 실재하는가, 실재한다면 통제 가능한 trade-off로 만들 구체적 방법은?

2. '전투 코드 0변경 / SM.Meta 순수 판정' 제약의 지속성: P2a는 기존 사실만으로 판정됐다. 그러나 P3의 진짜 objective — 민간인 보호(보호대상 생존), 증거 확보(오브젝트 상호작용), 비살상(적 제압 최종상태) — 는 전투가 새 사실을 산출해야 한다. 이 제약을 지키며 P3로 가는 길이 막히지 않나? combat이 objective를 모르는 채 '보호대상이 죽으면 실패' 같은 win-condition 변형을 어디서 판정해야 하나? 이 설계가 P3에서 자연스럽게 확장되나, 아니면 지금 골격이 P3에서 재설계를 강제하나?

3. warrant → Dossier(영구 기록) → story flag → 세력 반응 rail이 ludo-loop을 닫는 올바른 골격인가, 더 나은 골격이 있나? 특히 '세력이 서약 이력을 보고 반응한다'를 단순 flag 분기 대사를 넘어 실제 게임 상태(평판·해금·가격·적대·로스터 접근)로 물리려면 어디를 어떻게 설계해야 하나?

4. 이 P2 설계가 '전투가 정치의 원인'을 실제로 달성하기 위해 P2 범위에서 반드시 풀어야 할 '단 하나의 핵심 문제'는?

5. P2b로 진입하기 전에 P2a에서 지금 당장 고쳐야 할, 되돌리기 비싼 구조적 결정이 있나? (있으면 무엇을 어떻게.)

{{output_format_block}}
```
