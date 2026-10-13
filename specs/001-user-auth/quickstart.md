# Quickstart: Validating User Authentication (backend)

**Feature**: `001-user-auth` | **Plan**: [plan.md](plan.md) | **Contract**: [contracts/auth-api.md](contracts/auth-api.md)

This guide proves the backend works end to end. It covers setup, automated checks, and manual
scenarios mapped to the spec's user stories. The frontend is a later feature, so the manual checks
use `backend/src/Liftingo.Api/Liftingo.Api.http` (or any HTTP client that keeps cookies). Endpoint
shapes are in the contract and are not repeated here.

## Prerequisites

- .NET 10 SDK
- Docker Desktop (Redis for `dotnet run`; SQL Server and Redis containers for integration tests)
- SQL Server LocalDB (for `dotnet run`)
- Optional, for US5: developer apps at Google, Meta (Facebook) and X with redirect URIs
  `https://localhost:<api-port>/signin-google`, `/signin-facebook`, `/signin-x`, and "Request email
  from users" enabled on the X app

## One-time setup

```bash
cd backend
docker compose up -d     # starts Redis (backend/docker-compose.yml, added by this feature)

dotnet user-secrets --project src/Liftingo.Api set "Jwt:SigningKey" "<at least 32 random bytes, base64>"

# optional, per provider you want to try
dotnet user-secrets --project src/Liftingo.Api set "Authentication:Google:ClientId" "<id>"
dotnet user-secrets --project src/Liftingo.Api set "Authentication:Google:ClientSecret" "<secret>"
dotnet user-secrets --project src/Liftingo.Api set "Authentication:Facebook:AppId" "<id>"
dotnet user-secrets --project src/Liftingo.Api set "Authentication:Facebook:AppSecret" "<secret>"
dotnet user-secrets --project src/Liftingo.Api set "Authentication:X:ClientId" "<id>"
dotnet user-secrets --project src/Liftingo.Api set "Authentication:X:ClientSecret" "<secret>"

dotnet ef database update --project src/Liftingo.Infrastructure --startup-project src/Liftingo.Api
```

Unconfigured providers are simply missing from `GET /api/v1/auth/external/providers`.

## Automated checks

```bash
cd backend
dotnet build
dotnet test tests/Liftingo.Api.UnitTests          # domain entities, validators, device classifier, audit encryption, options
dotnet test tests/Liftingo.ArchitectureTests      # layer rules still hold
dotnet test tests/Liftingo.Api.IntegrationTests   # every slice against SQL Server + Redis containers
```

Expected: all green. Integration tests use `CapturingEmailSender`, `CapturingAuditSink`, a fake
external auth handler and a fake `TimeProvider`, so they need no provider credentials and test
expiry without waiting. The exception is Identity's own lockout, which uses the system clock;
those tests edit `LockoutEnd` directly. Redis key TTLs are asserted directly instead of waiting 30 days.

Coverage that must exist (by spec success criterion):

| Criterion | Proven by |
|---|---|
| SC-002, FR-010a | `RefreshSessionTests`: refresh resets the session TTL; a missing session key → 401; near the 90-day cap the TTL and cookie `Max-Age` are capped, and after it refresh → 401 even with daily renewals |
| SC-003 | `LogoutTests`: refresh and bearer use both rejected after logout |
| SC-004, FR-012 | `LoginTests`: 6th attempt → 429 with `retryAfter` 15 min ahead; after `LockoutEnd` is moved into the past in the database (Identity reads `DateTimeOffset.UtcNow`, not the fake clock), login works and the counter is 0; the unknown-e-mail throttle is driven by the fake clock; attempts during the lockout (even with the right password) are not counted and don't move `retryAfter`. `HandleExternalLoginCallbackTests` / `ConfirmProviderLinkTests`: a locked account is refused with the same retry time and the counter is unchanged |
| SC-005 | `SecurityAuditTests` (one captured record per attempt/event, IP and device decrypt with the user's key; unknown-e-mail and failed-provider records have no user, IP or device) and `LogPrivacyTests` (no e-mail, password, token or IP in captured logs) |
| SC-006 | `RegisterTests` etc.: the captured e-mail exists when the response returns |
| SC-007 | `RegisterTests` and `CompleteExternalLoginTests`: missing consent → no user row; success → 3 consent rows with version |
| SC-008 | `LoginTests` / `ForgotPasswordTests`: identical status and body for known and unknown e-mail, including lockout |
| SC-009 | `ResetPasswordTests`: old password fails, new works, old sessions (including the caller's) rejected, no `Set-Cookie` on the reset response |
| FR-035 (unverified) | `ResetPasswordTests`: without consents → `400 Auth.ConsentRequired`, password unchanged, token still usable; with consents → verified, old consents superseded, 3 new rows, verification tokens superseded |
| FR-015a, FR-035 known devices | `LogoutAllDevicesTests`, `ResetPasswordTests`: `KnownDevices` rows gone; the next login from the old device sends a new-device e-mail |
| FR-017 | `VerifyEmailTests`: second use of a token → `200 alreadyVerified`; a superseded token of an unverified account → `400` |
| FR-022a | `HandleExternalLoginCallbackTests`: linked login returning account B's e-mail → redirect `error=emailConflict`, both accounts unchanged; an e-mail of no account → normal sign-in, e-mail unchanged |
| FR-020 | `HandleExternalLoginCallbackTests`: provider failure → redirect `error=external`, nothing persisted, one audit record without user |
| FR-015c | `SessionStoreUnavailableTests`: Redis container paused → login, refresh and `GET /auth/me` return `503 Service.Unavailable` |
| FR-040 | the existing `RateLimitingTests` (global limiter); plus one test that a signed-in auth call uses the per-user bucket (authentication runs before the limiter) |
| FR-015b | `LogoutTests`, `LogoutAllDevicesTests`, `ResetPasswordTests`, `RefreshSessionTests` (reuse): an unexpired access token of the revoked session gets `401` on `GET /auth/me` |
| FR-022 takeover | `CompleteExternalLoginTests`: unverified match → consent step required; afterwards old consents have `SupersededAt`, 3 new rows exist, old password and sessions fail. Cancel at the consent step → account unchanged |
| FR-023a | `CompleteExternalLoginTests` (typed e-mail of an existing account → `202`, token holds provider + key, no user or login added) and `ConfirmProviderLinkTests` (link, `ConsentRequired` keeps the token, takeover on unverified, `ProviderAlreadyLinked`, limit of 3 per hour) |
| SC-010 (backend part) | `RegisterTests` / `LoginTests`: token only in the body; `Set-Cookie` has `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/api/v1/auth/session`. The browser-side check belongs to the frontend feature. |
| US-18 groundwork | `AuditEncryptionTests`: after the user row is deleted, the key row is gone and the captured records can no longer be decrypted |

## Run locally

```bash
cd backend && dotnet run --project src/Liftingo.Api
```

In the console:

- development e-mails appear under the `Liftingo.DevEmail` category, with recipient, subject and
  links (FR-028);
- audit records appear on lines prefixed `[AUDIT]` (a separate logger, not the application log),
  with IP and device type encrypted;
- e-mail and IP addresses in application log lines, the development e-mails included, show up
  masked by the Serilog masking enricher.

## Manual scenarios

Run them from `Liftingo.Api.http`. Send `X-Device-Id` with a fixed UUID per "device"; change it to
simulate a second device. E-mail links point to the future frontend; copy the `token` query value
into the matching API request.

1. **Register (US1, US7-1)**: register with a valid password and both consents. Expect `201`, an
   access token in the body, the `liftingo_rt` cookie, exactly one verification+welcome e-mail in
   the console and no new-device e-mail. With `acceptHealthData: false` → `400` with the
   body-weight explanation. With password `abcdefgh` → errors naming the missing digit and special
   character.
2. **Refresh (US2-2)**: call `POST /auth/session/refresh` with the cookie. Expect a new access
   token and a rotated cookie. Replay the *previous* cookie value → `401`, and the session is gone
   (the newer cookie now also fails): reuse detection.
3. **Verify e-mail (US4)**: on a fresh account, resend twice, then use the older token → `400
   Auth.VerificationLinkInvalid` (superseded). Post the newest token → `200 verified`. Again →
   `200 alreadyVerified`. The resent e-mails have no welcome text.
4. **Lockout (US2-4/5)**: wrong password 5 times → `429` with `retryAfter`. Repeat with an
   unregistered e-mail → the same responses.
5. **New device (US7-3/4)**: log in with a new `X-Device-Id` → one new-device e-mail. Log in again
   with it → no second e-mail.
6. **Logout (US3)**: log in as two devices. Logout with device A's cookie → A's refresh and bearer
   fail, B still works. `logout-all` with B's token → B fails too. Log in again as B → a
   new-device e-mail, because logout-all cleared the known devices.
7. **Password reset (US6)**: `forgot` for a registered and an unregistered e-mail → identical
   `202`, one e-mail only. Reset with the token → `204` with no `Set-Cookie`; every old session
   (refresh and bearer) rejected, old password fails, new succeeds. Four `forgot` calls within an
   hour → three e-mails. On an **unverified** account: reset without consents → `400
   Auth.ConsentRequired`; resend with both consents → `204`, and `GET /auth/me` after login shows
   `emailVerified: true`.
8. **External provider (US5, US7-2)**: with Google configured, open
   `https://localhost:<port>/api/v1/auth/external/google/start?deviceId=<uuid>` in a browser. A
   new address redirects to `{Frontend}/auth/consent` (the page will 404 until the frontend
   exists; that's expected). From the same browser session call `GET /auth/external/pending`, then
   `POST /auth/external/cancel` → nothing in `AspNetUsers`. Repeat and `complete` instead →
   account created, verified, one welcome-only e-mail. Starting again with Google → redirect to
   `/auth/callback` with a refresh cookie set, no consent step.
9. **Pre-hijack protection (FR-022)**: register with e-mail/password using your Google address
   without verifying, then sign in with Google using that address. Expect a redirect to
   `/auth/consent`, and `GET /auth/external/pending` → `accountExists: true`. `cancel` → nothing
   changed, the password still works. Repeat and `complete` with both consents → `200`; linked,
   verified, the password no longer works, the first session is revoked, the old consent rows are
   superseded and three new ones exist.
10. **Typed-in e-mail of an existing account (FR-023a)**: needs a provider that returns no e-mail
    (with real providers, use a Facebook account registered by phone; otherwise rely on
    `ConfirmProviderLinkTests`). At the consent step, enter the e-mail of an existing account →
    `202 linkConfirmationSent` and a link-confirmation e-mail in the console. Post the token to
    `/auth/external/confirm-link` → `200`, signed in, provider listed in `account.providers`.

11. **Performance (SC-011)**: with any HTTP load tool against a local Release run with Redis and
    LocalDB, run 1 minute of logins with a fixed account and 1 minute of refreshes. Expect p95 ≤ 1 s
    for login and ≤ 300 ms for refresh. Raise the `RateLimiting:Anonymous` limits for the run, or
    the limiter answers `429` first. This is a manual check, not a CI gate.

E-mail failure (FR-029) has no manual scenario: `RegisterTests` and `ForgotPasswordTests` swap in
a throwing `IEmailSender` and assert the request still succeeds and the log entry holds only the
template name and user id.
