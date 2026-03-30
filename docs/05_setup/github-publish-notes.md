# GitHub publish note

- 상태: active
- 최종수정일: 2026-03-31
- 적용범위: local repo publish / push workflow

## 목적

작업 환경이 실제로는 `gh` 또는 HTTPS credential 기반인데도 SSH remote를 써서 push 실수를 반복하는 상황을 막는다.

## 기본 규칙

이 저장소에서는 GitHub publish/push 경로를 아래 우선순위로 본다.

1. `gh` CLI가 있고 인증도 되어 있으면 `gh`
2. 저장된 GitHub credential이 있는 HTTPS remote
3. 현재 머신/세션에서 정상 동작이 명시적으로 확인된 경우에만 SSH

## Push 전 확인

아래 명령을 실행하고 확인한다.

1. `git remote -v`
2. `git branch --show-current`
3. `git status --short`
4. `gh`가 있으면 `gh auth status`

`origin`이 SSH 형식(`git@github.com:...`)인데 `gh`를 쓸 수 없거나 SSH가 확인되지 않았다면, push 전에 `origin`을 HTTPS로 바꾼다.

## 권장 remote

이 저장소의 권장 remote는 아래다.

- `https://github.com/var-gg/survival-manager.git`

## 복구 규칙

SSH host/auth 문제로 push가 실패했다면 아래를 따른다.

- 기본값으로 SSH를 반복 재시도하지 않는다.
- `gh`가 있는지 확인한다.
- `gh`를 쓸 수 없으면 Git Credential Manager의 HTTPS credential을 확인한다.
- `origin`을 HTTPS로 바꾼 뒤 다시 시도한다.

## Rebase 규칙

remote에 로컬에 없는 commit이 있어 push가 거절되면 아래를 따른다.

1. `git fetch origin`
2. local-only, remote-only history를 확인한다.
3. `git pull --rebase origin main`
4. 특별한 이유가 없으면 의도한 로컬 작업 트리를 기준으로 conflict를 해결한다.
5. 다시 push한다.

## incident 기록

2026-03-29:

- `origin`이 SSH로 설정된 상태에서 초기 push를 시도했다.
- 이 때문에 피할 수 있었던 host key verification failure가 발생했다.
- `origin`을 HTTPS로 바꾸고 remote `main`에 rebase해서 복구했다.

## 짧은 운영 메모

아래를 가정하지 않는다.

- `origin`이 이미 올바른 protocol일 것
- `gh`가 PATH에 있을 것
- 저장소가 있으니 SSH가 맞는 경로일 것

항상 remote를 먼저 확인한다.
