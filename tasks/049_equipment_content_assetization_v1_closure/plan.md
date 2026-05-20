# 장비 콘텐츠 V1 자산화 closure plan

- 상태: active
- 소유자: codex
- 최종수정일: 2026-05-20
- 소스오브트루스: `tasks/049_equipment_content_assetization_v1_closure/spec.md`

## Preflight

- GPT-Pro 제출로 equipment item/affix/crafting 범위 검수
- Pindoc Decision 발행
- dirty worktree 확인
- 관련 docs / validator / seed / runtime 파일 읽기
- `$docs-maintainer`, `$code-structure-guard` 기준 적용

## Phase 1 code-only

1. `EquipmentContentV1Contract`에 item/affix/drop manifest 추가
2. `EquipmentContentV1CatalogValidator`로 V1 수량과 forbidden surface 검증
3. `EquipmentContentV1Assetizer`로 sample content 후처리 경로 추가
4. `SampleSeedGenerator.Generate()` 끝에서 V1 assetizer 재적용
5. runtime generated item affix builder 추가
6. reward settlement와 expedition reward item 생성 경로 보정
7. refit 후보를 live `Prefix` / `Suffix` affix로 제한
8. content icon resolver에 affix semantic fallback 추가

## Phase 2 asset authoring

1. 42개 item asset의 rarity와 identity를 V1 manifest에 맞춘다.
2. Named/Unique item에는 granted skill 또는 rule marker payload를 둔다.
3. 30개 affix asset의 tier, family, spawn weight, compatibility, modifier payload를 채운다.
4. reserved 6개 affix는 `SpawnWeight = 0`, `ItemLevelMin = 999`로 live roll에서 제외한다.
5. skirmish / elite / boss drop table에 required item reward entry를 유지한다.
6. `first_playable_slice`에는 live affix order와 reserved parking lot을 기록한다.

## Phase 3 validation

- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
- `pwsh -File tools/unity-bridge.ps1 test-batch-edit -TestFilter EquipmentAssets_ExposeV1AssetizationContract`
- `pwsh -File tools/unity-bridge.ps1 content-validate`
- `pwsh -File tools/test-harness-lint.ps1`
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## rollback / escape hatch

Assetizer 적용 후 asset churn이 과하면 `EquipmentContentV1Contract` manifest만 남기고 asset patch를 한 번 되돌린다.
runtime reward item 생성이 기존 save/session 테스트와 충돌하면 old fake item 경로로 돌아가지 않고 generated affix count를 줄인다.
전체 `content-validate`가 장비 외 영역에서 실패하면 장비 관련 validator green 여부와 남은 에러 도메인을 분리 기록한다.

## tool usage plan

- 파일 탐색은 `rg`와 `Get-Content`
- 수동 편집은 `apply_patch`
- asset migration은 Unity execute method
- Unity 검증은 `tools/unity-bridge.ps1`
- Pindoc 결정은 pindoc artifact publish
- GPT-Pro 검수는 `$gpt-pro-submit`

## loop budget

이번 루프의 목표는 장비 assetization closure다.
material crafting, recipe, source/lock/rolled value는 같은 루프에서 열지 않는다.
전체 content validator의 encounter/support routing 잔여 오류는 별도 task로 분리한다.
