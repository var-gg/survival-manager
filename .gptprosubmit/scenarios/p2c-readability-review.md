---
name: p2c-readability-review
default_scope:
  slugs:
    - analysis-p2-warrant-system-design
    - analysis-ludonarrative-loop-implementation
  include_superseded: false
  exclude_templates: true
output_format: _output_format.md
---

# Scenario: p2c-readability-review

이전 검수(“체크박스 충족”)의 단 하나의 핵심 지적 — 정치적 의미가 compile hash 안에 숨어 자동전투 관전에서 체감 안 됨 — 에 대응한 두 슬라이스(P2b sortie 예고 + P2c settlement 귀결)를 올린다. 칭찬이 아니라, 가독성 #1이 실제로 닫혔는지·다음 최고 레버리지가 무엇인지 냉정하게 받는다.

## Prompt body

```text
[CONTEXT]
survival-manager는 직군(아키타입) 고용 + 4인 배치 자동전투(플레이어는 배치·택틱·로스터만 정하고 전투 중 직접 조작 없음, 관전) + 짧은 호흡 루프 게임이다. 적군은 전부 인간형 세력이고, 캠페인은 네 인간 세력(솔라룸/이리솔 부족/회상 결사/그물 결사)의 공동 죄를 다루는 무거운 정치극이다. ludonarrative 루프: 전투·고용이 "정치극 사이 콘텐츠"가 아니라 "정치극의 원인"이어야 한다.

[당신의 이전 검수 — "체크박스 충족" 판정 (그대로 인용)]
- "이 닫힌 루프는 체크박스 충족 — state는 전투 compile에 들어왔지만, 현재 효과가 generic ally stat buff라 정치적 원인으로 식별되기 약하다."
- (#1) "지금은 루프가 닫혔지만, 정치적 의미는 compile hash 안에 숨겨져 있다. 숨겨진 피드백은 자동전투 관전 게임에서 거의 체감되지 않는다. … sortie UI, dossier, replay/audit에는 반드시 '이 수치가 어느 세력의 어떤 정치적 mandate에서 왔는지'가 남아야 한다."
- (#3/#5) "opposed −1은 ledger에는 남지만 다음 전투 조건에는 흐르지 않는다 … 적 경계도(enemy alertness)를 붙여 루프를 양방향으로."
- (#4 되돌리기 비싼 함정) "정치 결과 전체를 stat package로만 흘려보내는 습관 + trust 단일 scalar를 모든 정치 상태의 alias로 굳히는 것. CombatModifierPackage는 leaf로만 두고 상위는 여러 channel을 담아라(provenance: SourceFactionId/Channel/Cause)."

[그 뒤 구현된 것 — 당신 권고를 코드로 옮김]
1. (slice3) 양방향: opposed standing ≤ −2 → EnemyAlertness(적 buff)가 다음 전투 적에 fold됨(EnemySnapshotHash 포착). PoliticalCombatCondition = 다채널(AllySupport/EnemyAlertness) + provenance(SourceFactionId/Channel/ReasonCode). 당신 #3/#4/#5 반영. magnitude는 실측(중간 스케일 squad에서 marginal tier 승률 13%→75%, 경계는 13%→0%; 이미 결정난 전투에선 무변 = "박빙에서만 결정적"). 당신 "+2pow noise?" 우려 실측 반증.
2. (P2b) sortie 예고 — 출격 전 warrant 선택 UI가 섰다. 4 세력 위임 카드에 발행세력·종류(속전/온전)·대립세력 + "정치 조건 미리보기"(후원: 발행세력 신뢰≥4 / 경계: 거스른 세력 누적)를 카드에 노출. 당신이 요구한 "출격 화면 후원/경계 표시"를 구현.
3. (P2c, 이번 슬라이스) settlement 귀결 — 전투 직후 정치 결과를 player-visible로. 핵심: 정치 정산을 PoliticalSettlementReporter(SM.Meta 순수)가 사유 태깅된 report로 박제 — line마다 (FactionId, Δtrust, Reason∈{KeptIssuer, BrokenIssuer, DefiedOpposed, RejectedOffer}). reward 화면에 노출: (a) Summary 패널 1줄 headline "정치: {발행세력} {약속 이행/위반} (신뢰 ±N)", (b) progression ledger에 세력별 상세 행 "{세력} · {사유} · 신뢰 {±N} (현재 {standing})", 신뢰 상승/하락 색 구분. provenance(어느 세력/무슨 사유)가 Dossier + 화면 둘 다에 남는다(당신 #1 권고). 표시명은 label layer로 분리(코드는 lore-free).

즉 이제 루프 양끝이 player-visible다: 출격 전 "이 세력에 서약하면 후원/경계가 이렇게 바뀐다"(예고) → 자동전투 → 직후 "이 약속을 지켰다/어겼다, 누구를 거슬렀다/거절했다, 신뢰가 이렇게 됐다"(귀결) → 다음 출격 예고에 반영.

첨부 설계 dump: {{bundle_summary}}

[TASK] 칭찬 최소화. 약점·반례·대안 위주로, 위 실제 구현 항목을 직접 인용하며 냉정하게:

1. [가장 중요] 당신의 #1 — "정치 의미가 compile hash에 숨어 관전에서 체감 안 됨" — 이 P2b(예고)+P2c(귀결)로 실제로 닫혔나? 관전 플레이어가 이제 "정치가 전투를 바꿨다 / 내 선택이 세력 관계를 바꿨다"를 읽나, 아니면 여전히 "좋은 평판이 버프를 줬다 / 지원 farming"으로 읽히나? 닫혔다면 어디까지, 안 닫혔다면 남은 legibility/agency gap의 가장 강한 반례는?

2. 루프가 양끝 player-visible로 닫힌 지금, "정치가 전투의 원인"을 더 felt하게 만들 다음 단 하나의 최고 레버리지 작업은? 후보를 냉정히 우선순위 매겨라: (a) 세력 standing 상설 뷰 — Town에서 4세력 신뢰 + 서약 이력(Dossier) 열람; (b) per-site offer 소스 — 어느 site가 어느 세력 위임을 제안하는지 content authoring으로 정박(현재는 카탈로그 전체가 placeholder offer); (c) trust 단일 scalar → 다축(Heat/Debt/Scandal) — 당신 #4 함정의 state 면; (d) mutation 신뢰-비례 scaling/세력별 차등; (e) 다른 것. 1순위와 그 이유, 그리고 "지금은 하지 마라"를 명시.

3. P2c가 정치 결과를 reason-tagged report(PoliticalSettlementReport: 사유 enum + line)로 박제했다. 이 구조가 당신이 경고한 #4 함정(다축 state, per-faction policy, 추가 channel)으로 확장될 때 막히는 곳이 있나? reason을 enum으로 둔 것이 나중에 데이터-주도 정책과 충돌하나?

4. P2 범위에서 지금 놓친, 되돌리기 비싼 구조적 결정이 더 있나? (있으면 무엇을 어떻게.)

{{output_format_block}}
```
