# Zombie Survival Game Server

**한국어로 답변해주시길 바랍니다**

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
|-- Auth/               # Login, logout, registration
|-- Player/             # Character stats, in-game stat increases
|-- Inventory/          # Gold, weapons owned, weapon enhancement/stats
|-- Gacha/              # Roulette, probability, reward grant
|-- GameSession/        # Continue, restart, new game, wave state
|-- Reward/             # Kill count and reward calculation
|-- Zombie/             # Difficulty scaling, wave configuration
|-- Contracts/          # Domain request/response DTOs
|-- Data/               # EF Core DbContext
|-- Options/            # Configuration binding types
`-- Program.cs          # DI, auth, EF Core, OpenAPI pipeline
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

- Modular monolith: one domain per folder, all inside a single project.
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
- Never trust a client-supplied player identifier; always read from JWT claims.

## Persistence Rules

- MySQL via `GameDbContext`.
- `Database.EnsureCreated()` at startup; no migrations unless intentionally adopted.
- `PlayerSaveData` owns `PlayerWeaponState` through cascade delete.
- New entities go in the owning domain's `Models/` folder and are registered in `GameDbContext`.

## Domain Status

| Domain | Status |
|--------|--------|
| Auth | 완료 |
| Inventory | 완료 |
| Player | 계획됨 |
| Gacha | 계획됨 |
| GameSession | 계획됨 |
| Reward | 계획됨 |
| Zombie | 계획됨 |

## Practical Notes

- No separate test project. Verification is `dotnet build` + endpoint smoke tests.
- Avoid hardcoding secrets; JWT settings belong in configuration.
- Minimal comments only where logic is not obvious.

## Harness Decision Guardrails

- Before changing code, inspect the relevant files and current git state first.
- Ask for user choice only for decisions that materially change the outcome, such as implementation direction, risky security behavior, commit scope, or commit message.
- When user input is needed, present exactly three meaningful options and mark one as `(Recommended)`.
- Limit decision conversations to 25 turns for important decisions and 15 turns for non-important decisions.
- If the turn limit is reached, summarize the options, choose the recommended safe default, and continue unless user input is strictly required.
- Do not ask about facts that can be discovered from the repository or tooling.
- Keep unrelated worktree changes out of commits unless the user explicitly chooses to include them.
- Keep a quick-view change summary in `.codex/change-summaries/CHANGE_SUMMARY.md` when harness, workflow, or multi-file implementation changes are made.
- The summary file should list the date, purpose, changed areas, verification, and any remaining user decisions.
- Write change summary content in Korean.

## Custom Skills

Skills in `.codex/skills/`:

- `aspnet-api-arch`: domain structure, controller/service rules
- `jwt-auth-flow`: claims, registration, login rules
- `player-save-flow`: save/load semantics, mapping rules
- `efcore-mysql-persistence`: entity config, query rules
- `dotnet-server-verify`: build and smoke test workflow
