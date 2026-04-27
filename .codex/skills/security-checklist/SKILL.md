---
name: security-checklist
description: >
  Performs a specialized security audit of a C# .NET game server.
  Covers anti-cheat, server authority validation, session/JWT handling, and input integrity.
  Use for "security review", "check for vulnerabilities", "anti-cheat verification",
  "any hardcoded keys?", "review auth logic", or any security-related request.
---

## Audit Scope

### 1. Anti-Cheat (Game Server Specific)

```
Speed / Position Validation
  - Is player movement speed within server-defined thresholds?
  - Does the server independently verify client-sent coordinates?
  - Is there detection logic for teleportation / position jumping?

Damage Validation
  - Is damage value never accepted from the client and applied directly?
  - Are weapon stats and damage calculations performed server-side?
  - Is there a maximum damage cap check?

Gacha Manipulation Prevention
  - Is the RNG seed generated server-side only?
  - Can the client not modify the probability table?
  - Are gacha results recorded in server logs?
```

### 2. Input Validation

```
Packet Validation (Networking layer)
  - Is there an upper limit on packet size?
  - Are packets with missing required fields rejected immediately?
  - Is the session terminated on receipt of a malformed packet?

Business Input
  - Are negative quantities and out-of-range values rejected?
  - Are identifiers like PlayerId and ItemId re-validated server-side?
```

### 3. Authentication / Session

```
- Are session tokens generated server-side?
- Are expired sessions invalidated immediately?
- Is concurrent login from the same account handled?
- Are session IDs unpredictable? (GUID or cryptographic random)
```

### 4. Hardcoded Secret Detection

Patterns to check:
```csharp
// ❌ Remove immediately
var secret = "my-secret-key-123";
const string JwtKey = "hardcoded-jwt-secret";
string connStr = "Server=prod.db;Password=P@ssw0rd";
```

Correct approach:
```csharp
// ✅ Load from environment variable or configuration
var secret = configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key is not configured.");
```

### 5. Rate Limiting

```
- Is there a rate limit on gacha requests?
- Is there a login attempt limit?
- Is per-packet rate limiting present in the Networking layer?
```

---

## Audit Output Format

```
## Security Audit Results

### 🚨 Critical — Immediate fix required
- [item] Problem description + specific fix

### ⚠️ Warning — Fix recommended
- [item] Problem description + recommended approach

### ℹ️ Info — Advisory
- [item] Improvement suggestion

### ✅ No Issues
- List of items that passed
```

If any Critical items are found, explicitly state: **Do not merge until resolved.**
