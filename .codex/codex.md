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
|-- Auth/               # Login, logout, registration
|-- Player/             # Character stats, in-game stat increases
|-- Inventory/          # Gold, weapons owned, weapon enhancement/stats
|-- Gacha/              # Roulette, probability, reward grant
|-- GameSession/        # Continue, restart, new game, wave state
|-- Reward/             # Kill count and reward calculation
|-- Zombie/             # Difficulty scaling, wave configuration
|-- Contracts/
|   |-- Auth/
|   |-- Player/
|   |-- Inventory/
|   |-- Gacha/
|   |-- GameSession/
|   |-- Reward/
|   `-- Zombie/
|-- Data/               # EF Core DbContext
|-- Options/            # Configuration binding classes
|-- Program.cs
|-- appsettings.json
`-- zombie_servival-3Dgame_Server.http
```

## Architecture

- Modular monolith: each domain is self-contained inside the single project
- Preferred flow: `Controller -> Service -> DbContext`
- Controllers own HTTP concerns only
- Services own business logic and persistence orchestration
- DTOs live under `Contracts/{Domain}/`
- Domain models live under `{Domain}/Models/`

## Current Domains

| Domain | Endpoints | Status |
|--------|-----------|--------|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` | 완료 |
| Inventory | `POST /api/player-data/save`, `GET /api/player-data/me` | 완료 |
| Player | Character stats, in-game stat upgrades | 계획됨 |
| Gacha | Weapon roulette, probability, pity system | 계획됨 |
| GameSession | Continue / restart / new game, wave state | 계획됨 |
| Reward | Kill count, gold rewards, item rewards | 계획됨 |
| Zombie | Wave difficulty scaling | 계획됨 |

## Data And Security

- MySQL persistence through `GameDbContext`
- Startup database creation uses `Database.EnsureCreated()`
- JWT Bearer authentication configured in `Program.cs`
- Controllers read authenticated identity from JWT claims, not request body fields

## Harness Decision Guardrails

- Before changing code, inspect the relevant files and current git state first.
- Before making a sudden or unplanned code change, or introducing a new implementation direction, do not decide on behalf of the developer; present exactly three meaningful implementation options, mark one as `(Recommended)`, and wait for the developer's choice.
- If it is unclear whether a code change is expected, treat it as requiring developer choice instead of making a judgment call.
- Ask for user choice only for decisions that materially change the outcome, such as implementation direction, risky security behavior, commit scope, or commit message.
- When user input is needed, present exactly three meaningful options and mark one as `(Recommended)`.
- Limit decision conversations to 25 turns for important decisions and 15 turns for minor decisions.
- If the turn limit is reached, summarize the options, choose the recommended safe default, and continue unless user input is strictly required.
- Do not ask about facts that can be discovered from the repository or tooling.
- Keep unrelated worktree changes out of commits unless the user explicitly chooses to include them.
- When committing, split changes into logical commits instead of putting all current changes into one commit, and make each commit message describe its own change.
- Keep a quick-view change summary in `.codex/change-summaries/CHANGE_SUMMARY.md` when harness, workflow, or multi-file implementation changes are made.
- The summary file should list the date, purpose, changed areas, verification, and any remaining user decisions.
- Write change summary content in Korean.

## Notes

- The project currently targets `net9.0`
- Supporting docs and skills are stored under `.codex/`
