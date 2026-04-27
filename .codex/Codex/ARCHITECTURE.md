# Architecture

## Entry Point

`Program.cs` composes the HTTP pipeline, authentication, EF Core, and feature services.

Current DI registrations:

- `IAuthService -> DbAuthService`
- `IJwtTokenService -> JwtTokenService`
- `IInventoryService -> InventoryService`

## Domain Layout

The project uses domain folders instead of shared `Controllers/` and `Services/` buckets.

```text
zombie_servival-3Dgame_Server/
戍式式 Auth/
弛   戍式式 AuthController.cs
弛   戍式式 IAuthService.cs
弛   戍式式 DbAuthService.cs
弛   戍式式 IJwtTokenService.cs
弛   戍式式 JwtTokenService.cs
弛   戌式式 Models/
戍式式 Inventory/
弛   戍式式 InventoryController.cs
弛   戍式式 IInventoryService.cs
弛   戍式式 InventoryService.cs
弛   戌式式 Models/
戍式式 Contracts/
弛   戍式式 Auth/
弛   戌式式 Inventory/
戍式式 Data/
戌式式 Options/
```

## Flow

Preferred request flow:

```text
HTTP Request
  -> Controller
  -> Domain Service
  -> GameDbContext
  -> Response DTO
```

Rules:

- Controllers own routing, status codes, auth attributes, and model binding
- Services own business logic and data coordination
- Persistence models stay inside their domain under `Models/`
- Shared DTOs stay in `Contracts/{Domain}/`

## Current Domains

### Auth

Responsibilities:

- Register users
- Validate username/password credentials
- Issue JWT access tokens
- Expose authenticated identity through `/api/auth/me`

### Inventory

Responsibilities:

- Save player gold and weapon ownership
- Read player save data for the authenticated user
- Map request dictionary data to persistence models

## Data Layer

`GameDbContext` currently manages:

- `Users`
- `PlayerSaveData`
- `PlayerWeaponStates`

SQLite is the only configured provider, and startup uses `Database.EnsureCreated()`.

## Adding A New Domain

1. Create a new domain folder under the project root.
2. Add a controller, service interface, service implementation, and domain models.
3. Add DTOs under `Contracts/{Domain}/` if the API contract is shared.
4. Register the service in `Program.cs`.
5. Extend `GameDbContext` and `OnModelCreating` if persistence is needed.
