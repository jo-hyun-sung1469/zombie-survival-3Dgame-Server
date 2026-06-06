---
name: commit
description: >
  Create Git commits by splitting changes into logical units following project conventions.
  Use for commit, staging, or push requests such as "commit this", "stage changes",
  "wrap this up", or "push it".
---

# Commit Skill

Use this skill when the user wants git changes staged, committed, or prepared for push.

## Branch Check

Run:

```bash
git branch --show-current
```

- Inspect the current branch before staging.
- Do not create or switch branches automatically unless the user asked for it.
- If the current branch looks wrong for the requested work, ask before proceeding.

## Commit Message Rules

Format:

```text
type(scope): description
```

Types:

- `add`
- `update`
- `fix`
- `refactor`
- `test`
- `docs`
- `merge`

Scopes:

- See `references/scope-guide.md`
- Use the narrowest scope that matches the change
- Use `global` only for broad cross-cutting work

Description rules:

- Write it in Korean
- Keep it to one subject line
- No trailing period
- Prefer imperative wording such as `jwt 클레임 검증 추가`
- Avoid past tense phrasing

## Commit Flow

1. Inspect changes with `git status` and targeted `git diff`.
2. Separate unrelated changes before staging.
3. Group one logical unit per commit.
4. Stage only the intended files or hunks.
5. Commit with `git commit -m "type(scope): description"` using a Korean description.
6. Verify recent history with `git log --oneline -n <count>`.

## Guardrails

- Do not mix feature, refactor, and docs changes in one commit unless they are inseparable.
- Do not stage generated files, local databases, temp outputs, or IDE noise unless the user explicitly wants them committed.
- If the worktree contains unrelated user changes, leave them untouched and commit only the requested subset.
- If the user asked to push, confirm the branch and remote after committing.
