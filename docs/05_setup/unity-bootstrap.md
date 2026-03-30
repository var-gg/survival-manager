# Unity Bootstrap

- 상태: active
- 최종수정일: 2026-03-29
- 단계: prototype

## 목적

이 문서는 현재 로컬 Unity 상태를 기준으로 프로젝트 부트스트랩 truth를 기록한다.
문서와 실제 프로젝트 파일이 어긋나면 이 문서보다 실제 Unity truth file을 우선해서 갱신한다.

## 실제 에디터 버전

현재 로컬 설치 확인 기준 에디터 버전은 다음과 같다.

- `6000.4.0f1`

`ProjectSettings/ProjectVersion.txt`는 이 값으로 고정한다.

## 실제 패키지 기준

현재/다음 단계에 필요한 최소 패키지만 유지한다.

- `com.unity.render-pipelines.universal`: `17.0.4`
- `com.unity.textmeshpro`: `3.0.9`
- `com.unity.ugui`: `2.0.0`
- `com.unity.inputsystem`: `1.13.1`
- `com.unity.test-framework`: `1.5.1`

보류:

- Addressables

Addressables는 아직 playable vertical slice를 검증하는 데 필수가 아니므로 보류한다.

## 빌드 씬 등록 순서

`ProjectSettings/EditorBuildSettings.asset` 기준 씬 순서는 다음과 같다.

1. `Assets/_Game/Scenes/Boot.unity`
2. `Assets/_Game/Scenes/Town.unity`
3. `Assets/_Game/Scenes/Expedition.unity`
4. `Assets/_Game/Scenes/Battle.unity`
5. `Assets/_Game/Scenes/Reward.unity`

## 현재 구조 기준

현재 truth 기준 구현 범위는 다음과 같다.

- 게임 구현: `Assets/_Game/**`
- 테스트: `Assets/Tests/**`

## 비고

- 문서에만 패키지를 적어두고 manifest를 비워 두는 상태는 허용하지 않는다.
- CI 설정은 위 에디터 버전, 패키지 truth, 테스트 구조와 일치해야 한다.
- `packages-lock.json`은 Unity가 실제 해석 후 생성/갱신한 결과를 따른다.
