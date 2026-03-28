# Unity 프로젝트 레이아웃

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/unity-project-layout.md`

## 목적

이 문서는 `survival-manager`의 Unity 프로젝트 구조 초안을 정의한다. asmdef 도입을 전제로 폴더 경계를 먼저 정리한다.

## 현재 실제 Unity 루트

`A:\projects\game\survival-manager\survival-manager`

## Assets 구조

- `Assets/ThirdParty/`: 외부 패키지/플러그인/벤더 자산
- `Assets/_Game/`: 프로젝트 고유 자산과 코드
- `Assets/_Generated/`: 재생성 가능한 생성 산출물
- `Assets/Tests/`: EditMode/PlayMode 테스트 자산

## _Game 내부 초안

- `Assets/_Game/Art/`
- `Assets/_Game/Audio/`
- `Assets/_Game/Prefabs/`
- `Assets/_Game/Scenes/`
- `Assets/_Game/Scripts/`
- `Assets/_Game/Settings/`

## asmdef 원칙

- 초기에는 최소 개수로 시작한다
- 기능 또는 계층 경계가 명확해질 때 분리한다
- 테스트용 asmdef는 `Assets/Tests/` 아래 별도 관리한다

## 기본 설정 기준

- Version Control: Visible Meta Files
- Asset Serialization: Force Text

## 범위 제외

- 실제 게임 로직 구현
- 시스템 설계 확정
- 아트/프리팹 제작
