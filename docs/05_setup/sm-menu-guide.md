# SM 메뉴 운영 가이드

- 상태: active
- 최종수정일: 2026-05-13
- 단계: prototype

## 목적

SM 메뉴 진입점 전체를 사용자(SM 메뉴로만 Unity 에디터를 만지는 페르소나)
관점에서 워크플로우 단계별로 정리한다. 어떤 항목이 일상용이고 어떤 항목이
Codex / 자동화 전용인지 한 곳에서 확인한다.

## 우선 기억할 메뉴 (4개로 모든 게임 검증이 가능)

| 메뉴 | 용도 |
| --- | --- |
| `SM/전체테스트` | 풀 루프 preflight + Boot.unity 자동 오픈. 모든 처음 작업의 시작 |
| `SM/전투테스트` | Battle 씬 + Combat Sandbox 시나리오로 즉시 Play 진입 |
| `SM/Town 테스트` | Boot → StartRealm + GoToTown 자동. Town UI를 곧장 검증할 때 |
| `SM/Expedition 테스트` | 위 + BeginNewExpedition + GoToExpedition 자동 |
| `SM/Atlas테스트` | Atlas (월드맵) graybox Play 진입 |
| `SM/Atlas 미리보기 셋업` | Atlas.unity Edit 모드 — hex/leyline/sigil Scene 뷰 튠 |
| `SM/캐릭터/P09 외형 편집` | P09 외형 + 라이팅 baseline 튜닝 Studio |

자세한 풀 루프 실행은 [first-playable-bootstrap.md](first-playable-bootstrap.md) 참조.

## A. 일상 검증 워크플로우

### A1. 전체 흐름 한 번 돌리기

> Boot → Town → Expedition → Battle → Reward까지 한 번 통과하는지 확인.

1. `SM/전체테스트` 실행
2. Boot.unity가 자동 오픈됐는지 확인
3. Play 누름
4. Boot에서 `Start Local Run`
5. Town `Start Expedition` → Expedition `Enter Battle` → Battle 종료 → Reward 카드 1장 → `Return to Town`

실패 시 [local-runbook.md](local-runbook.md) 절차로.

### A2. 전투만 빠르게 검증

1. `SM/전투테스트` 실행 — Battle 씬으로 직행, Combat Sandbox 시나리오 자동 로드
2. 30초 이내 전투 한 사이클 끝나는지 확인

### A3. Town / Expedition 직접 진입

> Boot → 사용자 클릭 시퀀스를 거치지 않고 특정 UI 단계만 곧장 검증.
> 자동 진행은 production 진입 path (`StartRealm(OfflineLocal)` 호출 이후
> `SceneFlow.GoTo*`)를 그대로 재사용하므로 SessionState 상태가 일반 사용자와 동일.

1. `SM/Town 테스트` — Boot 진입 후 약 0.5초 후 Town으로 자동 advance.
   Town UI(deploy preview, expedition CTA, status label 등) 검증.
2. `SM/Expedition 테스트` — 위 + `BeginNewExpedition()` + checkpoint 저장 +
   Expedition 씬 자동 로드. expedition route summary, node selection 검증.
3. Reward 직접 진입은 dummy 전투 결과 주입이 필요해 별도 마일스톤.

### A4. Atlas (월드맵)

- `SM/Atlas테스트` — Atlas.unity Play 진입. hex/leyline/sigil interaction 검증.
- `SM/Atlas 미리보기 셋업` — Play 없이 Atlas.unity 열기 + graybox 자산 보장.
  Scene 뷰에서 hex 배치 / leyline / sigil aura 머티리얼 / 카메라 framing 직접 튠.

### A6. Battle 비주얼 튠 (Edit 모드 미리보기)

> map + dummy 캐릭터를 Edit 모드에서 띄워 슬라이더로 라이팅을 즉시 튠.

1. `SM/Battle/맵 + 캐릭터 미리보기 셋업` — `BattleRenderEnvironment` GameObject가 자동 셋업 + map prefab + dummy capsule 4개
2. Hierarchy에서 `BattleRenderEnvironment` 선택 (자동 선택됨)
3. Inspector 슬라이더로 ambient / sun / fog / bloom / post 튠 — Scene 뷰에 즉시 반영
4. Play 모드 진입 시 미리보기 워크스페이스는 자동 숨김
5. 끝나면 `SM/Battle/미리보기 정리`

### A7. 캐릭터 외형 + 표현 baseline 튠

1. `SM/캐릭터/P09 외형 편집` — Studio Window 오픈
2. 좌측 캐릭터 리스트에서 1명 선택
3. 우측 패널: 장비 / 머리 / 체격 / 색상 오버라이드
4. 하단 `Showcase Profile` foldout: 라이팅 baseline 슬라이더 — wiki 캡쳐 출력에도 같은 자산이 적용됨
5. 변경 시 미리보기에 즉시 반영

## B. 콘텐츠 저작 (가끔)

### B1. 전투 시나리오 / 팀 구성

`SM/저작/전투 샌드박스` — Combat Sandbox active handoff Inspector. team preset, scenario, layout 직접 편집.  
자세한 사용: [editor-sandbox-tooling.md](../03_architecture/editor-sandbox-tooling.md) / [quick-battle-smoke.md](quick-battle-smoke.md)

### B2. 영입 / 가챠 시나리오

`SM/저작/영입 샌드박스` — Recruitment Sandbox Window. recruit phase, pity state, candidate scoring 미리보기.  
자세한 사용: [recruitment-and-retrain-harness.md](recruitment-and-retrain-harness.md)

### B3. 내러티브 자산

| 메뉴 | 용도 |
| --- | --- |
| `SM/내러티브/시드 임포트` | `tools/narrative_build.py` 산출물을 Unity 자산으로 임포트 |
| `SM/내러티브/포트레이트 플레이스홀더 생성` | art-pipeline에 아직 없는 portrait를 256px placeholder로 자동 생성 |
| `SM/내러티브/내러티브 자산 검증` | dialogue sequence + localization key 충돌 검증 |
| `SM/내러티브/포트레이트 자산 검증` | authoring map의 portrait reference 확인 |
| `SM/내러티브/내러티브 콘텐츠 검증` | chapter beat / hero lore + story localization 통합 검증 |

## C. UI 전용 미리보기 (특수 case)

`SM/UI 워크샵 (전술) 미리보기` — TacticalWorkshop UI sandbox 씬을 단독 Play. 전술 UI 컴포넌트 디자인 검토용.

## D. 자동화 / 빌드 / 복구 (Codex 영역 — 일상에선 안 보임)

`SM/Internal/...` 아래 항목들은 코드 변경 후 자산 빌드, 검증 자동화, 깨졌을 때 복구용. 일상 검증 워크플로우에서는 손대지 않는다.

### D1. 캡쳐 (`SM/Internal/Capture/...`)

- `Battle Scene` — Battle 씬 정적 캡쳐
- `Battle Live (Game View)` — Play 모드 중 Game View 캡쳐
- `Battle Play Auto` — Play 진입 + 360 frame 대기 + 캡쳐 + Play 종료 자동화

### D2. 콘텐츠 빌드 (`SM/Internal/Content/...`)

map catalog 생성, sample content 시드, snapshot export, P09 catalog 재생성 등. 새 자산 카테고리 추가 시 사용.

### D3. P09 일괄 컬러 (`SM/Internal/P09/...`)

`Apply Detail Preserving Palettes` / `Export Detail Preserving Full Body Crops` — 25명 P09 캐릭터의 palette를 일괄 적용하고 wiki용 full-body crop을 일괄 출력.

### D4. 복구 (`SM/Internal/Recovery/...`)

씬 / localization / runtime panel / atlas / tactical workshop / combat sandbox 자산이 깨졌을 때 명시 복구. `SM/전체테스트`가 안 통과하면 이 메뉴로 진입.

### D5. 검증 (`SM/Internal/Validation/...`)

content definition / canonical content / battle actor wrapper / loop D balance / first playable slice 자동 검증. CI / 커밋 전 확인용.

## E. 씬별 갭 (남은 누락 진입점)

다음 항목은 사용 빈도가 낮아 별도 마일스톤으로 미룬다.

| 씬 / 영역 | 현재 진입 방법 | 우선순위 |
| --- | --- | --- |
| Reward 직접 진입 (dummy 결과 주입) | 전체테스트 → 전투 한 사이클 거치기 | 저 |
| 캐릭터 스킬 UI 미리보기 | 없음 (전투 진입 시 자동 표현) | 저 |

필요 우선순위가 올라오면 별도 마일스톤으로 추가. 처음 갭 분석에 있던
Town / Expedition / Atlas Edit-time 항목은 위 A3, A4 섹션에서 해결됨.

## 관련 문서

- [first-playable-bootstrap.md](first-playable-bootstrap.md) — 풀 루프 진입 절차
- [local-runbook.md](local-runbook.md) — 실패 시 복구 흐름
- [editor-sandbox-tooling.md](../03_architecture/editor-sandbox-tooling.md) — Combat Sandbox 아키텍처
- [quick-battle-smoke.md](quick-battle-smoke.md) — 전투 스모크 lane
- [recruitment-and-retrain-harness.md](recruitment-and-retrain-harness.md) — 영입 sandbox 사용
