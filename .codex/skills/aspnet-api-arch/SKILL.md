---
name: aspnet-api-arch
description: Architecture guide for this ASP.NET Core game server. Use when adding or changing controllers, services, DTO contracts, dependency injection, domain folders, or request/response flow.
---

# ASP.NET API Architecture Guide

## Domain Structure

Each feature lives in its own domain folder. All domains follow the same internal layout.

```
zombie_servival-3Dgame_Server/
├── Auth/               # Login, logout, registration (complete)
├── Player/             # Character stats, in-game stat increases
├── Inventory/          # Gold, weapons owned, weapon enhancement/stats
├── Gacha/              # Roulette, probability, reward grant
├── GameSession/        # Continue, restart, new game, wave state
├── Reward/             # Kill count → reward calculation
├── Zombie/             # Difficulty scaling, wave configuration
├── Contracts/          # Shared request/response DTOs (if cross-domain)
├── Data/               # GameDbContext
└── Options/            # Configuration binding classes
```

## Internal Layout Per Domain

```
{Domain}/
├── {Domain}Controller.cs
├── {Domain}Service.cs        # interface
├── {Domain}ServiceImpl.cs    # implementation (or I{Domain}Service pattern)
└── Models/                   # persistence entities for this domain
```

DTOs for a domain go in `Contracts/{Domain}/` if shared, or inline in the domain folder if private.

## Preferred Flow

`Controller → Service → DbContext`

- Keep controllers thin — routing, auth attributes, status codes, model binding only.
- Do not put HTTP-specific return types inside services.
- Services own business rules and data loading/saving.

## Controller Rules

- Use `[ApiController]`.
- Use constructor injection.
- Keep routes explicit and stable.
- Accept `CancellationToken` in async actions and pass it through.
- Use `ActionResult<T>` when returning different status codes.
- Read player identity from JWT claims, never from client-supplied body fields.

## Service Rules

- One service per domain area.
- Keep token creation in `JwtTokenService`, auth logic in `DbAuthService`.
- New domains get their own service — do not extend existing services for unrelated features.

## DTO Rules

- Request DTOs use validation attributes where appropriate.
- Preserve response shape unless the task explicitly asks for an API contract change.
- Use `Gold` as non-negative. Do not trust client-supplied player identifiers when the JWT claim provides it.

## Adding a New Domain

1. Create the domain folder under the project root.
2. Add `{Domain}Controller.cs`, `{Domain}Service.cs`, and models.
3. Register the service in `Program.cs`.
4. Add any new entities to `GameDbContext` and update `OnModelCreating`.

## `Program.cs` Rules

- Register all feature services through DI here.
- Keep auth, authorization, controllers, DbContext, and OpenAPI setup centralized.
- Every new service must be wired in `Program.cs` immediately.