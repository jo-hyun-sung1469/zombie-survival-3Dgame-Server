---
name: code-review
description: >
  Performs checklist-based automated review of C# .NET game server code.
  Use for pre-PR review, "review this code", "any issues here?", "code review please",
  or any code inspection request.
  Reviews in priority order: Security -> Server Authority -> Architecture -> Performance -> Conventions.
---

## Review Order

## Pre-Review Guardrail Check

Before reviewing or changing code, inspect the current diff and relevant files.

If a finding requires an implementation choice, present exactly three options and mark one as `(Recommended)`.
Use this for decisions such as minimal fix versus broader refactor, security behavior changes, or whether to defer a follow-up.
Limit review decision conversations to 25 turns for important findings and 15 turns for minor findings.
If the limit is reached, summarize the remaining choice and recommend the safest actionable path.
Do not ask for user input when the correct action is directly determined by the repository or by this checklist.

### 1. Security (Highest Priority)

- [ ] No code that applies client input values directly to server state?
  - e.g. `player.Level = request.Level` → **Block immediately**
- [ ] Is RNG performed server-side only? (Receiving client seeds is forbidden)
- [ ] No hardcoded secrets or keys?
- [ ] Are player/user identifiers read from JWT claims instead of request bodies?
- [ ] Are login, email verification, gacha, save, and upgrade abuse paths rate-limited or at least called out?

### 2. Server Authority

- [ ] Are all game logic decisions made on the server?
- [ ] Are client-sent coordinates, speeds, or damage values never used as-is?
- [ ] Are client-supplied cost, reward, probability, gacha result, weapon ownership, and stat values revalidated server-side?

### 3. Architecture

- [ ] Does request flow follow `Controller -> Service -> GameDbContext/Options`?
- [ ] Do controllers handle HTTP concerns only?
- [ ] Do services contain business rules and database coordination?
- [ ] Are DTOs under `Contracts/{Domain}/` and domain models under `{Domain}/Models/`?
- [ ] Is `CancellationToken` propagated through async controller and service methods?

### 4. DTO / Models

- [ ] Are Response and Dto types `init` only (immutable)?
- [ ] Are nullable reference types handled properly? (No `!` operator abuse)
- [ ] Do request DTOs avoid privileged fields such as `PlayerId`, `UserId`, `Role`, result/reward IDs, or server-owned costs?

### 5. Async

- [ ] Do all async methods have the `Async` suffix?
- [ ] Is there no `async void`? (Except event handlers)
- [ ] Is there no `.Result`, `.Wait()`, or sync materialization inside request paths?

### 6. Performance

- [ ] Do read-only EF Core queries use `AsNoTracking()`?
- [ ] Are `Include` chains limited to relationships the response actually needs?
- [ ] Are common lookup fields indexed in `GameDbContext`?
- [ ] Are expensive or repeatable endpoints protected against abuse?

### 7. Conventions

- [ ] Are all public types `PascalCase`?
- [ ] Do log messages start with an English verb and use structured placeholders?
- [ ] No unnecessary comments? (No comments on self-evident code)
- [ ] Does `Program.cs` register new services and options consistently?

---

## Review Output Format

Report findings in the following format:

```
## Code Review Results

### Must Fix (Security / Server Authority)
- [filename:line] Problem description + how to fix

### Should Fix (Architecture / Performance / Conventions)
- [filename:line] Problem description + recommended fix

### Passed
- Summary of checked items
```

If no issues are found, output: `All checklist items passed.`
