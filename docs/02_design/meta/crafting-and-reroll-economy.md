# crafting and reroll economy

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/02_design/meta/crafting-and-reroll-economy.md`
- 관련문서:
  - `docs/02_design/meta/crafting-currencies-and-sinks.md`
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/recruitment-and-reroll.md`

## 목적

이 문서는 crafting과 reroll 경제를 분리해 정의한다.

## launch floor 구현 범위

- recruit candidate refresh: gold 기반
- item `temper` / `reforge`: `gold` + `ember_dust`
- item `seal`: `echo_crystal`
- item `imprint`: `boss_sigil`
- trait reroll / lock / purge: 전용 희소 token 기반

## 핵심 규칙

- gold는 반복 소모 자원이다.
- trait reroll 재화는 permanent augment와 동급 가치의 희소 재화다.
- item crafting 재화와 trait token economy를 섞지 않는다.
- launch floor에서는 crafting을 넓게 열지 않고, reroll/재련 수준만 허용한다.

## 장기 규칙

- material-based crafting 확장
- recipe crafting
- station-based crafting
- deep salvage economy
