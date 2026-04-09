# 덱 설계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/deck/index.md`
- 관련문서:
  - `docs/02_design/index.md`
  - `docs/02_design/narrative/index.md`
  - `docs/02_design/meta/campaign-chapter-and-expedition-sites.md`

## 목적

roster, archetype, 영웅 lore, 영웅풀 확장 로드맵을 모은다.

## 문서 목록

- `roster-archetype-launch-scope.md`: 출시 기준 roster와 archetype package
- `launch-core-roster-sheet.md`: 12 core archetype 운영용 roster truth sheet
- `character-lore-registry.md`: 영웅 canon short bio, tier, beat budget, unresolved hook 레지스트리
- `hero-expansion-roadmap.md`: MVP 12영웅에서 launch 20영웅까지의 확장 wave, specialist 정책, DLC/sequel 규칙

## heroes/ 폴더 정책

- heroes/hero-{id}.md 형태의 장문 열전 문서를 선택적으로 둘 수 있다.
- 생성 시 `character-lore-registry.md`에 `detail_doc` 열을 갱신한다.
- registry와 상세 문서의 충돌 시 registry가 우선한다.

## cross-link

- hero canon과 tier는 `character-lore-registry.md`가 소유한다.
- hero unlock gate와 story join timing은 `docs/02_design/narrative/chapter-beat-sheet.md`와 `docs/02_design/meta/story-gating-and-unlock-rules.md`가 소유한다.
- stat/mechanic 수치는 deck 문서가 소유하지 않는다.
