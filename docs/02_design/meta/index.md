# 메타 시스템 설계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-16
- 소스오브트루스: `docs/02_design/meta/index.md`
- 관련문서:
  - `docs/02_design/index.md`
  - `docs/02_design/narrative/index.md`
  - `docs/03_architecture/index.md`

## 목적

Town/expedition loop, 경제, 모집, 장비, augment, synergy, PVP, 캠페인 진행 등 메타 runtime/content contract 문서를 모은다.
제품/창작 방향의 source-of-truth는 Pindoc Wiki가 소유하고, 이 폴더는 현재 구현과 validator가 기대하는 규칙만 repo에 남긴다.

## 문서 목록

- `town-and-expedition-loop.md`: Town -> chapter/site -> Battle -> Reward 루프 기준
- `campaign-chapter-and-expedition-sites.md`: story chapter, site, endless unlock 기준 (topology/mechanics SoT)
- `story-gating-and-unlock-rules.md`: story unlock, hero join, endless gate, fail-safe, save field (narrative-meta 접점 SoT)
- `recruitment-contract.md`: 4-slot recruit pack, on-plan/protected, refresh, scout 기준
- `retrain-contract.md`: flex-only retrain, cost curve, previous-result exclusion, pity 기준
- `economy-protection-contract.md`: Gold/Echo split, refit/recovery rail, no-dead-reward, dismiss refund 기준
- `duplicate-handling-contract.md`: duplicate 판정, Echo conversion, recruit pool exclusion 기준
- `drop-table-rarity-bracket-and-source-matrix.md`: automatic drop/source/rarity floor
- `item-and-affix-system.md`: 아이템과 affix 구조, Echo refit, advanced crafting 경계
- `affix-authoring-schema.md`: affix schema와 authority / budget / line-density 기준
- `affix-pool-v1.md`: affix catalog와 live subset
- `equipment-family-and-crafting-depth.md`: weapon family floor와 deep crafting 경계
- `item-passive-augment-budget.md`: 출시 기준 item/passive/augment 예산 허브와 passive/permanent V1 cap
- `passive-board-node-catalog.md`: launch floor passive board node 카탈로그
- `augment-system.md`: augment 문법
- `augment-synergy-operating-model.md`: augment offer와 synergy 운영 기준
- `augment-catalog-v1.md`: augment catalog와 live subset
- `permanent-augment-progression.md`: `unlock many, equip one` permanent progression
- `pvp-boundary.md`: PVP 경계
- `pvp-ruleset-and-arena-loop.md`: async arena ruleset과 season cadence
- `session-realm-and-official-online-boundary.md`: `OfflineLocal` / `OnlineAuthoritative` session realm과 공식 온라인 경계
- `character-race-class-role-archetype-taxonomy.md`: `Character / Race / Class / Role / Archetype` taxonomy와 launch floor identity layer
- `synergy-breakpoints-and-soft-counters.md`: 출시 기준 2/4 breakpoint와 soft counter
- `synergy-family-catalog.md`: 7 family 2/4 payload 카탈로그
- `synergy-and-augment-taxonomy.md`: synergy / augment 역할 경계, 2/4 threshold, rarity

## cross-link

- site topology와 encounter lane은 `campaign-chapter-and-expedition-sites.md`가 소유한다. narrative 문서는 해당 ID를 참조만 한다.
- story unlock/fail-safe/UI 표면화는 `story-gating-and-unlock-rules.md`가 소유한다.
- narrative beat와 감정값은 Pindoc narrative artifacts가 소유한다. repo `docs/02_design/narrative/**`는 schema와 seed transition reference만 남긴다.
