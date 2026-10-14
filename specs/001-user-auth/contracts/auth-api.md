# Contract: Authentication API (v1)

**Feature**: `001-user-auth` | **Plan**: [../plan.md](../plan.md) | **Data model**: [../data-model.md](../data-model.md)

Base path: `/api/v1`. Route templates in `RouteConsts` omit the prefix (e.g. `/auth/login`). All
errors are RFC 9457 `ProblemDetails` with `code` and `traceId` extensions; validation errors add
`errors: [{ code, description }]` (existing `ResultExtensions.ToProblem`). User-facing descriptions
are Polish. The generated OpenAPI document `/openapi/v1.json` is the source of truth once
implemented; this file fixes the shape before then. The frontend is a later feature; what the API
expects from a client is listed in [research R16](../research.md#r16-client-expectations-frontend-is-a-later-feature).

Sessions and refresh tokens are kept in Redis and audit events go to the encrypted audit stream
([data model](../data-model.md)); neither changes the HTTP shapes below.

**Rev. e (2026-10-13)**: no route, status code, header, cookie or body changes. Each heading now
names the `Api` endpoint (`Liftingo.Api/Endpoints/Account/{UseCase}Endpoint.cs`) and the
Application slice folder it calls (research R19, R22). `ResultExtensions.ToProblem` moved to
`Liftingo.Api/Http/` with the same output. The 2026-10-13 clarifications change behaviour only
where noted: the `429 Auth.LockedOut` for an unknown e-mail now holds across restarts and
instances (FR-013), and e-mails are sent after the save (FR-029).

**Rev. f (2026-10-14)**: provider sign-in uses a credential the client gets from the provider
(research R9). `start`, `callback`, `pending`, `complete` and `cancel` are replaced by `POST
/auth/external/{provider}/login`; `GET /auth/external/providers` returns client ids; the
`Liftingo.External` cookie, the frontend redirects and `Auth.ExternalLoginExpired` are gone.
Twitter (X) is deferred.

## Conventions

| Item | Value |
|---|---|
| Access token | `Authorization: Bearer <jwt>`; returned in the body only |
| Transport | HTTPS only (TLS 1.2+); HSTS outside Development (FR-039) |
| Refresh cookie | `liftingo_rt`; `HttpOnly; Secure; SameSite=Lax; Path=/api/v1/auth/session; Max-Age=<session's remaining TTL, ≤ 2592000>` (never past the 90-day absolute lifetime, FR-010a) |
| Rate limit | every endpoint is under the existing global limiter: per IP without a bearer, per user with one (FR-040) |
| Device header | `X-Device-Id: <uuid>` on register, login, provider login, confirm-link (optional; a missing or invalid value counts as a new device) |
| Auth required | 🔒 = valid bearer token with an active session. A token whose session was revoked is rejected with `401` even before it expires (FR-015b) |

Shared response body `AuthSession` (returned by every endpoint that signs a user in, together with
a `Set-Cookie: liftingo_rt=...`):

```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2026-10-12T10:15:00Z",
  "account": {
    "id": "8f0d...",
    "email": "anna@example.pl",
    "emailVerified": false,
    "hasPassword": true,
    "providers": ["Google"]
  }
}
```

JWT claims: `sub` (user id), `sid` (session id), `email_verified` (`true`/`false`), `jti`, `iat`,
`exp`, `iss`, `aud`.

Shared error codes:

| Code | HTTP | Meaning |
|---|---|---|
| `Validation.*` | 400 | field errors (see each endpoint) |
| `Auth.InvalidCredentials` | 401 | wrong e-mail or password, unknown e-mail, provider-only account (same body in all cases) |
| `Auth.LockedOut` | 429 | `retryAfter` (ISO timestamp) extension + `Retry-After` header; identical for registered and unregistered e-mails |
| `Auth.SessionInvalid` | 401 | refresh cookie missing, unknown, revoked, expired or reused |
| `Auth.EmailTaken` | 409 | registration with an existing e-mail |
| `Auth.VerificationLinkInvalid` | 400 | unknown token, or an expired or superseded token of a still-unverified account |
| `Auth.ResetLinkInvalid` | 400 | used, expired, superseded or unknown reset token |
| `Auth.ProviderLinkInvalid` | 400 | used, expired, superseded or unknown link-confirmation token |
| `Auth.ConsentRequired` | 400 | provider login that would create or take over an account, or link confirmation or password reset for an unverified account, sent without both required consents; the token stays usable |
| `Auth.ProviderAlreadyLinked` | 409 | the provider identity in a link-confirmation token is already linked to an account |
| `Auth.ExternalLoginFailed` | 401 | provider credential invalid, expired or issued for another application |
| `Auth.ProviderEmailConflict` | 409 | provider login linked to one account returns another account's e-mail |
| `Auth.ProviderUnavailable` | 404 | provider not configured |
| `RateLimit.Exceeded` | 429 | existing global limiter, `Retry-After` header; distinct from `Auth.LockedOut` by `code` |
| `Service.Unavailable` | 503 | session store (Redis) unreachable; `Retry-After: 30` (FR-015c). Any endpoint that issues, renews, revokes or checks a session, including every 🔒 endpoint |

---

## Registration and login

### `POST /auth/register` — `RegisterEndpoint` → `Features/Account/Register/`

US1, FR-001…006, FR-016, FR-025.

Request:

```json
{
  "email": "anna@example.pl",
  "password": "Sztanga1!",
  "acceptTermsAndAge": true,
  "acceptHealthData": true,
  "acceptMarketing": false
}
```

Validation: `email` trimmed, then required, valid format, ≤ 256 chars; `password` required, ≤ 128
chars (`Validation.PasswordTooLong`; the other policy rules are enforced by Identity and reported
as `Validation.PasswordTooShort`,
`Validation.PasswordRequiresDigit`, `Validation.PasswordRequiresSpecial`);
`acceptTermsAndAge` must be `true` (`Validation.TermsRequired`); `acceptHealthData` must be `true`
(`Validation.HealthDataRequired`, message explains body weight is needed for safe starting loads).

Responses: `201 Created` + `AuthSession` + cookie · `400` · `409 Auth.EmailTaken` (also for the
loser of two simultaneous registrations of the same address).

Side effects: user, three `UserConsent` rows, session, refresh token, `KnownDevice` (no new-device
e-mail), verification token; then one verification+welcome e-mail.

### `POST /auth/login` — `LoginEndpoint` → `Features/Account/Login/`

US2, FR-007, FR-008, FR-012…014, FR-026.

Request: `{ "email": "anna@example.pl", "password": "Sztanga1!" }` (e-mail trimmed)

Responses: `200` + `AuthSession` + cookie · `400` (empty fields) · `401 Auth.InvalidCredentials` ·
`429 Auth.LockedOut`. During a lockout the password is not checked and the attempt is not counted.

Side effects: audit `Login` (always; for an unknown e-mail without user, IP or device type);
`LockoutTriggered` on the 5th failure; session + token on success; new-device e-mail if the device
is unknown, otherwise `KnownDevice.LastSeenAt` updated.

### `GET /auth/me` 🔒 — `GetCurrentAccountEndpoint` → `Features/Account/GetCurrentAccount/`

FR-019. Response `200`: the `account` object from `AuthSession`.

---

## Session (cookie endpoints)

All three live under `/auth/session/` so the cookie path covers exactly them.

### `POST /auth/session/refresh` — `RefreshSessionEndpoint` → `Features/Account/RefreshSession/`

FR-009…011. No body. Reads `liftingo_rt`.

Responses: `200` + `AuthSession` + rotated cookie · `401 Auth.SessionInvalid` + cookie cleared
(also when the 30-day inactivity window or the 90-day absolute lifetime has passed).

Reuse of an already-rotated token revokes that device's session only and writes a
`RefreshTokenReuse` audit row (FR-011).

### `POST /auth/session/logout` — `LogoutEndpoint` → `Features/Account/Logout/`

FR-015. No body, no bearer needed (works after the access token expired). Reads `liftingo_rt`.

Response: `204` always (idempotent), cookie cleared. If the cookie maps to an active session, it is
revoked (`Logout`) and audited.

### `POST /auth/session/logout-all` 🔒 — `LogoutAllDevicesEndpoint` → `Features/Account/LogoutAllDevices/`

FR-015a. No body. Deletes the user's `KnownDevice` rows, revokes every active session of the
user, including the current one, audits `LogoutAllDevices`, clears the cookie. Response: `204`.
The next sign-in on any device sends a new-device e-mail.

---

## E-mail verification

### `POST /auth/email/verify` — `VerifyEmailEndpoint` → `Features/Account/VerifyEmail/`

US4, FR-017. Anonymous (the token identifies the account; a signed-in different user is ignored).

Request: `{ "token": "base64url..." }`

Responses: `200 { "status": "verified" }` · `200 { "status": "alreadyVerified" }` when the
token's account is already verified, whatever the token's state (FR-017) · `400
Auth.VerificationLinkInvalid` (UI offers to request a new link). Tokens are deleted 24 h after
they stop being usable (FR-038), after which an old link is unknown and gets the `400`.

### `POST /auth/email/resend-verification` 🔒 — `ResendVerificationEmailEndpoint` → `Features/Account/ResendVerificationEmail/`

FR-018, FR-037. No body.

Response: `202` whether or not an e-mail was sent (limit reached, or already verified → nothing
sent). The e-mail is the verification-only template, without welcome text (FR-018).

---

## Password reset

### `POST /auth/password/forgot` — `ForgotPasswordEndpoint` → `Features/Account/ForgotPassword/`

US6, FR-032, FR-033, FR-037.

Request: `{ "email": "anna@example.pl" }`

Response: `202` with an empty body for registered, unregistered and rate-limited addresses alike
(SC-008). Only `400` for a malformed e-mail.

### `POST /auth/password/reset` — `ResetPasswordEndpoint` → `Features/Account/ResetPassword/`

FR-034…036.

Request:

```json
{
  "token": "base64url...",
  "newPassword": "Nowe1haslo!",
  "acceptTermsAndAge": null,
  "acceptHealthData": null,
  "acceptMarketing": null
}
```

The consent fields matter only when the account's e-mail is unverified (FR-035); the UI sends
without them first and shows the consent form on `Auth.ConsentRequired`. Works on a locked
account (FR-012).

Responses: `204` · `400 Auth.ResetLinkInvalid` · `400 Auth.ConsentRequired` (unverified account,
required consents missing; password unchanged, token stays usable) · `400` password-policy
validation errors (token stays usable).

Side effects: password set (or added, for provider-only accounts), lockout cleared, token
consumed, `KnownDevice` rows deleted, all sessions revoked, including the caller's if it sent a
cookie, audit `PasswordResetCompleted`. For an unverified account, in the same commit: e-mail
verified, current consents superseded and three new rows inserted, other open e-mail tokens
superseded. No session is created and no `Set-Cookie: liftingo_rt` is sent (FR-035); the user then
logs in normally.

---

## External providers

Provider names in routes: `google`, `facebook`. The client signs in at the provider itself and
posts the provider's credential; the API never redirects (R9).

### `GET /auth/external/providers` — `GetExternalProvidersEndpoint` → `Features/Account/GetExternalProviders/`

Response `200`, configured providers only:

```json
{
  "providers": [
    { "name": "google", "clientId": "1234-abc.apps.googleusercontent.com" },
    { "name": "facebook", "clientId": "987654321" }
  ]
}
```

`clientId` is the public id the provider's script needs (Google client id, Facebook app id).

### `POST /auth/external/{provider}/login` — `ExternalLoginEndpoint` → `Features/Account/ExternalLogin/`

US5, FR-020…025, FR-023a. Anonymous.

Request:

```json
{
  "token": "<Google ID token | Facebook access token>",
  "acceptTermsAndAge": null,
  "acceptHealthData": null,
  "acceptMarketing": null,
  "email": null
}
```

The consent fields are read only when the request would create an account or take over an
unverified one. A required consent that is missing or `false` then gets `400
Auth.ConsentRequired`, and the client sends the same `token` again with the consents. `email` is
read only when the provider gave none.

| Case | Response | Side effects |
|---|---|---|
| Login already linked | `200` + `AuthSession` + cookie | session, `KnownDevice`, audit `ExternalLogin`, new-device e-mail if new |
| Provider e-mail of a **verified** account | `200` + `AuthSession` + cookie | login added, audit `ProviderLinked` + `ExternalLogin`, session as above |
| Provider e-mail of an **unverified** account (takeover, R10), consents given | `200` + `AuthSession` + cookie | login added, e-mail verified, password removed, old consents superseded + 3 new, e-mail tokens superseded; then all old sessions revoked, audit `ProviderLinked`, new session |
| New address, consents given | `201` + `AuthSession` + cookie | user (`EmailConfirmed` = provider e-mail present), external login, 3 consents, session, `KnownDevice`; welcome-only e-mail (provider e-mail) or verification+welcome e-mail (typed e-mail) |
| Typed e-mail of an existing account (FR-023a, R17), consents given | `202` `{ "status": "linkConfirmationSent" }` | `ProviderLinkConfirmation` token + e-mail (none at the FR-037 limit, same response); consents not recorded |

Refusals (nothing persisted, never counted as a failed login attempt):

| Response | When |
|---|---|
| `400 Validation.*` | `token` empty, or `email` not a valid address |
| `400 Auth.ConsentRequired` | consents needed and not both given. Extensions: `accountExists` (`true` for a takeover, so the page can explain why consents are asked again) and `emailRequired` (`true` when the provider gave no e-mail and none was sent) |
| `400 Validation.General` (`errors[].code` = `Validation.EmailRequired`) | consents given, provider gave no e-mail, and `email` is empty |
| `401 Auth.ExternalLoginFailed` | credential invalid, expired, or issued for another application (FR-020); audit `ExternalLogin` `Failed` without user, IP or device |
| `404 Auth.ProviderUnavailable` | provider unknown or not configured |
| `409 Auth.ProviderEmailConflict` | login linked to account A, provider e-mail belongs to account B (FR-022a); audit `ExternalLogin` `Failed` for A |
| `409 Auth.EmailTaken` | the address or provider identity was taken between the check and the commit |
| `429 Auth.LockedOut` | the matched account is locked out (FR-012) |

A provider e-mail that belongs to no account never blocks the linked login and never changes the
account's e-mail.

### `POST /auth/external/confirm-link` — `ConfirmProviderLinkEndpoint` → `Features/Account/ConfirmProviderLink/`

FR-023a, R17. Anonymous; the token identifies the account.

Request:

```json
{
  "token": "base64url...",
  "acceptTermsAndAge": null,
  "acceptHealthData": null,
  "acceptMarketing": null
}
```

The consent fields are needed only when the account is unverified. The UI sends the token alone
first and shows the consent form on `Auth.ConsentRequired`.

Responses: `200` + `AuthSession` + cookie · `400 Auth.ProviderLinkInvalid` · `400
Auth.ConsentRequired` (token stays usable) · `409 Auth.ProviderAlreadyLinked` (token consumed) ·
`429 Auth.LockedOut` (account locked; token stays usable).

Side effects: login added; for an unverified account the takeover from the provider login table
above; token consumed; audit `ProviderLinked`; session, `KnownDevice` and new-device e-mail as
for a login.

---

## RouteConsts additions

`Liftingo.Api/Endpoints/RouteConsts.cs` (moved from `Application/Common/Routes/` in rev. e).

```csharp
public static class Auth
{
    public const string Register = "/auth/register";
    public const string Login = "/auth/login";
    public const string Me = "/auth/me";
    public const string RefreshSession = "/auth/session/refresh";
    public const string Logout = "/auth/session/logout";
    public const string LogoutAllDevices = "/auth/session/logout-all";
    public const string VerifyEmail = "/auth/email/verify";
    public const string ResendVerificationEmail = "/auth/email/resend-verification";
    public const string ForgotPassword = "/auth/password/forgot";
    public const string ResetPassword = "/auth/password/reset";
    public const string ExternalProviders = "/auth/external/providers";
    public const string ExternalLogin = "/auth/external/{provider}/login";
    public const string ConfirmProviderLink = "/auth/external/confirm-link";
}
```

The cookie path `/api/v1/auth/session` is built in `Api` from the version prefix plus a
`RouteConsts.Auth.SessionPrefix = "/auth/session"` constant and kept in Api's
`RefreshCookieOptions`. Since rev. e the cookie is read and written only by `Api`; Application
never sees the path.
