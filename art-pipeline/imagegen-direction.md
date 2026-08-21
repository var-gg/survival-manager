<!-- 이 문서는 이전의 `game-image-gen` 스킬 본문이다.
     이미지 생성 진입점은 `gpt-imagegen` 하나로 통합됐고(프로젝트마다 SKILL.md를 만들지 않는다),
     이 repo가 소유하는 것은 아트 디렉션과 절차뿐이라 여기로 내려왔다.
     `.ai-media/image-workflow.json`의 context.promptPolicyFiles가 이 파일을 가리킨다. -->

# 이미지 생성 아트 디렉션

survival-manager의 도메인 입력과 QC만 소유한다. 공통 이미지 실행은 `ai-media`에 맡긴다.

## 입력 계약

작업 루트는 `A:\projects\game\survival-manager`다.

- 먼저 `.ai-media/image-workflow.json`과 manifest가 가리키는
  `art-pipeline/README.md`, `art-pipeline/style/style-anchor-common.md`를 읽는다.
- 기존 legacy subject Markdown은 읽을 수 있다. 새 Art Bible, UI detail/mockup,
  skill/equipment/passive prompt 원본은 Pindoc가 SSOT다.
- 새 Pindoc-owned 입력은 repo 밖 임시 `subject.json`/`style.json`으로 hydrate한다.
  `art-pipeline/working`이나 `subjects/ui_*`에 새 prompt Markdown을 만들지 않는다.
- reference는 manifest의 `art-pipeline/ref`, `style`, `subjects` 범위 안에서만 고른다.

## 절차

1. `{kind?} {subject_id} {variant} [emotion]`을 정규화한다.
2. 기존 subject 또는 Pindoc 원본에서 prompt를 조립하고 repo 밖 prompt 파일로 저장한다.
3. `assemble_prompt.py --dry-run`으로 style, ref, 비율, chroma 지시를 확인한다.
4. 공통 계획을 확인한다.

```powershell
& C:\projects\ai-infra\scripts\ai-media.ps1 --json image-workflow plan `
  A:\projects\game\survival-manager\.ai-media\image-workflow.json `
  --prompt-file <absolute-prompt-file> `
  --reference <absolute-reference.png> `
  --output-name <subject-variant>
```

5. 대화형 생성은 `gpt-imagegen` 스킬이 수행한다 — 이 repo의 ChatGPT 프로젝트와 전용 크롬 프로필로 ChatGPT 웹 + Playwright 경로를 실행한다.
6. character/icon은 지정 chroma로 누끼하고, map/cutscene/background는 raw를 유지한다.
7. raw/final 경로, 크기, 해상도, QC 결과와 Unity import 다음 단계를 보고한다.

## 가드

- Pindoc-owned 원본을 repo Markdown으로 복제하지 않는다.
- reference와 prior output의 역할을 prompt에 명시하고 무관한 workspace 문맥을 섞지 않는다.
- headless API는 plan의 `durable.eligible`이 참이고 해당 생성의 비용 승인이 있을 때만
  `image-workflow prepare`로 넘긴다.
- 생성 전후 coverage 감사는 repo의 `audit_character_assets.py`와
  `audit_skill_assets.py`를 사용한다.
