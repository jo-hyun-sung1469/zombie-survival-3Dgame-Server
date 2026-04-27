---
name: resolve-pr-comments
description: >
  Prepare replies for resolved PR review comments after code changes are applied.
  Use when the user asks to answer review comments, summarize how comments were addressed,
  or wants concise reviewer-facing replies tied to actual commits.
---

# Resolve PR Comments

Use this skill when review feedback has already been handled in code and the user wants reply text.

## Step 1 - Collect Context

Inspect:

- The review comments
- The final branch diff
- Relevant commit hashes from `git log --oneline`

If GitHub CLI is available and the user wants live replies, collect PR metadata first:

```bash
gh pr view --json number,baseRefName,headRefName
gh pr view --comments
```

## Step 2 - Judge Resolution

For each comment:

- Match the comment to the affected file and behavior.
- Confirm the current diff actually addresses the concern.
- Mark unresolved comments separately instead of forcing a reply.

## Step 3 - Find The Commit

For resolved comments, find the most relevant commit:

```bash
git log --oneline -- <path>
```

Use the shortest commit hash that is still unambiguous.

## Step 4 - Write The Reply

Default to concise reviewer-facing replies:

- Keep replies short and factual.
- State what changed.
- Mention the commit hash if the user asked for it.
- Do not claim a comment is resolved unless the code actually changed accordingly.

## Template

```text
Addressed in <commit>.
Updated <behavior> by <what changed>.
```

## Rules

- One reply per review point.
- If a comment required no code change, explain why briefly.
- If a request was only partially applied, say what was done and what remains.
- Prefer reviewer-facing language over implementation trivia.
- If the user wants actual posting through GitHub, prepare the reply text first and only post once the mapping is verified.
