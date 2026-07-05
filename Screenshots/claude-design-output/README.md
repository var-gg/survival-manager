# claude.ai/design 1차 batch 산출 — 10 panel HTML

본 폴더는 claude.ai/design Chrome MCP 자동화로 받아낸 10 panel HTML 산출이다. survival-manager 시연 영상 cut sheet에 해당하는 surface 전부 (Town 외 modal 9 + Battle HUD 1) 다 받아놨다. 본 README는 산출 quality 평가와 다음 round 권고 두 파트로 짜여 있다.

## 산출 1줄 요약

| panel | file | size | USS-safe | 한국어 typography | mockup IA 정합 |
| --- | --- | --- | --- | --- | --- |
| Roster Grid | `roster-grid.html` | 26 KB / 1600×900 | ✅ 100% | ✅ `word-break: keep-all` + Pretendard | △ 12 hero grid + filter chip 갖춤 |
| Character Sheet | `character-sheet.html` | 79 KB / 1664×936 | ✅ 100% | ✅ belt-and-suspenders nowrap | ✅ stat rail + 4 CTA action bar + breadcrumb |
| Squad Workshop | `squad-workshop.html` | 31 KB / 1664×936 | ✅ 100% | ✅ | △ 배치/태세/위험 chip strip + footer CTA |
| Atlas | `atlas.html` | 52 KB / 1664×936 | ✅ 100% | ✅ | ✅ 2D 노드맵 + 출정/정찰 dual CTA + 마을 복귀 |
| Battle HUD | `battle-hud.html` | 52 KB / 1920×1080 | ✅ 100% | ✅ | ✅ 5-rail (hero / action queue / target / env / log) |
| Reward | `reward.html` | 35 KB / 1664×936 | ✅ 100% | ✅ | ✅ minimalist + tooltip drill-down 의도 반영 |
| Permanent Augment | `permanent-augment.html` | 40 KB / 1664×936 | ✅ 100% | ✅ | △ 16 augment 카드 grid, signature effect 표시 area 있음 |
| Passive Board | `passive-board.html` | 38 KB / 1664×936 | ✅ 100% | ✅ | △ node ring + tier label, density는 wave-passive-board-density 후속 |
| Equipment Refit | `equipment-refit.html` | 67 KB / 1664×936 | ✅ 100% (1건 text-transform false positive) | ✅ | ✅ 3-section (standee+slot / affix list / inventory pool) + Echo CTA |
| Inventory | `inventory.html` | 19 KB / 1680×945 | ✅ 100% | ✅ | △ 4-tab (전체/무기/방어/장신) + sort/filter strip |

`✅`는 별도 작업 없이 USS 변환 가능, `△`는 mockup 핵심 요소는 들어왔지만 density·세부 binding은 wave-3 단계에서 보강 필요.

## quality 평가 — 잘 나온 점

**1. USS portable subset 강제 효과가 산출 전반에 박혀 있다.**
모든 file 머리에 동일한 주석 패턴 (`flexbox only · no grid · no transform · no @keyframes · no box-shadow · no gradient`)이 있고, 실제로도 grid·transform·animation·box-shadow·gradient를 거의 안 쓴다. 코너 cap도 `::before / ::after` 없이 실제 `<span class="cap tl/tr/bl/br">` 노드로 만들어서 USS pseudo-element 부재 우회까지 미리 처리했다. 1664×936 frame을 잡고 그 안에서 flex column으로 풀어내는 패턴이 10 file에 일관돼 있어, presenter binding 작성할 때 같은 mental model을 재사용할 수 있다.

**2. design token이 file 간에 거의 동일하다.**
`--bg-0~3 / --line-1~2 / --gold-100~500 / --vellum / --ink-1~4 / --rare-* / --state-*` 계열은 10 file 모두 같은 hex 값으로 정의돼 있다. 색·typography가 panel 간에 자연스럽게 이어진다는 뜻이고, 사용자가 PlayMode walk에서 "panel을 옮겨다닐 때 어색하지 않다" 느낌을 받을 수 있는 baseline이 잡혔다.

**3. 한국어 typography 처리가 신중하다.**
모든 file에 `word-break: keep-all` + Pretendard fallback chain (system-ui → Apple SD Gothic Neo)이 들어 있다. character-sheet은 더 나아가 짧은 label마다 `white-space: nowrap`를 belt-and-suspenders로 박아 둬서, 렌더러가 `keep-all`을 무시해도 단어가 깨지지 않게 보장한다. ko/en 병기 위치(eyebrow caption, hero name 옆 별칭)도 mockup 의도와 정합한다.

**4. mockup IA가 prototype에 반영됐다.**
Equipment Refit은 좌측 standee + 3 slot / 중앙 affix 5줄 + Echo CTA / 우측 inventory pool 3-column 그대로, Atlas는 출정/정찰 dual CTA + 마을 복귀 + 진행도 표시, Character Sheet는 4 CTA action bar + stat rail + breadcrumb까지 들어왔다. 본 wave에서 P0/P1 fix로 직접 UXML에 박았던 element 구조가 prototype에도 같은 모양으로 잡혀 있어, 변환 pipeline이 element 단위 1:1 매핑으로 동작할 가능성이 높다.

## quality 평가 — 손봐야 할 점

**1. canvas size가 1664×936 / 1600×900 / 1680×945 / 1920×1080 네 가지로 갈렸다.**
Battle HUD가 1920×1080을 잡은 건 self-evident하지만 (full-screen HUD), modal panel 9개 중 Roster (1600×900) / Inventory (1680×945)가 다른 1664×936 panel과 size가 다르다. Unity 측 modal-overlay host가 사실 viewport에 stretch되므로 size 자체가 문제되진 않지만, prototype 간 비교할 때 동일 scale로 못 본다. Round 2에서 1664×936으로 통일 필요.

**2. token은 같지만 `:root{}` block이 file마다 반복된다.**
사용자가 의도했던 atomic design system이라면 `design-tokens.css` 한 개를 모든 panel이 `@import`하는 모양이어야 하는데, 실제로는 각 HTML이 자체 `<style>` 안에 토큰을 다시 선언했다. token 값이 drift할 위험이 있고 (현재는 동일하지만 다음 generation에서 한 file이 `--gold-300` 값을 바꾸면 silent inconsistency 발생), atomic 변경 (예: 4 세력 색 reskin)이 10 file × 4~5 토큰 = 40~50 spot 수동 sync 작업이 된다.

**3. race token name (`--fam-beastkin`)이 잔존한다.**
`character-sheet.html`과 `squad-workshop.html`에 `--fam-beastkin: #7a8a52` token이 들어 있다. ADR-0024로 4 race → 4 인간 세력 reskin이 lore-only 결정으로 진행 중이라 runtime SoT는 보존되지만, 이 token name은 reader-facing prototype에 박힌 라벨 영역이라 인간 세력 명칭 (또는 race-agnostic stable id `--fam-1/2/3/4`)으로 정리해야 한다. token name 변경은 prototype 재생성으로 흡수하는 게 깔끔하다.

**4. Equipment Refit page chrome에 radial-gradient가 있다.**
`equipment-refit.html`의 `.page` 클래스에 `background-image: radial-gradient(...)` 3중첩이 있다. 자체 주석으로 "NOT part of USS payload — scales preview only"라고 명시해뒀고 실제 modal frame `.erp-modal`은 USS-safe니까 변환 시 `.page` block은 drop하면 된다. 다만 자동 변환 pipeline이 `.page` block을 USS payload로 잘못 흡수하지 않게 marker가 필요하다.

## 사용자 원래 의도와의 gap — atomic system 미통합

> 처음에 저렇게 다 따로 디자인시스템이 아니라 아토믹하게 구성되고 필요하면 클로드디자인이 알아서 아토믹 추가하면서 패널프리뷰도 제공하고 그런걸 하나의 디자인시스템에 담길줄알았는데 다 따로했네

claude.ai/design은 prototype 단위로 동작하므로 "Design System" prototype 한 개 + "Panel Preview" prototype 9개를 묶어 cross-import하는 방식이 atomic 통합의 정공법이다. 본 batch에서는 그 워크플로우를 못 잡고 10 panel 각각을 독립 prototype으로 돌렸다.

### Round 2 권고 — atomic 통합 재생성

다음 round에서는 prototype 구성을 두 단계로 분리한다:

1. **첫 prototype: "Survival Manager Design System v0.3"** — 토큰만 정의 (`:root{}` block + 한국어 typography baseline + 4 corner cap pattern + frame shell). 산출은 `design-system.css` 한 file. 사용자가 색·typography·corner ornament만 review해서 baseline 확정.

2. **panel 9개 + Battle HUD 1개를 design system reference로 재생성** — 각 panel prompt에 "import design-system.css, do not redefine tokens, panel canvas 1664×936"을 박는다. 산출은 `<panel-name>.html` (body만) + `<panel-name>.css` (panel-specific class만). 토큰 drift 차단 + atomic 변경 (4 세력 reskin 같은 것) 한 곳 수정으로 propagate.

본 batch는 token drift가 아직 없으므로 "버리고 다시 받는다"가 아니라 "참조 산출로 보존, Round 2를 atomic 통합으로 새로 받는다"가 적절하다. 1차 batch도 mockup IA 정합·USS subset 준수 측면에서 reference quality 충분하니, presenter binding 설계 시 element 구조 reference로 그대로 활용 가능하다.

## 다음 작업 후보

- **Round 2 — atomic design system + 10 panel 재생성**: 위 권고대로 prototype 분리. claude.ai/design Chrome MCP 자동화 pattern은 본 batch에서 확립됐으므로 1.5~2시간 안에 batch 가능.
- **HTML → UXML/USS 변환 pipeline 설계**: element 1:1 매핑 (`<div class="...">` → `<ui:VisualElement class="...">`) + CSS subset 검증 (background-image / grid-template / transform / animation drop) + Unity-specific 후처리 (`<span class="cap">` → `<ui:VisualElement class="cap" picking-mode="Ignore">`) + presenter binding stub. 한 panel당 1시간 분량.
- **wave-passive-board-density**: Passive Board가 mockup 대비 sparse한 점은 본 batch가 해소 못 함. node 추가 + ring layout 풍부화는 별도 wave.

## 파일 인덱스

```text
Screenshots/claude-design-output/
├── README.md                   ← 본 문서
├── atlas.html                  ← 원정 지도 (출정/정찰 dual CTA + 마을 복귀)
├── battle-hud.html             ← Battle HUD 5-rail
├── character-sheet.html        ← 캐릭터 시트 + 4 CTA action bar
├── equipment-refit.html        ← REFIT 3-section + Echo CTA
├── inventory.html              ← 인벤토리 4-tab
├── passive-board.html          ← 패시브 보드 ring grid
├── permanent-augment.html      ← 영구 증강 16-card grid
├── reward.html                 ← 보상 선택 minimalist
├── roster-grid.html            ← 영웅 명부 12-hero grid
└── squad-workshop.html         ← 전술 편성 chip strip
```

산출일: 2026-05-28. claude.ai/design Chrome MCP batch generation (단일 세션, 10 prototype 분리). 평가 SoT: 본 README + `Logs/ux-bible-visual-qa/20260527-162019-02d22b229996/visual_verdict.json` (당시 production UXML 기준 점수).
