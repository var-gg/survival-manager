# Operator First Playable Checklist

## 반드시 먼저 실행할 메뉴 1개

- `SM/Setup/Prepare Observer Playable`

- 상태: active
- 최종수정일: 2026-04-07
- 단계: prototype

## normal lane acceptance

1. Unity 열기
2. `SM/Setup/Prepare Observer Playable`
3. `Boot.unity` Play
4. Boot에서 `Start Local Run`
5. Town에서 `Start Expedition`
6. Expedition에서 현재 site의 첫 battle node로 진입
7. Battle replay 종료 후 `Continue`
8. Reward에서 카드 1장 선택
9. `Return Town`
10. Town에서 `Resume Expedition` 노출과 chapter/site 잠금을 확인한다.
11. boss 이후 extract node가 `Reward -> Town(close)`로 끝나는지 확인한다.

## smoke lane acceptance

1. Town에서 secondary `Quick Battle (Smoke)`를 누른다.
2. Battle에서 `Re-battle` / direct `Return Town`이 보이는지 확인한다.
3. Town 복귀 뒤 campaign/site progression이 바뀌지 않았는지 확인한다.

## 확인 항목

- Town에서 recruit 카드 4개와 `Start Expedition` 또는 `Resume Expedition`, secondary `Quick Battle (Smoke)`, `Return to Start`가 보인다.
- Town active UI에서 Quick Battle은 secondary/debug CTA이고, active run 중에는 비활성화된다.
- Battle에서 primitive actor / HP / 로그 / pause / speed / progress가 보인다.
- Reward에서 카드 선택 즉시 summary/status가 갱신된다.
- Town 복귀 후 normal lane은 resume 가능 상태를 유지하고, final extract 뒤에는 run이 닫힌다.
