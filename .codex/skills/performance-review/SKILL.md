---
name: performance-review
description: >
  Reviews ASP.NET Core and EF Core performance risks in this Zombie Survival server.
  Use for "performance review", "slow endpoint", "query optimization", "EF Core performance",
  "expensive endpoint", "payload size", "rate-limit cost", or any code review focused on
  request latency, database load, thread blocking, startup work, or abuse-cost issues.
---

## Review Scope

Use this skill for performance-focused review. Do not rewrite code unless the user explicitly asked for implementation.

Start by inspecting the current diff, relevant controller/service methods, DTOs, `GameDbContext`, options, and `Program.cs`.
Keep findings concrete: file, line, runtime risk, and narrow fix.

## Checklist

### 1. Request Pipeline

- Pass `CancellationToken` from controllers into services and EF Core calls.
- Avoid `.Result`, `.Wait()`, `Task.Run`, and sync blocking inside request paths.
- Avoid synchronous materialization such as `ToList()` in services when async EF alternatives are available.
- Keep expensive unauthenticated endpoints protected by auth, rate-limit, or cheap pre-validation.

### 2. EF Core / MySQL

- Use `AsNoTracking()` for read-only queries.
- Avoid broad `Include` chains when projection to response DTOs is enough.
- Check `Include` + collection loading for cartesian explosion risk; consider narrower queries or split query only when measured/appropriate.
- Verify common lookup paths have indexes in `GameDbContext`, especially:
  - `AppUser.UserName`
  - `AppUser.Email`
  - `PlayerSaveData.PlayerId`
  - `PlayerWeaponState.PlayerSaveDataId + FirearmDefinitionId`
  - `FirearmDefinition.Name`
  - `PlayerStatUpgradeState.PlayerSaveDataId + StatName`
- Avoid loading full player save state when an endpoint only needs gold, one weapon, one stat, or one catalog row.

### 3. Data Shape

- Do not return internal state that the client does not need.
- Keep response DTOs stable and focused on the endpoint workflow.
- For catalog-style endpoints, prefer server-defined catalog data from configuration or read-only EF queries.
- Watch for dictionaries or lists that can grow without limits.

### 4. Startup And Background Cost

- Startup applies EF Core migrations and performs firearm catalog upsert. Review both for DB round trips and failure behavior.
- Do not add slow network calls or long-running data repair to startup without explicit developer choice.
- Prefer scoped repair work tied to login/save flows only when the cost is bounded and idempotent.

### 5. Abuse Cost

- Login, email verification, gacha, save, upgrade, and other repeatable endpoints should reject invalid input before expensive DB or RNG work.
- Identify missing rate limits for endpoints that send email, spend currency, roll gacha, or perform broad DB reads.
- Do not accept client-provided cost, probability, reward, seed, damage, player ID, or ownership values that can increase server work or bypass authority.

## Output Format

```text
## Performance Review Results

### High Impact
- [file:line] Risk + why it matters + narrow fix

### Medium Impact
- [file:line] Risk + recommended improvement

### Low Impact / Observation
- [file:line] Measurement or cleanup suggestion

### Passed
- Checked items that look correct

### Verification
- Build/smoke/measurement suggestions or completed checks
```

If a proposed optimization changes API shape, persistence behavior, security posture, or startup behavior, present exactly three options and mark one as `(Recommended)` before implementing.
