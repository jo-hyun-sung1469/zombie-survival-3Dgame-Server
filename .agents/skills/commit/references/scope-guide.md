# Scope Selection Guide

## Scope Table

| Scope | When to use | Example |
|---|---|---|
| `auth` | Registration, login, JWT issuance, claim handling | `fix(auth): reject duplicate usernames case-insensitively` |
| `player-data` | Player save endpoints, save DTOs, save mapping | `update(player-data): persist weapon ownership state` |
| `data` | `GameDbContext`, entity configuration, indexes, relationships | `refactor(data): tighten mysql entity constraints` |
| `services` | Service-layer changes not limited to one endpoint | `refactor(services): split token creation from credential validation` |
| `api` | Routing, controller response codes, API contract changes | `update(api): return not found for missing player save data` |
| `config` | `Program.cs`, app settings, DI, auth pipeline | `update(config): register mysql db context explicitly` |
| `security` | Password hashing, authorization checks, validation hardening | `fix(security): block save requests without userId claim` |
| `docs` | Documentation-only changes | `docs(docs): describe save-data endpoint flow` |
| `global` | Cross-cutting changes spanning 3 or more areas | `refactor(global): normalize cancellation token propagation` |

## Decision Rules

- Default to the narrowest scope that matches the changed files.
- If only controllers changed, `api` is often the right scope.
- If only service classes changed, use the owning domain scope when obvious, otherwise `services`.
- Use `config` for startup wiring and settings changes.
- Use `global` only when a narrow scope would hide the breadth of the change.
