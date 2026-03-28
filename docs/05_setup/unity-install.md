# Unity 설치 가이드

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/05_setup/unity-install.md`
- 관련문서:
  - `docs/05_setup/dev-environment.md`
  - `docs/03_architecture/unity-project-layout.md`
  - `ProjectSettings/ProjectVersion.txt`

## 목적

이 문서는 `survival-manager`의 Unity 개발 환경 부트스트랩 절차를 정의한다. 목표는 Unity Hub 기반 설치, 안정적인 LTS 에디터 사용, URP 기반 프로젝트 시작이다.

## 현재 상태

- Unity Hub 기반으로 프로젝트 생성 완료
- 실제 Unity 프로젝트 루트: `A:\projects\game\survival-manager\survival-manager`
- 상위 폴더 `A:\projects\game\survival-manager` 아래에 프로젝트명이 한 번 더 중첩되어 생성되었음
- 사용 에디터 버전과 패키지 구성은 아래 실제 파일을 기준으로 판단한다

## 실제 생성 결과 확인 포인트

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorSettings.asset`
- `Packages/manifest.json`

## 기본 설정 기준

- Version Control: Visible Meta Files
- Asset Serialization: Force Text
- URP 템플릿 사용
- 테스트용 기본 씬/폴더만 준비
- 게임 구현은 아직 포함하지 않음

## 주의

현재 폴더는 한 단계 중첩되었다. 장기적으로 repo 루트를 어디로 둘지 결정해야 한다.

- 현재 실제 Unity 루트: `A:\projects\game\survival-manager\survival-manager`
- 의도했던 상위 경로: `A:\projects\game\survival-manager`

필요하면 추후 루트 평탄화(한 단계 위로 이동)를 진행한다.
