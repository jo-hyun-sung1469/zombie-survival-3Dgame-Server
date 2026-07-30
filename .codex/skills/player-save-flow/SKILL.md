---
name: player-save-flow
description: Player save-data behavior for this project. Use when changing player save endpoints, save DTOs, weapon ownership state, or persistence mapping between requests and entities.
---

# Player Save Flow

## Current Endpoint

- `GET /api/player-data/me`

The endpoint requires authorization and identifies the player from the JWT `userId` claim.

## Request And Response Model

- Response DTO: `PlayerSaveResponse`
- Core response fields:
  - `Gold`
  - `WeaponStates`
  - `UpdatedAtUtc`

## Persistence Model

- `PlayerSaveData` has one logical row per `PlayerId`
- `PlayerWeaponState` stores weapon ownership as child rows
- `PlayerId` is unique

## Server Authority

- Do not accept gold, weapon ownership, weapon levels, or stat levels from a client save request.
- Gold changes only through server-owned reward and spending rules.
- Weapon ownership changes only through server-owned defaults, gacha, or another authoritative grant flow.
- Player state mutations must increment the save row concurrency version before persistence.

## Mapping Rules

- Persistence uses a list of `PlayerWeaponState`
- API response sorts weapon names case-insensitively before building the dictionary

When changing this flow, keep request/response shape, persistence shape, and sorting behavior aligned.

## Validation Rules

- Gold, costs, and owned states are calculated server-side
- Weapon names used by mutation endpoints must exist in the server firearm catalog
- Do not trust a client-supplied player identifier when the authenticated claim already provides it
