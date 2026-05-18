# 048 Skill VFX Animation Coverage Audit Implement

## 메타데이터

- 작업명: 스킬 VFX/애니메이션 커버리지 감사
- 담당: repository
- 상태: active
- 최종수정일: 2026-05-18
- 실행범위: task/report 문서

## Phase log

- Phase 0: 스킬 정의 88개, slot별 22개씩 구성된 상태를 확인했다.
- Phase 0: 유료 VFX와 애니메이션 에셋은 vendor original로 취급하고 쓰기 작업을 하지 않았다.
- Phase 1: Epic Toon FX prefab 1,424개와 TriForge particle prefab 17개의 표현 범위를 family 단위로 집계했다.
- Phase 1: Kevin Iglesias humanoid animation 1,359개와 combat gesture 범위를 확인했다.
- Phase 2: raw asset은 충분하지만 현재 runtime catalog는 generic cue 중심이라는 차이를 기록했다.
- Phase 2: 다음 구현 단위에서 필요한 family/skin/animation semantic 설계와 pilot 스킬을 정리했다.

## deviation

이번 loop에서는 실제 prefab catalog를 확장하지 않았다. 도감 VFX preview를 real particle로 연결하기 전에, 보유 에셋이 정의된 스킬셋을 감당하는지 평가하는 것이 먼저라는 판단이다.

## blockers

- Pindoc wiki MCP write tool이 현재 세션에 노출되어 있지 않아 repo task/report로 handoff한다.
- Unity project lock이 열려 있으면 batchmode FastUnit은 진입 전 실패한다.

## diagnostics

- 스킬 수요는 melee, projectile, aura, zone, nova, trap, heal, shield, status applier로 충분히 분산되어 있다.
- Epic Toon FX는 melee slash, missile, explosion, magic, shield, healing, aura, zone, portal, dust 계열이 넉넉하다.
- Kevin Iglesias는 1H/2H, bow, thrown, spell direct/omni, shield, dodge/roll, hit/death gesture가 충분하다.
- 현재 C# catalog는 hook별 표현이 아니라 cue/semantic fallback 위주라 스킬 차별성이 아직 낮다.

## why this loop happened

도감에서 스킬을 클릭해 VFX를 보는 단계로 가면, 단순히 prefab을 많이 보유한 것과 실제 스킬이 서로 구분되어 보이는 것은 다른 문제다. 이번 감사는 다음 구현이 prefab 노동이 아니라 반복 가능한 presentation taxonomy로 시작되게 하기 위한 사전 폐쇄다.
