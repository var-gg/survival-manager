# 내러티브 어서링 가이드

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-11
- 관련문서:
  - `docs/02_design/narrative/master-script.md`
  - `docs/02_design/narrative/dialogue-event-schema.md`
  - `tools/narrative-authoring-map.json`

## 목적

신규 contributor가 이 문서만 보고 dialogue-scene, dialogue-overlay, story-card, toast-banner를 작성할 수 있도록 한다.

## 작업 흐름

1. `docs/02_design/narrative/master-script.md`에서 대사 블록 작성
2. `docs/02_design/narrative/dialogue-event-schema.md`에서 이벤트 행 추가
3. `pwsh tools/narrative-validate.ps1` — 구조/교차참조 검증
4. `pwsh tools/narrative-build.ps1` — JSON manifest → SO + Localization + Portrait 자동 생성
5. Unity Play로 확인

## 대사 블록 포맷

### dialogue-scene / dialogue-overlay

heading + metadata + 테이블 형식:

```markdown
### `{presentation_key}` — {표시 제목}

> **컨텍스트**: 장면 설명
> **연출**: dialogue-scene

| # | 화자 | 감정 | 대사 |
|---|---|---|---|
| 0 | Narrator | — | 지문 묘사. 따옴표 없이. |
| 1 | Dawn Priest | resolute | "캐릭터 대사. 따옴표 포함." |
| 2 | Pack Raider | skeptical | "다른 캐릭터 대사." |
```

규칙:
- `presentation_key`는 backtick으로 감싸야 한다
- `연출`은 `dialogue-scene` 또는 `dialogue-overlay`
- dialogue-scene: 8~14줄, Narrator 지문 포함, non-narrator 화자 최대 4명
- dialogue-overlay: 2~6줄, non-narrator 화자 최대 2명 권장
- Narrator 줄은 따옴표 없이 작성
- 캐릭터 줄은 따옴표로 감싸서 작성 (빌드 시 자동 제거)

### story-card

```markdown
### `{presentation_key}` — {표시 제목}

> **연출**: story-card

**제목 텍스트**

본문 1~3문단.
```

### toast-banner

```markdown
### `{presentation_key}` — {표시 제목}

> **연출**: toast-banner

**제목 텍스트**

짧은 본문 1~2문장.
```

## 이벤트 스키마 행 작성법

`dialogue-event-schema.md`의 이벤트 테이블에 행을 추가:

```markdown
| `story_event_{purpose}` | `{Moment}` | {priority} | `{OncePolicy}` | `{conditions}` | `{effects}` | `{presentation_key}` |
```

- **story_event_id**: `story_event_` 접두사 + 목적 설명
- **moment**: `SiteEntered`, `BattleStarted`, `BattleResolved`, `RewardCommitted`, `ExtractCommitted`, `TownEntered` 등
- **priority**: 높을수록 먼저 평가 (일반 100, 보스 200~250, 중요 장면 500+)
- **once_policy**: `OncePerProfile` (기본), `OncePerRun`, `Repeatable`
- **conditions**: 쉼표로 구분. 예: `ChapterIs:chapter_ashen_gate`, `SiteIs:site_ashen_gate`, `NodeIs:4`
- **effects**: 쉼표로 구분. 예: `SetFlag:story_flag_name`, `UnlockHero:hero_id`
- **presentation_key**: master-script.md의 블록 heading과 일치해야 함

`EnqueuePresentation` effect는 자동 합성되므로 직접 쓰지 않는다.

## Alias 규칙

### 화자 Alias

| 작성명 | Runtime ID |
|---|---|
| Dawn Priest | `hero_dawn_priest` |
| Pack Raider | `hero_pack_raider` |
| Grave Hexer | `hero_grave_hexer` |
| Echo Savant | `hero_echo_savant` |
| Rift Stalker | `hero_rift_stalker` |
| Bastion Penitent | `hero_bastion_penitent` |
| Pale Executor | `hero_pale_executor` |
| Aegis Sentinel | `hero_aegis_sentinel` |
| Shardblade | `hero_shardblade` |
| Prism Seeker | `hero_prism_seeker` |
| Mirror Cantor | `hero_mirror_cantor` |
| Aldric Sternholt | `aldric_sternholt` |
| Narrator | `Narrator` |

### 감정 Alias

| 감정 | EmotionId | Portrait EmoteId |
|---|---|---|
| resolute | resolute | Resolute |
| grim | grim | Grim |
| bitter | bitter | Grim |
| skeptical | skeptical | Skeptical |
| defiant | defiant | Defiant |
| solemn | solemn | Resolute |
| shock | shock | Shock |
| sardonic | sardonic | Skeptical |
| tense | tense | Grim |
| gentle | gentle | Default |
| weary | weary | Grim |
| — | none | Default |

### Condition Alias

| 작성명 | Runtime |
|---|---|
| `ChapterIs` | ChapterIs |
| `SiteIs` | SiteIs |
| `NodeIs` | NodeIs |
| `FlagIs` | FlagSet |
| `FlagNot` | FlagNotSet |
| `HeroUnlocked` | HeroUnlocked |
| `HeroNotUnlocked` | HeroNotUnlocked |

### Effect Alias

| 작성명 | Runtime |
|---|---|
| `SetFlag` | SetFlag |
| `ClearFlag` | ClearFlag |
| `UnlockHero` | UnlockHero |
| `UnlockMode` | UnlockMode |

## 검증

```powershell
pwsh tools/narrative-validate.ps1    # 구조/교차참조/prose 검증
pwsh tools/narrative-build.ps1       # 전체 빌드 파이프라인
```

Error가 있으면 빌드가 실패한다. Warning은 빌드는 통과하지만 수정을 권장한다.

## Narrator 제약

- 감정 해설 금지: "무겁다", "무섭다", "두렵다", "슬프다" 등
- 의미 선포 금지: "가장 ~한", "상징하는", "의미하는"
- 허용: 물리적 관찰 — 동작, 사물, 환경, 소리, 빛
