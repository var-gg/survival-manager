# 콘텐츠 파이프라인

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/03_architecture/content-pipeline.md`
- 관련문서:
  - `docs/03_architecture/content-authoring-model.md`
  - `docs/03_architecture/content-loading-strategy.md`
  - `docs/05_setup/asset-workflow.md`

## 목적

이 문서는 콘텐츠 추가와 승격이 어떤 순서로 이뤄져야 하는지 정의한다.

## 권장 순서

1. 데이터 계약을 정의하거나 갱신한다.
2. `ScriptableObject` 기반 콘텐츠 asset을 만든다.
3. 프로젝트 소유 prefab과 wrapper를 만든다.
4. sandbox나 격리된 검증 경로에서 확인한다.
5. 승인된 asset과 설정만 메인 gameplay 경로로 승격한다.
6. 다른 방법이 없을 때만 scene을 좁게 수정한다.

## 자동화 선호

Codex 자동화는 아래 산출물을 우선 작성해야 한다.

- data asset
- project-owned prefab
- config asset
- validation report

live scene 대량 편집은 기본 경로로 삼지 않는다.
