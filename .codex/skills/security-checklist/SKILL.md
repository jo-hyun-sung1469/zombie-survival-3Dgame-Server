---
name: security-checklist
description: >
  Performs a specialized security audit of a C# .NET game server.
  Covers anti-cheat, server authority validation, JWT/auth flow, email verification,
  gacha authority, player identity, save-data integrity, secrets, and abuse controls.
  Use for "security review", "check for vulnerabilities", "anti-cheat verification",
  "any hardcoded keys?", "review auth logic", or any security-related request.
---

## Audit Scope

## Guardrail Check

Before proposing or applying a security fix, inspect the relevant configuration, auth flow, persistence code, and current git state.

If the fix has meaningful tradeoffs, present exactly three options and mark one as `(Recommended)`.
Use the options to separate minimal safe fixes, broader hardening, and documented follow-ups.
Limit security decision conversations to 25 turns for important findings and 15 turns for minor findings.
If the limit is reached, summarize the remaining choice and recommend the safest actionable path.
Do not offer insecure options as valid choices; if a critical issue is present, state that it must be fixed before merge.

### 1. Anti-Cheat

```text
Server Authority
  - Is player identity always read from JWT claims?
  - Are client-supplied cost, reward, probability, RNG seed, damage, ownership, and stat values rejected or revalidated?
  - Are weapon and stat identifiers validated against server catalogs/options?

Gacha Manipulation
  - Is RNG generated server-side only?
  - Is probability defined by server configuration/catalog data only?
  - Are pull cost, reward, rarity, and owned-state changes computed on the server?
```

### 2. Input Validation

```text
API Input
  - Are required fields validated before business logic runs?
  - Are negative quantities and out-of-range values rejected?
  - Are identifiers like PlayerId and weapon names re-validated server-side?

Business Input
  - Are config-backed catalogs used to validate client-submitted identifiers?
  - Are unauthorized resources rejected even if the client guesses valid names?
  - Are integer overflows and duplicate state rows handled safely?
```

### 3. Authentication / Session

```text
- Are JWT settings loaded from configuration instead of source code?
- Does startup fail fast when JWT secret or connection string is missing?
- Do authenticated endpoints reject missing or invalid `userId` claims?
- Are role and name claims read consistently across controllers?
- Do registration and login preserve the expected status codes: duplicate username 409, invalid login 401?
- Are email verification codes hashed, expiring, attempt-limited, and invalidated or superseded safely?
- Do auth responses and logs avoid leaking passwords, full tokens, verification codes, and SMTP credentials?
```

### 4. Hardcoded Secret Detection

Patterns to check:

```csharp
var secret = "my-secret-key-123";
const string JwtKey = "hardcoded-jwt-secret";
string connStr = "Server=prod.db;Password=<configured-password>";
```

Correct approach:

```csharp
var secret = configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key is not configured.");
```

### 5. Rate Limiting

```text
- Is there a rate limit on gacha requests?
- Is there a login attempt limit?
- Is there a send-code / verify-code attempt limit?
- Is abuse control present for repeated expensive endpoints?
- Are unauthenticated endpoints cheap before DB, SMTP, RNG, or broad query work?
```

### 6. Persistence And Data Ownership

```text
- Does `PlayerSaveData` belong to the authenticated user only?
- Are `PlayerWeaponState` and `PlayerStatUpgradeState` updates scoped through the owning save row?
- Are EF Core uniqueness constraints aligned with service assumptions?
- Are duplicate legacy rows handled without crashing privileged endpoints?
- Do startup migrations and seeding avoid writing secrets or destructive schema changes?
```

## Audit Output Format

```text
## Security Audit Results

### Critical
- Problem description + specific fix

### Warning
- Problem description + recommended approach

### Info
- Improvement suggestion

### No Issues
- Items that passed
```

If any Critical items are found, explicitly state: **Do not merge until resolved.**
