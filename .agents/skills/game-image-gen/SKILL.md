---
name: game-image-gen
description: 게임 이미지 리소스(캐릭터 일러스트, 스킬·패시브·equipment 아이콘, 환경·cutscene)를 ChatGPT 게임 프로젝트로 자동 생성. 사용자는 subject_id + variant + emotion만 지시. 에이전트가 Playwright로 ChatGPT UI를 직접 조작 — ref 첨부 + prompt 입력 + 폴링 + 다운로드 + chroma_key 누끼 + Unity import 후보 경로 저장까지 자동. vargg-webtoon comic-imagegen 패턴 차용.
---

# game-image-gen

survival-manager 게임용 이미지 자동 생성 스킬. 사용자는 지시만, 에이전트가 직접 ChatGPT를 조작해서 결과물까지 회수.

## Source-of-truth guardrail

Pindoc-owned prompt/spec/style 입력은 repo-local Markdown으로 새로 만들지 않는다.

- Art Bible, UI detail manifest, UI mockup prompt, 신규 skill/equipment/passive prompt 원본은 Pindoc Wiki가 source-of-truth다.
- 새 Pindoc-owned 입력은 repo 밖 JSON으로 hydrate해서 `--subject-json`, `--style-json`, `--pipeline-root`로 실행한다.
- `art-pipeline/working/**/*.md`, `art-pipeline/subjects/ui_detail/**/*.md`, `art-pipeline/subjects/ui_mockups/**/*.md`, UI style-anchor Markdown을 생성하지 않는다. `tools/test-harness-lint.ps1`가 이를 실패로 잡는다.
- 기존 legacy character/map/background/icon subject Markdown은 backward compatibility로 읽을 수 있다. 단, 새 prompt/spec Markdown을 repo에 늘리지 않는다.
- 기준 결정: `pindoc://decision-imagegen-prompt-source-no-repo-md`.

## 호출 패턴

```text
# character (kind 생략 시 character 폴백 — backward compat)
/game-image-gen 단린 portrait_full default
/game-image-gen hero_dawn_priest portrait_bust serious
/game-image-gen 단린 portrait_full default --force

# map (site당 시안 1장 + 선택적 painted variant)
/game-image-gen map site_wolfpine_trail concept_thumbnail
/game-image-gen map site_sunken_bastion concept_thumbnail
/game-image-gen map site_glass_forest painted_dusk          # baseline screenshot 있을 때 선택적 variant

# icon (skill / passive / equipment)
/game-image-gen icon_skill mystic_phantom_summon default
/game-image-gen icon_passive memory_anchor default
/game-image-gen icon_equipment grave_relic default

# cutscene
/game-image-gen cutscene aldric_journal_discovery shot_01

# background (town / UI BG / loading)
/game-image-gen background town_main_hall day
```

Legacy 매핑: `{kind?} {subject_id} {variant} [emotion]` → 이미 존재하는 `art-pipeline/subjects/{kind_dir}/{subject_id}/{variant}_{emotion}.md`

신규 Pindoc-owned 매핑: Pindoc artifact body → repo 밖 `subject.json` / `style.json` → `upload_subject.py --subject-json ... --style-json ... --pipeline-root art-pipeline`

| 호출 kind | kind_dir | frontmatter `kind` 후보 |
| --- | --- | --- |
| (생략) | `characters` | `character_portrait_full` 등 (backward compat) |
| `character` | `characters` | `character_portrait_full` / `_bust` / `_face` / `character_battle_stance` |
| `map` | `maps` | `map_concept` / `map_layout` / `map_decor_breakdown` / `map_painted` |
| `icon_skill` | `icons/skill` | `skill_icon` / `skill_icon_theme_sheet` |
| `icon_passive` | `icons/passive` | `passive_icon` |
| `icon_equipment` | `icons/equipment` | `equipment_icon` |
| `cutscene` | `cutscenes` | `cutscene_cut` |
| `background` | `backgrounds` | `environment_site` |

Legacy subject는 emotion이 default면 파일명은 `{variant}_default.md` 또는 `{variant}.md`를 읽을 수 있다. 신규 입력은 repo Markdown 파일명을 만들지 말고 JSON `frontmatter.variant` / `frontmatter.emotion`으로 표현한다.

## 실행 흐름 (에이전트가 직접 수행)

### Phase 1 — input 정규화

1. 사용자 메시지 파싱: subject_id (한국어/영문 별칭 모두 허용 — 단린 → hero_dawn_priest 매핑)
2. variant + emotion 추출
3. 입력 source 결정:
   - 기존 legacy subject가 이미 있으면 해당 `.md`를 읽어도 된다.
   - 새 Pindoc-owned prompt/spec/style이면 Pindoc artifact를 읽고 repo 밖 JSON으로 hydrate한다.
   - 캐릭터 팔레트 기반 skill theme 같은 임시 입력도 repo subject Markdown을 만들지 않고 JSON 또는 Pindoc artifact로 둔다.

### Phase 2 — 입력 spec 확인 / 생성

Legacy subject가 이미 존재하면 읽기 전용으로 사용한다. 없으면:
1. `mcp__pindoc__pindoc_artifact_read`로 wiki 읽고 `## 외모`/`## P09 visual spec` 추출
2. 신규 원본은 Pindoc Wiki에 작성/보강한다.
3. 실행 직전 repo 밖 임시 JSON을 만든다. 권장 위치는 `$env:TEMP/sm-imagegen-*/subject.json` / `style.json` 같은 repo 외부 경로다.
4. 사용자에게 미리 보여주고 confirm 받음 (자동 생성한 명세는 검수 필요)

### Phase 3 — ref 이미지 확인

`art-pipeline/ref/characters/{subject_id}/anchor.png` 존재 확인.

없으면 pindoc wiki에 첨부된 P09 캡쳐 자동 다운로드:
1. `mcp__pindoc__pindoc_artifact_read`로 wiki 본문에서 첨부 asset uuid 추출
2. `mcp__pindoc__pindoc_asset_read`로 sha256 받기
3. `pwsh -File art-pipeline/scripts/fetch_ref.ps1 -Sha256 {sha} -Out art-pipeline/ref/characters/{subject_id}/anchor.png`

### Phase 4 — 자동 생성 실행

```powershell
python art-pipeline/scripts/upload_subject.py `
    --subject-json $env:TEMP\sm-imagegen\subject.json `
    --style-json $env:TEMP\sm-imagegen\style.json `
    --pipeline-root art-pipeline
```

내부적으로:
1. `assemble_prompt.py`가 `style/style-anchor-common.md` + kind별 style JSON 또는 legacy style anchor + REF 첨부 블록 + subject prompt 조립
2. Playwright `launch_persistent_context` (`~/.Codex/game-image-gen/chrome-user-data/`)
3. ChatGPT 게임 프로젝트(`g-p-69c7aa09...`)로 navigate
4. 로그인 대기 (첫 실행만 사용자 supervision 필요. 이후는 세션 영구 저장)
5. force_new_chat 가드 (이전 `/c/...` 복원 방어)
6. disable_pro_mode (Pro extended mode chip 자동 제거 — 활성 시 reasoning 모드로 빠져 이미지 생성 안 됨)
7. REF 업로드 (DataTransfer JS 주입 — `set_input_files` 막힘)
8. prompt 주입 (ProseMirror `execCommand('insertText')`)
9. submit + 폴링 (`alt^=생성된 이미지` / `oaiusercontent` / `estuary/content`)
10. 이미지 다운로드 (page context fetch + 세션 쿠키 자동)
11. raw 저장: `art-pipeline/output/{subject_id}/{variant}_raw.png`
12. chroma_key 자동 적용: `art-pipeline/output/{subject_id}/{variant}.png` (transparent PNG)
13. legacy Markdown subject일 때만 frontmatter status: `prompted` → `rendered`
14. JSON subject 입력은 파일 status를 되쓰지 않는다. 상태 기록은 Pindoc Task/Artifact 또는 실행 로그가 담당한다.
15. 결과 JSON 출력 (size, width, height, paths)

### Phase 5 — 결과 보고

사용자에게 보고:
- final 파일 경로
- raw 파일 경로 (chroma 디버깅용)
- 시간 / 사이즈 / 해상도
- 다음 행동 제안 (재생성 `--force`, prompt 수정, Unity import 등)

## 디렉토리 구조

```text
art-pipeline/
├── style/                                    # legacy/common style anchors
│   ├── style-anchor-common.md                # 모든 kind 공통 (STYLE BASELINE + NEGATIVE COMMON)
│   ├── style-anchor-character.md             # character_*
│   ├── style-anchor-map.md                   # map_* / environment_site
│   ├── style-anchor-icon.md                  # skill_icon / passive_icon / equipment_icon
│   └── style-anchor-cutscene.md              # cutscene_cut
├── subjects/                                 # legacy subject inputs only; 신규 Pindoc-owned 입력 추가 금지
│   ├── characters/{subject_id}/{variant}_{emotion}.md
│   ├── maps/{site_id}/{cycle_stage}.md
│   ├── icons/skill/{skill_id}/{variant}.md
│   ├── icons/skill/character_theme_{character_id}/default.md  # legacy presentation-only theme bridge
│   ├── icons/passive/{passive_id}/{variant}.md
│   ├── icons/equipment/{equipment_id}/{variant}.md
│   ├── cutscenes/{scene_id}/{shot_id}.md
│   ├── backgrounds/{bg_id}/{variant}.md
│   └── _template/                            # kind별 5종 template subject 페이지
├── ref/
│   ├── characters/{subject_id}/anchor.png
│   ├── maps/{site_id}/{stage}.png            # cycle 5 reference_screenshot 또는 직접 ref
│   ├── icons/{subject_id}.png                # 선택적
│   ├── cutscenes/{scene_id}/...              # 선택적
│   └── backgrounds/{bg_id}/...               # 선택적
├── output/{subject_id}/                      # 생성 결과 (kind 무관 평면)
│   ├── {variant}.png                          # chroma 후 transparent (character/icon) 또는 raw 복사 (map/cutscene)
│   └── {variant}_raw.png                      # chroma 전 (디버깅용)
├── inbox/  working/  selected/               # 이미지/선별 working only; Markdown prompt hydrate 금지
├── postprocess/chroma_key.py
├── scripts/
│   ├── assemble_prompt.py                    # kind 분기 anchor + ref 디렉토리
│   ├── upload_subject.py                     # kind 기반 chroma 자동 분기
│   ├── audit_character_assets.py             # 캐릭터별 subject/ref/output coverage 감사
│   └── fetch_ref.ps1
├── config/
│   └── character_asset_manifest.yaml          # Pindoc dialogue character별 필요 이미지 matrix
├── .imagegen-config.yaml
├── .gitignore
└── README.md
```

## REF accumulation 정책 (chained REF)

자산 생성 순서대로 prior output을 ref로 누적 첨부. 캐릭터 6 cycle 표준 순서 + refs:

| 순서 | variant | refs |
| ---: | --- | --- |
| 1 | portrait_full | `[hero_X]` |
| 2 | face_emotion_sheet | `[hero_X, hero_X:portrait_full]` |
| 3 | face_combat_state_sheet | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 4 | bust_emotion_sheet_R | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 5 | bust_emotion_sheet_L | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default, hero_X:portrait_bust_default_R]` |
| 6 | battle_stance_sheet | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |

스킬/패시브/equipment 아이콘은 캐릭터 cycle에 종속하지 않는다. runtime/gameplay 기준은 `SkillId -> IconId -> Sprite` 프레젠테이션 카탈로그다. 캐릭터 팔레트가 필요한 임시 sheet도 신규 repo subject Markdown을 만들지 않고 Pindoc-owned spec + repo-external JSON의 `kind: skill_icon_theme_sheet`로 실행한다.

`refs:` syntax:
- `{char_id}` → P09 anchor (`ref/characters/{char_id}/anchor.png`)
- `{char_id}:{file_stem}` → prior output (`output/{char_id}/{file_stem}.png`)

prior output ref는 prompt에 "prior output illustration (canonical visual style baseline)"로 라벨링 — ChatGPT가 anchor와 다르게 weighting.

단린 38장은 chained REF 미적용 V0 정책으로 생성됨 (anchor 1장만). 다음 캐릭터부터 chained REF baseline.

## repo-external subject JSON schema

신규 Pindoc-owned 입력은 아래 JSON 형태로 repo 밖에 만든다.

```json
{
  "frontmatter": {
    "slug": "ui_card_frame_normal--default",
    "kind": "ui_detail_asset",
    "subject_id": "ui_card_frame_normal",
    "variant": "default",
    "refs": [],
    "aspect": "1:1",
    "output_size": "512x512",
    "chroma": "#FF00FF",
    "status": "prompted"
  },
  "prompt": "Generate one centered UI detail asset..."
}
```

style override가 필요한 Pindoc-owned UI/detail/mockup 작업은 repo 밖 `style.json`을 함께 넘긴다.

```json
{
  "art_style": "dark navy fantasy UI with restrained warm gold metalwork",
  "layout": "single centered asset with slice-safe margins",
  "shading": "subtle bevels, no exterior blur beyond the chroma area",
  "chroma": "pure #FF00FF outside the asset only",
  "negative": "no text, no watermark, no drop shadow outside the outer stroke"
}
```

실행 예:

```powershell
python art-pipeline/scripts/assemble_prompt.py `
    --subject-json $subjectJson `
    --style-json $styleJson `
    --pipeline-root art-pipeline `
    --json

python art-pipeline/scripts/upload_subject.py `
    --subject-json $subjectJson `
    --style-json $styleJson `
    --pipeline-root art-pipeline `
    --dry-run
```

## legacy subject 페이지 frontmatter (v2 schema)

아래 schema는 이미 존재하는 legacy subject Markdown을 읽을 때만 사용한다. 신규 Pindoc-owned 입력에는 사용하지 않는다.

```yaml
---
slug: hero_dawn_priest--portrait_full_default
kind: character_portrait_full              # character_portrait_* / skill_icon / passive_icon / equipment / environment / cutscene
subject_id: hero_dawn_priest
variant: portrait_full
emotion: default
refs:
  - hero_dawn_priest                       # → art-pipeline/ref/characters/{slug}/anchor.png
aspect: "2:3"
output_size: "1024x1536"
chroma: "#FF00FF"
status: prompted                           # idea | prompted | rendered | selected | published
---
```

## subject_kind 카탈로그

frontmatter `kind`가 sub-anchor + ref 디렉토리 + chroma default를 결정한다.

| kind | family | sub-anchor | ref dir | chroma default | 출력 사이즈 | aspect | composition |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `character_portrait_full` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 1024×1536 | 2:3 | 전신 3/4 view |
| `character_portrait_bust` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 1024×1536 | 2:3 | 흉상 |
| `character_portrait_face` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 1024×1024 | 1:1 | 얼굴 close-up |
| `character_battle_stance` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 1024×1536 | 2:3 | 전투 자세 |
| `face_emotion_sheet` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 3168×1568 | 4:2 | 8감정 face sheet |
| `face_combat_state_sheet` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 2368×1568 | 3:2 | 6전투상태 face sheet |
| `bust_emotion_sheet` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 3168×2336 | 4:2 | 8감정 bust sheet |
| `battle_stance_sheet` | character | `style-anchor-character.md` | `ref/characters` | `#FF00FF` ON | 1568×2336 | 2:2 | 4전투자세 full-body sheet |
| `skill_icon_theme_sheet` | icon | `style-anchor-icon.md` | `ref/characters` | `#FF00FF` ON | 1568×1568 | 2:2 | presentation-only character palette skill icon theme sheet |
| `map_concept` | map | `style-anchor-map.md` | `ref/maps` | OFF | 1920×1080 | 16:9 | site당 시안 1장 — quarter-view + 4-layer + mood + Unity kitbash ref + 아트북/로딩 통합 |
| `map_painted` | map | `style-anchor-map.md` | `ref/maps` | OFF | 1920×1080 | 16:9 | (선택) Unity baseline screenshot ref로 painted variant — 시간대/날씨/특수 mood 분기, narrative beat 강한 site만 |
| `skill_icon` | icon | `style-anchor-icon.md` | `ref/icons` | `#FF00FF` ON | 1024×1024 | 1:1 | centered icon |
| `passive_icon` | icon | `style-anchor-icon.md` | `ref/icons` | `#FF00FF` ON | 1024×1024 | 1:1 | abstract concept |
| `equipment_icon` | icon | `style-anchor-icon.md` | `ref/icons` | `#FF00FF` ON | 1024×1024 | 1:1 | item shot |
| `environment_site` | map (legacy) | `style-anchor-map.md` | `ref/backgrounds` | OFF | 1920×1080 | 16:9 | town BG / UI BG |
| `cutscene_cut` | cutscene | `style-anchor-cutscene.md` | `ref/cutscenes` | OFF | 1920×1080 | 16:9 | cinematic, multi-character |

맵 lifecycle (단순화 v2 — `pindoc://map-concept-cycle-and-edge-treatment-v1` 참조): site당 시안 1장(`map_concept`) + Unity 3D kitbash(skill 외부) + reference_screenshot 캡처(skill 외부) + 선택적 `map_painted` variant. 캐릭터 6-cycle은 맵에 적용 안 함 — 같은 site / 같은 quarter-view 각도라 다회 시안의 ROI가 낮다. ~~map_layout~~, ~~map_decor_breakdown~~ 폐기.

## 일관성 보장 — single anchor + prompt-driven

**핵심 원칙**: ref 이미지 1장 + 정밀한 prompt 명세로 일관성을 만듦. 다중 viewpoint ref나 강제 anchor 첨부에 의존하지 않음.

- ref 1장 (`anchor.png`): silhouette / 컬러 / 의상 layout 참조용. P09 모델링 캡쳐로 충분.
- subject 페이지 prompt fence: hair color HSL, 의상 색 HEX, accessory 위치, 무기 spec, eye color 등을 텍스트로 정밀 명시.
- "ATTACHED IMAGE은 simplified 3D MODEL 참조용. 더 detailed하게 그려라" 취지는 `style-anchor-character.md`의 reference relationship과 각 subject prompt의 REF-first block으로 유지한다.

## 캐릭터 필요 자산 감사

Pindoc wiki 기준 대사 캐릭터의 필요 이미지 세트는 `art-pipeline/config/character_asset_manifest.yaml`에서 관리한다.

Source:
- `pindoc://wiki-character-lore-registry-mirror` — 캐릭터 registry.
- `pindoc://analysis-character-operation-layer` — Support/Background/NPC까지 dialogue line budget.
- `pindoc://analysis-character-asset-matrix-dawn-priest` — 캐릭터당 full matrix.

정책:
- `story_dialogue_character`: 대사가 있는 모든 Pindoc wiki 캐릭터. full / face emotion / combat face / bust R-L / battle stance.
- `lead_story_combat`, `named_story_battle`, `battle_actor_core`는 legacy profile name이다. `battle_actor_core`도 더 이상 최소셋이 아니며 full matrix를 요구한다.

스킬/아이콘 필요 자산은 `art-pipeline/config/skill_asset_manifest.yaml`과 `art-pipeline/scripts/audit_skill_assets.py`가 별도 관리한다.

생성 전후에 다음을 실행해 ref, subject, output 누락을 확인한다.

```powershell
python art-pipeline/scripts/audit_character_assets.py
python art-pipeline/scripts/audit_character_assets.py --show-missing
python art-pipeline/scripts/audit_character_assets.py --profile story_dialogue_character --strict
```

## chroma key 누끼 (kind 자동 분기)

`upload_subject.py`가 frontmatter `kind` 기반으로 chroma 자동 결정.

| 상태 | 적용 |
| --- | --- |
| frontmatter `chroma: "#XXXXXX"` 명시 | 그 색으로 chroma_key 적용 |
| frontmatter `chroma: false` | OFF (raw → final 복사) |
| `--no-chroma` 플래그 | OFF 강제 (frontmatter 무시) |
| character_* / icon family / `skill_icon_theme_sheet` kind | `#FF00FF` 기본 ON |
| map_* / cutscene_cut / environment_site kind | 기본 OFF |
| 그 외 | `#FF00FF` 기본 ON (safe default) |

chroma_key 자동 적용 시 ChatGPT가 단색 배경 출력 → `chroma_key.py`가 floodfill + spill 제거 + edge feather → transparent PNG.
chroma 실패 시 raw는 `{variant}_raw.png`로 보존 → 사용자 수동 누끼 또는 rembg fallback (TBD).

## 의존성 (사용자가 한 번만 설치)

```powershell
pip install playwright pillow numpy scipy pyyaml psutil pywin32
python -m playwright install chromium
```

## 첫 실행 절차 (단린 portrait_full default 기준)

1. 의존성 설치 (위)
2. ref 이미지 확인: `art-pipeline/ref/characters/hero_dawn_priest/anchor.png` (이미 다운로드됨)
3. legacy subject가 이미 존재할 때만 명령 실행:

   ```powershell
   python art-pipeline/scripts/upload_subject.py `
       art-pipeline/subjects/characters/hero_dawn_priest/portrait_full_default.md
   ```

4. 첫 실행은 Playwright Chrome 창 뜸 → ChatGPT 수동 로그인 → 자동 진행 (이후 세션 영구 저장)
5. 결과: `art-pipeline/output/hero_dawn_priest/portrait_full.png` + `_raw.png`

## vargg-webtoon comic-imagegen와의 차이

차용: Playwright 영구 세션 + DataTransfer 주입 + force_new_chat + ProseMirror execCommand + 폴링 + 다운로드 + legacy subject frontmatter status 전이 + PID-scoped Win32 minimize.

게임 도메인 특화:
- series/episode 트리 폐기 → legacy flat subject page + 신규 repo-external JSON 입력
- style-anchor가 project-level 1개 (chapter LUT 도입 시 chapter-level로 확장 가능)
- single-anchor ref (P09 캡쳐 1장 / 캐릭터)
- chroma_key 누끼 자동 chain (vargg에는 없음, 게임 asset에 필요)
- KOREAN TEXT FIDELITY 블록 폐기 (말풍선 한글 렌더 불필요) → CHROMA BACKGROUND로 대체

## 함정 / 디버깅

| 증상 | 원인 | 대응 |
| --- | --- | --- |
| 캐릭터 일관성 깨짐 | prompt 명세 부족 (single anchor라 prompt가 anchor) | Pindoc spec 또는 repo-external JSON의 hair/outfit/accessory 명세 강화 (HEX, HSL, 위치) |
| 마젠타 안 빠짐 | 캐릭터에 마젠타 톤 들어감 / 외곽 outline 없음 | NEGATIVE 블록 강화, prompt에 outline 강조 |
| 누끼 외곽 거침 | ref outline이 약함 | chroma_key.py `--feather 1.5` 시도 |
| ChatGPT가 reasoning 모드로 빠짐 | Pro mode chip 활성 | `disable_pro_mode`가 자동 처리. 안 되면 ChatGPT UI에서 수동 OFF |
| 영구 세션이 이전 chat에서 시작 | persistent context restore | `force_new_chat`이 자동 처리 |
| 생성 timeout | ChatGPT 느림 | `--timeout 360` 시도 |
| Pro mode 다시 켜짐 | 다른 chat에서 켰음 | 매 실행마다 자동 비활성화 |

## 관련 문서

- `pindoc://flow-character-ref-image-pipeline` — 본 skill의 정책 baseline
- `pindoc://decision-imagegen-prompt-source-no-repo-md` — Pindoc-owned prompt/spec/style repo-local Markdown 금지선
- `pindoc://analysis-art-asset-volume-estimate` — 카테고리별 수량 추정
- `pindoc://analysis-cutscene-medium-mix` — 매체 mix 운용
- `pindoc://wiki-character-hero-dawn-priest` — 단린 wiki (첫 테스트 대상)
- `art-pipeline/style/style-anchor-common.md` + `art-pipeline/style/style-anchor-character.md` — legacy 캐릭터 프로젝트 베이스라인
- `art-pipeline/subjects/characters/_asset_matrix.md` — legacy P09 캐릭터별 필요 이미지 matrix
- `art-pipeline/config/skill_asset_manifest.yaml` — skill/presentation icon 필요 이미지 matrix
- vargg 원본: `A:\vargg-workspace\vargg-webtoon\.Codex\skills\comic-imagegen\`
