# Docs eval 초안

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/00_governance/docs-evals.md`
- 관련문서:
  - `docs/00_governance/docs-harness.md`
  - `docs/00_governance/docs-governance.md`
  - `tools/docs-policy-check.ps1`
  - `tasks/003_docs_harness_hardening/status.md`

## 목적

이 문서는 docs harness가 실제로 잘 작동하는지 반복적으로 평가할 최소 eval 세트를 정의한다.
prompt 문구만 고치는 대신, run과 check를 같이 기록해 drift를 계량적으로 확인하는 것이 목표다.

## 운영 원칙

- 각 eval은 입력, 기대 읽기 경로, 금지 경로, 통과 조건을 함께 가진다.
- eval 실행 결과는 task 또는 별도 report에 남긴다.
- deterministic check로 충분한 항목은 스크립트 우선, 판단이 필요한 항목은 캡처된 run review를 사용한다.

## Eval 1. Context Routing

- 질문 예시: "전투 가독성 규칙을 업데이트하라."
- 기대 경로: `AGENTS.md -> docs/index.md -> docs/02_design/index.md -> 관련 active combat 문서 -> 현재 task status`
- 금지 경로: unrelated 폴더 전체 sweep, deprecated 문서 우선 읽기
- 통과 조건: 시작 컨텍스트와 직접 관련 active source만 읽고 작업을 진행한다.

## Eval 2. Deprecated Suppression

- 질문 예시: "combat loop 기준을 요약하라."
- 기대 경로: `realtime-simulation-model.md`, `combat-runtime-architecture.md`, `battlefield-and-camera.md`
- 금지 경로: 제거된 `combat-loop.md`, `battle-replay-model.md`
- 통과 조건: registry에 등록된 제거 문서를 active source로 인용하지 않는다.

## Eval 3. Language Consistency

- 질문 예시: "`docs/05_setup/local-automation.md`를 수정하라."
- 기대 경로: 한국어 메타데이터와 한국어 본문 유지, 코드/명령어/식별자만 영어 유지
- 금지 경로: 영어 메타데이터 재도입, 혼합 언어 heading 확산
- 통과 조건: 수정 결과가 docs policy check의 언어 규칙을 통과한다.

## Eval 4. Change Completeness

- 질문 예시: "setup 문서를 추가하라."
- 기대 경로: 대상 문서, 관련 `index.md`, 관련문서 링크, 필요 시 task `status.md` 동시 갱신
- 금지 경로: 본문만 추가하고 인덱스/링크 미갱신
- 통과 조건: 새 문서가 index coverage와 related docs 검사에 모두 걸리지 않는다.

## Eval 5. Doc Garbage Collection

- 질문 예시: "deprecated 문서를 정리하라."
- 기대 경로: replacement 확정 -> registry 기록 -> active index 제거 -> 원본 삭제 -> policy check
- 금지 경로: deprecated pointer 유지, 원본 파일에 이유만 누적
- 통과 조건: active tree에 deprecated 문서가 남지 않고 registry와 replacement 경로가 유효하다.
