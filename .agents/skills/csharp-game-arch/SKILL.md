---
name: csharp-game-arch
description: >
  Guides the layer structure, system patterns, transaction strategy, and exception handling
  for a C# .NET zombie survival game server.
  Use whenever adding a new system, implementing a service layer, designing DTOs,
  or planning dependency structure.
  Triggers on: "add a system", "create a service", "make a DTO", "how should the layer structure work",
  or any architecture-related task.
---

## Layer Structure

```
Client Request
    ↓
Networking  (packet validation, session check)
    ↓
System      (domain logic entry point)
    ↓
Service     (business logic)
    ↓
Models      (DTO, Entity, Enum)
```

Dependencies are one-directional only. A System must not directly call another System.

---

## System Pattern

All systems follow this interface pattern:

```csharp
// Standard entry point signature
public Task<TResponse> ExecuteAsync(
    TRequest request,
    CancellationToken cancellationToken = default);
```

**Principles:**
- Keep systems stateless (state lives in external storage)
- Server-authoritative decisions — never trust client-provided values directly
- Input validation is pre-processed in the Networking layer before reaching a System

**System Responsibilities:**

| System | Responsibility |
|--------|----------------|
| `CombatSystem` | Hit detection, damage, hitboxes |
| `GachaSystem` | Probability calculation, RNG, reward decision |
| `InventorySystem` | Item CRUD, equipment, stacking |
| `ProgressionSystem` | Levels, XP, content unlocks |
| `MatchmakingSystem` | Session assignment, queue management |

For detailed patterns, see `${CLAUDE_SKILL_DIR}/references/layer-patterns.md`.

---

## DTO Rules

| Suffix | Purpose | Mutability |
|--------|---------|------------|
| `*Request` | Client input | Mutable |
| `*Response` | Server output | Immutable (`init` only) |
| `*Dto` | Inter-layer transfer | Immutable (`init` only) |

```csharp
// ✅ Correct — init only
public record GachaResultDto
{
    public required string ItemId { get; init; }
    public required Rarity Rarity { get; init; }
}

// ❌ Wrong — setter allowed
public class GachaResultDto
{
    public string ItemId { get; set; }
}
```

---

## Enum Rules

```csharp
// ✅ UPPER_CASE
public enum Rarity
{
    COMMON,
    RARE,
    EPIC
}

// ❌ PascalCase (not allowed)
public enum Rarity { Common, Rare, Epic }
```

---

## Async Rules

- All async methods must have the `Async` suffix
- Always propagate `CancellationToken` through all layers
- `async void` is forbidden — except event handlers

```csharp
// ✅ Correct
public async Task<GachaResultDto> PullAsync(
    GachaRequest request,
    CancellationToken cancellationToken = default)

// ❌ Wrong — missing Async suffix and CancellationToken
public async Task<GachaResultDto> Pull(GachaRequest request)
```

---

## Nullable Reference Types

Project has `<Nullable>enable</Nullable>`. Follow these rules:

```csharp
// ✅ Express nullability in the type
public string? FindItemName(string itemId) { ... }

// ✅ Check for null before use
if (item is null)
    throw new GameException("Item not found.");

// ❌ Avoid null-forgiving operator abuse
var name = item!.Name;
```

---

## Adding a New System

1. Create `Systems/<SystemName>/` folder
2. Define Request / Response / Dto in `Models/<Domain>/`
3. Implement system logic following the `ExecuteAsync` pattern
4. Register in `GameServer.cs`
5. Verify server-authoritative principle compliance
