---
name: security-checklist
description: >
  Performs a specialized security audit of a C# .NET game server.
  Covers anti-cheat, server authority validation, session/JWT handling, and input integrity.
  Use for "security review", "check for vulnerabilities", "anti-cheat verification",
  "any hardcoded keys?", "review auth logic", or any security-related request.
---

## Audit Scope

### 1. Anti-Cheat

```text
Movement Validation
  - Is movement speed checked against server-defined thresholds?
  - Does the server independently validate client-sent position state?
  - Is impossible movement detected and rejected?

Damage Validation
  - Is damage value never accepted from the client and applied directly?
  - Are weapon stats and damage calculations performed server-side?
  - Is there a maximum damage or sanity check?

Gacha Manipulation Prevention
  - Is RNG generated server-side only?
  - Can the client avoid modifying the probability table?
  - Are reward decisions based on server configuration only?
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
```

### 3. Authentication / Session

```text
- Are JWT settings loaded from configuration instead of source code?
- Does startup fail fast when JWT secret or connection string is missing?
- Do authenticated endpoints reject missing or invalid `userId` claims?
- Are role and name claims read consistently across controllers?
```

### 4. Hardcoded Secret Detection

Patterns to check:

```csharp
var secret = "my-secret-key-123";
const string JwtKey = "hardcoded-jwt-secret";
string connStr = "Server=prod.db;Password=P@ssw0rd";
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
- Is abuse control present for repeated expensive endpoints?
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
