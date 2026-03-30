# 에셋 워크플로

- 상태: draft
- 최종수정일: 2026-03-31
- 소유자: repository

## 목적

이 문서는 `survival-manager`에서 에셋을 반입하고, 검증하고, 승격하는 권장 워크플로를 설명한다.
목표는 Codex와 사람 기여자가 안전한 에셋 처리 기준을 공유하도록 만드는 것이다.

## 작업 규칙 요약

- vendor 원본은 `Assets/ThirdParty`에 둔다.
- project-owned wrapper와 gameplay asset은 `Assets/_Game`에 둔다.
- direct scene edit보다 data/prefab/settings 변경을 우선한다.
- mainline 사용 전에 sandbox에서 검증한다.
- high-risk 변경은 사람 검토로 올린다.

## 권장 워크플로

### 1. Intake

- 가져온 third-party source asset을 `Assets/ThirdParty`에 둔다.
- 에셋의 목적과 영향을 받을 시스템을 기록한다.
- rendering, input, save 등 넓은 프로젝트 concern을 건드리는지 확인한다.

### 2. Sandbox Validation

- 분리된 validation asset 또는 scene을 만든다.
- 최소 integration path만 먼저 테스트한다.
- vendor 원본을 수정하지 않고 쓸 수 있는지 확인한다.
- 필요한 wrapper, adapter, project-owned config asset을 식별한다.

### 3. Project-Owned Integration

- `Assets/_Game` 아래에 wrapper를 만든다.
- 필요하면 project ScriptableObject data asset이나 catalog를 만든다.
- 가져온 의존성을 격리하는 prefab 또는 config asset을 만든다.
- production scene을 첫 integration site로 쓰지 않는다.

### 4. Promotion

- 검토와 sandbox 검증을 마친 asset만 main gameplay path로 승격한다.
- 가정, naming, maintenance risk를 기록한다.
- 가능하면 reference가 project-owned asset을 가리키도록 유지한다.

## 사람 검토가 필요한 high-risk 변경

- vendor 원본 직접 수정
- 큰 scene rewiring
- project-wide setting 변경
- rendering pipeline 변경
- save/load 관련 identity 변경
- license나 update behavior가 불분명한 import

## 로컬 검증 체크리스트

- smoke check 통과
- docs 또는 intake note 갱신
- project-owned folder 아래 wrapper 존재
- accidental vendor-original edit 없음
- sandbox validation 결과를 검토자가 이해할 수 있음

## 열린 질문

- 다음에 표준화할 sandbox folder convention은 무엇인가?
- intake review에 `tasks/` 아래 checklist template를 써야 하는가?
- 어떤 imported asset category를 별도 workflow 문서로 분리해야 하는가?
- 최소 visual QA checklist에는 무엇이 들어가야 하는가?
