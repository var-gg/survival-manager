# ADR-0016 localization 경계와 공식 패키지 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 결정일: 2026-03-30
- 소스오브트루스: `docs/04_decisions/adr-0016-localization-boundary.md`
- 관련문서:
  - `docs/02_design/ui/localization-policy.md`
  - `docs/03_architecture/localization-runtime-and-content-pipeline.md`
  - `docs/03_architecture/unity-boundaries.md`

## 문맥

prototype이 Town, Expedition, Battle, Reward 화면과 데이터 주도 콘텐츠를 빠르게 늘리면서 raw 문자열이 asset과 controller에 누적되기 시작했다.
이 상태를 MVP 이후 한 번에 갈아엎으면 content id, UI refresh, battle log, font, import/export workflow를 동시에 retrofit해야 한다.

## 결정

다음 결정을 저장소 기준으로 채택한다.

- Unity 공식 `com.unity.localization` 패키지를 기본 표준으로 채택한다.
- MVP shipped locale은 `ko`, `en` 두 개만 유지한다.
- startup locale 선택 순서는 `PlayerPref -> System -> Specific(en)`로 고정한다.
- `SM.Content`, `SM.Combat`는 Unity Localization 타입을 참조하지 않는다.
- 콘텐츠와 전투 truth는 semantic key 또는 code만 저장하고, 최종 문자열 해석은 `SM.Unity`와 `SM.Editor`에서만 수행한다.
- 신규 플레이어 노출 텍스트는 localization 경유가 아니면 merge-ready가 아니다.

## 결과

### 기대 효과

- 콘텐츠 ID와 표시 문자열이 분리되어 문서 규칙과 충돌하지 않는다.
- locale change 시 UI, reward summary, battle log를 같은 구조로 다시 렌더링할 수 있다.
- asset table, pseudo-loc, Smart String, import/export workflow를 공식 패키지 기준으로 바로 사용할 수 있다.

### 감수할 비용

- sample asset과 validator를 함께 바꿔야 한다.
- Addressables/Localization 설정 자산이 repo에 durable하게 추가된다.
- 초기에 font와 table bootstrap을 같이 유지해야 한다.

## 기각한 대안

- MVP 이후 일괄 retrofit:
  - 현재 누적 속도상 content/UI/log 전부를 한 번에 치환해야 하므로 리스크가 커진다.
- 서드파티 localization 솔루션 기본 채택:
  - 현재 저장소는 Unity 6, 공식 문서 규칙, 장기 유지보수와 AI agent 협업을 우선하므로 공식 패키지가 더 안전하다.
