---
name: dotnet-server-verify
description: Verification workflow for this .NET game server. Use when validating changes, choosing build/run checks, or smoke-testing API behavior in a repository without a dedicated test project.
---

# .NET Server Verification

## Choose Verification Scope

- Small C# change with no API contract impact: run `dotnet build`
- Endpoint or DI change: run `dotnet build`, then consider `dotnet run` smoke testing
- Config or auth change: run `dotnet build` and verify startup or endpoint behavior

## Commands

```powershell
dotnet build
dotnet build .\zombie_servival-3Dgame_Server\zombie_survival-3Dgame_Server.csproj
dotnet run --project .\zombie_servival-3Dgame_Server\zombie_survival-3Dgame_Server.csproj
```

## Smoke Test Guidance

- Use `zombie_servival-3Dgame_Server.http` for manual endpoint checks.
- For auth changes, verify register, login, and an authorized endpoint.
- For player-data changes, verify authoritative mutation behavior and read-back state.

## Reporting

- State exactly what was run.
- If you only built and did not run the app, say so.
- Report the number of discovered and passed tests, plus any behavior that still requires integration smoke testing.
