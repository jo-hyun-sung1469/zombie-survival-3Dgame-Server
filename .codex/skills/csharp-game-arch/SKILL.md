---
name: csharp-game-arch
description: >
  Guides the layer structure, service patterns, DTO conventions, and configuration strategy
  for this C# .NET zombie survival game server.
  Use whenever adding a new system, implementing a service layer, designing DTOs,
  or planning dependency structure.
---

## Layer Structure

```text
Client Request
    ->
Controller
    ->
Service
    ->
DbContext / Options
```

Dependencies stay one-directional. Controllers should not contain business rules, and services should not depend on controllers.

## Service Pattern

Typical service signatures in this repository:

```csharp
public Task<TResponse> GetSomethingAsync(..., CancellationToken cancellationToken);
public Task<TResponse> SaveSomethingAsync(..., CancellationToken cancellationToken);
```

Principles:

- Keep services stateless. Persistent state belongs in the database or configuration.
- Keep server-authoritative decisions on the server. Never trust client-provided values directly.
- Put cross-domain shared lookups behind small interfaces when reuse appears, such as `IPlayerSaveDataStore`.

## DTO Rules

| Suffix | Purpose | Mutability |
|--------|---------|------------|
| `*Request` | Client input | Mutable or init-only |
| `*Response` | Server output | Prefer `init` only |
| `*Option` | Config-bound definition | Init-only |

```csharp
public sealed class FirearmStatsResponse
{
    public required string Name { get; init; }
    public required int Damage { get; init; }
}
```

Avoid public mutable DTO state unless model binding requires it.

## Enum And Constants Rules

- Use enums only when the value set is stable and code-owned.
- Use configuration-backed option objects when balance data may change without code changes.
- Prefer named constants only for behavior that is truly static.

## Async Rules

- All async methods must have the `Async` suffix.
- Always propagate `CancellationToken` through all async layers.
- `async void` is forbidden except event handlers.

## Nullable Reference Types

Project has `<Nullable>enable</Nullable>`. Follow these rules:

- Express nullability in the type.
- Check for `null` before use.
- Avoid null-forgiving operator abuse.

## Configuration Strategy

- Balance data such as gacha odds, firearm stats, and other tunable catalogs belongs in `Options/` and configuration.
- Authentication, persistence, and service registration stay centralized in `Program.cs`.
- If a domain definition is reused across multiple services, keep one canonical option model instead of duplicating lists.

## Adding A New Domain Or Service

1. Create the domain folder.
2. Add request and response DTOs in `Contracts/{Domain}/`.
3. Implement the service with async methods and cancellation support.
4. Register the service in `Program.cs`.
5. If persistence is needed, add models and EF mappings in `GameDbContext`.
6. If the feature is balance-driven, add an `Options/` type and bind it in `Program.cs`.
