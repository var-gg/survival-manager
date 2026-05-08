# art-pipeline

게임 이미지 리소스 자동 생성 워크스페이스. `.claude/skills/game-image-gen` skill의 운영 디렉토리.

## 디렉토리 구조

| 경로 | 역할 | git |
| --- | --- | --- |
| `style/style-anchor.md` | 4 블록 project-level 베이스라인 (ART STYLE / SHADING / CHROMA / NEGATIVE) | commit |
| `subjects/{kind}/{subject_id}/{variant}_{emotion}.md` | v2 schema subject 페이지 | commit |
| `ref/characters/{subject_id}/anchor.png` | single-anchor ref (P09 캡쳐) | commit |
| `output/{subject_id}/{variant}.png` | chroma 후 transparent 결과, Unity import 전 중간산출물 | gitignore |
| `output/{subject_id}/{variant}_raw.png` | chroma 전 마젠타 배경 raw, 재생성/검수용 중간산출물 | gitignore |
| `Assets/Resources/_Game/Art/Characters/{subject_id}/` | 런타임에서 사용하는 선별 production 이미지와 Unity `.meta` | commit |
| `inbox/` `working/` `selected/` | 수동 조작용 (재시도/선별) | gitignore |
| `postprocess/chroma_key.py` | 누끼 스크립트 | commit |
| `scripts/assemble_prompt.py` | prompt 조립 (Playwright 없이 단독 testable) | commit |
| `scripts/upload_subject.py` | 메인 오케스트레이터 (Playwright + ChatGPT + chroma chain) | commit |
| `scripts/fetch_ref.ps1` | pindoc asset → docker cp helper | commit |
| `.imagegen-config.yaml` | ChatGPT 게임 프로젝트 URL + user_data_dir 등 | commit |

## 의존성 설치 (한 번만)

```powershell
pip install playwright pillow numpy scipy pyyaml psutil pywin32
python -m playwright install chromium
```

## 호출

```
/game-image-gen 단린 portrait_full default
```

내부 처리: skill이 subject 페이지 path 결정 → `python scripts/upload_subject.py {그 페이지}` 호출 → ChatGPT 자동 조작 → chroma 누끼 → 결과 보고.

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

# chroma 건너뛰기 (환경/cutscene 등 누끼 불필요한 경우)
python art-pipeline/scripts/upload_subject.py {subject.md} --no-chroma

# 헤드리스 (세션 저장 후에만)
python art-pipeline/scripts/upload_subject.py {subject.md} --headless

# 디버깅: 브라우저 안 닫기
python art-pipeline/scripts/upload_subject.py {subject.md} --keep-browser-open
```

## 첫 실행 (단린 portrait_full default)

1. 의존성 설치
2. `art-pipeline/ref/characters/hero_dawn_priest/anchor.png` 확인 (없으면 `fetch_ref.ps1`로 다운로드)
3. 위 단독 실행 명령
4. **첫 실행만** Playwright Chrome 창 뜸 → ChatGPT 수동 로그인 → 자동 진행
5. 결과: `art-pipeline/output/hero_dawn_priest/portrait_full.png` (transparent) + `portrait_full_raw.png`

## REF accumulation 정책 (chained REF)

자산 생성 순서대로 prior output을 ref로 누적 첨부 — 같은 캐릭터의 직전 일러를 다음 cycle의 visual anchor로 사용해 일관성 강화.

### 캐릭터 자산 7 cycle 표준 순서 + REFs

| 순서 | variant | refs frontmatter |
|---:|---|---|
| 1 | `portrait_full_default` | `[hero_X]` (P09 anchor만) |
| 2 | `face_emotion_sheet` | `[hero_X, hero_X:portrait_full]` |
| 3 | `face_combat_state_sheet` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 4 | `bust_emotion_sheet_R` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 5 | `bust_emotion_sheet_L` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default, hero_X:portrait_bust_default_R]` |
| 6 | `battle_stance_sheet` | `[hero_X, hero_X:portrait_full, hero_X:portrait_face_default]` |
| 7 | `skill_icon_sheet` | `[hero_X, hero_X:portrait_full]` (color palette anchor만) |

### `refs:` syntax

- `{char_id}` → `art-pipeline/ref/characters/{char_id}/anchor.png` (P09 simplified 3D model)
- `{char_id}:{file_stem}` → `art-pipeline/output/{char_id}/{file_stem}.png` (prior output)

`assemble_prompt.py`가 두 syntax 자동 분기. prior output ref는 prompt에 "prior output illustration (canonical visual style baseline)" 라벨로 첨부 — ChatGPT가 단순 anchor와 prior 일러를 다르게 weighting하도록 instruct.

### 단린 (reference 케이스)

단린 38장은 **anchor 1장만 ref로 사용**한 V0 정책 (chained REF 미적용)으로 생성됨. baseline 안정화에는 충분했으나, 다음 캐릭터부터 chained REF로 일관성 강화. 단린 retrofit은 검증 후 결정.

## chroma key 누끼

prompt에서 background `#FF00FF` 강제 → `chroma_key.py`가 floodfill + spill 제거 + edge feather.

```powershell
# upload_subject.py가 자동 호출. 수동 호출 시:
python postprocess/chroma_key.py raw.png final.png
python postprocess/chroma_key.py raw.png final.png --tolerance 35 --feather 1.5 --spill 0.7
```

옵션 가이드:
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

- Granblue Fantasy character art (Hideo Minaba 디렉터 수준) 80%
- Honkai Star Rail character splash art 20%
- 일본 정통 painterly + cel mix
- 명암: 닫힐 땐 하드, 풀릴 땐 그라데이션
- 일관성: ref가 아니라 **subject 페이지 prompt 명세 정밀도**로 보장
- chapter LUT는 후처리 단계 (`pindoc://analysis-cutscene-medium-mix` 색 톤표)

## 관련 문서

- skill: `.claude/skills/game-image-gen/SKILL.md`
- pipeline 정책: `pindoc://flow-character-ref-image-pipeline`
- 수량 추정: `pindoc://analysis-art-asset-volume-estimate`
- 매체 mix: `pindoc://analysis-cutscene-medium-mix`
- vargg 원본 패턴: `A:\vargg-workspace\vargg-webtoon\.claude\skills\comic-imagegen\`
