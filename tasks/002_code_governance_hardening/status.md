# 작업 상태

## 메타데이터
- 작업명: Code Governance Hardening
- 담당: Codex
- 상태: handoff-ready
- 최종수정일: 2026-03-29

## 현재 상태
- task 폴더와 최소 실행 문서 3종 생성 완료
- 거버넌스/아키텍처/ADR 충돌 정리 완료
- 새 구조 정책 문서, 검수 체크리스트, 스킬, ADR 추가 완료
- 링크/메타데이터/용어 재검수 완료

## 완료됨
- task 템플릿 구조 확인
- 거버넌스 인덱스 누락 문서 확인
- 아키텍처 인덱스의 오래된 설명 확인
- ADR 번호 충돌과 `SM.*` 대 `SurvivalManager.*` 용어 충돌 확인
- `docs/00_governance/index.md` 누락 문서 보완
- `docs/03_architecture/index.md`와 상세 기준 문서 재정비
- ADR 번호를 `0001`~`0012` 단일 시퀀스로 정규화
- `coding-principles.md`, `dependency-direction.md`, `unity-boundaries.md` 작성
- `implementation-review-checklist.md`와 `code-structure-guard` 스킬 추가
- `docs/index.md`, `docs/00_governance/index.md`, `docs/03_architecture/index.md`, `docs/04_decisions/index.md` 갱신

## 보류
- 없음

## 이슈
- 없음

## 결정
- namespace/asmdef 기준은 실제 구현 상태에 맞춰 `SM.*`로 통일한다.
- 기존 충돌 정리를 신규 정책 문서 작성보다 먼저 수행한다.
- 구조 정책 기준 문서는 `docs/03_architecture/`에 두고, 운영 검수 규칙은 `docs/00_governance/`에 둔다.

## 다음 단계
- repo 전반의 legacy 영어 문서까지 같은 규칙으로 정규화할지 별도 task로 결정한다.
- sample content의 `Resources` 의존을 장기적으로 유지할지 catalog 방식으로 옮길지 후속 결정한다.
