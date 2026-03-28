# survival-manager

`survival-manager`는 Unity 프로젝트를 레포지토리 루트에 두는 구조를 전제로 한 게임 저장소 프로토타입이다.

## 목적

이 저장소의 현재 목적은 **목각인형 수준 playable vertical slice**를 빠르게 검증할 수 있는 구현/문서/테스트 기반을 유지하는 것이다.

## 현재 단계

- phase: `prototype`
- 구현 허용 범위: `Assets/_Game/**`, `Assets/Tests/**`
- 문서 언어: 한국어 기준
- 파일명, 코드, API 식별자: 영어 유지

## 현재 범위

- Unity 루트형 프로젝트 구조 유지
- 순수 C# 코어/전투/메타 최소 구현 유지
- 콘텐츠 정의, 시드 생성, 검증 루프 유지
- JSON fallback 중심 persistence 경계 정리
- 플레이어블 vertical slice를 위한 씬/프리미티브 기반 작업 준비

## 저장소 루트 구조

- `Assets/`: Unity 에셋과 게임 코드
- `Packages/`: Unity 패키지 선언 및 잠금 파일
- `ProjectSettings/`: Unity 프로젝트 설정
- `docs/`: 설계 및 운영 문서
- `prompts/`: 에이전트/자동화 프롬프트
- `tools/`: 로컬 보조 스크립트와 유틸리티
- `.agents/`: 에이전트 협업 규칙과 스킬 확장점
- `.github/`: GitHub 운영 메타파일
- `.codex/`: Codex 작업 보조 설정

## 작업 원칙

1. Unity 프로젝트는 레포 루트에 둔다.
2. 지속 문서는 한국어로 유지한다.
3. 구현 변경 시 문서와 인덱스를 함께 갱신한다.
4. 게임 구현은 `Assets/_Game/**`, 테스트는 `Assets/Tests/**` 범위에서 진행한다.
5. 현재 목표는 완성도보다 "한 판이 완주되는지"를 검증하는 playable vertical slice다.

## 다음 추천 단계

1. Boot -> Town -> Expedition -> Battle -> Reward -> Town 씬 루프 실제 연결
2. primitive 기반 UI/카메라/디버그 흐름 구현
3. JSON persistence를 실제 플레이 루프에 접속
4. PlayMode 통합 검증 루프 보강
