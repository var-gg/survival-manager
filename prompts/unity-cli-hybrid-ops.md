# Unity CLI Hybrid Ops Prompt

## 목적

이 프롬프트는 `survival-manager`에서 Unity 작업을 할 때 file-first + CLI-fast-lane + MCP-typed-lane 규칙을 강제하기 위한 운영 스니펫이다.

## Tool Routing Rules

1. 기본은 file-first다.
- C# source, docs, tests, repo-tracked YAML은 먼저 파일로 직접 수정한다.
- Unity editor는 authoring surface가 아니라 compile / bootstrap / smoke / report surface다.

2. Unity 확인 작업은 CLI-first다.
- 먼저 `pwsh -File tools/unity-bridge.ps1 <verb>`를 쓴다.
- bare `unity-cli --project .`는 기본 경로로 쓰지 않는다.
- wrapper로 해결되지 않을 때만 direct command를 검토하고, 이때는 절대 경로를 정규화한 `--project "A:/..."` 형태를 쓴다.
- 우선 대상:
  - `status`
  - `list`
  - `compile`
  - `clear-console`
  - `console`
  - `bootstrap`
  - `test-edit`
  - `test-play`
  - `report-town`
  - `report-battle`
  - `smoke-observer`
  - one-shot read probe

3. MCP는 typed lane이다.
- scene/prefab/component/package의 구조적 조작
- typed guardrail이 더 안전한 작업
- existing MCP custom tool이 이미 있는 작업
- CLI 결과만으로 충분하지 않은 경우의 targeted fallback

4. 탐색 낭비를 금지한다.
- 같은 목적 때문에 MCP tool catalog를 먼저 훑지 않는다.
- 2회 이상 Unity inspect가 필요해지면 현재 방식이 잘못된 것으로 본다.
- 3회째 MCP inspect 전에 멈추고 CLI aggregate report 또는 project custom tool로 압축한다.
- exploratory MCP fishing을 금지한다.

5. raw `unity-cli exec`는 read-first다.
- broad write, hidden global mutation, package 변경, large scene surgery에 쓰지 않는다.
- 반복 probe는 wrapper verb 또는 custom tool로 승격한다.
- wrapper에서 raw `exec`는 기본 차단이며 명시 opt-in이 있어야 한다.

6. text asset를 직접 고친 뒤에는 필요 시 `unity-cli reserialize <paths>`를 검토한다.

## Verification Loop

1. file edit
2. `pwsh -File tools/unity-bridge.ps1 compile`
3. `pwsh -File tools/unity-bridge.ps1 clear-console`
4. `pwsh -File tools/unity-bridge.ps1 bootstrap`
5. `pwsh -File tools/unity-bridge.ps1 report-town` 또는 `report-battle`
6. `pwsh -File tools/unity-bridge.ps1 console`
7. `pwsh -File tools/unity-bridge.ps1 test-edit` / `test-play`
8. 아직 불명확하면 targeted MCP

## Reporting Rules

- human-facing 보고는 한국어
- code / commands / file names / identifiers 는 영어
- 보고 형식은 아래 순서를 유지한다.
  - 현재 상태
  - 완료
  - 보류
  - 이슈 / 리스크
  - 결정
  - 다음 단계
