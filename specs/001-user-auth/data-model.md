# Data Model: User Authentication

**Feature**: `001-user-auth` | **Date**: 2026-10-12 (rev. d) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

All timestamps are `DateTimeOffset` in UTC, taken from an injected `TimeProvider`. All ids are
`Guid`. Durations and limits come from configuration (FR-031); the defaults are listed in
[Configuration](#configuration).

Data lives in three places:

| Store | What |
|---|---|
| SQL Server | accounts (Identity), external logins, consents, known devices, e-mail tokens, audit encryption keys, Data Protection key ring |
| Redis | sessions and refresh tokens (R4) |
| Audit stream → Azure Monitor Log Analytics (sink built later) | security audit events, personal fields encrypted per user (R14) |

## Overview

```text
SQL Server
AspNetUsers (ApplicationUser, Infrastructure)
 ├── 1─* AspNetUserLogins        (Identity built-in: external logins)
 ├── 1─* UserConsent             (Domain/Account)
 ├── 1─* KnownDevice             (Domain/Account)
 ├── 1─* EmailToken              (Domain/Account)
 └── 1─0..1 AuditEncryptionKey   (Infrastructure/Audit, cascade delete = crypto-shredding)
DataProtectionKeys               (Infrastructure, key ring)

Redis
session:{sessionId}        hash   TTL = inactivity timeout
rt:{tokenHash}             hash   TTL = inactivity timeout
user-sessions:{userId}     set    TTL = inactivity timeout (refreshed on each new/renewed session)

Audit stream
AuditRecord  (UserId plain, IP + device type encrypted with the user's AuditEncryptionKey)
```

Domain entities hold `Guid UserId` only, with no navigation to `ApplicationUser` (Domain cannot
reference Infrastructure). Foreign keys to `AspNetUsers` with cascade delete are set in the EF
configurations in `Infrastructure/Persistence/Configurations/`.

---

## SQL Server

### ApplicationUser (existing, extended)

`Liftingo.Infrastructure/Identity/ApplicationUser.cs`, table `AspNetUsers`.

| Field | Type | Source | Notes |
|---|---|---|---|
| `Id` | `Guid` | Identity | |
| `Email`, `NormalizedEmail` | `string` | Identity | `NormalizedEmail` gets a **unique filtered index** (`WHERE NormalizedEmail IS NOT NULL`), FR-005 |
| `UserName`, `NormalizedUserName` | `string` | Identity | set to the e-mail |
| `EmailConfirmed` | `bool` | Identity | verification status (FR-019) |
| `PasswordHash` | `string?` | Identity | `null` for provider-only accounts |
| `AccessFailedCount` | `int` | Identity | failed-attempt counter |
| `LockoutEnd` | `DateTimeOffset?` | Identity | lockout end time |
| `LockoutEnabled` | `bool` | Identity | always `true` |
| `SecurityStamp` | `string` | Identity | updated on password reset / removal |
| **`CreatedAt`** | `DateTimeOffset` | **new** | registration date; needed by the 7-day unverified-account job (out of scope) |

Rules:

- E-mail comparison is case-insensitive through `NormalizedEmail` (FR-005).
- Accounts created from a provider-supplied e-mail have `EmailConfirmed = true` (FR-024).
- Linking a provider to an **unverified** account (takeover, FR-022 / FR-023a) sets
  `EmailConfirmed = true` and `PasswordHash = null`, in the same commit that supersedes the old
  consents and records the new ones (R10). It never happens before the owner has accepted the
  consents.
- A password reset of an **unverified** account (FR-035) sets `EmailConfirmed = true` and the new
  `PasswordHash`, under the same consent rule and in the same commit (R7).
- Every completed reset clears `AccessFailedCount` and `LockoutEnd`.

Exposed to Application only through `IIdentityService` as a read model:

```text
AccountSummary(Guid Id, string Email, bool EmailVerified, bool HasPassword,
               IReadOnlyList<string> Providers, DateTimeOffset CreatedAt)
```

### AspNetUserLogins (existing, Identity)

External login link: `LoginProvider` (`Google` | `Facebook` | `X`), `ProviderKey` (provider's
user id), `ProviderDisplayName`, `UserId`. Primary key `(LoginProvider, ProviderKey)`. No change.

### UserConsent

`Liftingo.Domain/Account/UserConsent.cs`, table `UserConsents`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `UserId` | `Guid` | FK, cascade |
| `Type` | `ConsentType` | `TermsAndAge`, `HealthData`, `Marketing` (stored as string) |
| `Granted` | `bool` | |
| `RecordedAt` | `DateTimeOffset` | |
| `DocumentVersion` | `string(32)` | `Legal:DocumentsVersion` at the time (Terms + Privacy Policy shown together) |
| **`SupersededAt`** | `DateTimeOffset?` | set when a takeover of an unverified account replaces the consents (FR-022, FR-023a, FR-035) |

Rules:

- Every registration (e-mail or provider) writes exactly three rows. `TermsAndAge` and
  `HealthData` must be `Granted = true`, or the account is not created (FR-003, SC-007). An
  unticked marketing box is stored as `Granted = false` (FR-004).
- An account takeover (R10, R17, or a reset of an unverified account, R7) sets `SupersededAt` on every row where it is `null` and writes
  three new rows, under the same rule, in one commit.
- Current consents = rows with `SupersededAt IS NULL`. Rows are never deleted or edited otherwise
  (domain method `Supersede(now)` only). Withdrawal belongs to the privacy-centre feature.
- Index: `(UserId, Type, RecordedAt)`.

### KnownDevice

`Liftingo.Domain/Account/KnownDevice.cs`, table `KnownDevices`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `UserId` | `Guid` | FK, cascade |
| `DeviceId` | `Guid` | |
| `DeviceType` | `string(100)` | last seen description, e.g. "Chrome, Windows, komputer" |
| `FirstSeenAt` | `DateTimeOffset` | |
| `LastSeenAt` | `DateTimeOffset` | updated on each successful sign-in |

Rules:

- Unique `(UserId, DeviceId)`.
- Inserted on the device used at registration **without** a new-device e-mail.
- Inserted on the first device of every new account (e-mail registration or provider `complete`)
  **without** a new-device e-mail.
- Inserted on a successful sign-in from an unknown device **with** a new-device e-mail (FR-026).
  Sign-in = password login, provider sign-in into an existing account, confirmed provider link
  (R17). A known device gets `LastSeenAt` updated instead.
- Never inserted for failed attempts or refreshes.
- All rows of the user are deleted by "sign out of all devices" (FR-015a) and by a completed
  password reset (FR-035).
- Retention: rows with `LastSeenAt` older than `KnownDevices:Retention` (12 months) are deleted by
  the retention work's daily job (out of scope). Index: `(LastSeenAt)`.

### EmailToken

`Liftingo.Domain/Account/EmailToken.cs`, table `EmailTokens`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | |
| `UserId` | `Guid` | FK, cascade |
| `Purpose` | `EmailTokenPurpose` | `EmailVerification`, `PasswordReset`, `ProviderLinkConfirmation` |
| `TokenHash` | `binary(32)` | SHA-256, **unique index** |
| `CreatedAt` | `DateTimeOffset` | used for the per-hour limit |
| `ExpiresAt` | `DateTimeOffset` | +24 h (verification) / +1 h (reset) / +24 h (link confirmation) |
| `ConsumedAt` | `DateTimeOffset?` | |
| `SupersededAt` | `DateTimeOffset?` | |
| `LoginProvider` | `string(32)?` | `ProviderLinkConfirmation` only: `Google` / `Facebook` / `X` |
| `ProviderKey` | `string(256)?` | `ProviderLinkConfirmation` only: provider's user id |

Check constraint: `LoginProvider` and `ProviderKey` are both non-null exactly when
`Purpose = 'ProviderLinkConfirmation'`. The factory method for that purpose requires both; the
others take neither.

Behaviour (domain methods, unit-tested):

- `IsUsable(now)` = not consumed, not superseded, `now < ExpiresAt`.
- `Consume(now)` / `Supersede(now)`. `Consume` throws `DomainException` if not usable (the handler
  checks first and returns a `Result` failure).
- Issuing a new token supersedes all usable tokens of the same user and purpose (FR-018, FR-033).
- Issuing is skipped when the user already has `EmailTokens:MaxPerAddressPerHour` tokens of that
  purpose with `CreatedAt > now - 1h` (FR-037).
- An unusable token (used, expired, superseded or unknown) gives one error per purpose:
  `Auth.VerificationLinkInvalid` / `Auth.ResetLinkInvalid` / `Auth.ProviderLinkInvalid`.
  Exception: any existing verification token whose user is already verified answers "already
  verified" (FR-017, R7).
- `ProviderLinkConfirmation` and `PasswordReset` tokens are **not** consumed when the request is
  refused for missing consents (`Auth.ConsentRequired`), so the user can resubmit (R7, R17).
- A takeover (R10, R17, reset of an unverified account) supersedes the user's other usable tokens.
- `UnusableSince(now)` = the earliest of `ConsumedAt`, `SupersededAt` and `ExpiresAt` that has
  passed. Retention (FR-038): deleted 24 h after it (`EmailTokens:RetentionAfterUnusable`) by the
  retention work's daily job (out of scope).

Indexes: `(UserId, Purpose, CreatedAt)`; `(ExpiresAt)` for the retention job.

State transitions:

```text
Issued ──Consume──▶ Consumed   (terminal)
Issued ──Supersede──▶ Superseded (terminal)
Issued ──time passes ExpiresAt──▶ Expired (derived)
```

### AuditEncryptionKey

`Liftingo.Infrastructure/Audit/AuditEncryptionKey.cs`, table `AuditEncryptionKeys`. An encryption
detail, so it lives in Infrastructure: a `DbSet` on `ApplicationDbContext`, not on
`IApplicationDbContext`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | key id, written into every `AuditRecord` |
| `UserId` | `Guid` | FK to `AspNetUsers`, **cascade delete** |
| `ProtectedKey` | `varbinary(max)` | 256-bit AES key wrapped with Data Protection (purpose `Liftingo.AuditKeys`) |
| `CreatedAt` | `DateTimeOffset` | |

Rules:

- Unique index on `UserId`: at most one key per user.
- No shared key: events without a user carry no IP or device type, so nothing needs encrypting
  (FR-014, R14).
- Created lazily on the user's first audit event.
- Deleting the account deletes the row through the cascade, which makes that user's audit records
  unreadable (crypto-shredding, US-18). The key is never logged or exported.

### DataProtectionKeys

Table created by `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`
(`PersistKeysToDbContext<ApplicationDbContext>`). It keeps the key ring durable, so the wrapped
audit keys and the external-login cookie survive restarts (R14).

---

## Redis: sessions and refresh tokens

Accessed only through `ISessionStore` (`Infrastructure/Sessions/RedisSessionStore.cs`). Key prefix
`liftingo:` is added by the store so one Redis instance can be shared later.

### `session:{sessionId}` (hash)

One sign-in chain on one device. It is the unit that is logged out and the unit of reuse revocation.
`sessionId` is also the JWT `sid` claim.

| Field | Notes |
|---|---|
| `userId` | |
| `deviceId` | client device id (R6) |
| `deviceType` | e.g. "Chrome, Windows, komputer" |
| `createdAt` | sign-in time (ISO 8601) |
| `absoluteExpiresAt` | `createdAt` + `Sessions:AbsoluteLifetime` (90 days), never changed (FR-010a) |
| `lastRenewedAt` | |
| `currentTokenHash` | Base64Url SHA-256 of the current refresh token |

TTL: `min(Sessions:InactivityTimeout, absoluteExpiresAt - now)`, recomputed on every renewal
(FR-010, FR-010a). Key present = session active; revoking = deleting the key.

### `rt:{tokenHash}` (hash)

| Field | Notes |
|---|---|
| `sessionId` | |
| `state` | `active` or `used` |

TTL: same as the session, set when written. Only the newest token of a session is `active`.
Revoked and expired sessions leave nothing behind: deleted keys are gone, and `used` tokens
expire with their TTL (FR-038).

### `user-sessions:{userId}` (set)

Session ids of the user. Used by "sign out of all devices", password reset and pre-hijack linking.
Stale ids (expired sessions) are skipped and removed when the set is read. TTL is extended to the
inactivity timeout whenever a session is added or renewed.

### Operations (`ISessionStore`)

| Operation | Effect | Atomic via |
|---|---|---|
| `CreateAsync(userId, device)` → `(sessionId, refreshToken)` | write `session`, `rt` (active), add to `user-sessions` | transaction (`MULTI`) |
| `RotateAsync(tokenHash)` → `Rotated(session, newToken)` / `Reused(session)` / `Invalid` | see R4 steps 1–4; `Reused` deletes the session | Lua script |
| `RevokeAsync(sessionId)` | delete `session` + current `rt`, remove from set | Lua script |
| `RevokeByTokenAsync(tokenHash)` (logout) | look up session by token, then `RevokeAsync` | Lua script |
| `RevokeAllAsync(userId)` → revoked count | revoke every id in the set, delete the set | Lua script |
| `IsActiveAsync(sessionId)` | `EXISTS session:{id}` | — |

Every operation turns Redis connection and timeout errors into `SessionStoreUnavailableException`,
which `Api` maps to `503 Service.Unavailable` (FR-015c, R18).

State transitions:

```text
Active ──Rotate──▶ Active (TTL recomputed, capped at absoluteExpiresAt; new current token, old token = used)
Active ──Revoke / RevokeAll / reuse detected──▶ gone (key deleted)
Active ──TTL elapses──▶ gone (expired, no write)
```

Revocation reasons (logout, logout-all, password reset, token reuse, account linked) are not
stored in Redis; they go to the audit stream.

---

## Audit stream

### SecurityAuditEvent (Application input)

`Liftingo.Application/Common/Security/SecurityAuditEvent.cs`, a positional record passed to
`ISecurityAuditLog.WriteAsync`. Enums `SecurityEventType` and `SecurityEventOutcome` live in
`Liftingo.Domain/Account/`.

| Field | Type | Notes |
|---|---|---|
| `UserId` | `Guid?` | `null` when the e-mail is unknown |
| `Type` | `SecurityEventType` | `Login`, `ExternalLogin`, `Logout`, `LogoutAllDevices`, `PasswordResetCompleted`, `ProviderLinked`, `LockoutTriggered`, `RefreshTokenReuse` |
| `Outcome` | `SecurityEventOutcome` | `Succeeded`, `Failed`, `LockedOut` |
| `OccurredAt` | `DateTimeOffset` | |
| `IpAddress` | `string?` | personal data, encrypted before leaving the app; `null` when `UserId` is `null` |
| `DeviceType` | `string?` | personal data, encrypted before leaving the app; `null` when `UserId` is `null` |

Rules:

- Every login attempt writes one `Login`/`ExternalLogin` event (FR-014, SC-005). The attempt that
  trips the lockout also writes a `LockoutTriggered` event. A failed or cancelled provider sign-in
  writes `ExternalLogin` `Failed`, with the user id only when a linked account is known (FR-020,
  FR-022a).
- An event without a user holds only type, outcome and time (FR-014).
- Routine refreshes are not audited.
- No e-mail, password or token is ever part of an event.

### AuditRecord (what leaves the application)

Built by `EncryptingSecurityAuditLog`, handed to `IAuditSink`. This becomes one row of the Log
Analytics custom table once the Azure sink exists.

| Field | Notes |
|---|---|
| `EventId` | `Guid` |
| `UserId` | plain text, so KQL can filter by user (export, US-37) |
| `EventType`, `Outcome`, `OccurredAt` | plain text |
| `KeyId` | `AuditEncryptionKey.Id` used; empty for events without a user |
| `IpAddressCipher` | Base64 of nonce + ciphertext + tag (AES-GCM); empty for events without a user |
| `DeviceTypeCipher` | same |

Retention: 90 days, set on the Log Analytics table (`Audit:Retention` documents the value; the
later sink's setup applies it).

---

## Transient state

| State | Where | Lifetime | Purpose |
|---|---|---|---|
| Pending external sign-in | encrypted cookie `Liftingo.External` (auth scheme) | `ExternalLogin:PendingTimeout` (10 min) | provider name, provider key, e-mail (if any), display name, device id, until consent (FR-021). Used for new users and for takeovers of unverified accounts (R10); whether an account exists is looked up again on `complete`, not stored |
| Failed-login throttle for unknown e-mails | `IMemoryCache`, key = SHA-256 of normalised e-mail | lockout duration | equal lockout behaviour, no enumeration (R8) |
| Refresh token | `liftingo_rt` cookie on the client | the session's remaining TTL (≤ 30 days sliding, ≤ 90 days absolute) | |

## Configuration

All values bound to validated options classes (`ValidateOnStart`, same pattern as
`IdentitySecurityOptions`).

| Key | Default | Requirement |
|---|---|---|
| `ConnectionStrings:Redis` | `localhost:6379` (dev) | sessions |
| `Identity:Password:*` | 8 / digit / special (existing) | FR-002 |
| `Identity:Lockout:MaxFailedAccessAttempts` | 5 (existing) | FR-012 |
| `Identity:Lockout:Duration` | `00:15:00` (existing) | FR-012 |
| `Jwt:Issuer`, `Jwt:Audience` | `liftingo-api`, `liftingo-app` | |
| `Jwt:SigningKey` | user secrets / env only, ≥ 32 bytes | |
| `Jwt:AccessTokenLifetime` | `00:15:00` | FR-008 |
| `Sessions:InactivityTimeout` | `30.00:00:00` | FR-010 |
| `Sessions:AbsoluteLifetime` | `90.00:00:00` | FR-010a |
| `KnownDevices:Retention` | `365.00:00:00` | FR-026 (applied by the retention job) |
| `EmailTokens:RetentionAfterUnusable` | `1.00:00:00` | FR-038 (applied by the retention job) |
| `EmailTokens:VerificationLifetime` | `1.00:00:00` | FR-016 |
| `EmailTokens:PasswordResetLifetime` | `01:00:00` | FR-033 |
| `EmailTokens:ProviderLinkLifetime` | `1.00:00:00` | FR-023a |
| `EmailTokens:MaxPerAddressPerHour` | 3 (per purpose) | FR-037 |
| `ExternalLogin:PendingTimeout` | `00:10:00` | FR-021 |
| `Authentication:Google:ClientId/ClientSecret` | user secrets, optional | FR-020 |
| `Authentication:Facebook:AppId/AppSecret` | user secrets, optional | FR-020 |
| `Authentication:X:ClientId/ClientSecret` | user secrets, optional | FR-020 |
| `Audit:Retention` | `90.00:00:00` | FR-014 (applied by the Log Analytics table setup) |
| `Legal:DocumentsVersion` | e.g. `2026-10-01` | FR-004 |
| `App:FrontendBaseUrl` | `https://localhost:4200` (dev) | links in e-mails, redirects |
| `ForwardedHeaders:KnownProxies` | empty (dev) | correct audit IP behind App Service |

## Migration

One migration from `Liftingo.Infrastructure`, e.g. `AddUserAuthentication`: adds
`AspNetUsers.CreatedAt`, the unique filtered index on `NormalizedEmail`, and the tables
`UserConsents` (with `SupersededAt`), `KnownDevices` (with the `LastSeenAt` index), `EmailTokens`
(with the provider columns, their check constraint and the `ExpiresAt` index),
`AuditEncryptionKeys` (non-nullable `UserId`, unique) and `DataProtectionKeys`.
Redis needs no migration.
