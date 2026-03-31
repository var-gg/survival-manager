# 캠페인 chapter와 expedition site

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`
- 관련문서:
  - `docs/02_design/meta/town-and-expedition-loop.md`
  - `docs/02_design/combat/encounter-catalog-and-scaling.md`
  - `docs/03_architecture/encounter-authoring-and-runtime-resolution.md`

## 목적

이 문서는 story progression을 `chapter -> site -> encounter track` 구조로 고정한다.
launch floor에서 expedition은 branch graph가 아니라 authored site track을 따른다.

## progression 규칙

- story chapter는 `3`
- chapter당 expedition site는 `2`
- site당 battle node는 `4`
- extract node는 site당 `1`
- endless mode는 story clear 이후에만 열린다.

## chapter / site 카탈로그

| story order | chapter id | site ids | endless unlock |
| --- | --- | --- | --- |
| 1 | `chapter_ashen_frontier` | `site_ashen_gate`, `site_cinder_watch` | 아니오 |
| 2 | `chapter_warren_depths` | `site_forgotten_warren`, `site_twisted_den` | 아니오 |
| 3 | `chapter_ruined_crypts` | `site_ruined_crypt`, `site_grave_sanctum` | 예 |

## site track 규칙

각 site는 아래 선형 track을 사용한다.

1. `skirmish`
2. `skirmish`
3. `elite`
4. `boss`
5. `extract`

- Town에서는 해금된 chapter와 site를 선택한다.
- Expedition에서는 현재 site의 선형 progress만 보여 준다.
- node context는 `ChapterId`, `SiteId`, `SiteNodeIndex`, `EncounterId`, `BattleSeed`, `BattleContextHash`로 고정한다.

## clear / unlock 규칙

- site clear는 해당 site의 boss 전투와 extract를 모두 지난 뒤 확정한다.
- chapter clear는 chapter에 속한 두 site가 모두 clear된 뒤 확정한다.
- `StoryCleared`는 모든 chapter clear 시점에 true가 된다.
- `EndlessUnlocked`는 `StoryCleared`와 함께 true가 된다.

## launch floor UX 원칙

- story 사용자는 chapter/site를 따라 hand-authored progression을 밟는다.
- farm 사용자는 story clear 뒤 endless로 진입한다.
- 무작위 graph 생성은 이번 패스에 넣지 않는다.
