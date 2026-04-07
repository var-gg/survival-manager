# 도구 디렉터리

이 디렉터리는 로컬 개발 보조 스크립트와 반복 작업 유틸리티를 둔다.

현재 주요 진입점:

- `tools/unity-bridge.ps1`: local `unity-cli` fallback, project path normalization, transient retry, `prepare-playable` / `quick-battle-smoke` / `content-validate` / `balance-sweep-smoke` / test / Loop D shard 뒤 ready polling까지 포함한 repo-scoped wrapper
- `tools/pre-art-rc.ps1`: docs preflight, compile, batch tests, content validation, PlayMode, Loop D shard, observer report capture를 release-floor packet으로 묶는 RC wrapper
- `tools/docs-check.ps1`: 기본 docs policy + markdownlint + link check. `-Paths`를 주면 touched-file lint gate로 사용할 수 있다.
- `tools/smoke-check.ps1`: runtime smoke가 아니라 repo structure preflight sanity check다.
