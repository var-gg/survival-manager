# ADR-0018 Loop C 콘텐츠 거버넌스 채택

- 상태: 승인
- 날짜: 2026-04-01
- 관련 task: `tasks/011_loop_c_content_governance_closure/`

## 문맥

Loop A는 authority/loadout/targeting/summon ownership seam을 닫았고, Loop B는
recruit/retrain/pity/economy seam을 닫았다. 그러나 content를 더 넣을 때 power creep,
rarity drift, counter lane 확장, forbidden mechanic 유입을 막는 authoring contract는
아직 느슨했다.

## 결정

1. `BudgetCard`를 power-bearing content의 공통 governance ledger로 채택한다.
2. `ContentRarity`를 `Common / Rare / Epic` 3단으로 고정한다.
3. counter-system을 8-lane threat/answer topology로 고정한다.
4. V1 forbidden list를 validator fatal policy로 집행한다.
5. `RecruitTier`와 item/drop rarity는 계속 별도 체계로 유지한다.

## 근거

- authored ledger + derived sanity preview는 완전 sim보다 싸고, stealth drift를 빨리
  잡는다.
- 3단 rarity는 complexity cap과 authoring authority를 설명하기에 충분하고, prototype
  phase에서 예외 rarity proliferation을 막는다.
- threat/answer topology는 플레이어가 "왜 졌는지"와 "무엇을 보완해야 하는지"를 한
  문장으로 말할 수 있게 한다.
- forbidden list를 warning이 아니라 fatal로 박아야 V1 scope가 다시 열리지 않는다.

## 결과

- 새 content는 `BudgetCard` 없이 들어오지 않는다.
- town recruit UI는 `RecruitTier`만 보여 주고, `ContentRarity`와 budget drift는
  debug/sandbox에서만 본다.
- synergy breakpoint는 `2 / 4`로 고정되고, `2 / 3 / 4` legacy 문서는 대체 문서를
  우선 참조한다.
- audit markdown만으로도 rarity drift, counter coverage gap, forbidden feature 위반을
  PR에서 읽을 수 있다.

## 비결정

- recruit economy 재설계
- loadout topology 재설계
- item loot rarity 통합
- telemetry 기반 pruning
