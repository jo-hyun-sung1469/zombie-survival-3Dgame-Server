---
name: efcore-sqlite-persistence
description: EF Core and SQLite persistence guide for this repository. Use when editing entity configuration, DbContext mappings, relationships, indexes, or startup database behavior.
---

# EF Core + SQLite Persistence Guide

## Current Setup

- DbContext: `GameDbContext`
- Provider: SQLite
- Startup behavior: `Database.EnsureCreated()`

## Existing Tables And Relationships

- `AppUser`
  - unique index on `UserName`
- `PlayerSaveData`
  - unique index on `PlayerId`
- `PlayerWeaponState`
  - many-to-one with `PlayerSaveData`
  - cascade delete enabled

## Mapping Rules

- Keep entity constraints in `OnModelCreating`.
- Add indexes and max lengths there when introducing new persisted fields.
- Be careful with relationship changes because the project currently relies on startup creation, not migrations.

## Query Rules

- Use `AsNoTracking()` on read-only queries when entity tracking is unnecessary.
- Include related collections explicitly when the service depends on them.
- Keep case-insensitive username checks consistent with current auth behavior.

## Change Strategy

When adding a field:

1. Update the model
2. Update `OnModelCreating` constraints if needed
3. Update DTOs and service mappings
4. Consider impact on the existing SQLite file and `EnsureCreated()` workflow

If a change requires formal schema evolution, call that out clearly instead of silently assuming migrations exist.
