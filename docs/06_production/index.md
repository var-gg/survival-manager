# 운영 문서 인덱스

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-08
- 소스오브트루스: `docs/06_production/index.md`
- 관련문서:
  - `docs/index.md`
  - `tasks/001_mvp_vertical_slice/status.md`
  - `docs/05_setup/quick-battle-smoke.md`

## 목적

`06_production/`는 prototype 단계에서 필요한 플레이테스트, 운영 체크, live 검증 기준 문서를 모아 둔다.

## live 상태 문서

- playable boundary, 리스크, 다음 우선순위는 [tasks/001_mvp_vertical_slice/status.md](../../tasks/001_mvp_vertical_slice/status.md)를 기준으로 본다.

## 문서 목록

- `mvp-playtest-checklist.md`: MVP 플레이테스트 체크리스트
- `current-known-issues.md`: 현재 알려진 이슈와 우회 절차
- `balance-knobs.md`: 밸런스 조정 가능한 수치 목록
- `first-playable-review.md`: 현재 playable 범위 스냅샷
- `operator-first-playable-checklist.md`: 첫 전투 수동 검증 체크리스트
- `pre-art-release-floor.md`: paid asset pass 직전 같은 SHA automated/manual floor와 packet 규칙
- `runtime-hardening-contract.md`: `OfflineLocal` save/recovery/checkpoint/smoke 격리 운영 계약
- `mcp-usage-checklist.md`: MCP 세션 전후 운영 체크리스트
- `scene-integrity-contract.md`: first playable scene 최소 무결성 계약
- `loop-d-closure-note.md`: Loop D prune/move-out-of-v1/watchlist 메모
