# Zombie Survival Game Server

**??볥럢??以????酉釉???臾믩씜??몃빍??**

## Project Overview

ASP.NET Core Web API server for a 3D zombie survival game.
Players fight off zombies coming from all directions. The server handles authentication, player progression, inventory, gacha, game session state, rewards, and zombie difficulty.

## Source Of Truth

- The runnable server project is [`zombie_servival-3Dgame_Server/`](./zombie_servival-3Dgame_Server).
- Codex-facing docs and skills live under [`.codex/`](./.codex).
- When instructions conflict, prefer the actual C# project structure and source files.

## Project Structure

```text
zombie_servival-3Dgame_Server/
????? Auth/               # Login, logout, registration
????? Player/             # Character stats, in-game stat increases
????? Inventory/          # Gold, weapons owned, weapon enhancement/stats
????? Gacha/              # Roulette, probability, reward grant
????? GameSession/        # Continue, restart, new game, wave state
????? Reward/             # Kill count ??reward calculation
????? Zombie/             # Difficulty scaling, wave configuration
????? Contracts/          # Domain request/response DTOs
????? Data/               # EF Core DbContext
????? Options/            # Configuration binding types
?遺??? Program.cs          # DI, auth, EF Core, OpenAPI pipeline
```

## Tech Stack

- .NET 9 ASP.NET Core Web API
- EF Core with MySQL
- JWT Bearer authentication
- `PasswordHasher<TUser>` for password hashing
- OpenAPI in development

## Commands

- Build: `dotnet build`
- Run: `dotnet run --project .\zombie_servival-3Dgame_Server\zombie_servival-3Dgame_Server.csproj`
- Smoke test: use `zombie_servival-3Dgame_Server.http`

## Architecture Conventions

- Modular monolith ??one domain per folder, all inside a single project.
- Flow: `Controller -> Service -> DbContext`.
- Controllers handle HTTP concerns only: routing, auth attributes, status codes, model binding.
- Services contain business rules and database coordination.
- DTOs go in `Contracts/{Domain}/`. Domain models live under `{Domain}/Models/`.
- Pass `CancellationToken` through all async controller and service methods.
- Use constructor injection everywhere.
- When adding a new domain: create the folder, add controller + service + models, register in `Program.cs`.

## Authentication Rules

- JWT Bearer configured in `Program.cs`.
- Claims consumed by controllers: `userId`, `role`, `ClaimTypes.Name`.
- If claim names change in token generation, update every `User.FindFirst(...)` consumer.
- Registration returns `409 Conflict` for duplicate usernames.
- Login returns `401 Unauthorized` for invalid credentials.
- Never trust a client-supplied player identifier ??always read from JWT claims.

## Persistence Rules

- MySQL via `GameDbContext`.
- `Database.EnsureCreated()` at startup ??no migrations unless intentionally adopted.
- `PlayerSaveData` owns `PlayerWeaponState` through cascade delete.
- New entities go in the owning domain's `Models/` folder and are registered in `GameDbContext`.

## Domain Status

| Domain | Status |
|--------|--------|
| Auth | ??Complete |
| Inventory | ??Complete |
| Player | ?逾?Planned |
| Gacha | ?逾?Planned |
| GameSession | ?逾?Planned |
| Reward | ?逾?Planned |
| Zombie | ?逾?Planned |

## Practical Notes

- No separate test project. Verification is `dotnet build` + endpoint smoke tests.
- Avoid hardcoding secrets ??JWT settings belong in configuration.
- Minimal comments only ??where logic is not obvious.

## Custom Skills

Skills in `.codex/skills/`:

- `aspnet-api-arch` ??domain structure, controller/service rules
- `jwt-auth-flow` ??claims, registration, login rules
- `player-save-flow` ??save/load semantics, mapping rules
- `efcore-mysql-persistence` ??entity config, query rules
- `dotnet-server-verify` ??build and smoke test workflow
