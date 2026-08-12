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
├── Auth/
│   ├── AuthController.cs
│   ├── IAuthService.cs
│   ├── DbAuthService.cs
│   ├── IJwtTokenService.cs
│   ├── JwtTokenService.cs
│   └── Models/
├── Inventory/
│   ├── InventoryController.cs
│   ├── IInventoryService.cs
│   ├── InventoryService.cs
│   └── Models/
├── Contracts/
│   ├── Auth/
│   └── Inventory/
├── Data/
└── Options/
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

- Read authoritative player save data for the authenticated user
- Keep gold and weapon ownership mutations inside server-owned domain services
- Reject client attempts to replace privileged progression state

## Data Layer

`GameDbContext` currently manages:

- `Users`
- `PlayerSaveData`
- `PlayerWeaponStates`

MySQL is the configured provider, and startup applies committed EF Core migrations with `Database.MigrateAsync()`.

## Adding A New Domain

1. Create a new domain folder under the project root.
2. Add a controller, service interface, service implementation, and domain models.
3. Add DTOs under `Contracts/{Domain}/` if the API contract is shared.
4. Register the service in `Program.cs`.
5. Extend `GameDbContext` and `OnModelCreating` if persistence is needed.
