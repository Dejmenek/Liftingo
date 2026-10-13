# Implementation Plan: User Authentication (Register, Login, Logout, Password Reset)

**Branch**: `001-user-auth` (work happens on `feat/27-implement-user-authentication-register-login-logout-password-reset`) | **Date**: 2026-10-12 (rev. d) | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-user-auth/spec.md`

**Scope**: backend only. The Angular frontend will be specified and built in a later feature; what
the API expects from it is listed in [research R16](research.md#r16-client-expectations-frontend-is-a-later-feature).

**Revision b**: frontend removed from scope. Sessions moved to Redis. Security audit events moved
from a SQL table to an encrypted audit stream bound for Azure Monitor Log Analytics, with
crypto-shredding. Constitution amended to v1.1.0 to allow both.

**Revision c** (second clarification session): taking over an unverified account waits for the
owner's consents and supersedes the old ones (R10). A typed-in e-mail that already has an account
gets a link-confirmation e-mail and a new `ConfirmProviderLink` slice (R17). A password reset
revokes every session and starts none. Immediate access-token rejection after revocation (FR-015b)
and the equal lockout for unknown e-mails were already in the design (R3, R8). PRD US-24 now says
"all sessions" to match.

**Revision d** (completeness-checklist review): 90-day absolute session lifetime as a capped
Redis TTL (R4). Refresh cookie `SameSite=Lax` (R5). Logout-all and reset clear known devices
(R6). A reset of an unverified account verifies it and asks for the consents again (R7). Lockout
blocks provider sign-in; a linked provider returning another account's e-mail is refused (R8,
R9). Audit events without a user carry no IP or device, so the shared audit key is dropped
(R14). Existing rate limiter reused, HSTS, `503` for Redis outages, retention hooks and
performance targets (R18). PRD retention table gained known-device and e-mail-token rows.

## Summary

Accounts for Liftingo: e-mail/password registration with logged consents, login with lockout,
JWT access tokens with rotating refresh tokens in an HttpOnly cookie, logout of one or all devices,
e-mail verification, password reset, Google/Facebook/X sign-in with a consent interstitial, and a
security audit log. E-mails go through an `IEmailSender` port. Its only implementation in this
feature writes to the development console.

Technical approach, in short (details in [research.md](research.md)):

- ASP.NET Core Identity stays in Infrastructure behind a narrow `IIdentityService` port, because
  Application may not depend on `Microsoft.AspNetCore.Identity`. The user store does not
  auto-save, so each handler commits its SQL writes with one `SaveChangesAsync`.
- Consents, known devices and e-mail tokens are Domain entities in SQL Server, queried directly
  through `IApplicationDbContext`.
- Sessions and refresh tokens live in Redis behind `ISessionStore`. Rotation and reuse detection
  run as one Lua script. TTLs give the 30-day sliding inactivity window, capped at 90 days from
  sign-in. A Redis outage answers `503`. Every bearer request
  checks `EXISTS session:{sid}`, so logout and password reset take effect immediately without
  querying SQL.
- Audit events go through `ISecurityAuditLog`. IP and device type are encrypted with a per-user key
  stored in SQL (cascade-deleted with the account, which is crypto-shredding). The Log Analytics
  sink comes later; this feature ships a development console sink.
- Lockout for unknown e-mails is mirrored in memory, so responses never reveal whether an address
  is registered.
- External sign-in uses server-side redirect handlers and keeps provider data in a short-lived
  encrypted cookie until consent is given. Nothing is persisted before that. Taking over an
  unverified account uses the same consent step and replaces the old consents (R10).
- A typed-in e-mail that already has an account is linked only after a 24 h e-mail link is opened;
  the token carries the provider name and key (R17).
- A password reset is a takeover too when the e-mail was unverified: it verifies the address and
  asks for the consents again, through the same `AccountTakeover` helper (R7).
- The existing global rate limiter covers every endpoint; authentication moves in front of it so
  signed-in calls are limited per user (R18).

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: ASP.NET Core Minimal APIs, ASP.NET Core Identity (EF stores),
`Microsoft.AspNetCore.Authentication.JwtBearer` (already referenced), FluentValidation, Serilog,
Asp.Versioning (existing). **New**: `StackExchange.Redis`,
`Microsoft.AspNetCore.Authentication.Google`, `Microsoft.AspNetCore.Authentication.Facebook`,
`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (10.0.11), `AspNet.Security.OAuth.Twitter`
10.0.0, `Serilog.Enrichers.Sensitive` 2.1.0; tests: `Testcontainers.Redis`.

**Storage**: SQL Server via EF Core (LocalDB for dev, Testcontainers for tests), one migration.
Redis (Docker for dev, Testcontainers for tests, Azure Cache for Redis in prod) for sessions.
`IMemoryCache` for the unknown-e-mail login throttle. Audit stream → console sink in dev; Azure
Monitor Log Analytics sink later.

**Testing**: xUnit v3 + NSubstitute + Bogus (unit), Testcontainers SQL Server + Redis + Respawn +
`WebApplicationFactory` (integration), ArchUnitNET (architecture).

**Target Platform**: Azure App Service.

**Project Type**: Web service (backend monolith).

**Performance Goals**: SC-011 p95 under normal load: login and registration ≤ 1 s, refresh
≤ 300 ms (manual check, R18). SC-006 dev e-mails ≤ 5 s after the action. The per-request session
check is one Redis `EXISTS`; SQL is touched by sign-in, verification, reset and account flows, plus one read on refresh that
keeps the `email_verified` claim current (FR-019).

**Constraints**: refresh token never readable by scripts (FR-008a); no personal data in
application logs (FR-030, constitution III); audit personal fields encrypted per user
(constitution III); all windows and thresholds configurable (FR-031); SPA and API must be same-site
for the `SameSite=Lax` cookie (deployment constraint for the frontend feature); HTTPS only, with
App Service's minimum TLS version set to 1.2 at deployment (FR-039).

**Scale/Scope**: single App Service instance, thesis-scale user base. 17 endpoints, 5 new SQL
tables, 3 Redis key types.

No open NEEDS CLARIFICATION items. Library details marked *verify* in research
(`UserStore.AutoSaveChanges`, external handlers' log content) each have a fallback that keeps the
design valid. The X e-mail mapping was checked against the package source and needs explicit
configuration (R9). Lockout behaviour and the non-alphanumeric rule were checked against the
ASP.NET Core v10.0.1 source (R8, R13): lockout matches the spec as is; the password validator
needs a small override, and Identity's lockout clock ignores `TimeProvider`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Checked against
constitution **v1.1.0**.

| Principle | Check | Status |
|---|---|---|
| **I. Vertical slice on Clean Architecture** | One slice file per endpoint in `Application/Features/Account/`, named by use case. Identity, JWT, Redis sessions, e-mail and the audit stream are behind Application-defined ports (`IIdentityService`, `IAccessTokenIssuer`, `ISessionStore`, `IEmailSender`, `ISecurityAuditLog`) implemented in Infrastructure. Domain entities have no EF/framework types. Routes in `RouteConsts.Auth`. `Api` only wires DI, auth middleware and the version prefix for the cookie path. | ✅ Pass |
| **II. Explicit results, no hidden abstractions** | Handlers return `Result<T>`; lockout, invalid links and conflicts are `Error`s (new `ErrorType.TooManyRequests` → 429). SQL data is queried directly through `IApplicationDbContext`. `ISessionStore` and `IIdentityService` wrap non-EF stores or a framework, not EF aggregates, so they are not the banned per-aggregate repositories. No mapper or mediator. Shared helpers in `Features/Account/Shared/` are concrete classes used by several slices. The `AuthSession` response is the one response DTO kept there instead of in a slice file, because five slices return the same contract shape. DTOs are positional records; validators colocated. | ✅ Pass |
| **III. Privacy and compliance (non-negotiable)** | Consents explicit and logged with timestamp + document version for e-mail **and** provider sign-up, and asked again from the real owner after any takeover of an unverified account, by provider or by password reset (old rows kept, superseded). Audit events without a known user hold no IP or device type. Retention periods for known devices and e-mail tokens are defined (PRD NFR-40). Provider data not persisted before consent; a link-confirmation token holds only provider name and key. Application logs carry user ids only, enforced by a test, and a Serilog masking enricher masks e-mail and IP addresses in every log event as a safety net. Audit stream is separate from application logs; IP and device type are encrypted per user; the key is cascade-deleted with the account. Dev e-mail and audit console sinks are registered only in Development. Tokens stored as hashes in Redis and SQL. Limits and retention are config values. | ✅ Pass |
| **IV. Explainable AI boundary** | Not touched. `IsEmailVerified` is exposed so the AI feature can apply the unverified quota later. | ✅ N/A |
| **V. Offline-first, mobile-first PWA** | No UI in this feature. The API supports the offline rule: a network failure on refresh is distinguishable from a `401` (R16). | ✅ N/A |
| **VI. Test-backed delivery** | Unit tests for domain entities, validators, device classifier, audit encryption, options validators. Integration tests per slice against real SQL Server and Redis containers, including privacy-critical ones (consents, no enumeration, log privacy, crypto-shredding, pre-hijack). Architecture tests extended. Naming `[Method]_[Scenario]_[ExpectedResult]`. Playwright waits for the frontend feature. | ✅ Pass |
| **VII. Contract-driven frontend** | No frontend in scope. The OpenAPI document is produced for the later NSwag client; no Angular workspace exists to regenerate yet. | ✅ N/A |
| **VIII. Simplicity and scope** | FR-01/02/03 are P0. New dependencies each answer a current requirement: Redis (session store, v1.1.0 stack), provider handlers (FR-020), DataProtection EF store (durable keys for audit encryption and the external cookie). No Hangfire, templating engine, UA-parser package or throttle table. The Log Analytics sink, production e-mail, account deletion and retention jobs stay out. | ✅ Pass |
| **Tech stack** | Identity + Google/Facebook/X + JWT with refresh tokens, Redis for sessions, Log Analytics for the audit log, all listed in v1.1.0. `AspNet.Security.OAuth.Twitter` is a community package that implements the stack's "X external provider" item. | ✅ Pass |

**Gate result (pre-research)**: pass, no violations.

**Re-check after Phase 1 design**: pass. Notes:

1. `backend/AGENTS.md` was corrected in this revision (Google/Facebook/X providers, Redis, Log
   Analytics, Redis in Docker).
2. Nothing runs outside local development yet. The Log Analytics audit sink belongs to a later
   infrastructure feature, together with the Azure e-mail sender. That feature has to be done
   before any non-local deployment, because `DisabledAuditSink` drops audit events (NFR-17). It
   must also batch and retry, since a network send can fail after the request has committed.
   The retention jobs for known devices and e-mail tokens (FR-026, FR-038) must also exist before
   any non-local deployment.
3. Rev. c re-check: the new slice follows Principle I (one file, ports only); `AccountTakeover` is
   a concrete helper shared by two slices (`CompleteExternalLogin`, `ConfirmProviderLink`), not a
   repository; consent re-collection strengthens Principle III. No new dependency. Still pass.
4. Rev. d re-check: no new slice, no new dependency. `AccountTakeover` gains a third caller
   (`ResetPassword`). The `503` mapping uses an Application-level exception, so Application still
   does not reference StackExchange.Redis. Dropping IP/device from user-less audit events reduces
   personal data (Principle III). Still pass.

## Project Structure

### Documentation (this feature)

```text
specs/001-user-auth/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── auth-api.md      # Phase 1: HTTP contract
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root)

```text
backend/
├── AGENTS.md                                     # stack list corrected (done in this revision)
├── Directory.Packages.props                      # + StackExchange.Redis, Google, Facebook,
│                                                 #   AspNet.Security.OAuth.Twitter,
│                                                 #   DataProtection.EntityFrameworkCore, Testcontainers.Redis
├── docker-compose.yml                            # new: Redis for local development
├── src/
│   ├── Liftingo.Domain/
│   │   └── Account/
│   │       ├── UserConsent.cs, ConsentType.cs
│   │       ├── KnownDevice.cs
│   │       ├── EmailToken.cs, EmailTokenPurpose.cs
│   │       └── SecurityEventType.cs, SecurityEventOutcome.cs
│   ├── Liftingo.Application/
│   │   ├── Common/
│   │   │   ├── IUserContext.cs                   # + IsEmailVerified, SessionId
│   │   │   ├── Exceptions/SessionStoreUnavailableException.cs   # mapped to 503 in Api
│   │   │   ├── Persistence/IApplicationDbContext.cs   # + UserConsents, KnownDevices, EmailTokens
│   │   │   ├── Result/ErrorType.cs, ResultExtensions.cs, LockedOutError.cs   # 429 + retryAfter
│   │   │   ├── Routes/RouteConsts.cs             # + Auth
│   │   │   ├── Email/IEmailSender.cs, EmailMessage.cs
│   │   │   └── Security/
│   │   │       ├── IIdentityService.cs, AccountSummary.cs
│   │   │       ├── IAccessTokenIssuer.cs
│   │   │       ├── ISessionStore.cs, SessionDevice.cs, RotationResult.cs
│   │   │       ├── ISecurityAuditLog.cs, SecurityAuditEvent.cs
│   │   │       ├── OpaqueToken.cs                # random token + SHA-256
│   │   │       └── AuthenticationSchemes.cs
│   │   └── Features/Account/
│   │       ├── Register.cs, Login.cs, GetCurrentAccount.cs
│   │       ├── RefreshSession.cs, Logout.cs, LogoutAllDevices.cs
│   │       ├── VerifyEmail.cs, ResendVerificationEmail.cs
│   │       ├── ForgotPassword.cs, ResetPassword.cs
│   │       ├── GetExternalProviders.cs, StartExternalLogin.cs, HandleExternalLoginCallback.cs,
│   │       │   GetPendingExternalLogin.cs, CompleteExternalLogin.cs, CancelExternalLogin.cs,
│   │       │   ConfirmProviderLink.cs
│   │       └── Shared/
│   │           ├── AccountOptions.cs             # Sessions, EmailTokens, ExternalLogin, Legal, App
│   │           ├── SessionIssuer.cs              # ISessionStore + JWT + cookie + known device
│   │           ├── RefreshTokenCookie.cs
│   │           ├── ClientInfo.cs, DeviceDescriptor.cs
│   │           ├── EmailTokenIssuer.cs           # supersede + per-hour limit
│   │           ├── AccountEmails.cs              # 6 Polish templates, safe send
│   │           ├── AccountTakeover.cs            # R10/R7: verify, remove or keep password, supersede consents/tokens
│   │           ├── SecurityAudit.cs              # builds events, safe write, plain log line
│   │           ├── FailedLoginThrottle.cs        # unknown e-mails, IMemoryCache
│   │           └── ConsentRecorder.cs
│   ├── Liftingo.Infrastructure/
│   │   ├── Identity/
│   │   │   ├── ApplicationUser.cs                # + CreatedAt
│   │   │   ├── LiftingoUserStore.cs              # AutoSaveChanges = false
│   │   │   ├── LiftingoPasswordValidator.cs      # Unicode letters are not "special" (R13)
│   │   │   ├── IdentityService.cs
│   │   │   ├── JwtAccessTokenIssuer.cs, JwtOptions.cs
│   │   │   ├── SessionValidation.cs              # JwtBearerEvents.OnTokenValidated → ISessionStore
│   │   │   └── ExternalProviderOptions.cs        # Google/Facebook/X, registered only if configured
│   │   ├── Sessions/
│   │   │   ├── RedisSessionStore.cs              # ISessionStore
│   │   │   └── Scripts/*.lua                     # rotate, revoke, revoke-all (embedded resources)
│   │   ├── Audit/
│   │   │   ├── AuditEncryptionKey.cs             # entity, cascade-deleted with the user
│   │   │   ├── AuditKeyProvider.cs               # get-or-create, Data Protection wrapping
│   │   │   ├── EncryptingSecurityAuditLog.cs     # ISecurityAuditLog, AES-GCM
│   │   │   ├── AuditRecord.cs, IAuditSink.cs
│   │   │   └── ConsoleAuditSink.cs, DisabledAuditSink.cs
│   │   ├── Email/ConsoleEmailSender.cs, DisabledEmailSender.cs
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs           # + DataProtectionKeys, AuditEncryptionKeys
│   │   │   ├── Configurations/*.cs               # one per new entity + ApplicationUser
│   │   │   └── Migrations/<timestamp>_AddUserAuthentication.cs
│   │   └── InfrastructureExtensions.cs           # authN, providers, Redis, Data Protection,
│   │                                             # e-mail, audit, TimeProvider, memory cache
│   └── Liftingo.Api/
│       ├── Endpoints/V1Endpoints.cs              # map Account slices, remove Ping
│       ├── Extensions/AuthenticationExtensions.cs, ForwardedHeadersExtensions.cs
│       ├── ExceptionHandling/SessionStoreUnavailableExceptionHandler.cs   # IExceptionHandler → 503
│       ├── Program.cs                            # UseForwardedHeaders, UseHsts (non-dev),
│       │                                         # UseAuthentication before UseRateLimiting, UseAuthorization
│       ├── appsettings*.json                     # new sections (no secrets)
│       └── Liftingo.Api.http                     # auth requests
└── tests/
    ├── Liftingo.Api.UnitTests/
    │   ├── Domain/Account/EmailTokenTests.cs, KnownDeviceTests.cs, UserConsentTests.cs
    │   ├── Features/Account/RegisterValidatorTests.cs, …, DeviceDescriptorTests.cs
    │   ├── Infrastructure/Audit/EncryptingSecurityAuditLogTests.cs
    │   └── Common/OptionsValidatorTests (Jwt, Account)
    ├── Liftingo.Api.IntegrationTests/
    │   ├── Features/Account/RegisterTests.cs, LoginTests.cs, RefreshSessionTests.cs, LogoutTests.cs,
    │   │   LogoutAllDevicesTests.cs, VerifyEmailTests.cs, ResendVerificationEmailTests.cs,
    │   │   ForgotPasswordTests.cs, ResetPasswordTests.cs, CompleteExternalLoginTests.cs,
    │   │   HandleExternalLoginCallbackTests.cs, ConfirmProviderLinkTests.cs,
    │   │   SecurityAuditTests.cs, AuditEncryptionTests.cs,
    │   │   LogPrivacyTests.cs, SessionStoreUnavailableTests.cs
    │   └── Infrastructure/
    │       ├── SqlServerFixture.cs → also starts a RedisContainer, flushes it before each test
    │       ├── ApiFactory.cs (+ Redis connection string, CapturingEmailSender, CapturingAuditSink,
    │       │   FakeExternalAuthHandler, FakeTimeProvider, JWT test key)
    │       └── Fakers/*
    └── Liftingo.ArchitectureTests/DependencyGuardTests.cs   # + Domain must not depend on EF Core;
                                                             #   Application must not depend on StackExchange.Redis
```

**Structure decision**: the existing four-project backend. All auth endpoints sit in the `Account`
module. Helpers shared by several Account slices live in `Features/Account/Shared/` instead of
`Common/`, because no other module uses them. Redis and audit encryption are Infrastructure
details behind Application ports.

## Delivery order

Follows the spec priorities; each step ends with green tests.

1. Foundation: Domain entities, EF configurations, migration, `IApplicationDbContext` DbSets,
   options + validators, `docker-compose.yml` with Redis, `ISessionStore` + Redis implementation
   and Lua scripts, `IIdentityService`, JWT issuing and validation with the session check,
   `IEmailSender` + dev sender, `ISecurityAuditLog` + encryption + key table + console sink,
   Data Protection key persistence, test infrastructure (Redis container, capturing sinks, fake
   time, JWT key).
2. P1 slices: Register + GetCurrentAccount (these replace the `Ping` placeholder, which only
   proved versioned routing) → Login (lockout, throttle, audit, new device) → RefreshSession →
   Logout → LogoutAllDevices.
3. P2 slices: VerifyEmail + Resend → ForgotPassword + ResetPassword → external providers
   (handlers, callback, pending, complete, cancel, linking, takeover with consent step) →
   ConfirmProviderLink.
4. P3: welcome-only and new-device e-mail content, log-privacy and crypto-shredding test sweep.

## Risks

| Risk | Mitigation |
|---|---|
| Redis loses data (no persistence on Basic/Standard tier) | Users sign in again; accounts and consents are in SQL. Accepted; Premium persistence is config-only |
| Redis down | Login, refresh and every authenticated request fail with 503 (connection errors mapped, not 500). Accepted at thesis scale |
| SQL commit succeeds, Redis session creation fails | Request returns 500, account exists, user can log in (R4) |
| Data Protection key ring lost → audit keys unreadable | Key ring persisted to SQL Server (`DataProtectionKeys`) |
| The X handler drops the e-mail unless configured (no `users.email` scope or `confirmed_email` mapping by default) | Configured explicitly and unit-tested (R9); "Request email from users" must be on in the X app (quickstart prerequisite) |
| `AutoSaveChanges = false` behaves differently than expected | Verify early with an integration test; fallback is an explicit transaction in the few multi-write handlers |
| An unverified account stays usable by its holder until the real owner finishes the consent step (takeover is deferred, R10) | Bounded by the 7-day unverified-account deletion (retention feature); the owner can also reset the password at any time |
| Multiple browser tabs rotating the same refresh token look like theft | The frontend serialises refresh with Web Locks (R16); the server treats it as reuse by design, ending only that device's session (FR-011) |
| `refresh` / `logout` have no bearer, so users behind one NAT share an IP rate-limit bucket | Limits are configuration; accepted at thesis scale (R18) |
| Retention jobs for known devices and e-mail tokens are not built here | Tables grow until the retention work lands; rows are small and indexed for the later delete (R18) |
| An unverified account's holder can lock it to delay the owner's provider takeover for 15 min | Lockout is temporary; the owner can reset the password instead, which works on locked accounts (FR-012) |

## Complexity Tracking

No constitution violations to justify.
