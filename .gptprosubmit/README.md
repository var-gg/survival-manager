# `.gptprosubmit/` — workspace hook

글로벌 `gpt-pro-submit` 스킬의 워크스페이스 hook.
orchestrate.py가 cwd 위쪽에서 이 디렉토리를 발견하면 여기 config + scenarios + payload를 사용한다.

기본 실행 경로:

- Codex: `~/.codex/skills/gpt-pro-submit/scripts/orchestrate.py`
- Claude Code: `~/.claude/skills/gpt-pro-submit/scripts/orchestrate.py`

## 구성

- `config.yaml` — ChatGPT 프로젝트 URL, pindoc enable, user-data-dir 등
- `scenarios/` — 워크스페이스 전용 시나리오 (글로벌 fallback보다 우선)
- `payload/` — bundle/prompt/response 임시 산출물 (gitignored, 최근 N개 자동 보존)

## 호출 (예)

```powershell
# pindoc bundle 풀 사이클 (submit + fetch)
python ~/.codex/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix

# focus hint 추가
python ~/.codex/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix --extra "단린 voice 위주로"

# 특정 slug만
python ~/.codex/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix --slugs hero-dawn-priest,hero-grave-hexer

# bundle/prompt 검토만 (브라우저 안 띄움)
python ~/.codex/skills/gpt-pro-submit/scripts/orchestrate.py narrative-consistency-fix --dry-run
```

## 레거시 로컬 스킬 정리

이전의 `.agents/skills/gpt-pro-submit/` 로컬 스킬은 제거했다. 중복 스킬 노출과 코드 분기를 막기 위해 `gpt-pro-submit` 실행 코드는 글로벌 스킬 하나만 사용한다.
