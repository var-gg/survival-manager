# 작업 계획

## 메타데이터
- 작업명: Code Governance Hardening
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-03-29
- 의존:
  - `tasks/_templates/spec.md`
  - `tasks/_templates/plan.md`
  - `tasks/_templates/status.md`

## 마일스톤
1. 기존 거버넌스/아키텍처/ADR 충돌과 오래된 인덱스를 정리한다.
2. 구조 정책 기준 문서, 검수 체크리스트, 스킬, ADR을 추가한다.
3. 링크·메타데이터·용어 정합성을 재검수하고 task 상태를 갱신한다.

## 승인 기준
- 구조 정책이 `03_architecture/`, 운영 규칙이 `00_governance/`, durable decision이 `04_decisions/`에 분리되어 있다.
- 새 정책이 추상적 미사여구가 아니라 실제 분리 판단 기준과 금지 패턴을 제공한다.
- 구조 변경을 문서 없이 남기지 않도록 체크리스트와 스킬이 같은 방향을 가리킨다.

## 검증 명령

```bash
Get-ChildItem docs -Recurse -Filter *.md | Select-String -Pattern 'adr-000[0-9]{1,4}-'
Get-ChildItem docs -Recurse -Filter *.md | Select-String -Pattern 'SurvivalManager\.'
Get-ChildItem docs -Recurse -Filter *.md | Select-String -Pattern '\]\([^)]+\)'
```

## 중단 조건
- 기존 문서와 새 정책 문서의 목적이 분리되지 않으면 신규 문서 추가를 중단하고 분리 기준부터 다시 명시한다.
- ADR 번호 정규화 후에도 옛 파일명을 참조하는 링크가 남아 있으면 완료로 처리하지 않는다.
- 메타데이터 누락 문서가 남아 있으면 해당 범위를 다시 점검한다.
