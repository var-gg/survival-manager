# 로컬 자동화

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/05_setup/local-automation.md`
- 적용범위: Codex와 로컬 기여자

## 목적

이 문서는 문서 검사, Unity 테스트, 기본 smoke check를 위한 최소 로컬 자동화와 CI 진입점을 정의한다.
목표는 배포 자동화를 과하게 키우지 않으면서도, 즉시 도움이 되는 가벼운 검증 루프를 유지하는 것이다.

## 원칙

- markdown lint와 link/structure check를 먼저 수행한다.
- Unity smoke-test의 첫 계층은 EditMode를 우선한다.
- runtime test coverage가 정당화되기 전까지 PlayMode는 placeholder로 유지한다.
- 저장소에 secret이나 token을 보관하지 않는다.
- 로컬 명령과 CI 명령을 같은 문서에서 관리한다.

## 문서 검사

### 로컬

```powershell
$paths = @('README.md', 'docs/05_setup/local-runbook.md')
& .\tools\docs-check.ps1 -RepoRoot . -Paths $paths
```

### CI

워크플로: `.github/workflows/docs-lint.yml`

로컬 기본 명령:

```bash
npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"
npx --yes markdown-link-check "docs/**/*.md" "*.md" --quiet
```

CI 기본 명령:

```powershell
pwsh -File tools/docs-policy-check.ps1 -RepoRoot .
pwsh -File tools/docs-check.ps1 -RepoRoot . -Paths <changed-md>
pwsh -File tools/smoke-check.ps1 -RepoRoot .
```

`tools/docs-check.ps1`는 `tools/docs-policy-check.ps1`를 먼저 실행한 뒤
repo-root `.markdownlint-cli2.jsonc` 설정을 사용해 markdownlint와
markdown-link-check를 수행한다. 기본은 repo-wide이지만, 현재 운영 gate는
touched markdown만 `-Paths`로 lint하는 쪽을 우선한다.
`Assets/ThirdParty/**` 같은 upstream/vendor 문서는 lint 대상에서 제외한다.

## Unity 테스트

### 로컬

로컬 Unity editor 경로를 지정해 EditMode smoke test를 실행한다.

```powershell
pwsh -File tools/unity-editmode-smoke.ps1 -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.0.56f1\Editor\Unity.exe" -ProjectPath .
```

### CI

워크플로: `.github/workflows/unity-tests.yml`

현재 CI 대상:

- `game-ci/unity-test-runner@v4` 기반 EditMode smoke test
- PlayMode placeholder job

GitHub Actions secrets에 기대하는 환경 키:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

실제 값은 커밋하지 않는다.

## 기본 Smoke Check

### 로컬

```powershell
pwsh -File tools/smoke-check.ps1 -RepoRoot .
```

### CI와 맞춘 의도

smoke check는 핵심 저장소 경로와 거버넌스 문서가 여전히 존재하는지 확인한다.
더 무거운 검증 앞뒤에 둘 수 있는 빠른 sanity layer로 유지한다.
runtime playable smoke로 집계하지 않는다.

## 실패 시 먼저 볼 것

검증이 실패하면 아래 항목을 먼저 확인한다.

1. 필수 파일이나 폴더가 의도적으로 이름 변경 또는 이동되었는지
2. 문서 링크가 더 이상 존재하지 않는 파일을 가리키는지
3. Unity editor 버전과 package 상태가 프로젝트 기대값과 맞는지
4. 로컬 Unity 경로가 해당 머신에서 올바른지
5. Unity 테스트용 GitHub Actions secrets가 올바르게 설정됐는지
6. 실패 원인이 project-owned code인지 third-party/vendor 경계인지

## 범위 경계

이 자동화 계층은 아직 full build/deploy automation을 포함하지 않는다.
현재 범위는 아래로 제한한다.

- documentation validation
- EditMode-oriented Unity smoke testing
- basic repository smoke checks

## 현재 readiness 구분

### 즉시 CI에서 실행 가능

- markdown lint
- markdown link check
- PlayMode placeholder job

### 추가 설정이 필요한 항목

- Unity EditMode CI 실행에는 Unity license 관련 secrets가 필요하다:
  - `UNITY_LICENSE`
  - `UNITY_EMAIL`
  - `UNITY_PASSWORD`

### 즉시 로컬에서 실행 가능

- `tools/docs-check.ps1`
- `tools/smoke-check.ps1`
- `tools/unity-editmode-smoke.ps1`는 올바른 로컬 Unity editor 경로를 넣으면 바로 실행할 수 있다
