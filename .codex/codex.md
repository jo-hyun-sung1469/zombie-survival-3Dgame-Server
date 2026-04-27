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
├── Auth/               # Login, logout, registration
├── Player/             # Character stats, in-game stat increases
├── Inventory/          # Gold, weapons owned, weapon enhancement/stats
├── Gacha/              # Roulette, probability, reward grant
├── GameSession/        # Continue, restart, new game, wave state
├── Reward/             # Kill count → reward calculation
├── Zombie/             # Difficulty scaling, wave configuration
├── Contracts/
│   ├── Auth/
│   ├── Player/
│   ├── Inventory/
│   ├── Gacha/
│   ├── GameSession/
│   ├── Reward/
│   └── Zombie/
├── Data/               # EF Core DbContext
├── Options/            # Configuration binding classes
├── Program.cs
├── appsettings.json
└── zombie_servival-3Dgame_Server.http
```

## Architecture

- Modular monolith — each domain is self-contained inside the single project
- Preferred flow: `Controller -> Service -> DbContext`
- Controllers own HTTP concerns only
- Services own business logic and persistence orchestration
- DTOs live under `Contracts/{Domain}/`
- Domain models live under `{Domain}/Models/`

## Current Domains

| Domain | Endpoints | Status |
|--------|-----------|--------|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` | ✅ Complete |
| Inventory | `POST /api/player-data/save`, `GET /api/player-data/me` | ✅ Complete |
| Player | Character stats, in-game stat upgrades | 🔲 Planned |
| Gacha | Weapon roulette, probability, pity system | 🔲 Planned |
| GameSession | Continue / restart / new game, wave state | 🔲 Planned |
| Reward | Kill count → gold / item rewards | 🔲 Planned |
| Zombie | Wave difficulty scaling | 🔲 Planned |

## Data And Security

- SQLite persistence through `GameDbContext`
- Startup database creation uses `Database.EnsureCreated()`
- JWT Bearer authentication configured in `Program.cs`
- Controllers read authenticated identity from JWT claims, not request body fields

## Notes

- The project currently targets `net9.0`
- Supporting docs and skills are stored under `.codex/`