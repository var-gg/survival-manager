# art-pipeline

게임 이미지 리소스 자동 생성 워크스페이스. `.claude/skills/game-image-gen` skill의 운영 디렉토리.

multi-subject 운영 정책: `pindoc://decision-game-image-gen-multi-subject-operations`.

## 디렉토리 구조

| 경로 | 역할 | git |
| --- | --- | --- |
| `style/style-anchor-common.md` | 모든 kind 공통 STYLE BASELINE + NEGATIVE COMMON | commit |
| `style/style-anchor-{character,map,icon,cutscene}.md` | kind별 sub-anchor 4종 | commit |
| `subjects/characters/{subject_id}/{variant}_{emotion}.md` | character v2 schema subject 페이지 | commit |
| `subjects/maps/{site_id}/{cycle_stage}.md` | map cycle subject 페이지 | commit |
| `subjects/icons/{skill,passive,equipment}/{id}/{variant}.md` | icon subject 페이지. 스킬 이미지는 character hierarchy가 아니라 이 하위에 둔다. | commit |
| `subjects/cutscenes/{scene_id}/{shot_id}.md` | cutscene subject 페이지 | commit |
| `subjects/backgrounds/{bg_id}/{variant}.md` | town BG / UI BG / 로딩 화면 subject 페이지 | commit |
| `subjects/_template/{character,map,icon,cutscene,background}.md` | kind별 5종 template (복사 시작점) | commit |
| `ref/characters/{subject_id}/anchor.png` | character anchor ref (P09 캡쳐) | commit |
| `ref/maps/{site_id}/{stage}.png` | map ref (cycle 5 reference_screenshot 또는 직접 ref) | commit |
| `ref/{icons,cutscenes,backgrounds}/...` | kind별 선택적 ref | commit |
| `output/{subject_id}/{variant}.png` | chroma 후 transparent (character/icon) 또는 raw 복사 (map/cutscene) | gitignore |
| `output/{subject_id}/{variant}_raw.png` | chroma 전 raw (디버깅용 / map·cutscene은 raw==final) | gitignore |
| `Assets/Resources/_Game/Art/Characters/{subject_id}/` | 런타임 production 이미지 + Unity `.meta` | commit |
| `inbox/` `working/` `selected/` | 수동 조작용 (재시도/선별) | gitignore |
| `postprocess/chroma_key.py` | 누끼 스크립트 | commit |
| `scripts/assemble_prompt.py` | prompt 조립 (kind 분기 anchor + ref 디렉토리, 단독 testable) | commit |
| `scripts/upload_subject.py` | 메인 오케스트레이터 (Playwright + ChatGPT + kind 기반 chroma 자동) | commit |
| `scripts/audit_character_assets.py` | 캐릭터별 ref/subject/output coverage 감사 | commit |
| `scripts/audit_skill_assets.py` | skill/presentation icon subject/output coverage 감사 | commit |
| `scripts/audit_p09_visual_identity.py` | P09 preset 기반 alias/의상 겹침/색상 식별성 감사 | commit |
| `scripts/seed_character_portrait_subjects.py` | missing `portrait_full_default.md` subject page 생성 | commit |
| `scripts/fetch_ref.ps1` | pindoc asset → docker cp helper | commit |
| `config/character_asset_manifest.yaml` | Pindoc dialogue character별 필요 이미지 matrix | commit |
| `config/skill_asset_manifest.yaml` | skill/presentation icon 필요 이미지 matrix | commit |
| `config/p09_visual_identity_manifest.yaml` | P09 캐릭터별 소속/job/uniform group/식별 intent | commit |
| `.imagegen-config.yaml` | ChatGPT 게임 프로젝트 URL + user_data_dir 등 | commit |

## 의존성 설치 (한 번만)

```powershell
pip install playwright pillow numpy scipy pyyaml psutil pywin32
python -m playwright install chromium
```

## 호출

```text
# character (kind 생략 시 character 폴백 — backward compat)
/game-image-gen 단린 portrait_full default

# map cycle
/game-image-gen map site_wolfpine_trail concept_thumbnail
/game-image-gen map site_sunken_bastion layout_isometric

# icon
/game-image-gen icon_skill mystic_phantom_summon default

# cutscene
/game-image-gen cutscene aldric_journal_discovery shot_01

# background
/game-image-gen background town_main_hall day
```

skill 내부 처리: subject 페이지 path 결정 → `python scripts/upload_subject.py {그 페이지}` 호출 → ChatGPT 자동 조작 → chroma 누끼 자동 분기 → 결과 보고.

자세한 호출 패턴 + kind 매핑 + chroma 정책은 `.claude/skills/game-image-gen/SKILL.md` 참조.

## 단독 실행 (skill 없이 직접)

```powershell
# dry-run: prompt 조립만 출력 (브라우저 안 띄움)
python art-pipeline/scripts/upload_subject.py `
    art-pipeline/subjects/characters/hero_dawn_priest/portrait_full_default.md `
    --dry-run

# 실제 실행
python art-pipeline/scripts/upload_subject.py `
    art-pipeline/subjects/characters/hero_dawn_priest/portrait_full_default.md

# 재생성
python art-pipeline/scripts/upload_subject.py {subject.md} --force

# chroma 강제 OFF (frontmatter / kind default 무시)
python art-pipeline/scripts/upload_subject.py {subject.md} --no-chroma

# 헤드리스 (세션 저장 후에만)
python art-pipeline/scripts/upload_subject.py {subject.md} --headless

# 디버깅: 브라우저 안 닫기
python art-pipeline/scripts/upload_subject.py {subject.md} --keep-browser-open
```

## 캐릭터 자산 감사

생성 전후에 Pindoc wiki 기준 대사 캐릭터의 ref, subject page, output coverage를 확인한다. 기준은 `pindoc://wiki-character-lore-registry-mirror`, `pindoc://analysis-character-operation-layer`, `pindoc://analysis-character-asset-matrix-dawn-priest`다.

정책: 대사가 있는 캐릭터는 Lead/Support/Background/NPC 구분 없이 모두 `portrait_full`, 8종 narrative face, 6종 combat face, 8종 bust R/L, 4종 stance 대상이다. 예전 `battle_actor_core` 최소셋은 폐기된 호환 이름이며, 현재는 full set을 요구한다.

```powershell
python art-pipeline/scripts/audit_character_assets.py
python art-pipeline/scripts/audit_character_assets.py --show-missing
python art-pipeline/scripts/audit_character_assets.py --profile story_dialogue_character --strict
```

필요 세트와 캐릭터 배정은 `config/character_asset_manifest.yaml`이 source-of-truth이고, 사람이 읽는 요약은 `subjects/characters/_asset_matrix.md`에 둔다. 스킬 이미지는 캐릭터 필요 세트에 포함하지 않고 `config/skill_asset_manifest.yaml`에서 따로 관리한다.

former `battle_actor_core` 13명의 감정 face/bust gap을 채우는 subject와 queue:

```powershell
python art-pipeline/scripts/seed_character_sheet_subjects.py --profiles story_dialogue_character --skip-missing-identity
python art-pipeline/scripts/run_character_asset_queue.py --phase core-story-gap --skip-existing-output
```

P09 모델 캡쳐와 wiki/일러스트 생성 전 단계에서는 외형 식별성 감사도 같이 돌린다.

```powershell
python art-pipeline/scripts/audit_p09_visual_identity.py
python art-pipeline/scripts/audit_p09_visual_identity.py --format markdown
python art-pipeline/scripts/seed_character_portrait_subjects.py
```

`audit_p09_visual_identity.py`는 `config/p09_visual_identity_manifest.yaml`과 실제
`Assets/Resources/_Game/Battle/Appearances/P09/p09_appearance_*.asset`을 대조한다.
`priest`, `raider`, `hexer`는 runtime archetype fallback alias이며 Studio 편집 목록에서는 숨긴다.
`seed_character_portrait_subjects.py`는 이미 존재하는 Lead 4 subject page를 덮어쓰지 않고,
비어 있는 캐릭터의 `portrait_full_default.md`만 만든다.

## 스킬/아이콘 자산 감사

스킬 이미지는 캐릭터 하위 자산이 아니다. 런타임 바인딩은 `SkillId -> IconId -> Sprite` 프레젠테이션 카탈로그가 source-of-truth가 되어야 하며, `character_theme_*` sheet는 팔레트와 아이콘 언어를 잡기 위한 임시 presentation bridge로만 둔다.

```powershell
python art-pipeline/scripts/audit_skill_assets.py
python art-pipeline/scripts/audit_skill_assets.py --show-missing
```

## REF accumulation 정책 (chained REF)

자산 생성 순서대로 prior output을 ref로 누적 첨부 — 같은 subject의 직전 일러를 다음 cycle의 visual anchor로 사용해 일관성 강화.

### 캐릭터 6 cycle 표준 순서

| 순서 | variant | refs |
| ---: | --- | --- |
| 1 | `portrait_full_default` | `[hero_X]` (P09 anchor만) |
| 2 | `face_emotion_sheet` | `[hero_X, hero_X:portrait_full]` |
| 3 | `face_combat_state_sheet` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 4 | `bust_emotion_sheet_R` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 5 | `bust_emotion_sheet_L` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default, hero_X:portrait_bust_default_R]` |
| 6 | `battle_stance_sheet` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |

스킬/패시브/equipment 아이콘은 character cycle 밖의 `subjects/icons/**`에서 관리한다. 캐릭터 팔레트를 빌린 임시 sheet가 필요하면 `subjects/icons/skill/character_theme_{character_id}/default.md` + `kind: skill_icon_theme_sheet`를 사용한다.

### 맵 lifecycle (v2 단순화 — site당 1-2장)

`pindoc://map-concept-cycle-and-edge-treatment-v1`이 source-of-truth. 캐릭터 6-cycle은 맵에 적용 안 함 — 같은 site / 같은 quarter-view 각도라 다회 시안 ROI가 낮다.

| 단계 | 산출물 | 책임자 |
| --- | --- | --- |
| **map_concept** (시안 1장) | quarter-view + 4-layer + mood + Unity kitbash ref + 아트북/로딩 통합 | game-image-gen |
| Unity 3D kitbash | `BattleMap_{site}.prefab` (project-owned, vendor nested ref) | 사용자 + AI 협업 |
| reference_screenshot | Unity 카메라 캡처 → `ref/maps/{site_id}/baseline.png` | `tools/unity-bridge.ps1 capture-map` (후속 추가 예정) |
| **map_painted_{variant}** (선택) | 시간대/날씨/특수 mood 분기, narrative beat 강한 site만 (보스/결말 등). baseline screenshot을 ref로 | game-image-gen |

### `refs:` syntax

- `{subject_id}` → `art-pipeline/ref/{kind_dir}/{subject_id}/anchor.png` 또는 `art-pipeline/ref/{kind_dir}/{subject_id}.png` fallback
- `{subject_id}:{file_stem}` → `art-pipeline/output/{subject_id}/{file_stem}.png` (prior output)

`assemble_prompt.py`가 두 syntax 자동 분기. prior output ref는 prompt에 "prior output illustration (canonical visual style baseline)"로 라벨링 — ChatGPT가 단순 anchor와 prior 일러를 다르게 weighting하도록 instruct.

### 단린 (reference 케이스)

단린 38장은 **anchor 1장만 ref로 사용**한 V0 정책 (chained REF 미적용)으로 생성됨. baseline 안정화에는 충분했으나, 다음 캐릭터부터 chained REF로 일관성 강화. 단린 retrofit은 검증 후 결정.

## chroma key 누끼 (kind 자동 분기)

prompt에 `#FF00FF` 마젠타 배경 강제 → `chroma_key.py`가 floodfill + spill 제거 + edge feather → transparent PNG.

frontmatter `kind`에 따라 자동 분기:
- character_* / icon family / `skill_icon_theme_sheet` → ON
- map_* / cutscene_cut / environment_site → OFF (raw → final 복사)
- frontmatter `chroma` 명시 시 우선
- `--no-chroma` 플래그는 강제 OFF

수동 호출:

```powershell
python art-pipeline/postprocess/chroma_key.py raw.png final.png
python art-pipeline/postprocess/chroma_key.py raw.png final.png --tolerance 35 --feather 1.5 --spill 0.7
```

옵션:
- `--tolerance` (40 기본): 외곽 거칠면 ↑, 안쪽 잘리면 ↓
- `--feather` (1.0 기본): jagged 방지. 너무 크면 sharp 잃음
- `--spill` (0.6 기본): 외곽 마젠타 보라끼 제거 강도

## pindoc asset 직접 가져오기

캐릭터 wiki(`wiki-character-{id}`)에 첨부된 P09 캡쳐를 ref로 가져오기:

```powershell
# 1. asset uuid에서 sha256 추출 (Claude가 mcp__pindoc__pindoc_asset_read로)
# 2. helper 호출
pwsh -File scripts/fetch_ref.ps1 `
    -Sha256 {sha} `
    -Out ref/characters/{subject_id}/anchor.png
```

storage 매핑: `pindoc-server-daemon:/var/lib/pindoc/assets/{sha[0:2]}/{sha}` (extension 없음, pure binary blob).

## 그림체 baseline

- Granblue Fantasy character/environment art (Hideo Minaba 디렉터 수준) 80%
- Honkai Star Rail character splash & environment art 20%
- 일본 정통 painterly + cel mix
- 명암: 닫힐 땐 하드, 풀릴 땐 그라데이션
- 일관성: ref가 아니라 **subject 페이지 prompt 명세 정밀도**로 보장
- chapter LUT는 후처리 단계 (`pindoc://analysis-cutscene-medium-mix` 색 톤표)

## 관련 문서

- skill: `.claude/skills/game-image-gen/SKILL.md`
- character asset matrix: `art-pipeline/subjects/characters/_asset_matrix.md`
- multi-subject 운영 결정: `pindoc://decision-game-image-gen-multi-subject-operations`
- 맵 cycle 정책: `pindoc://map-concept-cycle-and-edge-treatment-v1`
- forest asset catalog: `pindoc://forest-semantic-asset-catalog-v1`
- site × asset matrix: `pindoc://chapter-site-asset-semantic-routing-matrix-v1`
- VFX catalog: `pindoc://epic-toon-fx-vfx-catalog-v1`
- pipeline 정책: `pindoc://flow-character-ref-image-pipeline`
- 수량 추정: `pindoc://analysis-art-asset-volume-estimate`
- 매체 mix: `pindoc://analysis-cutscene-medium-mix`
- vargg 원본 패턴: `A:\vargg-workspace\vargg-webtoon\.claude\skills\comic-imagegen\`
