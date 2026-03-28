# GitHub 설정 가이드

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-28
- 소스오브트루스: `docs/05_setup/github-setup.md`
- 관련문서:
  - `.github/CODEOWNERS`
  - `.github/pull_request_template.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/04_decisions/adr-0001-docs-architecture.md`

## 목적

이 문서는 `survival-manager` 저장소의 GitHub 협업 기본 장치와 수동 설정 항목을 정리한다.

## 기본 원칙

- 기본 브랜치는 `main`이다.
- 직접 작업은 기능 브랜치에서 한다.
- 작업 브랜치 이름은 아래 중 하나를 따른다.
  - `feat/*`
  - `fix/*`
  - `docs/*`
  - `chore/*`
- 모든 변경은 Pull Request를 통해 `main`에 병합한다.

## 파일로 자동화 가능한 항목

### CODEOWNERS

- `.github/`, `docs/`, `Assets/`, `Packages/`, `ProjectSettings/` 경로의 기본 소유 규칙을 `@var-gg`로 정의했다.

### Pull Request 템플릿

다음을 체크리스트에 포함했다.

- 문서 갱신 여부
- ADR 필요 여부
- 테스트 여부
- 리스크

### Issue 템플릿

아래 템플릿을 추가했다.

- 버그 리포트
- 기능 요청
- 문서 작업
- 설계 결정 제안

## GitHub 웹에서 수동 설정할 항목

아래 설정은 GitHub 저장소 설정 화면에서 사람이 직접 적용해야 한다.

### 1. 기본 브랜치 확인

- Settings → Branches 또는 General에서 기본 브랜치가 `main`인지 확인한다.

### 2. 브랜치 보호 규칙

`main` 브랜치에 아래 규칙을 설정한다.

- Pull Request 없이는 병합 금지
- 최소 1명 리뷰 승인 필요
- 상태 체크 통과 전 병합 금지
- 강제 푸시 금지
- 브랜치 삭제 제한 또는 보호 사용
- CODEOWNERS가 지정된 중요 경로는 소유자 리뷰 요구

### 3. 권장 옵션

가능하면 아래도 함께 켠다.

- 리뷰 대기 중 오래된 승인은 새 커밋 시 재검토 요구
- 대화가 해결되기 전 병합 금지
- 병합 후 브랜치 자동 삭제

## 상태 체크 메모

현재 저장소에는 CI 워크플로가 아직 없다. 따라서 "상태 체크 통과" 규칙을 실효성 있게 쓰려면 이후 GitHub Actions 또는 외부 체크를 추가해야 한다.

그 전까지는 PR 템플릿의 테스트/리스크/문서 체크리스트를 최소 운영 장치로 사용한다.

## 적용 순서 권장

1. 저장소 파일 커밋 및 푸시
2. 기본 브랜치 `main` 확인
3. `main` 브랜치 보호 규칙 수동 설정
4. 이후 CI 체크 추가 시 보호 규칙에 상태 체크 연결
