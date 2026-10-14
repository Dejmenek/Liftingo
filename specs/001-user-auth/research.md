# Research: User Authentication

**Feature**: `001-user-auth` | **Date**: 2026-10-12 | **Plan**: [plan.md](plan.md)

Each section records a decision, why it was made, and what else was considered. Context7 was
unavailable during planning (MCP connection failure), so library behavior marked *verify* should
be checked against current docs at the start of implementation.

**Revision 2026-10-12 (b)**: scope narrowed to the backend (the frontend is a later feature; R16
now lists only what the backend expects from a client). Sessions and refresh tokens moved from SQL
Server to Redis (R3, R4). The security audit log moved from a SQL table to an encrypted audit
stream bound for Azure Monitor Log Analytics, with crypto-shredding (R14). Both follow
constitution v1.1.0.

**Revision 2026-10-12 (c)**: follows the second clarification session. Taking over an unverified
account now asks for the consents again before anything is written (R10). A typed-in e-mail that
already has an account gets a link-confirmation e-mail instead of `409` (R17). A password reset
signs out every session and starts none (R7). Revoked sessions reject their access token at once,
and unknown e-mails get the same lockout; R3 and R8 already did both and are unchanged.

**Revision 2026-10-12 (d)**: follows the completeness-checklist review. Sessions get a 90-day
absolute lifetime (R4). The refresh cookie is `SameSite=Lax` (R5). Sign out of all devices and
password reset clear known devices (R6). A reset verifies an unverified e-mail and then asks for
the consents again, like a takeover (R7). Lockout also blocks provider sign-in (R8, R9). A linked
provider that returns another account's e-mail is refused (R9). Audit events without a known user
carry no IP or device type, so the shared audit key is gone (R14). New R18 covers the existing
rate limiter, HTTPS, the 503 mapping, retention and performance targets.

**Revision 2026-10-13 (e)**: a refactor pass for constitution v2.0.0 and the third clarification
session. No endpoint, status code or response body changes. Endpoints and every HTTP concern move
to `Api`; Application slices become command/query folders with explicit validator calls (R19,
R22, R23). Authentication schemes are registered by `Api`, with Infrastructure supplying the
provider and JWT configuration (R20). The unknown-e-mail lockout moves from memory to Redis
(R21, replaces the memory throttle in R8). E-mail templates move to Infrastructure behind a
port that takes one record per e-mail, and e-mail times use a configured display zone that is
checked at startup (R24, replaces the template part of R11). Application's package list is
enforced by architecture tests (R25).

**Revision 2026-10-14 (f)**: follows the fourth clarification session. Provider sign-in switches
from server-side redirects to a credential the client gets from the provider and posts to the API
(R9). Consents can arrive with that request or with a repeat of it, so no pending sign-in is kept
between requests (R10). Twitter (X) is deferred. Five external endpoints become one
(`POST /auth/external/{provider}/login`); the `Liftingo.External` cookie scheme, the provider
handler packages, `FrontendRedirects` and `ExternalLogin:PendingTimeout` are removed (R19, R20,
R22).

---

## R1. How Application slices use ASP.NET Core Identity

`ApplicationUser : IdentityUser<Guid>` lives in `Liftingo.Infrastructure/Identity/`, and
`DependencyGuardTests` forbid `Application` from depending on `Microsoft.AspNetCore.Identity`.
Slices therefore cannot call `UserManager<ApplicationUser>` directly.

- **Decision**: `Application/Common/Security/IIdentityService.cs` defines a narrow port with the
  operations Identity owns (create user, check password with lockout, find by e-mail / external
  login, add external login, confirm e-mail, remove password, reset password, read account
  summary). `Infrastructure/Identity/IdentityService.cs` implements it with `UserManager`. The other
  SQL tables (consents, known devices, e-mail tokens) are Domain entities exposed as `DbSet<T>` on
  `IApplicationDbContext` and queried directly with LINQ. Sessions live in Redis behind
  `ISessionStore` (R4); audit events go through `ISecurityAuditLog` (R14).
- **Rationale**: Principle I says Application defines the interfaces it needs and Infrastructure
  implements them (same category as `IEmailSender`). The port wraps a framework, not an aggregate,
  so it is not a per-aggregate repository (Principle II). Identity keeps hashing, lockout and the
  password policy, which already read from the `Identity` config section.
- **Alternatives considered**:
  - Move the user into Domain and drop Identity: breaks the binding stack ("ASP.NET Core Identity").
  - Relax the architecture test: breaks Principle I's dependency rule.
  - Put the auth slices in Infrastructure: breaks "every endpoint is a slice in Application".

## R2. One `SaveChanges` per request with Identity

`UserManager` methods call `SaveChanges` themselves by default, so registration (user + consents +
session + device + audit) would not be atomic.

- **Decision**: register a `LiftingoUserStore` (subclass of EF `UserStore`) with
  `AutoSaveChanges = false`. `IIdentityService` methods only stage changes on the shared scoped
  `ApplicationDbContext`; the slice handler calls `db.SaveChangesAsync(ct)` once.
- **Rationale**: atomic writes without adding `BeginTransaction` to `IApplicationDbContext`.
  *Verify*: `UserManager.CreateAsync` with `AutoSaveChanges = false` still runs validators and leaves
  the entity tracked as `Added`.
- **Exception**: `ExecuteDeleteAsync` runs immediately, outside `SaveChangesAsync`. A handler that
  uses it together with other writes (password reset: token, password, consents, known devices)
  wraps both in a transaction, so `IApplicationDbContext` gains `BeginTransactionAsync`. A handler
  whose only SQL write is the bulk delete (sign out of all devices) needs none.
- **Alternatives**: explicit transactions in every handler (more ceremony); accept partial writes
  (violates SC-007: "no account without both consents").
- **Race on unique e-mail**: Identity's `RequireUniqueEmail` check is a read-then-write. Add a
  unique filtered index on `AspNetUsers.NormalizedEmail` and map the `DbUpdateException` to the
  same `Conflict` result as the validator.

## R3. Access credential (JWT)

- **Decision**: HS256 JWT issued with `JsonWebTokenHandler` (already pulled in by
  `Microsoft.AspNetCore.Authentication.JwtBearer`), 15-minute lifetime. Claims: `sub` (user id,
  mapped to `ClaimTypes.NameIdentifier` as `UserContext` expects), `sid` (session id),
  `email_verified`, `jti`. Signing key, issuer, audience and lifetime come from a validated `Jwt`
  options section; the dev key lives in user secrets, the test key in `ApiFactory`.
- **Session check on every request**: `JwtBearerEvents.OnTokenValidated` calls
  `ISessionStore.IsActiveAsync(sid)`, a single Redis `EXISTS session:{sid}`, and fails
  authentication if the key is gone (revoked or expired).
- **Rationale**: SC-003 and FR-035 require that a revoked session is rejected for *use*, not only
  renewal. Without the check a stolen access token keeps working for up to 15 minutes after logout,
  "sign out of all devices" or a password reset. A Redis round trip per request keeps that load
  off SQL Server, which was the reason for moving sessions to Redis.
- **Alternatives**: pure stateless JWT (fails SC-003 for up to 15 min); session lookup in SQL Server
  with a HybridCache in front (no new infrastructure, but rejected in favour of Redis, constitution
  v1.1.0); Identity security stamp validation (designed for cookies, needs a user lookup anyway).
- `IUserContext` gains `bool IsEmailVerified` (from the claim) so later features can apply the
  unverified-account limits (FR-019). The client calls refresh after verification to get an updated
  claim. Until then the claim can say `false` for up to 15 minutes; the spec accepts this because
  it only errs on the restrictive side (FR-019).

## R4. Renewal credential (refresh token), rotation and reuse detection

- **Decision**: opaque 256-bit random token, Base64Url, stored only as its SHA-256 hash, in Redis
  (StackExchange.Redis) behind an Application port `ISessionStore`, implemented by
  `Infrastructure/Sessions/RedisSessionStore`. Key layout (exact shapes in
  [data-model.md](data-model.md#redis-sessions-and-refresh-tokens)):
  - `session:{sessionId}`: hash with user id, device, timestamps, `absoluteExpiresAt` and the
    current token hash. TTL = `min(inactivity timeout, absoluteExpiresAt - now)`, recomputed on
    every renewal, so both FR-010's sliding 30-day window and FR-010a's 90-day cap are plain Redis
    expiry. `absoluteExpiresAt` = sign-in time + `Sessions:AbsoluteLifetime`, set once at creation
    and never moved by renewals.
  - `rt:{tokenHash}`: session id + state (`active` / `used`), same TTL.
  - `user-sessions:{userId}`: set of the user's session ids, for "sign out of all devices" and
    "revoke all on password reset".

  Refresh:
  1. Hash the cookie value and load `rt:{hash}`.
  2. Missing, or `session:{id}` missing (revoked or expired) → 401.
  3. State `used` → **reuse detected**: delete the session, audit `RefreshTokenReuse`, 401.
  4. Otherwise mark it `used`, write the new `rt:{newHash}`, update the session's current hash and
     `lastRenewedAt`, and set both TTLs to the capped value above. The new cookie's `Max-Age`
     uses the same value, so the cookie never outlives the session.
- **Atomicity and concurrency**: steps 1–4 run as one Lua script (`ScriptEvaluateAsync`), so two
  simultaneous uses of the same token cannot both succeed. The second sees `used` and is treated
  as reuse. Different devices have different sessions, so they never interfere (edge case "several
  devices renew at once"). A session is one device's chain, so reuse revokes only that device
  (FR-011), including the accepted case of one device sending two renewals or retrying after a
  lost response. Revoking deletes `session:{id}` and its current `rt` key; older `used`
  keys expire on their own and point to a missing session, so they are rejected anyway.
- **Why Redis**: the per-request session check (R3) becomes a cheap `EXISTS` instead of a SQL
  query, and TTLs implement inactivity expiry without a cleanup job.
- **Durability**: Azure Cache for Redis Basic/Standard does not persist to disk. If the cache loses
  data, every user has to sign in again. Nothing else is lost: accounts, consents and devices stay
  in SQL Server. Accepted risk for a thesis-scale app; Premium persistence is a configuration
  change if it is ever needed.
- **Cross-store writes**: handlers commit SQL first (`SaveChangesAsync`) and create the session in
  Redis second. If Redis fails after the commit, the request returns 500 but the account exists, and
  the user can log in. The reverse order could leave a session for a user that was never saved.
- **Same browser, several tabs**: the cookie is shared, so two tabs could present the same token at
  once and trigger reuse detection. The client serialises refresh calls across tabs with the Web
  Locks API (`navigator.locks.request('liftingo-auth-refresh', ...)`). The second tab then sends
  the already-rotated cookie. A server-side grace window was rejected because the server only has
  hashes and could not return the new token.
- **Alternatives**: SQL Server tables `UserSession` + `RefreshToken` with `rowversion` (first plan
  revision; durable, but every authenticated request queried SQL); JWT refresh tokens (cannot be
  revoked without storage anyway); `IDistributedCache` (no sets or scripts, so no atomic rotation
  and no "revoke all").

## R5. Cookie and client storage

- **Decision**: refresh cookie `liftingo_rt`, `HttpOnly`, `Secure`, `SameSite=Lax`,
  `Path=/api/v1/auth/session`, `Max-Age` = the session's remaining TTL (at most 30 days, never past
  the 90-day cap, R4), reissued on every rotation. All three cookie-bearing endpoints (`refresh`,
  `logout`, `logout-all`) live under that path, so the browser sends it nowhere else (FR-008a). The
  access token is returned in the JSON body; keeping it in memory only is the client's job (R16).
  On start the client calls `refresh` to get one.
- **Why `Lax`** (FR-008a, decided in review): `Strict` adds nothing here because every cookie
  endpoint is POST-only and `Lax` never attaches cookies to cross-site POSTs. Rev. a–e also needed
  `Lax` for the provider redirect chain, which rev. f removed.
- **Same-site requirement**: `SameSite` cookies are only sent on XHR when the SPA and the API are
  on the same site. The later frontend will use a dev-server proxy for `/api` (same origin). In
  production the API must be on a subdomain of the frontend's registrable domain (e.g.
  `app.liftingo.pl` + `api.liftingo.pl`) or behind the Static Web Apps linked backend. Production
  topology is outside this feature but recorded in the plan as a deployment constraint.
- **CSRF**: `SameSite=Lax` plus POST-only cookie endpoints. No anti-forgery token needed.
- **Alternatives**: refresh token in `localStorage` (rejected by FR-008a / SC-010);
  `SameSite=Strict` (first plan revision; replaced by `Lax` in review, see above); `SameSite=None`
  (would send the cookie on cross-site POSTs and need CSRF tokens).

## R6. Device identity, device type and new-device e-mail

- **Decision**: the client generates a UUID once and keeps it in `localStorage`
  (`liftingo.deviceId`). It is not a credential. It is sent as header `X-Device-Id` on register,
  login, provider login and link confirmation. The server derives the device type from `User-Agent` with a
  small built-in classifier (`DeviceDescriptor`: form factor + browser family + OS family, e.g.
  "Chrome, Windows, komputer"). A missing or malformed id is replaced with a fresh server-side
  UUID, so the sign-in counts as a new device.
- `KnownDevice(UserId, DeviceId)` is unique. A sign-in (FR-026) is a successful password login,
  provider sign-in into an existing account, or confirmed provider link (R17). On a sign-in, an
  unknown pair is inserted and the new-device e-mail sent; a known pair gets `LastSeenAt` updated.
  Every way of creating an account (register, a provider login that creates one) inserts the first device
  without an e-mail (US7-6). Refresh never touches `KnownDevice`. Failed attempts never insert
  (US7-5).
- **Clearing**: "sign out of all devices" (FR-015a) and a completed password reset (FR-035) delete
  every `KnownDevice` row of the user in the same commit, so the next sign-in anywhere sends a
  new-device e-mail.
- **Retention**: a row whose `LastSeenAt` is older than `KnownDevices:Retention` (12 months) is
  deleted by the retention work's daily job (out of scope, NFR-40); this feature keeps `LastSeenAt`
  current and indexes it.
- **Spoofing** (spec Assumptions): the id comes from the client. Accepted; faking it needs the
  password and the victim's stored id, and only suppresses the e-mail.
- **Alternatives**: a UA-parser NuGet package (extra dependency for one coarse label, Principle
  VIII); fingerprinting (privacy-hostile); the refresh session as the device (a logout would make
  the next login look "new").

## R7. E-mail tokens (verification, password reset) and per-address limit

ASP.NET Identity's built-in `DataProtectorTokenProvider` cannot meet the spec: tokens are not
single-use (verification), are not superseded when a new one is issued, and share one lifespan.

- **Decision**: `EmailToken` entity with `Purpose` (`EmailVerification` | `PasswordReset` |
  `ProviderLinkConfirmation`, the third added in R17),
  SHA-256 hash of a 256-bit random value, `ExpiresAt`, `ConsumedAt`, `SupersededAt`. Issuing a
  token marks all open tokens of that user and purpose as superseded (FR-018, FR-033). Lifetimes
  come from config (24 h / 1 h).
- **Per-address limit (FR-037)**: before issuing, count the user's tokens of that purpose created
  in the last rolling hour; at the limit (default 3), return the normal response and send nothing.
  Only registered addresses ever get tokens, so counting tokens per user is counting e-mails per
  address. No extra table.
- **Reset completion**: `IIdentityService.ResetPasswordAsync` sets the new hash (or adds a password
  for provider-only accounts, FR-036), clears `AccessFailedCount` and `LockoutEnd`, and updates the
  security stamp. The handler, in the same commit, consumes the token and deletes the user's
  `KnownDevice` rows; after the commit it revokes every session, including the one of the device
  doing the reset if it has one. It issues no new session and sets no cookie; the user logs in
  with the new password (FR-035). If the new password fails the policy, nothing is consumed
  (US6-5).
- **Reset of an unverified account** (FR-035, decided in review): opening the reset link proves
  the inbox, so it is the same takeover as R10. Request body gains optional
  `acceptTermsAndAge` / `acceptHealthData` / `acceptMarketing`. If the account is unverified and
  the two required consents are not both `true`, the handler returns `400 Auth.ConsentRequired`
  before touching the password; the token stays usable and the UI shows the consent form and
  resubmits (the same pattern as R17). With the consents, the same commit also sets
  `EmailConfirmed = true`, supersedes the current consents and inserts three new rows, and
  supersedes open verification and link-confirmation tokens. `AccountTakeover` (R10) does this with
  a "keep the new password" mode instead of removing it. A verified account ignores the consent
  fields.
- **Third purpose**: `ProviderLinkConfirmation` (R17) uses the same entity, supersede rule and
  per-hour limit, with a 24 h lifetime.
- **What the limit counts** (FR-037): tokens per purpose. The registration e-mail issues a
  verification token, so it counts toward the verification limit. Welcome-only and new-device
  e-mails issue no token and are not limited.
- **Already verified** (FR-017): `VerifyEmail` looks the token up by hash whatever its state. If
  it exists and its user is already verified, the answer is `200 { "status": "alreadyVerified" }`;
  a fresh verification answers `200 { "status": "verified" }`. Unknown hashes and unusable tokens
  of an unverified user give `Auth.VerificationLinkInvalid`.
- **Retention** (FR-038): a token is deleted 24 h (`EmailTokens:RetentionAfterUnusable`) after it
  stopped being usable: the earliest of `ConsumedAt`, `SupersededAt` and `ExpiresAt`. The daily
  delete job belongs to the retention work (NFR-40); this feature indexes `ExpiresAt` so the job's
  query is cheap. Once deleted, an old link is simply unknown (FR-017's "already verified" answer
  then no longer applies).
- **Alternatives**: custom Identity `IUserTwoFactorTokenProvider` (still no supersede/single use
  without storage); storing tokens in plain text (a DB leak would hand out reset links).

## R8. Login lockout without account enumeration

FR-012 says to tell the user when they can retry. FR-013/SC-008 say the response must not reveal
whether the e-mail is registered. A lockout message shown only for real accounts would leak that.

- **Decision**:
  - Real accounts: Identity lockout (`AccessFailedAsync`, 5 attempts, 15 min, from the `Identity`
    section). A provider-only account (no password) counts the failure the same way.
  - Unknown e-mails: a `FailedLoginThrottle` in `IMemoryCache`, keyed by the SHA-256 of the
    normalised e-mail, applies the same 5/15 rule, so the 6th attempt also gets the lockout
    response with a retry time.
  - Both paths return the same `401` problem (`Auth.InvalidCredentials`) or the same lockout
    problem: `429`, code `Auth.LockedOut`, `retryAfter` extension and `Retry-After` header. This
    needs a new `ErrorType.TooManyRequests` mapped to 429 in `ResultExtensions`, plus a way to
    attach the retry time (an `Error` subtype `LockedOutError(DateTimeOffset RetryAfter)`, mapped
    like `ValidationError` is today).
  - Timing: for unknown e-mails the handler verifies the password against a fixed dummy hash, so
    response time does not reveal registration.
  - **During a lockout** (FR-012): attempts are refused before the password is checked and are not
    counted, so they never extend the lockout. The counter has no decay; it resets on success,
    when the lockout triggers, and on reset. This is Identity's default behaviour for
    `CheckPasswordSignInAsync` with `lockoutOnFailure: true`, checked against the v10.0.1 source:
    `PreSignInCheck` returns `LockedOut` before `CheckPasswordAsync` runs, `AccessFailedAsync` is
    called only after a wrong password, and when the count reaches `MaxFailedAccessAttempts` it
    sets `LockoutEnd` and resets the count to 0. `FailedLoginThrottle` copies the same rules.
  - **Clock**: `UserManager.AccessFailedAsync` and `IsLockedOutAsync` use `DateTimeOffset.UtcNow`,
    not `TimeProvider` (v10.0.1 source). A `FakeTimeProvider` therefore cannot end an Identity
    lockout in tests. Lockout-expiry tests move `LockoutEnd` into the past through the database
    instead; `FailedLoginThrottle` takes `TimeProvider` and is tested with the fake clock.
  - **Provider sign-in on a locked account** (FR-012): the provider login (R10) and
    `confirm-link` (R17) check `IsLockedOutAsync` on the matched account before signing in and
    refuse with the same retry time. These refusals and failed provider credentials never call
    `AccessFailedAsync`.
- **Limitation** (superseded in rev. e): the memory throttle resets on restart and is per
  instance. The clarification of 2026-10-13 (FR-013) does not accept that; the throttle moves to
  Redis in R21.
- **Alternatives**: no lockout message at all (breaks FR-012); persisting throttle rows for unknown
  e-mails (writes attacker-chosen data to the DB).
- **Audit**: unknown-e-mail attempts are audited with `UserId = null` and no IP or device type
  (FR-014, R14); the e-mail itself is never stored or logged.

## R9. External providers (rev. f)

- **Flow** (client-supplied credential, no redirects through the API):
  1. The client signs in at the provider with the provider's own script and gets a credential:
     a Google **ID token** (Google Identity Services) or a Facebook **access token** (Facebook
     JavaScript SDK, scope `email`).
  2. It posts `POST /api/v1/auth/external/{provider}/login` with `{ token, acceptTermsAndAge?,
     acceptHealthData?, acceptMarketing?, email? }`
     and `X-Device-Id`.
  3. The `ExternalLogin` handler resolves the provider in `IExternalProviderCatalog` (`404
     Auth.ProviderUnavailable` if not configured) and calls the `IExternalIdentityVerifier` port.
     A credential that fails verification is audited as `ExternalLogin` `Failed` without user, IP
     or device, and answered `401 Auth.ExternalLoginFailed`. Then the account rules of R10 apply.
- **Google** (`GoogleIdTokenVerifier`, package `Google.Apis.Auth`):
  `GoogleJsonWebSignature.ValidateAsync(token, new ValidationSettings { Audience = [ClientId] })`
  checks the signature against Google's cached keys, the issuer and the expiry. `Audience` MUST be
  set: without it any Google ID token, including one issued to another application, would be
  accepted. The library does not enforce `email_verified`; a payload with `EmailVerified = false`
  is treated as "no e-mail" (FR-023 then asks for one) rather than refused. `ProviderKey` = `sub`.
  Only `ClientId` is configured: checking an ID token needs no secret.
- **Facebook** (`FacebookTokenVerifier`, typed `HttpClient`, no SDK):
  1. `GET https://graph.facebook.com/{version}/debug_token?input_token={token}&access_token={AppId}|{AppSecret}`;
     require `data.is_valid == true` and `data.app_id == AppId`. The app-id check is what stops a
     token issued to another Facebook app from signing in here.
  2. `GET /{version}/me?fields=id,name,email&access_token={token}&appsecret_proof={hex HMAC-SHA256(token, AppSecret)}`;
     require `id == data.user_id`. `email` is missing for accounts registered by phone (FR-023).

  A non-success status or a network error counts as a failed verification and is logged as a
  warning naming only the provider. `Authentication:Facebook:GraphApiVersion` defaults to `v26.0`
  (current in October 2026).
- **Port**: `Application/Common/Security/IExternalIdentityVerifier.VerifyAsync(loginProvider, token, ct)`
  returns `ExternalIdentity(Provider, ProviderKey, Email?, DisplayName?)` or `null`. Infrastructure
  registers a verifier per configured provider behind it. Application never sees provider HTTP or
  token formats (constitution Principle I).
- **Unconfigured providers**: a provider is registered only when its keys are present
  (`Authentication:Google:ClientId`; `Authentication:Facebook:AppId` and `AppSecret`).
  `GET /auth/external/providers` lists the registered ones with their public client id, which the
  client needs to start the provider's sign-in.
- **No server state between requests**: both credentials stay valid for at least an hour, so when
  the API answers "consents required" the client sends the same token again with them (R10). No
  cookie, no pending record, no `ExternalLogin:PendingTimeout`.
- **Replay**: a stolen ID or access token could sign its owner in until it expires. Taking it needs
  script access in the user's browser, which already exposes the in-memory access token. A
  server-issued nonce was considered and left out (extra round trip and short-lived state).
- **Twitter (X)**: deferred (spec clarification 2026-10-14). X has no ID token; its OAuth 2.0 code
  must be exchanged with the client secret, which needs a different request shape.
- **Testing**: no integration tests drive this endpoint (decided 2026-10-14); a fake verifier
  would only test the fake. The Facebook verifier's app-id and user-id checks are unit-tested
  against a stubbed `HttpMessageHandler`, and a development-only page (quickstart) runs the real
  provider sign-ins by hand.
- **Alternatives**: server-side redirect handlers with a temporary `Liftingo.External` cookie
  (rev. a–e; replaced: three handler packages, a cookie scheme, five endpoints, and a redirect to
  the frontend for every outcome); a pending token returned with "consents required" (not needed
  once X is out, because both remaining credentials can be sent twice).

## R10. Linking by e-mail and pre-hijacking protection (rev. f)

- **Decision** (FR-021, FR-022), applied by `ExternalLogin` after the credential is verified:

  | Situation | Required consents not both `true` | Both required consents `true` |
  |---|---|---|
  | Login `(provider, key)` already linked | sign in | sign in, consents ignored |
  | Linked, but the provider e-mail belongs to another account (FR-022a) | `409 Auth.ProviderEmailConflict` | same |
  | Provider e-mail matches a **verified** account | add the login, audit `ProviderLinked`, sign in | same, consents ignored |
  | Provider e-mail matches an **unverified** account (takeover) | `400 Auth.ConsentRequired`, `accountExists: true` | takeover (below), sign in |
  | No account, provider gave an e-mail | `400 Auth.ConsentRequired` | create the account, `EmailConfirmed = true`, welcome-only e-mail |
  | No account, provider gave no e-mail | `400 Auth.ConsentRequired`, `emailRequired: true` | with `email`: a new address creates an unverified account with the verification+welcome e-mail, an existing one goes to R17; without `email`: `400 Validation.EmailRequired` |

  A matched account that is locked out (FR-012) refuses with `429 Auth.LockedOut` before anything
  is written, in every row that signs in.
- **Takeover**: in one `SaveChangesAsync`: add the login, set `EmailConfirmed = true`, remove the
  password, mark every current `UserConsent` row superseded and insert three new ones, supersede
  open e-mail tokens. After the commit: revoke all sessions in Redis (the pre-hijacker loses
  access), audit `ProviderLinked`, sign the owner in.
- **Why consents before the takeover**: if the owner walks away at the consent step, nothing has
  changed and no provider data is kept (FR-021). Taking over first and asking afterwards could
  leave a verified account whose only consents were given by someone else, which constitution III
  does not allow. The unverified account keeps working for its holder until the owner completes
  the step; the 7-day unverified-account deletion (out of scope) bounds that.
- **Consents sent with a plain sign-in** are ignored, not recorded. The account already has its
  own, and recording them would add consent history the user did not knowingly change.
- **Consent fields** are nullable, as in `confirm-link` (R17): `acceptTermsAndAge`,
  `acceptHealthData`, `acceptMarketing`. A required one that is missing or `false` counts as
  not given, so the answer is `Auth.ConsentRequired`, never a field error. Marketing defaults to
  `false` (FR-004).
- **Superseded consents**: rows stay (append-only history, provable), with `SupersededAt` set.
  "Current consents" = rows with `SupersededAt IS NULL`. Account data is kept.
- **Shared helper**: `Features/Account/Shared/AccountTakeover.cs` is used by three slices:
  `ExternalLogin`, `ConfirmProviderLink` (both remove the password) and `ResetPassword` (keeps the
  new password, R7).
- Provider-supplied e-mails are treated as verified (spec assumption). An e-mail the user types
  (FR-023) is not: a new address gets the normal verification e-mail; an address that already has
  an account goes through R17.

## R11. E-mail sending abstraction and development output

- **Decision**: `Application/Common/Email/IEmailSender` with
  `Task SendAsync(EmailMessage message, CancellationToken ct)` and a positional record
  `EmailMessage(string To, string Subject, string HtmlBody, string TextBody)`.
  - `Infrastructure/Email/ConsoleEmailSender` writes recipient, subject and text body (with links)
    through a dedicated logger category `Liftingo.DevEmail`. It is registered **only** when
    `IHostEnvironment.IsDevelopment()` (FR-028).
  - In every other environment `DisabledEmailSender` is registered: it drops the message and logs a
    warning with the template name and user id only, no recipient (FR-028, FR-030). The calling
    flow still succeeds.
  - Integration tests replace it with `CapturingEmailSender` in `ApiFactory`.
- **Templates** (superseded in rev. e: the port now takes one record per e-mail and the templates
  live in Infrastructure, see R24): `Features/Account/Shared/AccountEmails.cs` builds the six Polish messages
  (verification+welcome, verification-only for resends (FR-018), welcome-only, password reset,
  new device, provider link confirmation) from plain C# string templates and the frontend base URL
  (`App:FrontendBaseUrl`). No templating engine. Verification+welcome goes to e-mail/password
  sign-ups and to provider sign-ups with a typed-in e-mail (FR-025).
- **Failures (FR-029)**: `AccountEmails` sends after the handler's `SaveChangesAsync` and catches
  exceptions, logging template name and user id only. Sending is inline: the console sender is
  instant, so SC-006 (≤ 5 s) holds. Hangfire/outbox is left to the Azure Communication Services
  feature.
- **Alternatives**: Hangfire job per e-mail (Hangfire is not set up yet; adds storage and a
  dashboard for a console logger); `Channel<T>` background queue (loses e-mails on restart for no
  current benefit).

## R12. Consents and document versions

- **Decision**: `UserConsent(Type, Granted, RecordedAt, DocumentVersion)`, one row per decision,
  three rows at every registration (marketing stored even when declined, so "declined" is
  provable). `DocumentVersion` is the single `Legal:DocumentsVersion` value, because Terms and
  Privacy Policy are shown and accepted together. Required consents are validated with
  FluentValidation (`Must(x => x)`) with the health-data message from US1-2.

## R13. Password policy errors

- **Decision**: FluentValidation checks `NotEmpty` and `MaximumLength(128)`
  (`Validation.PasswordTooLong`; Identity has no maximum-length option). Identity's password
  validators (driven by the existing `Identity:Password` config) run in `CreateAsync` /
  `ResetPasswordAsync`, and `IdentityService` maps their error codes (`PasswordTooShort`,
  `PasswordRequiresDigit`, `PasswordRequiresNonAlphanumeric`) to `ValidationError` entries with
  Polish messages naming the failed rule (US1-3).
- **Special character** (FR-002): Identity's `PasswordValidator.IsLetterOrDigit` is ASCII only
  (`A-Z`, `a-z`, `0-9`; checked against the v10.0.1 source), so by default a Polish letter such as
  `ą` would count as a special character. The spec says a special character is neither a letter
  nor a digit, so `Infrastructure/Identity/LiftingoPasswordValidator` subclasses
  `PasswordValidator<ApplicationUser>` and overrides the virtual `IsLetterOrDigit` with
  `char.IsLetterOrDigit`. It replaces the default validator in DI. Spaces and punctuation still
  count as special; Polish letters don't. Unit-tested with `zażółć12` (rejected) and `zażółć1!`
  (accepted).
- **E-mail input** (FR-005): every slice that takes an e-mail trims it before validation, then
  validates `EmailAddress()` and `MaximumLength(256)`. Identity's `NormalizedEmail` (upper-case
  invariant) handles case. No other normalisation.
- **Rationale**: one source of truth for the policy, and it stays configurable (FR-031).
- **Alternative**: duplicate the rules in FluentValidation (two places to keep in sync).


## R14. Security audit log (Log Analytics, crypto-shredding)

- **Decision**: audit events are not stored in SQL Server. Slices write them through an
  Application port `ISecurityAuditLog.WriteAsync(SecurityAuditEvent, ct)` with
  `SecurityAuditEvent(Guid? UserId, SecurityEventType Type, SecurityEventOutcome Outcome,
  DateTimeOffset OccurredAt, string? IpAddress, string? DeviceType)`. IP and device type are set
  only when `UserId` is set (FR-014). Infrastructure implements it in two steps:
  1. `EncryptingSecurityAuditLog` encrypts `IpAddress` and `DeviceType` with AES-GCM under the
     user's key and builds an `AuditRecord` (event id, user id in plain text, type, outcome, time,
     key id, nonce + ciphertext per field).
  2. It hands the record to an internal `IAuditSink`.
- **Sinks**: the production sink is Azure Monitor Log Analytics: a dedicated custom table (e.g.
  `LiftingoSecurityAudit_CL`) fed through the Logs Ingestion API, kept apart from application
  logs, with 90-day table retention (NFR-40). **It is not built in this feature**, the same way the
  Azure e-mail sender is not. This feature ships:
  - `ConsoleAuditSink`, registered only in Development, which writes the encrypted record to the
    console through its own Serilog logger, separate from the application's logging pipeline, so
    the audit stream stays apart from application logs even locally;
  - `DisabledAuditSink` everywhere else, which drops the record and logs a warning with the event
    type only;
  - `CapturingAuditSink` in integration tests.

  The app runs only locally for now. The Log Analytics sink has to exist before any non-local
  deployment (plan, constitution re-check note 2).
- **Per-user keys (crypto-shredding)**: `AuditEncryptionKey(Id, UserId, ProtectedKey, CreatedAt)`
  in SQL Server. The 256-bit key is wrapped with ASP.NET Core Data Protection (purpose
  `Liftingo.AuditKeys`). It is created lazily on the user's first audit event. `UserId` has an FK
  to `AspNetUsers` with **cascade delete**, so deleting the account deletes the key, and every
  audit row of that user becomes unreadable (US-18) with no extra step. The entity belongs to
  Infrastructure (an encryption detail), so it is a `DbSet` on `ApplicationDbContext` but not on
  `IApplicationDbContext`.
- **Events without a user** (login with an unknown e-mail, a provider sign-in that failed before
  any account was known): only event type, outcome and time are recorded, no IP and no device type
  (FR-014, decided in review). With no personal data there is nothing to encrypt, so there is no
  shared key and `AuditEncryptionKey.UserId` is non-nullable. Those records are erased by the
  90-day retention like the rest.
- **Export (US-37, later feature)**: a KQL query `where UserId == '<id>'`, then decryption in the
  app with the user's key. Ingestion latency (minutes) is fine for an export.
- **Data Protection keys must be durable**: if the Data Protection key ring is lost, every wrapped
  audit key is lost with it. This feature persists the key ring to SQL Server with
  `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (`PersistKeysToDbContext`).
- **Write order**: `WriteAsync` runs after the handler's SQL commit, as a separate step. A sink
  failure is caught and logged (event type and user id only) and never fails the request. The
  sinks in this feature (console, test capture) do no network I/O, so in practice every event is
  written. SC-005 ("100% of attempts produce a record") is checked at the sink boundary in tests.
  Delivery guarantees (batching, retry on network failure) are a design concern for the later
  Log Analytics sink.
- **Application log line**: each event also produces a Serilog `Information` entry with event
  type, outcome and user id only (no IP, no device), so normal log searches still show auth
  activity.
- **IP address**: `HttpContext.Connection.RemoteIpAddress`. Behind App Service this needs
  `ForwardedHeaders` middleware; added in this feature with known proxies from config, since audit
  correctness depends on it.
- **Alternatives**: SQL table `SecurityAuditEvents` (first plan revision; transactional, but
  replaced by Log Analytics, constitution v1.1.0); plain-text IP in Log Analytics + purge API
  (purges are rate-limited and can take up to 30 days, so they don't fit routine deletion);
  per-user keys in Key Vault (one secret per user and a network call per event).

## R15. Logging without personal data (FR-030, SC-005)

- **Decision**: handlers log user ids only. A new integration test runs every flow and asserts that
  no captured log event (via the existing `LogEventCollector`) contains the test e-mail, password,
  token, IP or the `@`-pattern of the test address, excluding the `Liftingo.DevEmail` category
  (which is never registered in `Testing`). As a safety net, `Serilog.Enrichers.Sensitive` masks
  e-mail addresses (built-in operator) and IP addresses (a custom `RegexMaskingOperator`) in the
  properties and rendered message of every log event, the development e-mail output included.
  It is configured in code, so configuration cannot turn it off. Identity's own log messages use user ids. Provider
  credentials are never logged; the verifiers log only the provider name on failure.

## R16. Client expectations (frontend is a later feature)

This feature delivers only the backend. The frontend will be specified and built later. The API
assumes the following client behaviour, and the frontend feature must implement it:

- Keep the access token in memory only; never put any token in `localStorage`,
  `sessionStorage` or a readable cookie (FR-008a, SC-010). SC-010 is verified in the frontend
  feature; the backend proves its part with tests on the `Set-Cookie` attributes.
- Generate a device id once, keep it in `localStorage` (`liftingo.deviceId`), and send it as
  `X-Device-Id` on register, login, provider login and link confirmation (R6).
- On start, call `POST /auth/session/refresh` to get an access token. Call it again right after a
  successful e-mail verification so the `email_verified` claim updates (FR-019).
- Serialise refresh calls across tabs with the Web Locks API (R4), refresh once on a 401, and retry
  the original call once. Never retry a refresh whose response was lost with the same cookie value
  blindly; a second use counts as reuse and ends the session (FR-011).
- Tell `429 Auth.LockedOut` (show the retry time) apart from `429 RateLimit.Exceeded` (generic
  "too many requests", honour `Retry-After`) by the `code` extension (R18).
- When refresh fails for network reasons (not a 401), stay signed in offline so offline logging
  keeps working (Principle V).
- Serve the SPA and API from the same site (dev proxy locally), so the `SameSite=Lax` cookie is
  sent (R5).
- Sign in at Google (Google Identity Services, client id from `GET /auth/external/providers`) or
  Facebook (JavaScript SDK, scope `email`) and post the credential to
  `POST /auth/external/{provider}/login`. Show the consents first on the registration page and
  send them along; from the login page send none, and on `400 Auth.ConsentRequired` show them
  (with an e-mail field when `emailRequired`) and resend the same credential with them
  ([contract](contracts/auth-api.md#external-providers)).
- Provide pages for the link-confirmation e-mail (R17) and the password-reset e-mail (R7) that
  show the consent form when the API answers `Auth.ConsentRequired`, and resubmit with the
  consents.

The NSwag client will be generated by the frontend feature from `/openapi/v1.json`. Until the
Angular workspace exists, the "regenerate after endpoint change" rule has nothing to act on.

## R17. Typed-in e-mail that already has an account (FR-023a)

A provider returned no e-mail, and the address the user typed with the consents belongs to an
existing account. Linking immediately would let anyone with a provider account take over any
Liftingo account by typing its address.

- **Decision**: `POST /auth/external/{provider}/login` detects the match and, instead of creating or
  linking anything:
  1. issues an `EmailToken` with purpose `ProviderLinkConfirmation` for the existing account,
     holding `LoginProvider` and `ProviderKey` (nothing else from the provider), lifetime
     `EmailTokens:ProviderLinkLifetime` (24 h), subject to the supersede rule and FR-037's limit;
  2. sends the link-confirmation e-mail (fifth template in `AccountEmails`) to the address;
  3. returns `202` with
     `{ "status": "linkConfirmationSent" }`. At the limit it returns the same `202` and sends
     nothing.

  The consents submitted with that request are not recorded: the account already has its own, and
  the person who opens the link may be someone else.
- **Confirming**: a new slice `ConfirmProviderLink.cs`, `POST /auth/external/confirm-link`,
  anonymous, body `{ token, acceptTermsAndAge?, acceptHealthData?, acceptMarketing? }`.
  1. Unusable token → `400 Auth.ProviderLinkInvalid`.
  2. The `(LoginProvider, ProviderKey)` pair is already linked to some account (the user finished
     another path meanwhile) → consume the token, `409 Auth.ProviderAlreadyLinked`.
  3. Account unverified and the two required consents not both `true` → `400
     Auth.ConsentRequired`; the token stays usable so the page can show the consent form and
     resubmit.
  4. Otherwise: add the login; if unverified, apply the R10 takeover (verify, remove password,
     supersede consents and insert the new ones, supersede e-mail tokens, revoke all sessions after
     the commit); consume the token; audit `ProviderLinked`; sign in on this device (`200` +
     `AuthSession` + cookie). Known-device and new-device e-mail rules are those of a normal
     sign-in (R6).
- **Why the token carries the provider key**: the provider credential is short-lived and stays in
  the browser that signed in, but the e-mail may be opened hours later on another device.
- **Response differs from a new address** (`201` signed in vs. `202` check inbox). This reveals
  that the address has an account. Accepted in the spec (FR-023a) because `POST /auth/register`
  already reveals it with `409 Auth.EmailTaken` (US1-4), and SC-008 covers login and reset only.
- **Alternatives**: `409` and "log in with your existing method" (leaves provider users with no
  way to link); always confirming by e-mail, even for new addresses (no enumeration, but needs a
  pending-registration state that stores consents before an account exists; rejected in the
  plan session for complexity, Principle VIII).

## R18. Rate limiting, transport, availability, retention and performance

- **Rate limiting** (FR-040): the API already has a global limiter
  (`Api/Extensions/RateLimitingExtensions.cs`): a token bucket partitioned per user id for
  authenticated requests and per IP for the rest (defaults: anonymous burst 20, then 5 per 10 s;
  authenticated burst 60, then 10 per 10 s), answering `429` with code `RateLimit.Exceeded` and
  `Retry-After`. Every auth endpoint is covered without changes; no endpoint-specific policies are
  added. Two adjustments:
  - `UseAuthentication()` must run **before** `UseRateLimiting()`; today the limiter runs with no
    authentication middleware, so every request falls into the IP partition. With the bearer
    validated first, signed-in calls get the per-user bucket as designed.
  - `refresh` and `logout` carry no bearer, so they use the IP bucket. Many users behind one NAT
    share it; accepted at thesis scale, and the limits are configuration.
- **HTTPS** (FR-039, PRD NFR-13): `UseHttpsRedirection` is already in the pipeline. This feature
  adds `UseHsts()` outside Development. The minimum TLS version (1.2) is an App Service setting and
  is recorded as a deployment requirement in the plan. The cookie is always `Secure` (R5).
- **Session store unavailable** (FR-015c): `RedisSessionStore` catches `RedisConnectionException`
  and `RedisTimeoutException` and throws an Application-level `SessionStoreUnavailableException`.
  An `IExceptionHandler` in `Api` maps it to `503` with code `Service.Unavailable` and a
  `Retry-After` of 30 s. This covers login, register, refresh, logout and the per-request session
  check in `OnTokenValidated`, which runs inside the same pipeline. Application does not reference
  StackExchange.Redis (architecture test).
  StackExchange.Redis 3.x changed the default `BacklogPolicy` to queue commands while
  disconnected until their async timeout (checked with Context7 at task generation). The
  multiplexer is configured with `BacklogPolicy.FailFast` (the 2.x behaviour) and
  `AbortOnConnectFail = false`, so an outage fails at once with a `503` instead of making every
  request wait out the timeout, and startup does not crash when Redis is down.
- **Retention** (FR-026, FR-038, NFR-40): known devices (12 months after `LastSeenAt`) and e-mail
  tokens (24 h after they stop being usable) are deleted by the retention work's daily jobs, which
  are out of scope like the audit purge. This feature adds the config keys, the timestamps and the
  indexes those jobs need. Sessions need no job: Redis TTLs delete them, and revocation deletes the
  keys.
- **Performance** (SC-011): p95 under normal load ≤ 1 s for login and registration, ≤ 300 ms for
  refresh. The expensive part is the password hash (Identity's PBKDF2, roughly 100 ms per check);
  refresh does no hashing; it runs one Redis script and one SQL read of the account (for the
  `email_verified` claim, FR-019). Checked manually before the feature is closed
  with an HTTP load tool against a local run (quickstart). No automated performance gate, the same
  as the PRD's NFR-01.

## R19. What moves from Application to Api (rev. e)

Constitution v2.0.0 Principle I: Application holds no HTTP concerns, and `Api` owns endpoints,
routes, `Result` mapping, cookies, redirects and the `HttpContext`-based user and client info.
Today 33 Application files import ASP.NET Core or `IMemoryCache`.

- **Decision**: each HTTP concern moves to `Api` as an adapter. The handler receives plain values
  and returns plain values.

  | Today (Application) | After (Api) | What Application gets or returns instead |
  |---|---|---|
  | `Common/Routes/IEndpoint.cs`, `RouteConsts.cs` | `Endpoints/IEndpoint.cs`, `Endpoints/RouteConsts.cs` | nothing |
  | `Common/Result/ResultExtensions.cs` (`ToProblem`, `RetryAfterProblemResult`) | `Http/ResultExtensions.cs` | `Result<T>`; `LockedOutError` and `ErrorType` stay in Application |
  | `Shared/RefreshTokenCookie.cs` (read, append, delete) | `Endpoints/Account/Shared/RefreshTokenCookie.cs` | input: `string? RefreshToken` on the command; output: `IssuedSession` (R22) |
  | `Shared/ClientInfo.From(HttpContext)` | `Endpoints/Account/Shared/ClientInfoReader.cs` | `ClientInfo` stays in Application with a pure factory `ClientInfo.Create(ip, userAgent, deviceIdHeader)`, so the R6 parsing rules and `DeviceDescriptor` stay unit-tested where they are |
  | `Shared/PendingExternalLogin.ReadAsync/ClearAsync` (auth cookie) | removed in rev. f (R9) | |
  | `Shared/FrontendRedirects.cs` | removed in rev. f (R9) | |
  | `SessionsOptions.CookiePath` | `RefreshCookieOptions.Path` in Api, set from the version prefix | `SessionsOptions` keeps the two lifetimes |
  | `Infrastructure/Identity/UserContext.cs` (`IHttpContextAccessor`) | `Api/Security/UserContext.cs` | `IUserContext` port unchanged |

- `AppOptions.FrontendBaseUrl` is needed by Infrastructure to build e-mail links (R24). It moves to `Application/Common/Configuration/AppOptions.cs`
  and is validated once at startup. It is a plain options class, so Application gains no
  dependency.
- **Alternatives**: an `IHttpResponseWriter` port that handlers call to set cookies (keeps HTTP
  semantics in the handler under another name); an `ICurrentClient` port for `ClientInfo` (hides a
  real input of the use case and gives jobs a port they can't fill). Both were rejected.

## R20. Authentication schemes registered by Api (rev. e)

Principle I gives `Api` "authentication schemes and authorization policies" and Infrastructure
"Identity and the external auth providers". Today `InfrastructureExtensions` does both, and the
scheme and path constants (`AuthenticationSchemes`, `ExternalLoginPaths`) sit in Application
because both layers need them.

- **Decision**: `Api/Extensions/AuthenticationExtensions.cs` calls `AddAuthentication`, registers
  the JWT bearer scheme, and sets the authorization defaults. Infrastructure contributes the
  `IConfigureNamedOptions<JwtBearerOptions>` that sets the signing key, issuer and audience from
  `JwtOptions` and hooks `SessionValidation` into `OnTokenValidated`. Since rev. f there are no
  provider schemes: the provider verifiers (R9) are plain Infrastructure services registered by
  `AddInfrastructure`.
- **Rationale**: Api decides which schemes exist and how requests are authenticated; Infrastructure
  owns everything that talks to Identity, providers or token signing. Calling an Infrastructure
  extension method from `Program`/`Extensions` is DI wiring, which Principle I allows.
- **Alternatives**: leave registration in Infrastructure (breaks Principle I's split).

## R21. Unknown-e-mail lockout in Redis (rev. e, FR-013)

The clarification of 2026-10-13 requires the counter and lockout for unregistered addresses to
live in the shared session store, never in process memory.

- **Decision**: port `Application/Common/Security/ILoginThrottle.cs` with
  `GetLockoutEndAsync(email, ct)` and `RegisterFailureAsync(email, ct)`, returning the same
  `LockoutTriggered` result as today. `Infrastructure/Sessions/RedisLoginThrottle.cs` implements it
  on the session store's multiplexer:
  - key `liftingo:login-throttle:{hex SHA-256 of trimmed, upper-cased e-mail}`, a hash with
    `failures` and `lockoutEnd` (ISO 8601). The address itself is never stored (FR-030);
  - `RegisterFailureAsync` runs one Lua script: refuse if `lockoutEnd` is still in the future;
    otherwise clear `lockoutEnd`, increment `failures`, and when it reaches
    `MaxFailedAccessAttempts`, set `failures = 0`, `lockoutEnd = now + Duration` and
    `PEXPIRE key Duration`. The current time is passed in from `TimeProvider`, so tests keep using
    the fake clock;
  - an entry with `failures > 0` and no lockout has no TTL: the counter does not decay (FR-012).
    An entry expires only when its counter is zero and its lockout has ended, as the spec states.
    Growth is bounded by the global rate limiter (FR-040);
  - Redis connection and timeout errors become `SessionStoreUnavailableException` → `503`, like
    every other session operation (FR-015c).
- The rules (refuse during lockout without counting, no decay, reset on trigger) are unchanged from
  R8; only the store changes. `LoginLockoutOptions` still comes from `Identity:Lockout`.
- **Tests**: an integration test locks out an unknown address, builds a second `ApiFactory` on the
  same Redis container (a restart or a second instance), and checks that the sixth attempt is
  still refused with the same retry time. The existing unit tests of the rules move to an
  integration test against the Redis container, since the logic now lives in the script.
- **Alternatives**: memory (rejected by the clarification); a SQL table (a database write on
  every failed login; rejected in the clarification).

## R22. Account slices as commands and queries (rev. e)

- **Decision**: 13 endpoints map to 13 slices (17 and 16 before rev. f) under `Application/Features/Account/{UseCase}/`, one
  type per file (`{UseCase}Command`/`Query`, `…CommandHandler`/`QueryHandler`,
  `…CommandValidator` where the input has rules, `{UseCase}Response`):

  | Endpoint | Slice | Kind | Validator |
  |---|---|---|---|
  | `POST /auth/register` | `Register` | command | yes |
  | `POST /auth/login` | `Login` | command | yes |
  | `GET /auth/me` | `GetCurrentAccount` | query | no |
  | `POST /auth/session/refresh` | `RefreshSession` | command (rotates, may audit reuse) | no |
  | `POST /auth/session/logout` | `Logout` | command | no |
  | `POST /auth/session/logout-all` | `LogoutAllDevices` | command | no |
  | `POST /auth/email/verify` | `VerifyEmail` | command | yes |
  | `POST /auth/email/resend-verification` | `ResendVerificationEmail` | command | no |
  | `POST /auth/password/forgot` | `ForgotPassword` | command | yes |
  | `POST /auth/password/reset` | `ResetPassword` | command | yes |
  | `GET /auth/external/providers` | `GetExternalProviders` | query | no |
  | `POST /auth/external/{provider}/login` | `ExternalLogin` | command | yes |
  | `POST /auth/external/confirm-link` | `ConfirmProviderLink` | command | yes |

  The validator column follows the validators that exist today; the implementation keeps any
  validator a slice already has.
- **Inputs from HTTP**: a command carries what Api read from the request: `ClientInfo Client`,
  `string? RefreshToken`. The signed-in user comes from
  `IUserContext`, as today.
- **Sessions in the output**: `SessionIssuer.IssueAsync` returns
  `IssuedSession(AccessToken, AccessTokenExpiresAt, AccountSummary Account, RefreshToken,
  RefreshTokenLifetime)` instead of writing the cookie. Every signing-in response
  (`RegisterResponse`, `LoginResponse`, `RefreshSessionResponse`, `ExternalLoginResponse`,
  `ConfirmProviderLinkResponse`) holds an `IssuedSession`. Api writes the cookie and returns the
  unchanged `AuthSessionResponse` body. That body type lives in `Api/Endpoints/Account/Shared/`:
  it is the HTTP contract several endpoints share (contract unchanged), not an Application
  response, so the "response DTOs are not shared" rule holds.
- **Provider login outcomes** (rev. f): `ExternalLoginResponse(Outcome, IssuedSession?)` with
  `Outcome` = `AccountCreated` | `SignedIn` | `LinkConfirmationSent`. Refusals are `Result` errors
  returned as problem bodies: `LockedOutError`, `ConsentRequiredError` (carries `accountExists`
  and `emailRequired`, which Api adds as extensions), and the `ExternalLoginErrors` codes
  `Auth.ExternalLoginFailed`, `Auth.ProviderEmailConflict` and `Auth.ProviderUnavailable`.
- **Cookie deletion**: decided by the endpoint from the result: logout and logout-all always
  delete the refresh cookie; refresh deletes it on any failure; reset deletes it on success.
- **Shared helpers** stay in `Features/Account/Shared/` as concrete classes the handlers call
  explicitly: `SessionIssuer` (without the cookie), `SecurityAudit`, `EmailTokenIssuer`,
  `ConsentRecorder`, `AccountTakeover`, `DeviceDescriptor`, `ClientInfo`,
  `AccountValidationRules`. The e-mail records of R24 live in `Shared/Emails/`.
- **Interfaces**: `Application/Common/Messaging/` holds `ICommand`, `ICommand<TResponse>`,
  `IQuery<TResponse>`, `ICommandHandler<TCommand>` (returns `Result`),
  `ICommandHandler<TCommand, TResponse>` and `IQueryHandler<TQuery, TResponse>` (both return
  `Result<TResponse>`). Every command, query and handler implements the matching one. They fix
  the handler signature; they are not a mediator, and nothing dispatches through them.
- **Registration**: `AddApplication` scans the Application assembly and registers every handler
  against the handler interface it implements, and every `IValidator<T>` implementation as its
  concrete type, all scoped. Endpoints inject the handler interface, e.g.
  `ICommandHandler<LoginCommand, LoginResponse>`. Nothing else is registered against these
  interfaces: no decorators. It uses only `Microsoft.Extensions.DependencyInjection.Abstractions`
  (no Scrutor, no FluentValidation DI package).

## R23. Validation, transactions, logging and audit inside the handler (rev. e)

- **Decision**: the validator is injected (no more `static readonly s_validator = new()`) and
  called first in `HandleAsync`; a failure returns `ValidationError` as today. The handler keeps
  its explicit `SaveChangesAsync` (and the explicit transaction in `ResetPassword`), calls
  `SecurityAudit.WriteAsync` and the e-mail port itself, in this order: save, issue the Redis
  session, audit, e-mail (R4, R14, FR-029). Nothing moves into endpoint filters.
- **Why the order is unchanged**: the existing handlers already save before touching Redis, audit
  after the commit (R14) and send e-mails last. The refactor keeps that and only removes HTTP
  calls from between the steps.

## R24. E-mail records, templates in Infrastructure, display time zone (rev. e)

- **Port**: `Application/Common/Email/IEmailSender.SendAsync(OutgoingEmail email, ct)`.
  `OutgoingEmail` is an abstract record with `UserId` and `To`. The Account module's records live
  in `Features/Account/Shared/Emails/`:
  `VerificationAndWelcomeEmail(UserId, To, Token)`, `VerificationEmail(UserId, To, Token)`,
  `PasswordResetEmail(UserId, To, Token)`,
  `ProviderLinkConfirmationEmail(UserId, To, Provider, Token)`, `WelcomeEmail(UserId, To)`,
  `NewDeviceEmail(UserId, To, SignedInAt, DeviceType)`. They hold data only: no subject, no
  HTML, no link, no time zone.
- **Infrastructure/Email/**:
  - `EmailSender : IEmailSender` renders the record, hands the `EmailMessage` to the transport, and
    owns the safe send of FR-029: it catches every exception and logs the e-mail type and user id
    only. The port's contract says it never throws (except on cancellation), so handlers don't
    wrap the call;
  - `AccountEmailRenderer` holds the six Polish templates from today's `AccountEmails`, text and
    HTML, with HTML encoding, and builds links from `AppOptions.FrontendBaseUrl` (R19);
  - `IEmailTransport` with `ConsoleEmailTransport` (Development only) and `DisabledEmailTransport`
    (everywhere else, logs type and user id). These are today's `ConsoleEmailSender` and
    `DisabledEmailSender` renamed; FR-028 is unchanged. `EmailMessage` moves here, since only
    Infrastructure uses it.
- **Display time zone** (FR-026a): `EmailOptions.DisplayTimeZone` (section `Email`, default
  `Europe/Warsaw`). Its validator resolves the id with `TimeZoneInfo.FindSystemTimeZoneById`, and
  `ValidateOnStart` makes an unknown zone stop the app at startup. The silent UTC fallback is
  removed. .NET resolves IANA ids on Windows (through ICU) and on Linux App Service (tzdata), so the
  same id works locally and in production. `NewDeviceEmail.SignedInAt` is the UTC time from
  the handler's `TimeProvider`; the renderer converts it, so DST is applied by the zone rules.
- **Tests**: `CapturingEmailSender` in `ApiFactory` becomes `CapturingEmailTransport`, so the
  integration tests still see the rendered subject, body and links. New unit tests for the
  renderer cover the time zone (a summer and a winter timestamp), HTML encoding of the provider
  name and device type, and link building. A startup test checks that an unknown zone fails
  options validation.
- **Alternatives**: one port method per e-mail type (`IAccountEmails.SendWelcomeAsync`, …: grows
  with every module); a templating engine such as Razor or Scriban (constitution VIII, six short
  templates).

## R25. Application package allowlist and architecture tests (rev. e)

- **Decision** (`Liftingo.Application.csproj`): remove `<FrameworkReference
  Include="Microsoft.AspNetCore.App" />`. Add `PackageReference`s for
  `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`
  and `Microsoft.Extensions.Options`, which came through the framework reference before. Keep
  `Microsoft.EntityFrameworkCore` and `FluentValidation`. Versions go in
  `Directory.Packages.props`, pinned to the 10.0.x patch that EF Core 10.0.11 already pulls in
  (*verify* with `dotnet list package --include-transitive` so there is no downgrade warning).
- **Architecture tests** (`Liftingo.ArchitectureTests`, new `LayerResponsibilityTests.cs`):
  1. The Application assembly's referenced assemblies are a subset of: `Liftingo.Domain`,
     `System.*`, `netstandard`, `Microsoft.EntityFrameworkCore` (+ `.Abstractions`),
     `Microsoft.Extensions.DependencyInjection.Abstractions`,
     `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`,
     `Microsoft.Extensions.Primitives` (pulled in by Options), `FluentValidation`. Checked with
     `Assembly.GetReferencedAssemblies()`, which sees what the compiled code actually uses. The
     exact transitive names are *verify* on the first build.
  2. Application types do not depend on `Microsoft.AspNetCore.*`, `Microsoft.Extensions.Caching.*`,
     `StackExchange.Redis` or `Microsoft.EntityFrameworkCore.SqlServer`.
  3. Application has no type whose name ends in `Endpoint`; every handler sits in a
     `Features.{Module}.{UseCase}` namespace. A `…CommandHandler` implements `ICommandHandler`, a
     `…QueryHandler` implements `IQueryHandler`, a `…Command` implements `ICommand` and a `…Query`
     implements `IQuery`, and nothing else does.
  4. A handler does not depend on another handler, by concrete type or by handler interface.
  5. `Api` types other than `Program` and the classes in `Liftingo.Api.Extensions` do not depend on
     `Liftingo.Infrastructure` (DI wiring only), and no `Api` type depends on
     `IApplicationDbContext`.
  The existing `DependencyGuardTests` stay.
