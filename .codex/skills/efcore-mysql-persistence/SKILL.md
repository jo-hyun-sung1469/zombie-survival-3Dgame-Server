---
name: efcore-mysql-persistence
description: EF Core and MySQL persistence guide for this repository. Use when editing entity configuration, DbContext mappings, relationships, indexes, or startup database behavior.
---

# EF Core + MySQL Persistence Guide

## Current Setup

- DbContext: `GameDbContext`
- Provider: MySQL
- Startup behavior: `Database.MigrateAsync()`

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
- Add a migration for every intentional schema change and keep the model snapshot synchronized.

## Query Rules

- Use `AsNoTracking()` on read-only queries when entity tracking is unnecessary.
- Include related collections explicitly when the service depends on them.
- Keep case-insensitive username checks consistent with current auth behavior.

## Change Strategy

When adding a field:

1. Update the model
2. Update `OnModelCreating` constraints if needed
3. Update DTOs and service mappings
4. Add and review an EF Core migration for the MySQL schema change

Run `dotnet ef migrations has-pending-model-changes` so model changes cannot bypass migration review.
