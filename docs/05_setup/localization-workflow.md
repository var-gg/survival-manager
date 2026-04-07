# Localization workflow

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-07
- 소스오브트루스: `docs/05_setup/localization-workflow.md`
- 관련문서:
  - `docs/02_design/ui/localization-policy.md`
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`
  - `docs/04_decisions/adr-0016-localization-boundary.md`

## 목적

이 문서는 localization foundation을 로컬에서 재생성하고, 새 key를 추가하고, ko/en/pseudo-loc를 검증하는 운영 절차를 정리한다.

## foundation 재생성

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. `SM/Setup/Ensure Localization Foundation`를 실행한다.
3. `Assets/Localization/Localization Settings.asset`이 존재하는지 확인한다.
4. `Assets/Localization/Locales/ko.asset`, `Assets/Localization/Locales/en.asset`, pseudo locale asset이 있는지 확인한다.
5. `Assets/Localization/StringTables/**`와 `Assets/Resources/_Game/Fonts/GameFontCatalog.asset`이 생성됐는지 확인한다.

## committed content 확인과 bootstrap repair

1. runtime truth는 committed asset `Assets/Resources/_Game/Content/Definitions/**`다.
2. 누락 복구만 필요하면 `SM/Setup/Ensure Sample Content`를 실행한다.
3. committed floor authoring이 실제로 깨졌을 때만 `SM/Setup/Generate Sample Content`를 repair 용도로 사용한다.
4. `SM/Validation/Validate Content Definitions`를 실행한다.
5. console과 report에서 missing key, duplicate key, legacy prose error가 없어야 한다.

## 새 localization key 추가 절차

1. 먼저 table ownership을 정한다.
2. lower-case dot key를 만든다.
3. ko/en 값을 같은 작업 단위에서 추가한다.
4. controller나 view model에서는 raw string 대신 key를 참조한다.
5. content asset이면 `NameKey`, `DescriptionKey`, `LabelKey` 필드로 연결한다.

## locale 전환 확인

1. `SM/Setup/Prepare Observer Playable`를 실행한다.
2. `Boot.unity` Play 후 화면 우측 상단 language overlay를 확인한다.
3. `ko`와 `en`을 번갈아 선택한다.
4. Town, Expedition, Battle, Reward 정적 라벨이 즉시 갱신되는지 본다.
5. scene 이동 후에도 선택 locale이 유지되는지 본다.

## fallback phase 확인

1. 현재 phase는 `PhaseB`를 기본으로 본다.
2. `editor/dev`에서는 fallback이 보일 수 있지만 shipped player-facing content validator는 missing entry를 error로 취급한다.
3. 출시 직전에는 `PhaseC` 기준으로 fallback 없는 경로를 재검증한다.

## pseudo-localization 사용

1. Localization Settings에서 pseudo locale을 editor 검증용으로 선택한다.
2. Town, Expedition, Battle, Reward를 한 번씩 순회한다.
3. overflow, 잘림, 하드코딩 문자열, font glyph 누락을 잡는다.
4. pseudo locale은 shipped locale에 포함하지 않는다.

## 폰트 운영 규칙

- 공용 UI font는 `Assets/ThirdParty/Fonts/NotoSansCJK/NotoSansCJKkr-Regular.otf`를 source로 사용한다.
- project-owned wrapper asset은 `Assets/Resources/_Game/Fonts/GameFontCatalog.asset`이다.
- runtime이 shared font를 못 찾으면 editor/test 호환용 fallback만 사용하고, 실제 해결은 catalog/source font 복구로 한다.
