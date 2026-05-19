# `.gptprosubmit/` — workspace hook

`~/.claude/skills/gpt-pro-submit/` 글로벌 스킬의 워크스페이스 hook.
orchestrate.py가 cwd 위쪽에서 이 디렉토리를 발견하면 여기 config + scenarios + payload를 사용한다.

## 구성

- `config.yaml` — ChatGPT 프로젝트 URL, pindoc enable, user-data-dir 등
- `scenarios/` — 워크스페이스 전용 시나리오 (글로벌 fallback보다 우선)
- `payload/` — bundle/prompt/response 임시 산출물 (gitignored, 최근 N개 자동 보존)

## 호출 (예)

```powershell
# pindoc bundle 풀 사이클 (submit + fetch)
python ~/.claude/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix

# focus hint 추가
python ~/.claude/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix --extra "단린 voice 위주로"

# 특정 slug만
python ~/.claude/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix --slugs hero-dawn-priest,hero-grave-hexer

# bundle/prompt 검토만 (브라우저 안 띄움)
python ~/.claude/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix --dry-run
```

## 기존 Codex 스킬과의 관계

`.agents/skills/gpt-pro-submit/`는 Codex가 사용하는 동등 스킬로 그대로 유지. 이 디렉토리(`.gptprosubmit/`)는 Claude Code 글로벌 스킬용. 둘은 같은 ChatGPT 프로젝트를 가리키되 코드는 분리되어 있다.
