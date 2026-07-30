# Layer Patterns — Detailed Guide

## System Implementation Example (GachaSystem)

```csharp
public sealed class GachaSystem
{
    private readonly IGachaService _gachaService;

    public GachaSystem(IGachaService gachaService)
    {
        _gachaService = gachaService;
    }

    public async Task<GachaResultResponse> ExecuteAsync(
        GachaRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Server-side validation — never trust client values
        ValidateRequest(request);

        // 2. Delegate to business logic
        var result = await _gachaService.PullAsync(request.PlayerId, cancellationToken);

        // 3. Map to immutable Response
        return new GachaResultResponse
        {
            ItemId = result.ItemId,
            Rarity = result.Rarity,
            PityCount = result.PityCount
        };
    }

    private static void ValidateRequest(GachaRequest request)
    {
        if (request.PlayerId <= 0)
            throw new GameException("Invalid player ID.");
    }
}
```

---

## Service Implementation Pattern

```csharp
public interface IGachaService
{
    Task<GachaResultDto> PullAsync(long playerId, CancellationToken ct = default);
}

public sealed class GachaService : IGachaService
{
    private readonly IGachaRepository _repository;
    private readonly IRngProvider _rng;

    public GachaService(IGachaRepository repository, IRngProvider rng)
    {
        _repository = repository;
        _rng = rng;
    }

    public async Task<GachaResultDto> PullAsync(long playerId, CancellationToken ct = default)
    {
        var pool = await _repository.GetActivePoolAsync(ct);
        var item = SelectByWeight(pool, _rng.NextDouble());

        await _repository.RecordPullAsync(playerId, item.Id, ct);

        return new GachaResultDto
        {
            ItemId = item.Id,
            Rarity = item.Rarity,
            PityCount = await _repository.GetPityCountAsync(playerId, ct)
        };
    }

    private static PoolItem SelectByWeight(IReadOnlyList<PoolItem> pool, double roll)
    {
        // Server-side RNG — never use a seed from the client
        double cumulative = 0;
        foreach (var item in pool)
        {
            cumulative += item.Weight;
            if (roll <= cumulative) return item;
        }
        return pool[^1];
    }
}
```

---

## Exception Handling Pattern

```csharp
// Business exception — safe to expose to the client
public sealed class GameException : Exception
{
    public int StatusCode { get; }
    public GameException(string message, int statusCode = 400)
        : base(message) => StatusCode = statusCode;
}

// Usage
throw new GameException("Inventory is full.");
throw new GameException("Player not found.", 404);
```

**Rules:**
- Messages should be user-friendly (shown directly as client toasts/alerts)
- Do not include dynamic data (IDs, names) in the message
- Internal errors are logged separately on the server

---

## Logging Rules

```csharp
// ✅ English verb-led sentence, structured placeholder
_logger.LogInformation("Gacha pull completed for player {PlayerId}", playerId);
_logger.LogError("Failed to process combat hit for session {SessionId}", sessionId);

// ❌ Not allowed
_logger.LogError($"Error: {message}");            // string interpolation
_logger.LogError("Error occurred: " + message);   // string concatenation
```

---

## Dependency Registration (GameServer.cs)

```csharp
// When adding a new system
services.AddScoped<GachaSystem>();
services.AddScoped<IGachaService, GachaService>();
services.AddScoped<IGachaRepository, GachaRepository>();
```

---

## Forbidden Patterns

```csharp
// ❌ Direct System-to-System call
public class CombatSystem
{
    private readonly GachaSystem _gacha; // Forbidden — domain contamination
}

// ❌ Applying client value directly to server state
player.Level = request.Level; // Forbidden — server must calculate this

// ❌ async void
public async void HandlePacket() { } // Forbidden except event handlers
```
