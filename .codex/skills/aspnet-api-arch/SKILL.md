---
name: aspnet-api-arch
description: Architecture guide for this ASP.NET Core game server. Use when adding or changing controllers, services, DTO contracts, dependency injection, domain folders, or request/response flow.
---

# ASP.NET API Architecture Guide

## Domain Structure

Each feature lives in its own domain folder. Keep folders aligned with the runnable project layout.

```text
zombie_servival-3Dgame_Server/
- Auth/         # Login, registration, JWT token flow
- Player/       # Player stats and player-facing state
- Firearm/      # Firearm stat catalog and firearm-facing APIs
- Inventory/    # Gold, owned weapons, save data
- Gacha/        # Pull logic and reward pool
- Contracts/    # Request/response DTOs by domain
- Data/         # GameDbContext and EF Core mappings
- Options/      # Configuration binding classes
```

## Internal Layout Per Domain

```text
{Domain}/
- {Domain}Controller.cs
- I{Domain}Service.cs
- {Domain}Service.cs
- Models/        # persistence entities when the domain owns DB tables
```

DTOs for a domain go in `Contracts/{Domain}/`.

## Preferred Flow

`Controller -> Service -> DbContext or Options`

- Keep controllers thin: routing, auth attributes, status codes, and model binding only.
- Do not return HTTP-specific types from services.
- Services own business rules, validation coordination, and persistence access.

## Controller Rules

- Use `[ApiController]`.
- Use constructor injection.
- Keep routes explicit and stable.
- Accept `CancellationToken` in async actions and pass it through.
- Use `ActionResult<T>` when returning multiple status codes.
- Read player identity from JWT claims, never from client-supplied body fields.

## Service Rules

- One service per domain area.
- Keep token creation in `JwtTokenService` and auth logic in `DbAuthService`.
- Prefer `IOptions<T>` for configuration-backed catalogs such as gacha and firearm definitions.
- New domains get their own service. Do not overload unrelated existing services.

## DTO Rules

- Request DTOs use validation attributes where appropriate.
- Preserve response shape unless the task explicitly asks for an API contract change.
- Keep response DTOs immutable with `init` setters when practical.
- Do not trust client-supplied player identifiers when the JWT claim already provides the identity.

## Adding A New Domain

1. Create the domain folder under the project root.
2. Add controller, service interface, and service implementation.
3. Add DTOs under `Contracts/{Domain}/`.
4. Register the service in `Program.cs`.
5. Add entities and EF mappings only if the new domain persists data.

## `Program.cs` Rules

- Register all feature services through DI here.
- Keep auth, authorization, controllers, DbContext, and OpenAPI setup centralized.
- Bind configuration option classes here for new catalogs or tunable values.
- Every new service must be wired in `Program.cs` immediately.
