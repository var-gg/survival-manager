# GitHub Publish Notes

- Status: active
- Last Updated: 2026-03-29
- Scope: local repo publish / push workflow

## Purpose

Prevent repeat push mistakes caused by using an SSH remote when the working environment is actually set up for `gh` or HTTPS credential-based GitHub access.

## Default Rule

For this repository, prefer GitHub publish/push paths in this order:

1. `gh` CLI if available and authenticated
2. HTTPS remote with stored GitHub credentials
3. SSH only if it is explicitly confirmed working for the current machine/session

## Before Pushing

Run and verify:

1. `git remote -v`
2. `git branch --show-current`
3. `git status --short`
4. `gh auth status` if `gh` exists

If `origin` is SSH-form (`git@github.com:...`) but `gh` is unavailable or SSH is not confirmed, switch `origin` to HTTPS before pushing.

## Preferred Remote

Preferred remote for this repo:

- `https://github.com/var-gg/survival-manager.git`

## Recovery Rule

If push fails because of SSH host/auth issues:

- do not keep retrying SSH by default
- check whether `gh` exists
- if `gh` does not exist or is not usable, check Git Credential Manager HTTPS credentials
- switch `origin` to HTTPS and retry

## Rebase Rule

If push is rejected because remote contains commits not present locally:

1. `git fetch origin`
2. inspect local-only and remote-only history
3. `git pull --rebase origin main`
4. resolve conflicts in favor of the intended local working tree unless there is a reason not to
5. push again

## Incident Note

2026-03-29:
- initial push attempted over SSH because `origin` was configured as SSH
- this caused an avoidable host key verification failure
- repository was successfully recovered by switching `origin` to HTTPS and rebasing onto remote `main`

## Short Operator Reminder

Do not assume:
- `origin` is on the correct protocol
- `gh` is available in PATH
- SSH is the right path just because the repo already exists

Always inspect the remote first.
