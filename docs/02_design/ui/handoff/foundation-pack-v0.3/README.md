# Foundation Pack v0.3 — claude.ai/design 핸드오프 결과 (cycle 1) — historical reference

> ⚠ **이 HTML은 현재 비주얼 SoT가 아니다.** anime-painted 톤과 mismatch로 채택
> 보류. 비주얼 reference로 읽지 말 것. 현재 SoT는 `pindoc://town-ui-ux-시안-갤러리-v1-gallery-town-ui-mockups-v1`.

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-13
- 소스오브트루스: 이 README는 historical 자산의 위치 기준만 다룬다.
- 관련문서:
  - `docs/02_design/ui/handoff/foundation-pack-v0.2/README.md` (v0.2 baseline)
  - `docs/02_design/ui/battle-observer-ui.md`
  - `docs/02_design/ui/town-character-sheet-ui.md`
  - `docs/03_architecture/ui-runtime-architecture.md`
  - pindoc Analysis: `pindoc://ux-surface-catalog-v1-draft` (UX Surface 카탈로그 v3)
  - pindoc Analysis: `pindoc://claude-design-handoff-brief` (의뢰 brief v4)
  - pindoc Analysis: `pindoc://analysis-character-asset-matrix-dawn-priest` (단린 38장 자산 매트릭스)
  - pindoc Task: `pindoc://task-claude-design-cycle-2-to-7` (5/8 reset 후 cycle plan)

## 목적

5/8 weekly reset 후 첫 cycle. 잔여 6 cycle 중 자산 매칭 우선순위 ★★★ 인 narrative surface(`Dialogue.PortraitScene`)를 v0.2 톤 baseline으로 mock. 단린 캐릭터 자산 매트릭스(bust_R 8장 + bust_L 8장 + face_emotion 8장)가 직접 시각 검증 가능한 첫 surface.

## 파일 목록

| 파일 | 크기 | Cycle | 역할 |
| --- | --- | --- | --- |
| `surface-dialogue-portrait-scene.html` | ~80 KB | 1 | Surface · Dialogue.PortraitScene v0.1 — VN-style 대화 surface, PC + Mobile 각 5 state (idle / typing / completed / auto-advance / backlog) = 10 frame |

총 1 file. v0.2의 9 file에 누적되어 v0.3에서 + 1 surface.

## 디자인 결정 요약

claude.ai/design 출력에서 흡수한 핵심 설계 결정. **v0.2 token 그대로 인용** — 새 token 추가 없음.

### Layout

- **PC** (1280×720 ref) — 좌 bust + 우 bust (2-character default) 또는 좌·중·우 (3-character beat). Active speaker는 focus state (gold ring + family-tint glow), 비활성은 dim state (filter brightness 0.45). Bottom-anchored DialogueBox at viewport ~30%. 우상단 narrative HUD strip (Skip + Auto + VoicePlayer + Backlog + Settings). BacklogList = 340w right drawer (Modal shell drawer variant).
- **Mobile** (380×760 ref) — Portrait 작게, 상반부. DialogueBox 하단 ~40% full-width + bottomSheet drag grip. HUD strip top safe-area inset (notch / dynamic island 회피). 44pt min touch target. BacklogList = fullscreen modal.

### State matrix (5 × 2 = 10 frame)

1. **idle** — speaker bust focus + DialogueBox text fully revealed + Next hint pulse
2. **typing** — caret cursor 표시 (ms/char cadence) + voice indicator playing + AutoAdvanceToggle off
3. **completed** — 3-character beat + ChoicePromptList 3-row visible (선택 분기 시점)
4. **auto-advance** — toggle ON + ring fill progress + speed pill (1× / 1.5× / 2×) + voice playing
5. **backlog** — drawer (PC) / fullscreen (Mobile) 6 line history + family-tint left rule per line + top/bottom fade

### 새 mini-molecule

- **`ChoicePromptList`** — Button(ghost) row stack + ChoiceId mono tag + KO/JP label + ▶ arrow + first-row hi state. Reuse Button atom v0.1, no new atom.

### Token 인용 (변경 없음, v0.2 그대로)

- `--vellum` / `--gold-300` (cap + ring)
- `--fam-mystic` / `--fam-mystic-deep` (단린 + 묵향)
- `--fam-beastkin` (이빨바람, 3-character beat 전용)
- 5-font stack: Pretendard (KO) / Cinzel (EN display) / Cormorant Garamond (italic) / Shippori Mincho (JA) / JetBrains Mono (tag)
- Dotted grid stage BG (style-guide v0.2 인용)

### 캐릭터 placeholder (한국어 reskin 정책 일관)

- **단린 / Dawn Priest** — Mystic family-tint, Right-position bust (R variant), serious 표정. (단린 자산 매트릭스 reference 캐릭터 — bust_serious_R / face_serious 두 자산이 idle/typing/completed state placeholder로 대응)
- **묵향 / Grave Hexer** — Mystic family-tint deep variant, Left-position bust (L variant), somber 표정. (자산 미생성 — 다음 ref 캐릭터 후보)
- **이빨바람 / Pack Raider** — Beastkin family-tint, Center-position bust, 3-character beat 전용. (자산 미생성)

`カエデ` 같은 일본 이름 placeholder 함정 회피 — 한국어 ID + 영문 별칭 형식 일관 (메모리 `project_character_naming_korean.md`).

## 단린 자산 매트릭스 매핑

`pindoc://analysis-character-asset-matrix-dawn-priest`의 38장 자산 중 본 surface 직접 매핑 후보:

| 매트릭스 카테고리 | 본 surface state 매핑 |
| --- | --- |
| bust_emotion_R (8) — 화면 좌측 위치 | (현재는 단린 right-position이라 L 사용) — 다른 캐릭터가 좌측 화자일 때 R 변형 사용 |
| bust_emotion_L (8) — 화면 우측 위치 | 단린 default placement = right-position bust → bust_default_L / bust_serious_L 호출 |
| face_emotion (8 narrative) | DialogueBox header 옆 작은 face indicator 후보 (현재 mock에는 없음, 후속 cycle에서 add 검토) |
| `portrait_bust_<emotion>_R/L` | `resolve_dialogue_bust(speaker_id, position, emotion)` lookup function 직결 — 본 mock은 P09 placeholder silhouette이지만 실 구현 단계에서 swap 1:1 |

mock 단계는 silhouette + family-tint glow + "P09 RT" 라벨로 placeholder. 실 구현에서는 PortraitCaptureService → `bust_<emotion>_<R|L>.png` lookup으로 자동 swap.

## 사용 원칙 (v0.2와 동일, 재확인)

**claude.ai/design 출력은 React/Tailwind 기반 HTML/CSS이고 Unity UITK는 USS/UXML이라 코드 직접 transpile 부적합.** 시각 reference / 컨셉 / 일관성 / component contract만 흡수한다. 본 surface mock은 v0.2 token + molecule contract를 그대로 인용해 `Dialogue.PortraitScene`의 layout pattern + state matrix만 추가 정의.

## Unity UITK 흡수 (대기 — v0.2 phase 진행 우선)

본 v0.3는 **시각 reference 단계**. Unity 적용은 v0.2 phase 3-4 (atom UXML/USS) 완료 후 narrative surface로 이어감 (`task-foundation-pack-v0-2-unity-adoption`).

2026-05-08 audit 기준으로 Unity runtime에는 아직 `ChoicePromptList`, backlog drawer, auto-advance ring, voice player state가 들어오지 않았다. 구현 Task는 `pindoc://task-dialogue-portrait-scene-v0-3-uitk-adoption`이며, 본 HTML은 layout/state matrix reference로만 사용한다.

```text
start docs/02_design/ui/handoff/foundation-pack-v0.3/surface-dialogue-portrait-scene.html
```

브라우저에서 열어 v0.2 `molecules-narrative-battle.html`과 시각 톤 비교.

## 다음 단계 (cycle 3 — Battle shell)

`task-claude-design-cycle-2-to-7` plan에 따라 다음 cycle은 **Battle shell 전체** 권장:
- LeftRoster + RightRoster + CenterSummary + StatusHeadline + CompactLog + ControlBar + UnitDetailPanel
- 카탈로그 v3 surface 우선순위 P0
- Foundation pack v0.4/ 폴더로 누적

본 cycle에서 정의된 `ChoicePromptList` mini-molecule은 v0.4에서 직접 재사용 가능 (Reward.AugmentChoice 3선다와 contract 유사).

## 한계 및 주의사항

- **Mobile artboard 폭 380px**: 1080×1920 portrait reference보다 작게 mock됨. 시각 검증용으로는 비율 유지, Unity 구현 단계에선 1080 ref 그대로 사용.
- **Choice prompt copy는 placeholder**: 실제 dialogue choice는 narrative SoT (한국어 1차, 일본어 voice)에서 host. mock의 choice 라벨은 demo용.
- **BG layer = scene_art_placeholder**: 실 cutscene 배경 image는 Cutscene.Player3D 또는 narrative cutscene asset에서 호출. 본 mock은 dotted grid stage BG로 대체.
- **face_emotion swap은 mock 미포함**: face_combat_state는 전투 face indicator (HP / CC 트리거)와 별도 매트릭스. dialogue surface는 narrative emotion (default/smile/serious/shock/anger/sad/cry/quiet) 8종이 후보 — 후속 cycle에서 face indicator molecule add 검토.
- **claude.ai/design omelette wrapper 제거됨**: 저장 시 `<style data-omelette-injected>` + `<script data-omelette-injected>` 제거 (v0.2 패턴 일관). claude.ai 외부에서 standalone preview 가능.

## 변경 이력

- 2026-05-08 16:30 — Cycle 1 (Dialogue.PortraitScene) generation 완료 + repo move + README 작성. weekly limit reset 후 첫 cycle.
