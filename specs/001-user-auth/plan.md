# Implementation Plan: User Authentication (Register, Login, Logout, Password Reset)

**Branch**: `001-user-auth` (work happens on `feat/27-implement-user-authentication-register-login-logout-password-reset`) | **Date**: 2026-10-14 (rev. f) | **Spec**: [spec.md](spec.md)

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

**Revision e** (constitution v2.0.0 and the third clarification session): a refactor of the
built feature with no contract change. Endpoints, routes, `Result` mapping, cookies, redirects
and the `HttpContext`-based user and client info move to `Api` (R19). `Api` registers the
authentication schemes, and Infrastructure supplies the provider and JWT configuration (R20).
Application slices become command/query folders, one type per file, with the validator injected
and called explicitly (R22, R23). The unknown-e-mail lockout moves from memory to Redis (R21,
FR-013). E-mail templates move to Infrastructure behind a port that takes one record per e-mail;
e-mails are sent after the save, and times use a configured display zone checked at startup
(R24, FR-026a, FR-029). Application drops the ASP.NET Core framework reference, and architecture
tests enforce its package allowlist (R25). Frontend-driven provider sign-in is out of scope for
this revision: the redirect flow keeps its behaviour and only its HTTP parts move.

**Revision f** (constitution v2.1.1 and the fourth clarification session): provider sign-in moves
to the client. The client signs in at Google or Facebook and posts the credential to
`POST /auth/external/{provider}/login`; an `IExternalIdentityVerifier` port, implemented in
Infrastructure, checks it with the provider (R9). Consents come with that request or with a
repeat of it, so the API keeps nothing between requests (R10). Four Application slices
(`StartExternalLogin`, `HandleExternalLoginCallback`, `GetPendingExternalLogin`,
`CompleteExternalLogin`) become one, `ExternalLogin`; the `CancelExternalLogin` endpoint, the
`Liftingo.External` cookie scheme, `FrontendRedirects`, `ExternalLogin:PendingTimeout` and the
three provider handler packages are removed, and `Google.Apis.Auth` is added. Twitter (X) is
deferred. A development-only page served by the API runs the real provider sign-ins by hand; no
integration tests drive the provider endpoint.

## Summary

Accounts for Liftingo: e-mail/password registration with logged consents, login with lockout,
JWT access tokens with rotating refresh tokens in an HttpOnly cookie, logout of one or all devices,
e-mail verification, password reset, Google/Facebook sign-in with consents asked before any account
is created, and a
security audit log. E-mails go through an `IEmailSender` port that takes one record per e-mail.
Its only transport in this feature writes to the development console.

Technical approach, in short (details in [research.md](research.md)):

- Each use case is a command or query slice in `Application/Features/Account/{UseCase}/`. Its
  handler is injected into an endpoint in `Api/Endpoints/Account/`, calls its validator first,
  commits with one `SaveChangesAsync`, then issues the Redis session, writes audit events and sends
  e-mails, in that order. The endpoint turns the `Result` into the HTTP response, cookie and
  redirect included (R19, R22, R23).
- ASP.NET Core Identity stays in Infrastructure behind a narrow `IIdentityService` port, because
  Application may not depend on `Microsoft.AspNetCore.Identity`. The user store does not
  auto-save, so each handler commits its SQL writes with one `SaveChangesAsync`.
- Consents, known devices and e-mail tokens are Domain entities in SQL Server, queried directly
  through `IApplicationDbContext`.
- Sessions and refresh tokens live in Redis behind `ISessionStore`. Rotation and reuse detection
  run as one Lua script. TTLs give the 30-day sliding inactivity window, capped at 90 days from
  sign-in. A Redis outage answers `503`. Every bearer request checks `EXISTS session:{sid}`, so
  logout and password reset take effect immediately without querying SQL.
- Audit events go through `ISecurityAuditLog`. IP and device type are encrypted with a per-user key
  stored in SQL (cascade-deleted with the account, which is crypto-shredding). The Log Analytics
  sink comes later; this feature ships a development console sink.
- Lockout for unknown e-mails is kept in Redis behind `ILoginThrottle`, with the same rules as
  Identity's, so responses never reveal whether an address is registered, on any instance and
  after a restart (R8, R21).
- External sign-in (rev. f): the client gets a Google ID token or a Facebook access token and
  posts it; Infrastructure verifies it with the provider behind `IExternalIdentityVerifier`
  (audience/app id included). When an account would be created or taken over without consents,
  the API answers `Auth.ConsentRequired` and the client resends the same token with them. Nothing
  is persisted before consent, and no state is kept between the two requests (R9, R10).
- A typed-in e-mail that already has an account is linked only after a 24 h e-mail link is opened;
  the token carries the provider name and key (R17).
- A password reset is a takeover too when the e-mail was unverified: it verifies the address and
  asks for the consents again, through the same `AccountTakeover` helper (R7).
- E-mail templates live in Infrastructure and are rendered from records such as
  `NewDeviceEmail(UserId, To, SignedInAt, DeviceType)`; times are shown in `Email:DisplayTimeZone`
  (R24).
- The existing global rate limiter covers every endpoint; authentication runs in front of it so
  signed-in calls are limited per user (R18).

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: ASP.NET Core Minimal APIs, ASP.NET Core Identity (EF stores),
`Microsoft.AspNetCore.Authentication.JwtBearer`, FluentValidation, Serilog, Asp.Versioning,
`StackExchange.Redis`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (10.0.11),
`Google.Apis.Auth` 1.77.0 (rev. f; replaces the Google, Facebook and Twitter authentication handler
packages), `Serilog.Enrichers.Sensitive` 2.1.0; tests: `Testcontainers.Redis`. **Rev. e**:
Application gets explicit `Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.Options` references in place
of the ASP.NET Core framework reference (R25). No new third-party package.

**Storage**: SQL Server via EF Core (LocalDB for dev, Testcontainers for tests), one migration
(no change in rev. e). Redis (Docker for dev, Testcontainers for tests, Azure Cache for Redis in
prod) for sessions and, since rev. e, the unknown-e-mail lockout. Audit stream → console sink in
dev; Azure Monitor Log Analytics sink later.

**Testing**: xUnit v3 + NSubstitute + Bogus (unit), Testcontainers SQL Server + Redis + Respawn +
`WebApplicationFactory` (integration), ArchUnitNET (architecture).

**Target Platform**: Azure App Service.

**Project Type**: Web service (backend monolith).

**Performance Goals**: SC-011 p95 under normal load: login and registration ≤ 1 s, refresh
≤ 300 ms (manual check, R18). SC-006 dev e-mails ≤ 5 s after the action. The per-request session
check is one Redis `EXISTS`; SQL is touched by sign-in, verification, reset and account flows,
plus one read on refresh that keeps the `email_verified` claim current (FR-019). A failed login
for an unknown e-mail adds one Redis script call (R21).

**Constraints**: refresh token never readable by scripts (FR-008a); no personal data in
application logs (FR-030, constitution III); audit personal fields encrypted per user
(constitution III); all windows, thresholds and the e-mail display zone configurable (FR-031);
e-mails only after the save (FR-029); SPA and API must be same-site for the `SameSite=Lax`
cookie (deployment constraint for the frontend feature); HTTPS only, with App Service's minimum
TLS version set to 1.2 at deployment (FR-039). Rev. e: no route, status code, header, cookie or
body may change; the integration tests are the check.

**Scale/Scope**: single App Service instance today, but the lockout no longer depends on that
(R21). 13 endpoints and 13 slices since rev. f (17 endpoints before), 5 SQL tables, 4 Redis key
types.

No open NEEDS CLARIFICATION items. Library details marked *verify* in research
(`UserStore.AutoSaveChanges`, the transitive
`Microsoft.Extensions.*` patch version and the exact referenced-assembly names for the allowlist
test, R25) each have a fallback that keeps the design valid.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Checked against
constitution **v2.0.0**.

| Principle | Check | Status |
|---|---|---|
| **I. Clean Architecture layers, vertical slices** | Domain holds the entities and enums. Application holds 16 slices under `Features/Account/{UseCase}/`, the ports (`IIdentityService`, `IAccessTokenIssuer`, `ISessionStore`, `ILoginThrottle`, `IEmailSender`, `ISecurityAuditLog`, `IExternalProviderCatalog`, `IExternalIdentityVerifier`, `IUserContext`) and business checks (lockout, ownership of tokens, takeover rules). It references only Domain, EF Core core, the three `Microsoft.Extensions` abstraction packages and FluentValidation, with no HTTP types (R19, R25). Infrastructure implements the ports: Identity, Redis sessions and throttle, e-mail rendering and transport, audit encryption and sinks, provider token verifiers (rev. f). `Api` owns endpoints, `RouteConsts`, `Result` → `ProblemDetails`, cookies, `UserContext`, authentication schemes (R20), rate limiting and the exception handlers; it uses Infrastructure only in its wiring classes. Architecture tests enforce this (R25). | ✅ Pass |
| **II. Explicit use cases, no hidden pipeline** | Commands and queries implement `ICommand`/`IQuery`, and their handlers implement `ICommandHandler`/`IQueryHandler` and are injected straight into endpoints by that interface; no mediator, and no decorators registered against the interfaces (R22). One type per file; validators are separate classes, injected and called first. Handlers own `SaveChangesAsync` (and the one explicit transaction in `ResetPassword`), audit and e-mail calls, in a fixed order (R23); no endpoint filters or decorators. Handlers return `Result<T>` with HTTP-agnostic error types; `Api` maps them. Data is queried directly through `IApplicationDbContext`; `ISessionStore`, `ILoginThrottle` and `IIdentityService` wrap Redis and Identity, not EF aggregates, so they are not per-aggregate repositories. Response DTOs are per slice; the shared `AuthSessionResponse` is an `Api` HTTP contract type, and the Application responses share only the `IssuedSession` value they contain (R22). Shared Account helpers are concrete classes in `Features/Account/Shared/`; no handler calls another. | ✅ Pass |
| **III. Privacy and compliance (non-negotiable)** | Unchanged from rev. d, plus: the Redis lockout key is a hash of the address, never the address (R21); e-mail records carry no rendered text, and the sender logs only the e-mail type and user id on failure (R24). Consents, audit encryption, crypto-shredding, log masking and the dev-only e-mail and audit sinks stay as they are. | ✅ Pass |
| **IV. Explainable AI boundary** | Not touched. `IsEmailVerified` is exposed so the AI feature can apply the unverified quota later. | ✅ N/A |
| **V. Offline-first, mobile-first PWA** | No UI in this feature. The API supports the offline rule: a network failure on refresh is distinguishable from a `401` (R16). | ✅ N/A |
| **VI. Test-backed delivery** | Integration tests per endpoint against real SQL Server and Redis containers stay the safety net and must pass unchanged after every refactor step. New: Redis throttle tests including a second-instance check, renderer and time-zone tests, options validation for the zone, and layer-responsibility architecture tests (quickstart). Naming `[Method]_[Scenario]_[ExpectedResult]`; test files mirror the new slice folders. | ✅ Pass |
| **VII. Contract-driven frontend** | No frontend in scope. The contract and the OpenAPI document do not change in rev. e. | ✅ N/A |
| **VIII. Simplicity and scope** | Rev. f swaps three authentication handler packages for one (`Google.Apis.Auth`, for Google's ID-token signature check); Facebook needs only two HTTP calls. Rev. e added no third-party dependency. The new types each answer a current rule: `ILoginThrottle` (FR-013 clarification), e-mail records and transport split (Principle I: templates out of Application), `IssuedSession` and `ExternalIdentity` (HTTP out of handlers). Reflection-based handler registration instead of Scrutor. No templating engine, outbox or Hangfire. | ✅ Pass |
| **Tech stack** | Identity + Google/Facebook sign-in from client-supplied tokens (constitution v2.1.1) + JWT with refresh tokens, Redis, Log Analytics for the audit log. | ✅ Pass |

**Gate result (pre-research)**: the built code fails Principles I and II of v2.0.0 (33
Application files use ASP.NET Core or `IMemoryCache`; slices map their own routes; validators are
static fields in the slice file). That is the reason for this revision, not a violation the plan
accepts: the design below removes every case. Pass for the design.

**Re-check after Phase 1 design**: pass. Notes:

1. Earlier notes still hold: the Log Analytics audit sink, the Azure e-mail transport and the
   retention jobs for known devices and e-mail tokens must exist before any non-local
   deployment (`DisabledAuditSink` drops audit events, NFR-17).
2. `AppOptions.FrontendBaseUrl` stays in Application as a plain options class: Infrastructure
   needs it for e-mail links (R19). It adds no dependency to Application. (Before rev. f `Api`
   also used it for redirects, and scheme names and the callback path were passed from `Api` to
   Infrastructure; both went with the redirect flow.)
3. Since rev. f every endpoint has a slice.
4. Follow-ups outside this plan: `docs/adr/0001-vertical-slice-plus-clean-architecture.md` still
   describes one file per endpoint in Application and needs a superseding ADR; Twitter (X)
   sign-in is a later feature.

## Project Structure

### Documentation (this feature)

```text
specs/001-user-auth/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0 (R19–R25 added in rev. e)
├── data-model.md        # Phase 1 (Redis lockout key, Email:DisplayTimeZone)
├── quickstart.md        # Phase 1 (rev. e checks)
├── contracts/
│   └── auth-api.md      # Phase 1: HTTP contract (unchanged shapes, new endpoint/slice names)
├── checklists/
│   ├── requirements.md
│   └── completeness.md
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code (repository root), after rev. e

Only the parts rev. e changes are expanded; Domain, persistence, sessions and audit encryption
keep the rev. d layout.

```text
backend/
├── Directory.Packages.props                      # + Microsoft.Extensions.DependencyInjection.Abstractions,
│                                                 #   Logging.Abstractions, Options (R25)
├── src/
│   ├── Liftingo.Domain/Account/                  # unchanged
│   ├── Liftingo.Application/
│   │   ├── Liftingo.Application.csproj           # FrameworkReference removed (R25)
│   │   ├── ApplicationExtensions.cs              # registers handlers by their handler interface,
│   │   │                                         #   and every IValidator<T> (R22)
│   │   ├── Common/
│   │   │   ├── IUserContext.cs                   # port; implementation moves to Api
│   │   │   ├── Configuration/AppOptions.cs       # FrontendBaseUrl, used by Infrastructure and Api (R19)
│   │   │   ├── Email/IEmailSender.cs, OutgoingEmail.cs          # one record per e-mail (R24)
│   │   │   ├── Exceptions/                       # unchanged
│   │   │   ├── Messaging/                        # ICommand, IQuery, ICommandHandler, IQueryHandler (R22)
│   │   │   ├── Persistence/IApplicationDbContext.cs
│   │   │   ├── Result/                           # Result, Error, ErrorType, LockedOutError, ValidationError
│   │   │   │                                     #   (ResultExtensions moves to Api)
│   │   │   └── Security/                         # ports as before + ILoginThrottle.cs, ExternalIdentity.cs,
│   │   │                                         #   IExternalIdentityVerifier.cs (rev. f), IssuedSession.cs
│   │   └── Features/Account/
│   │       ├── Register/
│   │       │   ├── RegisterCommand.cs
│   │       │   ├── RegisterCommandHandler.cs
│   │       │   ├── RegisterCommandValidator.cs
│   │       │   └── RegisterResponse.cs
│   │       ├── Login/ …                          # same four files
│   │       ├── GetCurrentAccount/                # Query, QueryHandler, Response
│   │       ├── RefreshSession/, Logout/, LogoutAllDevices/,
│   │       ├── VerifyEmail/, ResendVerificationEmail/, ForgotPassword/, ResetPassword/,
│   │       ├── GetExternalProviders/, ExternalLogin/ (rev. f), ConfirmProviderLink/
│   │       └── Shared/
│   │           ├── AccountOptions.cs             # Sessions (no CookiePath), EmailTokens,
│   │           │                                 #   Legal, LoginLockout, Audit, KnownDevices
│   │           ├── SessionIssuer.cs              # StageDevice + Issue → IssuedSession, no cookie
│   │           ├── ClientInfo.cs                 # pure Create(ip, userAgent, deviceIdHeader)
│   │           ├── DeviceDescriptor.cs, AccountValidationRules.cs, SignInKind.cs
│   │           ├── EmailTokenIssuer.cs, ConsentRecorder.cs, AccountTakeover.cs, SecurityAudit.cs
│   │           ├── ExternalLoginErrors.cs        # Auth.ExternalLoginFailed, Auth.ProviderEmailConflict,
│   │           │                                 #   Auth.ProviderUnavailable, Auth.ConsentRequired
│   │           └── Emails/                       # VerificationAndWelcomeEmail, VerificationEmail,
│   │                                             #   PasswordResetEmail, ProviderLinkConfirmationEmail,
│   │                                             #   WelcomeEmail, NewDeviceEmail
│   │                                             # removed: AccountEmails, FailedLoginThrottle,
│   │                                             #   RefreshTokenCookie, FrontendRedirects,
│   │                                             #   PendingExternalLogin, AuthSessionResponse
│   ├── Liftingo.Infrastructure/
│   │   ├── Email/
│   │   │   ├── EmailSender.cs                    # IEmailSender: render, transport, safe send (FR-029)
│   │   │   ├── AccountEmailRenderer.cs           # the six Polish templates, links, display zone
│   │   │   ├── EmailMessage.cs, IEmailTransport.cs
│   │   │   ├── ConsoleEmailTransport.cs, DisabledEmailTransport.cs   # renamed senders, FR-028
│   │   │   └── EmailOptions.cs                   # DisplayTimeZone + validator (FR-026a)
│   │   ├── Sessions/
│   │   │   ├── RedisSessionStore.cs              # unchanged
│   │   │   ├── RedisLoginThrottle.cs             # ILoginThrottle (R21)
│   │   │   └── Scripts/*.lua                     # + login-throttle-fail.lua
│   │   ├── Identity/
│   │   │   ├── ExternalProviders/                # rev. f: GoogleIdTokenVerifier, FacebookTokenVerifier,
│   │   │   │                                     #   options, catalog, verifier dispatcher (R9)
│   │   │   ├── JwtBearerOptionsSetup.cs          # IConfigureNamedOptions<JwtBearerOptions> + SessionValidation
│   │   │   └── …                                 # IdentityService, store, validator, JWT issuer unchanged;
│   │   │                                         #   UserContext moves to Api
│   │   └── InfrastructureExtensions.cs           # no AddAuthentication, no memory cache
│   └── Liftingo.Api/
│       ├── Endpoints/
│       │   ├── IEndpoint.cs, RouteConsts.cs, V1Endpoints.cs
│       │   └── Account/
│       │       ├── RegisterEndpoint.cs, LoginEndpoint.cs, GetCurrentAccountEndpoint.cs, …
│       │       ├── {UseCase}Request.cs                       # only where the HTTP shape differs
│       │       └── Shared/
│       │           ├── AuthSessionResponse.cs, AccountResponse.cs   # HTTP contract, unchanged JSON
│       │           ├── RefreshTokenCookie.cs, RefreshCookieOptions.cs
│       │           └── ClientInfoReader.cs                   # HttpContext → ClientInfo.Create
│       ├── Http/ResultExtensions.cs              # ToProblem, RetryAfterProblemResult (moved)
│       ├── Security/UserContext.cs
│       ├── Extensions/AuthenticationExtensions.cs   # AddAuthentication, JWT bearer scheme (R20)
│       ├── DevPages/provider-sign-in.html        # rev. f, Development only: real provider sign-in by hand
│       └── Program.cs                            # unchanged pipeline order
└── tests/
    ├── Liftingo.Api.UnitTests/
    │   ├── Features/Account/{UseCase}/{UseCase}CommandValidatorTests.cs   # renamed, mirror slice folders
    │   ├── Features/Account/Shared/DeviceDescriptorTests.cs, ClientInfoTests.cs
    │   ├── Infrastructure/Email/AccountEmailRendererTests.cs, EmailOptionsValidatorTests.cs
    │   └── Http/ResultExtensionsTests.cs                                  # moved with the type
    ├── Liftingo.Api.IntegrationTests/
    │   ├── Features/Account/*Tests.cs            # unchanged behaviour; LoginTests + second-instance case
    │   └── Infrastructure/
    │       ├── CapturingEmailTransport.cs        # was CapturingEmailSender
    │       └── RedisLoginThrottleTests.cs        # replaces FailedLoginThrottleTests (unit)
    └── Liftingo.ArchitectureTests/
        └── LayerResponsibilityTests.cs           # allowlist, naming, Api wiring-only (R25)
```

**Structure decision**: the existing four-project backend. All auth endpoints sit in the `Account`
module. Helpers shared by several Account slices live in `Features/Account/Shared/` in Application
and in `Endpoints/Account/Shared/` in Api, because no other module uses them. Redis, Identity,
e-mail templates and audit encryption are Infrastructure details behind Application ports.

## Delivery order

Rev. e is a refactor of working code. Every step ends with the full integration suite green and
unchanged, so a regression points at one step.

1. **Api adapters first, Application untouched in behaviour**: move `ResultExtensions`,
   `IEndpoint`, `RouteConsts` and `UserContext` to Api; add `RefreshTokenCookie`,
   `ExternalLoginCookie`, `ClientInfoReader` and `FrontendRedirects` in `Api/Endpoints/Account/Shared/`;
   move scheme registration to Api with the Infrastructure configuration hooks (R20).
2. **E-mail port**: add the records, `EmailSender`, `AccountEmailRenderer`, the transports and
   `EmailOptions` with the startup check (FR-026a); point the handlers at the records; delete
   `AccountEmails`. Rename the capturing test double.
3. **Lockout store**: add `ILoginThrottle` and `RedisLoginThrottle` with its script and tests,
   including the second-instance case (FR-013); delete `FailedLoginThrottle` and the memory cache.
4. **Slices to command/query folders**, one at a time, each with its Api endpoint, in the spec's
   priority order: `GetCurrentAccount`, `Register`, `Login`, `RefreshSession`, `Logout`,
   `LogoutAllDevices`, `VerifyEmail`, `ResendVerificationEmail`, `ForgotPassword`,
   `ResetPassword`, then the external slices (`GetExternalProviders`, `StartExternalLogin`,
   `HandleExternalLoginCallback`, `GetPendingExternalLogin`, `CompleteExternalLogin`,
   `ConfirmProviderLink`) and the Api-only `CancelExternalLogin`. `SessionIssuer` switches to
   `IssuedSession` with the first signing-in slice. Validators become injected classes.
5. **Close the boundary**: switch `AddApplication` to scanning, remove the framework reference,
   add the three abstraction packages, and add `LayerResponsibilityTests` (R25). The build and the
   architecture tests prove nothing HTTP-related is left in Application.
6. **Docs**: `Liftingo.Api.http` needs no change (routes are the same); refresh the backend
   `AGENTS.md` Testing section if test names changed.

Rev. f, after rev. e:

7. **Provider verification**: add `IExternalIdentityVerifier`, the Google and Facebook verifiers,
   the new catalog with client ids and the Facebook verifier's unit tests.
8. **One provider endpoint**: add the `ExternalLogin` slice and endpoint; delete the four old
   slices, their endpoints, `CancelExternalLogin`, the cookie scheme, `FrontendRedirects`,
   `ExternalLogin:PendingTimeout` and the handler packages.
9. **Dev page and docs**: add the Development-only provider sign-in page, update
   `Liftingo.Api.http` and the quickstart.

## Risks

| Risk | Mitigation |
|---|---|
| Redis loses data (no persistence on Basic/Standard tier) | Users sign in again; accounts and consents are in SQL. Since rev. e an outage also clears unknown-e-mail lockouts, which then restart from zero for every address, so no address stands out. Accepted; Premium persistence is config-only |
| Redis down | Login, refresh and every authenticated request fail with 503 (connection errors mapped, not 500). Accepted at thesis scale |
| SQL commit succeeds, Redis session creation fails | Request returns 500, account exists, user can log in (R4). No e-mail is sent, since e-mails come last (FR-029) |
| Data Protection key ring lost → audit keys unreadable | Key ring persisted to SQL Server (`DataProtectionKeys`) |
| Rev. f: a provider credential issued to another application is accepted | Google: `Audience` is always set; Facebook: `app_id` is compared with ours and unit-tested (R9) |
| Rev. f: the provider endpoint has no integration tests | Decided 2026-10-14. The account rules it applies are shared with tested slices (`AccountTakeover`, `SessionIssuer`, `EmailTokenIssuer`); the dev page covers the real providers by hand |
| `AutoSaveChanges = false` behaves differently than expected | Verified by the existing integration tests; fallback is an explicit transaction in the few multi-write handlers |
| An unverified account stays usable by its holder until the real owner finishes the consent step (takeover is deferred, R10) | Bounded by the 7-day unverified-account deletion (retention feature); the owner can also reset the password at any time |
| Multiple browser tabs rotating the same refresh token look like theft | The frontend serialises refresh with Web Locks (R16); the server treats it as reuse by design, ending only that device's session (FR-011) |
| `refresh` / `logout` have no bearer, so users behind one NAT share an IP rate-limit bucket | Limits are configuration; accepted at thesis scale (R18) |
| Retention jobs for known devices and e-mail tokens are not built here | Tables grow until the retention work lands; rows are small and indexed for the later delete (R18) |
| An unverified account's holder can lock it to delay the owner's provider takeover for 15 min | Lockout is temporary; the owner can reset the password instead, which works on locked accounts (FR-012) |
| Rev. e: a moved HTTP detail changes a response (status, header, cookie attribute, redirect URL) | Delivery is step by step with the integration suite unchanged after each step; the suite already asserts status codes, `Retry-After` and cookie attributes |
| Rev. e: unknown-e-mail lockout entries in Redis never expire while their counter is above zero | Required by FR-012 (no decay). Growth is bounded by the global rate limiter; each entry is two small hash fields |
| Rev. e: a container image without time-zone data makes `Europe/Warsaw` unknown | By design the app refuses to start (FR-026a), so the problem shows at deployment, not as wrong times in e-mails. App Service images include tzdata |
| Rev. e: the referenced-assembly allowlist test is brittle across patch updates | It lists assembly names, not versions; a new transitive name fails the test once and is reviewed against the constitution |

## Complexity Tracking

No constitution violations to justify.
