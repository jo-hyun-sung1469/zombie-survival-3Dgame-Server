# Coding Conventions

## DTO Suffix Convention

| Suffix | Purpose | Mutability |
|--------|---------|------------|
| `*Request` | Client input | Mutable |
| `*Response` | Server output | Immutable (`init` only) |
| `*Dto` | Inter-layer transfer | Immutable (`init` only) |

## Coding Style

| Rule | Convention |
|------|------------|
| Public types | `PascalCase` |
| Async methods | `Async` suffix |
| DTOs | `init` only |
| Enums | `UPPER_CASE` |
| Nullable refs | Enabled — no `!` abuse |
| Dependencies | Minimize external libs |
| Comments | Only where logic is not obvious |

## Domain Naming

Each domain folder owns its controller, service, and models:

```
{Domain}/
├── {Domain}Controller.cs
├── {Domain}Service.cs
└── Models/
```

DTOs go in `Contracts/{Domain}/`.

## Logging

Log the following in all domains:

| Domain | What to log |
|--------|------------|
| Auth | Login attempts, registration, token issuance |
| Inventory | Save events, weapon state changes |
| Gacha | Every pull result with rarity and item ID |
| Reward | Kill count milestones, reward granted |
| GameSession | Session start, resume, restart, wave reached |
| Zombie | Wave start, difficulty tier applied |

Use structured logging with named placeholders — no string interpolation:

```csharp
// ✅
_logger.LogInformation("Gacha pull completed for player {PlayerId}, item {ItemId}", playerId, itemId);

// ❌
_logger.LogInformation($"Gacha pull: {playerId} got {itemId}");
```