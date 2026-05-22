---
name: player-save-flow
description: Player save-data behavior for this project. Use when changing player save endpoints, save DTOs, weapon ownership state, or persistence mapping between requests and entities.
---

# Player Save Flow

## Current Endpoints

- `POST /api/player-data/save`
- `GET /api/player-data/me`

Both endpoints require authorization and identify the player from the JWT `userId` claim.

## Request And Response Model

- Request DTO: `SavePlayerDataRequest`
- Response DTO: `PlayerSaveResponse`
- Core fields today:
  - `Gold`
  - `WeaponStates`
  - `UpdatedAtUtc`

## Persistence Model

- `PlayerSaveData` has one logical row per `PlayerId`
- `PlayerWeaponState` stores weapon ownership as child rows
- `PlayerId` is unique

## Save Semantics

Current save behavior is key-based upsert:

1. Load the player's save row with `WeaponStates`
2. Create the root save row if missing
3. Update scalar fields such as `Gold`
4. Update existing weapon rows by weapon name
5. Add newly requested weapon rows
6. Remove rows omitted from the incoming dictionary

Preserve this behavior unless the task explicitly asks for patch semantics.

## Mapping Rules

- API request uses `Dictionary<string, bool>`
- Persistence uses a list of `PlayerWeaponState`
- API response sorts weapon names case-insensitively before building the dictionary

When changing this flow, keep request/response shape, persistence shape, and sorting behavior aligned.

## Validation Rules

- `Gold` is non-negative
- `WeaponStates` is required
- Weapon names must exist in the configured firearm catalog
- Do not trust a client-supplied player identifier when the authenticated claim already provides it
