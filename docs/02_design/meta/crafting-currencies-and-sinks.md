# 제작 재화와 sink

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/meta/crafting-currencies-and-sinks.md`
- 관련문서:
  - `docs/02_design/meta/equipment-family-and-crafting-depth.md`
  - `docs/02_design/meta/crafting-and-reroll-economy.md`
  - `docs/02_design/combat/hero-traits.md`

## 목적

이 문서는 launch floor의 crafting currency와 operation을 최소 집합으로 잠근다.
목표는 장기 sink를 만들되 시스템 복잡도를 폭발시키지 않는 것이다.

## crafting currency

| currency id | 역할 |
| --- | --- |
| `gold` | broad sink, refresh, 기본 reroll 비용 |
| `ember_dust` | common crafting material, affix reroll |
| `echo_crystal` | rarer material, affix lock / upgrade |
| `boss_sigil` | boss / unique imprint |

trait reroll economy는 item crafting 통화와 분리한다.

## trait token economy

아래 세 token은 recruit/trait 계층의 별도 희소 재화다.

- `trait_reroll_token`
- `trait_lock_token`
- `trait_purge_token`

item crafting currency와 interchangeable하게 쓰지 않는다.

## crafting operation

| operation id | 역할 |
| --- | --- |
| `temper` | 수치 재굴림 |
| `reforge` | affix 1개 재굴림 |
| `seal` | affix 고정 |
| `imprint` | unique / boss signature rule 추가 |
| `salvage` | 재료 회수 |

## 운영 규칙

- launch floor는 station UI를 복잡하게 열지 않는다.
- crafting은 build identity를 다듬는 수준으로 유지한다.
- 장기 sink는 `gold`, `ember_dust`, `echo_crystal`, `boss_sigil` 네 축으로만 본다.
- trait token 가치와 item crafting value를 섞지 않는다.

## deferred

- recipe unlock tree
- station specialization
- market/trade sink
- multi-step meta crafting
