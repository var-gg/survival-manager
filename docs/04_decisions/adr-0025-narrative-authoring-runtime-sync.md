# ADR-0025 내러티브 authoring↔runtime 동기화 거버넌스

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-06-02
- 소스오브트루스: `docs/04_decisions/adr-0025-narrative-authoring-runtime-sync.md`
- 관련문서:
  - `docs/04_decisions/adr-0022-narrative-architecture.md`
  - `docs/04_decisions/adr-0024-narrative-human-centric-reskin.md`
  - `docs/04_decisions/adr-0016-localization-boundary.md`
  - `docs/02_design/narrative/index.md`
  - `tools/narrative-authoring-map.json`
  - GPT Pro 자문 (2026-06-02) — 본 ADR 보강 근거. 구현 디테일 원문은 `.gptprosubmit/payload/response-20260602-022305.md` (임시 산출물)

## 문맥

ADR-0022가 narrative asmdef 배치(Content/Meta/Persistence/Unity)를 정했고, 2026-05-07 결정으로 narrative creative SoT가 git docs → Pindoc Wiki로 이관됐다. 그러나 SoT 전환 이후 authoring(pindoc)과 runtime(Unity asset/localization)이 동기화되지 않아 drift가 굳었다.

확인된 사실(2026-06-02 점검):

- 런타임 본문 dialogue/StoryEvent asset이 2026-04-19에 동결. SoT 전환·5/21 톤 재정렬·ADR-0024 reskin이 게임에 미반영. 빌드하면 폐기 plot(`foreshadow_lattice`, `relicborn_awakening`, `aldric_face_revealed`, `savant_seal_decision`)이 재생된다.
- 메커니즘: 이중 소스(레거시 `narrative-seed.json` + 신규 `narrative-seed-wiki.json`) 공존 → `NarrativeSeedImporter`가 orphan pruning OFF → 폐기 scene 영구 잔존. `wiki_narrative_extract.py`가 `storyEvents:[]`만 출력 → StoryEvent 레거시 고정. `narrative_validate.py`가 stale `master-script.md` 검증. raw-wiki는 `.gitignore` 임시물, `sourceHash` 계산만 하고 미사용.

GPT Pro 자문(2026-06-02)으로 교차검증: 위 진단과 "단방향 파이프라인" 방향은 맞으나, **scene 단위 동기화에 머물면 line / localization-key / cue / event / AI-diff 단위 drift가 남는다.** 핵심 보강은 (a) pindoc과 Unity 사이에 **Git-tracked canonical manifest(계약 SoT)** 를 두고, (b) 텍스트와 독립된 **stable `line_uid`** 를 도입하는 것이다.

제약:

- ADR-0022 의존방향 유지.
- prototype / 인디 규모 — 과도한 파이프라인 엔지니어링 회피.
- pindoc은 자체 운영 dogfood — importer가 pindoc을 직접 해석하지 않고 덤프/manifest를 경유(headless/CI에서 MCP 인증 의존 회피).

## 결정

### 3계층 하이브리드 SoT

사실의 종류별로 SoT를 나누되, **런타임으로 흘러가는 경로는 하나로 강제**한다.

| 계층 | 역할 | 대상 |
| --- | --- | --- |
| **pindoc Wiki** | 창작·검토 SoT (사람이 읽는 산문, AI 편집) | 대본 산문, 캐릭터/세계관/톤, 작의 |
| **Canonical Narrative Manifest (Git)** | 런타임 계약 SoT | scene / line / speaker / emotion / cue / event / 한국어 원문의 정규화 데이터 |
| **Unity asset + Localization** | 파생물(generated) | `DialogueSequence`/`StoryEvent` asset, `Content_Story` StringTable |

단방향: **pindoc → export 덤프 → canonical manifest(Git) → Unity deterministic import → 파생 asset.** Unity asset·레거시 C# seed가 다시 SoT처럼 행동하지 못하게 막는다.

### 규칙

1. **단일 정방향 파이프라인** — 레거시 `narrative-seed.json` 경로 폐기. importer는 canonical manifest 하나만 읽는다.
2. **canonical manifest 계층 신설(Git-tracked)** — `Narrative/Manifest/`에 `narrative.lock` + `scenes/*.json` + `events/*.json` + `registries/*.json` + `cues/*.json`. 중간 산출물을 Temp 임시물이 아니라 **커밋되는 계약**으로 승격.
3. **stable `line_uid` 분리** — 한 줄의 정체성(`line_uid`)은 텍스트 수정·재정렬에도 유지한다. `text_hash_ko`(drift 감지)·`loc_key`(StringTable 조회)·`source_hash`(pindoc 원천)·`revision`을 별개 필드로 둔다. `loc_key = loc.story.<scene_id>.<line_uid>`. `line_uid`/`loc_key` 없으면 import fail.
4. **extract 전체화** — `wiki_narrative_extract.py`가 dialogue뿐 아니라 `storyEvents`·beats·character/term/cue registry·폐기 어휘 blacklist를 채운다.
5. **`Content_Story/ko`도 generated** — 한국어 원문은 manifest `text_ko`에서 생성한다. Unity StringTable 직접 수정 금지(이중 SoT 방지). shared metadata에 `line_uid`/`scene_id`/`speaker_id`/`text_hash_ko`/`source_revision` 기록.
6. **다층 drift 게이트** — scene_id 집합 diff는 1차일 뿐이다. `line_uid`·`loc_key`·`text_hash`·`event_id`·`cue_ref`·`speaker_id` 단위 diff + referential integrity + 폐기 어휘 blacklist + generated asset 수동 수정 감지까지. 하나라도 위반이면 `tools/smoke-check.ps1`/CI fail. 사람이 읽는 impact report를 출력.
7. **orphan pruning은 3단계** — 즉시 hard delete 금지. `dry-run → quarantine → prune`. generated metadata(`generated_by`/`manifest_hash`/`scene_id`/`line_uid`)가 있는 asset만 대상. tombstone은 한 content build 동안 유지.
8. **validator 대상 전환** — `narrative_validate.py`를 `master-script.md` → canonical manifest 1차 검증으로. raw 덤프는 reproducibility 검증 대상.
9. **AI edit protocol** — AI가 주 편집자이므로 편집 단위에 `edit_intent`(editor / human_reviewer / reason / preserved_constraints / risk_tags) + line mutation 규칙(오탈자=같은 `line_uid`, 분할=새 uid, 병합=대표 uid + `superseded_by`, 삭제=tombstone)을 강제한다. 변경 시 영향 리포트(line_uid/loc_key/event/asset/translation/voice/smoke)를 자동 출력.
10. **pindoc mirror는 generated include** — character/term/faction 등 runtime/consistency-critical 사실값은 canonical registry 한 곳에서만 정의하고 pindoc에는 transclusion한다. 사람이 손으로 복제하는 mirror는 금지. 산문 설명은 유지.
11. **레거시 제거** — `NarrativeSeedData.*.cs`(하드코딩 seed), `narrative_build.py`(git-docs reader), `docs/02_design/narrative/master-script.md`를 archive.
12. **raw-wiki 재현성 + done 기준** — export를 build 필수 선행으로 강제하고 `narrative.lock`에 `pindoc_revision`/`extractor_version`/`schema_version`/`manifest_hash`를 기록한다. narrative 변경 done = pindoc 수정 + manifest 재생성 + drift 0 + Unity batchmode import + playmode smoke + 인게임 확인. `CLAUDE.md`의 "compile green은 done이 아니다" 절과 `AGENTS.md` done 정의에 추가.

## 검토한 대안

| option | 장점 | 단점 | verdict |
| --- | --- | --- | --- |
| `pindoc_solo_sot` | AI/사람 읽고 쓰기 좋음, 한곳에 | schema 약함, mirror stale 재발, runtime diff/import 재현성 약함 | reject |
| `structured_solo_sot` (JSON/DB) | CI·schema·diff·import 강함, line/key/event 추적 탁월 | 산문 대본 검토성 낮음, tone editing 경직 | reject |
| `hybrid_three_layer` | 창작 UX + 기계 검증 동시 확보, 현재 구조와 정합 | extractor/manifest/validator 투자 필요 | **accept** |
| `importer_reads_pindoc_direct` | 덤프 단계 제거 | headless/CI에서 MCP 인증 불가, 외부 서비스 의존, 재현성 악화 | reject |

(GPT Pro 자문 2026-06-02의 SoT trade-off 분석 반영.)

## 결과

장점:

- pindoc 한 곳을 수정하면 manifest → build 한 번으로 게임까지 전파되고, drift는 CI가 차단한다.
- `line_uid` 정체성 덕분에 대사 한 줄 편집이 localization key·voice·cue 연결을 흔들지 않는다.
- 폐기 scene이 orphan pruning으로 사라져 런타임이 baseline과 정합한다.
- "게임에 반영됐는가"가 `manifest_hash` + drift report로 가시화된다.

감수할 비용:

- canonical manifest 스키마·extractor 확장·drift validator·AI edit protocol 투자.
- `line_uid` 마이그레이션(기존 ordinal `loc_key` → `line_uid`, alias table 경유).
- 1회성 전체 재빌드 시 대량 asset diff.

## 후속 작업 (구현은 별도 task)

GPT Pro 자문의 Step 0~8을 baseline으로 한다.

- 선행: **동기화 audit** — 런타임 asset/event/loc ↔ pindoc scene_id/line 전수 diff로 현재 drift 규모 정량화.
0. **레거시 경로 차단** — 하드코딩 seed import entrypoint 제거, importer 단일 소스화, import 후 `event_count == 0`이면 fail.
1. canonical manifest 디렉터리 + JSON Schema 도입.
2. `line_uid`/`loc_key` 마이그레이션 (alias table).
3. `Content_Story/ko` generated 전환.
4. extract event/beat/registry 확장.
5. generated asset metadata + drift checker.
6. CI fail 조건 구체화(missing/orphan/text_hash/cue/speaker/deprecated/manual-edit).
7. Unity 메뉴 단일화(Rebuild / Validate / Open-in-pindoc / Impact) + Inspector drift 표시.
8. runtime smoke(EditMode/PlayMode): 모든 active event condition/effect parse, `_textKey` resolve, portrait/cue resolve, deprecated blacklist 미검출.

## 리스크와 대응

(GPT Pro 자문 반영)

- **line-id churn**: AI 대량 재작성이 `line_uid`를 갈아치움 → 유사도 linter로 보존 권고/warn.
- **manifest가 또 stale**: `pindoc_revision`/`export_hash`를 CI가 최신과 비교, lock 없으면 release build 불가.
- **generated asset 수동 수정**: `manifest_hash` 불일치 시 fail, Inspector는 "pindoc으로 이동"만 제공.
- **pruning 손실**: dry-run → quarantine → hard delete, tombstone 유지.
- **event graph 누락**: active scene은 inbound event 또는 `manual_only` 필수, `StoryEvent`는 manifest에서만 생성.
- **번역 stale**: locale entry에 `source_text_hash_ko_at_translation` 저장, 한국어 hash 변경 시 `needs_review`.
- **pindoc mirror stale**: registry fact는 canonical에서 generated include.

## 작성 지침

- 기술적 trade-off를 우선하고, 실제 타입명·script path·scene_id를 사용한다.
- 이 ADR은 ADR-0022(배치)를 대체하지 않고 authoring↔runtime 동기화 거버넌스를 보강한다.
- 구현 디테일(manifest JSON 예시, CI 명령 흐름, line mutation 규칙 전문)은 GPT Pro 자문 원문을 참조한다.
