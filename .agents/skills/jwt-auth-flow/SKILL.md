---
name: jwt-auth-flow
description: JWT authentication workflow for this project. Use when modifying registration, login, token claims, authorization attributes, or authenticated user lookup.
---

# JWT Authentication Flow

## Current Endpoints

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`

## Current Services

- `DbAuthService`: registration and credential validation
- `JwtTokenService`: access-token generation

## Token Contract

The token currently includes:

- `sub`
- `unique_name`
- `ClaimTypes.Name`
- `ClaimTypes.Role`
- `"userId"`
- `"role"`

Controllers currently read:

- `User.FindFirst("userId")`
- `User.FindFirst("role")`
- `User.Identity?.Name`

If you rename or remove claims, update every consumer in controllers and any future services.

## Registration Rules

- Normalize username with `Trim()`.
- Reject duplicates case-insensitively.
- Hash passwords with `PasswordHasher<AppUser>`.
- Keep the duplicate-user path as `409 Conflict` unless a task explicitly changes API behavior.

## Login Rules

- Validate user existence and password hash.
- Return `401 Unauthorized` for invalid credentials.
- Keep JWT expiration based on `JwtOptions.ExpirationMinutes`.

## Authorization Rules

- Anonymous access only on register/login endpoints.
- Endpoints using player identity must fail fast if `userId` claim is missing.
- When adding new authenticated endpoints, read identity from claims, not from client-supplied body fields.

## Configuration

JWT settings are bound from configuration through `JwtOptions`. Keep secrets and issuer/audience values in config, not source code.
