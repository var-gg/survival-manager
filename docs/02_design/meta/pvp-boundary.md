# PVP boundary

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-06
- 소스오브트루스: `docs/02_design/meta/pvp-boundary.md`
- 관련문서:
  - `docs/02_design/meta/pvp-ruleset-and-arena-loop.md`
  - `docs/03_architecture/arena-snapshot-matchmaking-and-season-contract.md`

## 목적

이 문서는 PvP를 MVP 범위 밖에 두되, 이후 도입 시 어떤 경계로만 열 수 있는지 정의한다.

## 핵심 규칙

- PVP는 비-MVP다.
- 이번 패스는 live service가 아니라 local/offline arena scaffold까지만 연다.
- 공식 PvP 진입 자체는 `OnlineAuthoritative` realm에서만 허용한다.
- `OfflineLocal`에서는 PvP 버튼을 비활성화하고 Boot에서 realm을 다시 선택하게 한다.
- PVP가 나중에 도입되더라도 run 중 temporary augment는 적용하지 않는다.
- PVP에는 permanent augment만 적용한다.
- offense / defense는 async snapshot 모델을 따른다.
- rating update ownership은 arena meta service가 가진다.
- season reset cadence는 `4주`를 기본으로 둔다.

## 이유

- run 중 temporary augment까지 포함하면 밸런스와 판별성이 급격히 무너진다.
- PVP는 run 내 운 요소보다 장기 progression과 roster 준비를 더 직접 반영해야 한다.
- 현재 replay / ledger 구조는 async arena와 맞지만, live matchmaking backend는 별도 운영 비용을 요구한다.

## 장기 규칙

- live matchmaking backend
- leaderboard / season ops
- PVP 전용 보상 확장
- ruleset-only balance pass
