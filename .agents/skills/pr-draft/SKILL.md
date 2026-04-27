---
name: pr-draft
description: >
  Draft a pull request title and body from the current git diff.
  Use when the user asks for a PR draft, PR summary, change summary for review,
  or wants a branch-ready explanation before opening a pull request.
---

# PR Draft Skill

Use this skill after inspecting the current branch, commit range, and actual diff.

## Step 1 - Gather Context

Run the following depending on what refs exist:

```bash
git branch --show-current
git log --oneline -15
git diff --stat
git diff
```

If a likely base branch exists, prefer comparing against it:

```bash
git diff origin/main...HEAD --stat
git diff origin/main...HEAD
git diff origin/master...HEAD --stat
git diff origin/master...HEAD
git diff origin/develop...HEAD --stat
git diff origin/develop...HEAD
```

If `.github/PULL_REQUEST_TEMPLATE.md` exists, read it and follow that structure.

## Step 2 - Determine PR Shape

- Identify the dominant scope of the change.
- Separate user-visible behavior changes from refactors.
- Note real verification results only.
- Note risks around auth, persistence, startup, or API contract changes.

## Step 3 - Generate Output

Produce:

1. A concise English PR title
2. A short PR body with:
   - `Summary`
   - `Changes`
   - `Verification`
   - `Risks` or `Follow-ups`

## Rules

- Base the draft on actual diffs, not assumptions.
- Group by behavior and impact, not by raw file list.
- Mention verification that was actually run.
- If verification was not run, say that clearly.
- Call out auth, persistence, startup, or API contract risks explicitly when relevant.
- Do not claim tests passed unless they were actually run.

## Project-Specific Guidance

- Mention auth changes separately from player save-data changes.
- Call out `Program.cs` or configuration changes because they affect startup behavior.
- For EF Core or SQLite changes, note whether the current `EnsureCreated()` workflow is affected.
