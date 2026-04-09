# 캐릭터 열전 레지스트리

- 상태: draft
- 소유자: repository
- 최종수정일: 2026-04-10
- 소스오브트루스: `docs/02_design/deck/character-lore-registry.md`
- 관련문서:
  - `docs/02_design/deck/hero-expansion-roadmap.md`
  - `docs/02_design/narrative/world-building-bible.md`
  - `docs/02_design/narrative/chapter-beat-sheet.md`

## 목적

모든 영웅의 canonical short bio, tier, beat budget, unresolved hook를 한곳에 정리한다. 장문 열전 문서가 생기더라도 이 문서가 registry SoT다.

## 레지스트리 규칙

- 영웅 1명당 1행을 유지한다.
- 장문 biography는 `docs/02_design/deck/heroes/hero-*.md`에 두고, 본 문서에서는 요약과 참조만 남긴다.
- stat/mechanic 수치는 여기서 소유하지 않는다.
- tier와 beat budget은 `narrative-pacing-formula.md` 공식을 따른다.
- hero ID는 roster/system 문서와 동일해야 한다.

## 영웅 표

| hero_id | display_name | race | class | tier | beat_budget | canon_status | narrative_role | unresolved_hook |
|---|---|---|---|---|---:|---|---|---|
| `hero_iron_warden` | Iron Warden | Human | Vanguard | `support` | 3 | `launch` | 질서의 얼굴, 초반 안전판 | — |
| `hero_crypt_guardian` | Crypt Guardian | Undead | Vanguard | `support` | 3 | `launch` | 죽은 문명의 죄를 증언 | — |
| `hero_fang_bulwark` | Fang Bulwark | Beastkin | Vanguard | `background` | 1 | `launch` | 야수족 현장성의 상징 | — |
| `hero_oath_slayer` | Oath Slayer | Human | Duelist | `support` | 3 | `launch` | 왕국의 폭력성과 명분을 드러내는 칼 | — |
| `hero_pack_raider` | Pack Raider | Beastkin | Duelist | `lead` | 6 | `launch` | 야수족 측 주연 시점 | `hook_pack_treaty` |
| `hero_grave_reaver` | Grave Reaver | Undead | Duelist | `background` | 1 | `launch` | 파괴된 충성의 잔재 | — |
| `hero_longshot_hunter` | Longshot Hunter | Human | Ranger | `support` | 3 | `launch` | 원정 실무자의 시선 | — |
| `hero_trail_scout` | Trail Scout | Beastkin | Ranger | `support` | 3 | `launch` | 유머와 경계의 완충재 | — |
| `hero_dread_marksman` | Dread Marksman | Undead | Ranger | `background` | 1 | `launch` | 언데드의 냉혹한 효율 | — |
| `hero_dawn_priest` | Dawn Priest | Human | Mystic | `lead` | 6 | `launch` | 인간 진영 주연 시점 | `hook_faith_crack` |
| `hero_grave_hexer` | Grave Hexer | Undead | Mystic | `lead` | 6 | `launch` | 언데드 측 주연 시점 | `hook_memory_debt` |
| `hero_storm_shaman` | Storm Shaman | Beastkin | Mystic | `support` | 3 | `launch` | 야수족 공동체의 의례 | — |
| `hero_rift_stalker` | Rift Stalker | Beastkin | Duelist | `background` | 1 | `launch` | 적-동맹 전환의 첫 사례 | — |
| `hero_bastion_penitent` | Bastion Penitent | Human | Vanguard | `background` | 1 | `launch` | 왕국 내부 균열의 증거 | — |
| `hero_pale_executor` | Pale Executor | Undead | Ranger | `background` | 1 | `launch` | 언데드 실용주의의 흑백 | — |
| `hero_aegis_sentinel` | Aegis Sentinel | Relicborn | Vanguard | `support` | 3 | `launch` | Relicborn의 문지기 | — |
| `hero_echo_savant` | Echo Savant | Relicborn | Mystic | `lead` | 6 | `launch` | Relicborn 측 주연 시점 | `hook_memory_custody` |
| `hero_shardblade` | Shardblade | Relicborn | Duelist | `background` | 1 | `launch` | 중후반 전열 돌파 장치 | — |
| `hero_prism_seeker` | Prism Seeker | Relicborn | Ranger | `background` | 1 | `launch` | 진실 추적자 | — |
| `hero_mirror_cantor` | Mirror Cantor | Relicborn | Mystic | `support` | 3 | `launch` | 최종부 정합성 봉합자 | — |

## 미해결 개인 갈등 표

campaign ending 이후 남아도 되는 personal hook만 여기에 둔다.

| hero_id | hook_id | status_at_launch_end | allowed_followup |
|---|---|---|---|
| `hero_dawn_priest` | `hook_faith_crack` | partial — 신앙 균열을 인지했으나 완전히 해결하지 않음 | DLC |
| `hero_pack_raider` | `hook_pack_treaty` | partial — 동맹은 맺었으나 씨족 내부 합의는 미완 | DLC |
| `hero_grave_hexer` | `hook_memory_debt` | partial — 기억의 빚을 인지했으나 완전 청산은 미래 | sequel |
| `hero_echo_savant` | `hook_memory_custody` | open — 봉인 기억의 관리 책임이 남아 있음 | sequel |

## 열전 상세 문서 규칙

상세 문서를 만들 경우:
- `docs/02_design/deck/heroes/hero-{id}.md` 경로를 사용한다.
- 필수 섹션: 출신, 동기, 핵심 갈등, quote bank, 개인 비트 상세
- 금지 중복: registry에 이미 있는 tier/beat_budget을 상세 문서에서 다시 정의하지 않는다.
- registry와 상세 문서의 충돌 시 registry가 우선한다.

## 작성 지침

- stat/mechanic 수치는 여기서 소유하지 않는다.
- tier와 beat budget은 `narrative-pacing-formula.md` 공식을 따른다.
- hero ID는 roster/system 문서와 동일해야 한다.
