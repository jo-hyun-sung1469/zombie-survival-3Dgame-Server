# Scope Selection Guide

## Scope Table

| Scope         | When to use                                                        | Example |
|---------------|--------------------------------------------------------------------|---------|
| `combat`      | Hit detection, damage calculation, hitboxes, skill effects         | `fix(combat): correct melee attack range detection` |
| `gacha`       | Probability tables, RNG, reward pools, pity system                 | `add(gacha): reset pity counter on guaranteed pull` |
| `inventory`   | Item CRUD, equipment slots, stackable resource management          | `update(inventory): enforce max stack size limit` |
| `progression` | Level-up, XP calculation, content unlocks                          | `add(progression): define XP threshold table per level` |
| `matchmaking` | Match queue, room assignment, session creation                     | `fix(matchmaking): prevent duplicate queue entry` |
| `networking`  | Packet parsing, session management, rate limiting                  | `update(networking): disconnect on malformed packet` |
| `security`    | Anti-cheat, server-side validation, input integrity                | `add(security): detect movement speed threshold violation` |
| `models`      | DTO, entity, or enum definition changes                            | `update(models): add health field to ZombieDto` |
| `services`    | Business logic layer changes not tied to a single domain           | `refactor(services): extract common reward grant service` |
| `global`      | Changes spanning 3+ domains, shared utilities, global config       | `refactor(global): unify CancellationToken propagation` |
| `ci/cd`       | Build scripts, deployment pipelines                                | `update(ci/cd): add dotnet build workflow` |
| `docs`        | Documentation-only changes (no code changes)                       | `docs(docs): update gacha probability table in ARCHITECTURE.md` |

---

## Decision Tree

```
Changes concentrated in a single Systems/ subfolder?
  → Use that domain scope (combat, gacha, inventory, ...)

Only Models/ or Services/ files changed?
  → Use models / services
  Exception: if only a specific domain's DTO is modified, that domain scope is also fine

Changes span 3+ domains?
  → Use global

Only build/deploy/CI files?
  → Use ci/cd

Only documentation files?
  → Use docs
```

---

## Examples

```
add(gacha): implement weighted random selection by rarity
fix(combat): resolve missing hit detection on zombie
update(inventory): enforce weapon slot count limit
refactor(progression): extract XP calculation into separate method
add(networking): apply per-client packet rate limiting
fix(security): adjust speed hack detection threshold
update(models): add health field to ZombieDto
refactor(global): unify CancellationToken propagation pattern
docs(docs): add system registration steps to ARCHITECTURE.md
```
