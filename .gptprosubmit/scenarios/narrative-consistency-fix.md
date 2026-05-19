---
name: narrative-consistency-fix
default_scope:
  areas: [narrative-script, character-lore]
  include_superseded: false
  exclude_templates: true
description: narrative 문서 정합성·번역체·재미 4축 직접 교정 요청 (1호 use case)
output_format: _output_format.md
---

# Scenario: narrative-consistency-fix

이 markdown은 Claude가 GPT Pro 전달용 prompt를 조립하기 위한 template다.
placeholder는 `{{...}}` 형식이며 Claude가 호출 인자/맥락으로 채운다.

## 조립 규칙 (Claude용 메타 — prompt에 들어가지 않음)

- `default_scope.areas` 두 area를 pindoc.artifact.search/read로 모두 수집. 사용자가 `--slugs` 명시했으면 그것이 우선
- `_template_*` slug, `status: superseded` artifact 제외 (frontmatter 지시 준수)
- bundle md 상단 manifest YAML에 slug/title/area/status/revision 박을 것
- 아래 `## Prompt body` 섹션의 내용을 그대로 prompt로 사용. placeholder는 다음 규칙으로 치환:
  - `{{focus_block}}` — 사용자가 `--extra "..."` 인자 줬을 때만 `[FOCUS]\n{value}\n` 로, 없으면 빈 문자열
  - `{{bundle_summary}}` — 첨부 md에 들어간 artifact 개수와 area 분포 한 줄 요약 (e.g., "첨부: 38개 artifact — character-lore 25 / narrative-script 13")
  - `{{output_format_block}}` — `scenarios/_output_format.md` 파일 내용 그대로 append

## Prompt body

```text
[CONTEXT]
이 채팅은 survival-manager 프로젝트의 narrative wiki 정합성·품질 검수다.

프로젝트 성격:
- Unity prototype phase의 캐릭터 수집/성장 RPG. 자동전투 + 짧은 호흡 + 인간형 적군 only
- 서브컬쳐 감성을 정면으로 흡수하는 방향. 단일 캠페인 RPG 아님. 캐릭터 voice/관계망/세력 갈등이 narrative core
- 인디 1인 개발 ($10 가격대), 음성 70-110 line, cutscene 12-15, illust commission 0, P09+lilToon+MagicaCloth2 stack

narrative 운영 기준:
- SoT는 pindoc wiki (현재 미호스팅이라 첨부 md로 우회 전달)
- 1차 언어 한국어. 영문 ID/slug는 유지하되 표시명·prose는 한국어
- 번역체 금지 (한국 작가가 실제 쓰는 어휘만)
- 현재 in-flight: ADR-0024 race lore-only reskin — 4 인간 세력으로 narrative reskin 중. race-touch는 lore-only이고 runtime SoT(asmdef, persistence schema, archetype/augment 시스템)는 절대 보존

첨부 wiki dump:
- {{bundle_summary}}
- 각 artifact는 `# {slug} ({title})` H1로 구분
- 상단 manifest YAML에 area/status/revision 명시
- 본문은 pindoc artifact body 원문 그대로

{{focus_block}}

[TASK]
첨부된 wiki dump를 정독하고, 아래 4축에 대해 직접 교정한 patch를 산출하라:

1. **정합성** — cross-document inconsistency
   - 인물명/지명/연도/사건 순서/관계도 충돌
   - 동일 사건이 두 곳에서 다르게 서술되는 경우
   - 캐릭터 동기/성격이 wiki 간 불일치

2. **대사 자연스러움** — 번역체 차단
   - 금지 예: "한 박자", "X라 부르네", "좋은 냄새가 나는 단어"
   - 한국 작가가 실제 쓰는 어휘 풀만 사용
   - 캐릭터별 voice가 식별 가능해야 함 (말투/어휘 차별화)

3. **재미가 빠진 부분** — 콘텐츠 품질
   - 서브컬쳐 감성 부족 (밋밋한 정보 나열)
   - 텐션이 빠진 구간
   - 캐릭터 매력이 약한 묘사
   - 갈등/관계의 회색 지대가 평탄화된 경우

4. **더 재밌게 가능한 부분** — 능동 개선
   - "여기는 더 좋게 할 수 있다" 식의 막연한 코멘트 금지
   - 구체적 before/after diff로 제시
   - 캐릭터 voice를 더 살리는 방향, 갈등의 결을 더 드러내는 방향

[CONSTRAINTS]
- 한국어 narrative prose 1차. 영문 ID/slug는 유지
- 새 lore 도입 금지 — 첨부된 wiki 안에서 fix only
- 새 캐릭터/지명/사건/조직 도입 금지
- race-touch는 lore-only (4 인간 세력 reskin 진행 중). runtime/asmdef/persistence/시스템 결정은 절대 건드리지 마라
- 캐릭터 핵심 컨셉(role, archetype, P09 visual baseline) 보존 — voice/표현만 교정
- patch 출력 시 첨부 manifest의 slug ID를 그대로 사용 (영문 ID 변형 금지)
- 전체 wiki 구조 재편성 제안 금지 (개별 문서 안에서의 교정만)

[DELIVERABLE]
slug별 patch. 각 slug에 대해 아래 4 sub-section을 두되, **해당 항목에 issue가 없으면 생략**한다 (불필요한 sub-section 출력 금지).

### {slug}

**정합성 이슈**
- 무엇이 무엇과 충돌하는지 (다른 slug 명시적 cross-reference)
- 권장 해결 방향 (어느 쪽을 기준으로 맞춰야 하는지)

**대사/문장 교정**
- before: "..."
- after: "..."
- reason: 한 줄 (왜 바꿔야 하는지)

**재미 강화 제안**
- 위치: 어느 단락/문장 (해당 부분 인용)
- before/after diff
- reason: 한 줄

**4축 외 추가 발견** (있을 때만)
- 그 외 눈에 띄는 narrative 이슈

교정이 전혀 필요 없는 slug는 **응답에서 완전히 생략하라** — 빈 헤더도 남기지 마라. 응답 길이 낭비 금지.

응답 마지막에 **요약 표** 1개:

| slug | 정합성 | 대사 | 재미 | 우선순위 |
|---|---|---|---|---|
| ... | O/- | O/- | O/- | P0/P1/P2 |

(O=교정 제안 있음 / - =없음. 우선순위 P0=치명적 정합성 깨짐, P1=명확한 품질 결손, P2=개선 여지)

{{output_format_block}}
```

## Default scope 보완

기본 두 area만으로 부족한 경우 Claude가 판단해서 추가:

- `narrative-process` (22): 작업 메타라 기본 제외하되, 사용자가 reskin 진행 상황 검수를 원하면 포함
- `content` (16): reader copy 포함. wiki 외부 노출 텍스트라 narrative 검수 대상으로 포함 가능
- `flows` (14): 사용자 flow 명세라 보통 narrative 검수 대상 X. 필요 시만

## 호출 예시

```text
/gpt-pro-submit narrative-consistency-fix
→ narrative-script + character-lore 전부 dump

/gpt-pro-submit narrative-consistency-fix --slugs=hero-dawn-priest,hero-grave-hexer
→ 두 캐릭터만

/gpt-pro-submit narrative-consistency-fix --extra="단린 voice가 너무 평이함, 그쪽 위주로"
→ 전체 dump + focus_block에 사용자 hint 삽입
```
