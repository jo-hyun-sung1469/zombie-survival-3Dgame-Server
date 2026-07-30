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

- `feat`
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
- Do not include an issue number in the subject line

Issue reference rules:

- Put the related issue number in the commit body after a blank line
- Write only `#<issue-number>` without prefixes such as `Refs:`, `Closes:`, or explanatory text
- When multiple issue numbers are provided, write each `#<issue-number>` on its own line
- Omit the issue reference body when no issue number was provided

Example:

```text
feat(data): EF 마이그레이션과 DB 헬스체크 추가

#20
```

## Commit Flow

1. Inspect changes with `git status` and targeted `git diff`.
2. Separate unrelated changes before staging.
3. Split work into logical commits instead of putting all current changes into one commit.
4. Group one logical unit per commit.
5. Write each commit message so it describes the specific change in that commit.
6. Stage only the intended files or hunks.
7. Commit with `git commit -m "type(scope): description"` using a Korean description. When an issue number was provided, add a second message paragraph such as `-m "#20"` instead of putting it in the subject.
8. Verify recent history with `git log --oneline -n <count>`.

## Fine-Grained Feature Commit Splitting

Prefer small, reviewable feature commits over one broad feature commit. When a feature spans multiple layers, split by independently understandable implementation step whenever the diffs can still build or be reviewed coherently.

Default feature split order:

1. DTO/contracts: request/response records and API contract shapes.
2. Models/entities: domain models, EF entities, enums, and value objects.
3. Persistence mapping: `GameDbContext`, relationships, indexes, seed/catalog configuration, and storage helpers.
4. Options/configuration: options classes, `appsettings` examples, and DI/startup registration.
5. Service logic: business rules, validation, calculations, and database coordination.
6. Controller/endpoints: routes, authorization attributes, status codes, model binding, and HTTP response mapping.
7. Verification/docs: `.http` smoke cases, tests when present, docs, and change summaries.

Rules:

- Do not combine DTO creation, entity creation, service implementation, and controller wiring into one commit when they can be reviewed separately.
- Keep commits buildable when practical. If an intermediate layer cannot compile alone, group only the minimum adjacent layer needed to preserve buildability and mention that reason.
- Use pathspecs or patch staging to stage only the files or hunks for the current layer.
- For large features, present the planned commit stack before committing so the developer can catch wrong boundaries.
- If a previous broad commit needs cleanup, prefer follow-up commits that restore clear feature boundaries; do not rewrite published history unless the developer explicitly asks.

## User Choice Points

When a commit decision materially affects the result, present exactly three options and mark one as `(Recommended)`.
Limit commit decision conversations to 25 turns for important decisions and 15 turns for minor decisions.

Ask for a user choice when:

- There are unrelated existing changes that could be excluded, included, or committed separately.
- The commit scope is ambiguous across multiple domains.
- The commit message could reasonably emphasize different intent, such as security, service behavior, or documentation.
- A risky change could be handled with a minimal fix, broader refactor, or deferred follow-up.

Default option sets:

- Worktree scope: `Commit requested files only (Recommended)`, `Include all current changes`, `Split into separate commits`.
- Commit message: provide three valid `type(scope): Korean description` candidates and recommend the narrowest accurate one.
- Risk handling: `Minimal safe fix (Recommended)`, `Broader cleanup`, `Document follow-up only`.

If the user does not choose and the task can proceed safely, use the recommended option and state that assumption.
If the conversation limit is reached, summarize the pending choice, apply the recommended safe default, and continue unless committing would include unrelated changes or otherwise require explicit user approval.

## Change Summary Artifact

For harness, workflow, or multi-file implementation changes, update `.codex/change-summaries/CHANGE_SUMMARY.md` before committing.

The summary should be brief and scannable:

- Date
- Purpose
- Changed areas
- Verification
- Remaining user decisions

Write the change summary content in Korean.

## Guardrails

- Do not mix feature, refactor, and docs changes in one commit unless they are inseparable.
- Do not collapse unrelated or independently reviewable changes into one commit just because they are all currently modified.
- Do not stage generated files, local databases, temp outputs, or IDE noise unless the user explicitly wants them committed.
- If the worktree contains unrelated user changes, leave them untouched and commit only the requested subset.
- If the user asked to push, confirm the branch and remote after committing.
