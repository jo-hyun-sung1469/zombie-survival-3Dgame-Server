# Zombie Survival Game Server

3D Survival + RPG + Gacha | ASP.NET Core Web API

## Quick Start

```bash
dotnet build .\zombie_servival-3Dgame_Server\zombie_servival-3Dgame_Server.csproj
dotnet run --project .\zombie_servival-3Dgame_Server\zombie_servival-3Dgame_Server.csproj
```

## Structure

```text
zombie_servival-3Dgame_Server/
?쒋?? Auth/               # Login, logout, registration
?쒋?? Player/             # Character stats, in-game stat increases
?쒋?? Inventory/          # Gold, weapons owned, weapon enhancement/stats
?쒋?? Gacha/              # Roulette, probability, reward grant
?쒋?? GameSession/        # Continue, restart, new game, wave state
?쒋?? Reward/             # Kill count ??reward calculation
?쒋?? Zombie/             # Difficulty scaling, wave configuration
?쒋?? Contracts/
??  ?쒋?? Auth/
??  ?쒋?? Player/
??  ?쒋?? Inventory/
??  ?쒋?? Gacha/
??  ?쒋?? GameSession/
??  ?쒋?? Reward/
??  ?붴?? Zombie/
?쒋?? Data/               # EF Core DbContext
?쒋?? Options/            # Configuration binding classes
?쒋?? Program.cs
?쒋?? appsettings.json
?붴?? zombie_servival-3Dgame_Server.http
```

## Architecture

- Modular monolith ??each domain is self-contained inside the single project
- Preferred flow: `Controller -> Service -> DbContext`
- Controllers own HTTP concerns only
- Services own business logic and persistence orchestration
- DTOs live under `Contracts/{Domain}/`
- Domain models live under `{Domain}/Models/`

## Current Domains

| Domain | Endpoints | Status |
|--------|-----------|--------|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` | ??Complete |
| Inventory | `POST /api/player-data/save`, `GET /api/player-data/me` | ??Complete |
| Player | Character stats, in-game stat upgrades | ?뵴 Planned |
| Gacha | Weapon roulette, probability, pity system | ?뵴 Planned |
| GameSession | Continue / restart / new game, wave state | ?뵴 Planned |
| Reward | Kill count ??gold / item rewards | ?뵴 Planned |
| Zombie | Wave difficulty scaling | ?뵴 Planned |

## Data And Security

- MySQL persistence through `GameDbContext`
- Startup database creation uses `Database.EnsureCreated()`
- JWT Bearer authentication configured in `Program.cs`
- Controllers read authenticated identity from JWT claims, not request body fields

## Notes

- The project currently targets `net9.0`
- Supporting docs and skills are stored under `.codex/`