---
name: code-review
description: >
  Performs checklist-based automated review of C# .NET game server code.
  Use for pre-PR review, "review this code", "any issues here?", "code review please",
  or any code inspection request.
  Reviews in priority order: Security → Server Authority → Architecture → Conventions.
---

## Review Order

## Pre-Review Guardrail Check

Before reviewing or changing code, inspect the current diff and relevant files.

If a finding requires an implementation choice, present exactly three options and mark one as `(Recommended)`.
Use this for decisions such as minimal fix versus broader refactor, security behavior changes, or whether to defer a follow-up.
Limit review decision conversations to 25 turns for important findings and 15 turns for non-important findings.
If the limit is reached, summarize the remaining choice and recommend the safest actionable path.
Do not ask for user input when the correct action is directly determined by the repository or by this checklist.

### 1. Security (Highest Priority)

- [ ] No code that applies client input values directly to server state?
  - e.g. `player.Level = request.Level` → **Block immediately**
- [ ] Is RNG performed server-side only? (Receiving client seeds is forbidden)
- [ ] Is packet validation handled in the Networking layer?
- [ ] No hardcoded secrets or keys?
- [ ] Are all rate-limited endpoints covered?

### 2. Server Authority

- [ ] Are all game logic decisions made on the server?
- [ ] Are client-sent coordinates, speeds, or damage values never used as-is?
- [ ] Is there anomaly detection logic for speed and position? (Security system)

### 3. Architecture

- [ ] Is the `System → Service → Repository` dependency one-directional?
- [ ] Does no System directly call another System?
- [ ] Does every system follow the `ExecuteAsync(Request, CancellationToken)` signature?
- [ ] Is `CancellationToken` propagated down through all layers?

### 4. DTO / Models

- [ ] Are Response and Dto types `init` only (immutable)?
- [ ] Are all enums `UPPER_CASE`?
- [ ] Are nullable reference types handled properly? (No `!` operator abuse)

### 5. Async

- [ ] Do all async methods have the `Async` suffix?
- [ ] Is there no `async void`? (Except event handlers)
- [ ] Is no `Task` returned without `await`?

### 6. Conventions

- [ ] Are all public types `PascalCase`?
- [ ] Do log messages start with an English verb and use structured placeholders?
- [ ] Do `GameException` messages end with a period and contain no dynamic data?
- [ ] No unnecessary comments? (No comments on self-evident code)

---

## Review Output Format

Report findings in the following format:

```
## Code Review Results

### 🚨 Must Fix (Security / Server Authority)
- [filename:line] Problem description + how to fix

### ⚠️ Should Fix (Architecture / Conventions)
- [filename:line] Problem description + recommended fix

### ✅ Passed
- Summary of checked items
```

If no issues are found, output: `✅ All checklist items passed.`
