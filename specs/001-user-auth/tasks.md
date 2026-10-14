---

description: "Task list for 001-user-auth (backend only)"
---

# Tasks: User Authentication (Register, Login, Logout, Password Reset)

**Input**: Design documents from `specs/001-user-auth/`

**Prerequisites**: [plan.md](plan.md) (rev. f), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/auth-api.md](contracts/auth-api.md), [quickstart.md](quickstart.md)

**Tests**: required. Constitution VI requires unit and integration tests, and the plan and quickstart list the test files and the criteria each one proves. In each story, write the tests first and make sure they fail before you implement.

**Organization**: tasks are grouped by user story so each story can be built and tested as a separate increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story the task belongs to (US1…US7)
- All paths are relative to the repository root. Backend paths start with `backend/`.

## Conventions every task follows

- Slices live in `backend/src/Liftingo.Application/Features/Account/{UseCase}.cs`. Each one contains, in this order: request record, response record, FluentValidation validator, and a `public sealed class {UseCase} : IEndpoint` with `static void Map(RouteGroupBuilder group)` and an `async` handler that takes a `CancellationToken` and returns `Result<T>` mapped through `ResultExtensions` (see `Features/Common/Ping.cs` and `Common/Result/`).
- Routes come from `RouteConsts.Auth` only. Every slice is mapped in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs`.
- DTOs are positional records. Use primary constructors for DI. Hand-map entities to DTOs. No repositories, mappers or mediators.
- Handlers commit SQL with a single `db.SaveChangesAsync(ct)`, then write to Redis, then write audit events, then send e-mails (R2, R4, R11, R14).
- Logs carry user ids only, never e-mail, password, token or IP (FR-030).
- User-facing error descriptions are in Polish.
- Test names follow `[Method]_[Scenario]_[ExpectedResult]`. Integration tests go in `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/`; unit tests mirror the source path under `backend/tests/Liftingo.Api.UnitTests/`.
- Commands: `cd backend && dotnet build`, `dotnet test tests/Liftingo.Api.UnitTests`, `dotnet test tests/Liftingo.ArchitectureTests`, `dotnet test tests/Liftingo.Api.IntegrationTests`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dependencies, local Redis and configuration skeleton.

- [X] T001 Add `PackageVersion` entries to `backend/Directory.Packages.props`: `StackExchange.Redis` (latest stable **3.x**, exact version; 3.x changed the default backlog policy, see T038), `Microsoft.AspNetCore.Authentication.Google` 10.0.11, `Microsoft.AspNetCore.Authentication.Facebook` 10.0.11, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.11, `AspNet.Security.OAuth.Twitter` 10.0.0, `Testcontainers.Redis` 4.16.0 (same as `Testcontainers.MsSql`), `Microsoft.Extensions.TimeProvider.Testing` (latest 10.x, for `FakeTimeProvider`), `Serilog.Enrichers.Sensitive` 2.1.0 (masks personal data in log events)
- [X] T002 Reference `StackExchange.Redis`, `Microsoft.AspNetCore.Authentication.Google`, `Microsoft.AspNetCore.Authentication.Facebook`, `AspNet.Security.OAuth.Twitter` and `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` from `backend/src/Liftingo.Infrastructure/Liftingo.Infrastructure.csproj`, and `Serilog.Enrichers.Sensitive` from `backend/src/Liftingo.Api/Liftingo.Api.csproj` (no version attributes, central package management)
- [X] T003 [P] Reference `Testcontainers.Redis` and `Microsoft.Extensions.TimeProvider.Testing` from `backend/tests/Liftingo.Api.IntegrationTests/Liftingo.Api.IntegrationTests.csproj`, and `Microsoft.Extensions.TimeProvider.Testing` from `backend/tests/Liftingo.Api.UnitTests/Liftingo.Api.UnitTests.csproj`
- [X] T004 [P] Create `backend/docker-compose.yml` with one `redis` service (pinned `redis:latest` image, port `6379:6379`, no persistence volume) for local development
- [X] T005 [P] Add the non-secret configuration sections from the data-model [Configuration](data-model.md#configuration) table to `backend/src/Liftingo.Api/appsettings.json`: `ConnectionStrings:Redis`, `Jwt` (`Issuer` `liftingo-api`, `Audience` `liftingo-app`, `AccessTokenLifetime` `00:15:00`, **no** `SigningKey`), `Sessions` (`InactivityTimeout` `30.00:00:00`, `AbsoluteLifetime` `90.00:00:00`), `KnownDevices:Retention` `365.00:00:00`, `EmailTokens` (`VerificationLifetime` `1.00:00:00`, `PasswordResetLifetime` `01:00:00`, `ProviderLinkLifetime` `1.00:00:00`, `MaxPerAddressPerHour` 3, `RetentionAfterUnusable` `1.00:00:00`), `ExternalLogin:PendingTimeout` `00:10:00`, `Audit:Retention` `90.00:00:00`, `Legal:DocumentsVersion` `2026-10-01`, `App:FrontendBaseUrl`, `ForwardedHeaders:KnownProxies` `[]`. Put dev values (`ConnectionStrings:Redis` `localhost:6379`, `App:FrontendBaseUrl` `https://localhost:4200`) in `backend/src/Liftingo.Api/appsettings.Development.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: domain entities, schema, ports and their implementations, the request pipeline, shared Account helpers and test infrastructure. Every story depends on this phase.

**⚠️ CRITICAL**: no user story work can begin until this phase is complete.

### Domain (`backend/src/Liftingo.Domain/Account/`)

- [X] T006 [P] Create `ConsentType.cs` (`TermsAndAge`, `HealthData`, `Marketing`) and `UserConsent.cs` in `backend/src/Liftingo.Domain/Account/`: properties `Id` (Guid), `UserId` (Guid, no navigation), `Type`, `Granted` (bool), `RecordedAt` (DateTimeOffset), `DocumentVersion` (string, "`string(32)`"), `SupersededAt` (DateTimeOffset?, "set when a takeover of an unverified account replaces the consents"). Static factory `Record(userId, type, granted, documentVersion, now)` that throws `DomainException` for an empty or > 32-char version; method `Supersede(now)` that sets `SupersededAt` once (throws if already set); private parameterless ctor for EF. No other mutators ("Rows are never deleted or edited otherwise")
- [X] T007 [P] Create `KnownDevice.cs` in `backend/src/Liftingo.Domain/Account/`: `Id`, `UserId`, `DeviceId` (Guid), `DeviceType` ("`string(100)`", truncate longer input), `FirstSeenAt`, `LastSeenAt`. Factory `Create(userId, deviceId, deviceType, now)` sets both timestamps to `now`; method `RecordSignIn(deviceType, now)` updates `DeviceType` and `LastSeenAt`
- [X] T008 [P] Create `EmailTokenPurpose.cs` (`EmailVerification`, `PasswordReset`, `ProviderLinkConfirmation`) and `EmailToken.cs` in `backend/src/Liftingo.Domain/Account/`: `Id`, `UserId`, `Purpose`, `TokenHash` (byte[32], "SHA-256"), `CreatedAt`, `ExpiresAt`, `ConsumedAt?`, `SupersededAt?`, `LoginProvider` ("`string(32)?` — `ProviderLinkConfirmation` only: `Google` / `Facebook` / `X`"), `ProviderKey` ("`string(256)?` — `ProviderLinkConfirmation` only"). Factories `Issue(userId, purpose, tokenHash, now, lifetime)` (rejects `ProviderLinkConfirmation`) and `IssueProviderLink(userId, tokenHash, loginProvider, providerKey, now, lifetime)` (requires both provider fields). Methods `IsUsable(now)` = "not consumed, not superseded, `now < ExpiresAt`", `Consume(now)` ("throws `DomainException` if not usable"), `Supersede(now)`, `UnusableSince(now)` = "the earliest of `ConsumedAt`, `SupersededAt` and `ExpiresAt` that has passed" (null while usable)
- [X] T009 [P] Create `SecurityEventType.cs` (`Login`, `ExternalLogin`, `Logout`, `LogoutAllDevices`, `PasswordResetCompleted`, `ProviderLinked`, `LockoutTriggered`, `RefreshTokenReuse`) and `SecurityEventOutcome.cs` (`Succeeded`, `Failed`, `LockedOut`) in `backend/src/Liftingo.Domain/Account/`
- [X] T010 [P] Unit tests `UserConsentTests.cs`, `KnownDeviceTests.cs` and `EmailTokenTests.cs` in `backend/tests/Liftingo.Api.UnitTests/Domain/Account/`, using `FakeTimeProvider` times: consent factory and single supersede; known-device sign-in update and 100-char truncation; token usable/consumed/superseded/expired states, `Consume` on an unusable token throws, provider-link factory requires both fields, `UnusableSince` picks the earliest passed timestamp

### Persistence

- [X] T011 Add `public DateTimeOffset CreatedAt { get; set; }` to `backend/src/Liftingo.Infrastructure/Identity/ApplicationUser.cs`
- [X] T012 [P] Create `backend/src/Liftingo.Infrastructure/Audit/AuditEncryptionKey.cs`: `Id` (Guid, the key id), `UserId` (Guid, non-nullable), `ProtectedKey` (byte[]), `CreatedAt`
- [X] T013 Create EF configurations in `backend/src/Liftingo.Infrastructure/Persistence/Configurations/` (picked up by the existing `ApplyConfigurationsFromAssembly`): `ApplicationUserConfiguration.cs` (unique filtered index on `NormalizedEmail` "`WHERE NormalizedEmail IS NOT NULL`", replacing Identity's non-unique `EmailIndex`); `UserConsentConfiguration.cs` (`Type` stored as string, `DocumentVersion` max 32 required, FK to `ApplicationUser` cascade, index `(UserId, Type, RecordedAt)`); `KnownDeviceConfiguration.cs` (`DeviceType` max 100 required, unique `(UserId, DeviceId)`, index `(LastSeenAt)`, FK cascade); `EmailTokenConfiguration.cs` (`Purpose` as string, `TokenHash` `binary(32)` with unique index, `LoginProvider` max 32, `ProviderKey` max 256, check constraint "`LoginProvider` and `ProviderKey` are both non-null exactly when `Purpose = 'ProviderLinkConfirmation'`", indexes `(UserId, Purpose, CreatedAt)` and `(ExpiresAt)`, FK cascade); `AuditEncryptionKeyConfiguration.cs` (unique index on `UserId`, FK to `ApplicationUser` with **cascade delete**, `ProtectedKey` `varbinary(max)`) (depends on T006–T008, T011, T012)
- [X] T014 Add `DbSet<UserConsent> UserConsents`, `DbSet<KnownDevice> KnownDevices` and `DbSet<EmailToken> EmailTokens` to `backend/src/Liftingo.Application/Common/Persistence/IApplicationDbContext.cs` and implement them in `backend/src/Liftingo.Infrastructure/Persistence/ApplicationDbContext.cs`. Also add `DbSet<AuditEncryptionKey> AuditEncryptionKeys` (Infrastructure only, not on the interface) and implement `IDataProtectionKeyContext` (`DbSet<DataProtectionKey> DataProtectionKeys`). Add `Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)` to the interface (implemented with `Database.BeginTransactionAsync`) for handlers that combine `ExecuteDeleteAsync` with `SaveChangesAsync` (T107 ResetPassword). No retrying execution strategy is configured, so a plain transaction is safe
- [X] T015 Generate the migration `AddUserAuthentication` with `dotnet ef migrations add AddUserAuthentication --project src/Liftingo.Infrastructure --startup-project src/Liftingo.Api --output-dir Persistence/Migrations`. Check that it adds `AspNetUsers.CreatedAt`, the unique filtered `NormalizedEmail` index and the tables `UserConsents`, `KnownDevices`, `EmailTokens` (with the check constraint), `AuditEncryptionKeys` and `DataProtectionKeys` in `backend/src/Liftingo.Infrastructure/Persistence/Migrations/` (depends on T013, T014)

### Result, routes, user context

- [X] T016 [P] Add `ErrorType.TooManyRequests` to `backend/src/Liftingo.Application/Common/Result/ErrorType.cs`. Create `backend/src/Liftingo.Application/Common/Result/LockedOutError.cs` (`Error` subtype with code `Auth.LockedOut`, Polish description, `DateTimeOffset RetryAfter`). In `ResultExtensions.cs`, map `TooManyRequests` to 429 and, for `LockedOutError`, add the `retryAfter` (ISO 8601) ProblemDetails extension and a `Retry-After` header in seconds, the same way `ValidationError` adds `errors`. Extend `backend/tests/Liftingo.Api.UnitTests/Common/Result/ResultExtensionsTests.cs`
- [X] T017 [P] Add a nested `public static class Auth` to `backend/src/Liftingo.Application/Common/Routes/RouteConsts.cs` holding the 17 constants from [contracts/auth-api.md › RouteConsts additions](contracts/auth-api.md#routeconsts-additions) plus `public const string SessionPrefix = "/auth/session";`. `RouteConsts.Ping` is deleted with the Ping placeholder in US1 (T065)
- [X] T018 [P] Add `bool IsEmailVerified { get; }` (from the `email_verified` claim) and `Guid? SessionId { get; }` (from `sid`) to `backend/src/Liftingo.Application/Common/IUserContext.cs`, implement them in `backend/src/Liftingo.Infrastructure/Identity/UserContext.cs`, and extend `backend/tests/Liftingo.Api.UnitTests/Common/UserContextTests.cs`

### Options

- [X] T019 [P] Create `backend/src/Liftingo.Application/Features/Account/Shared/AccountOptions.cs` with one options class per section (`SessionsOptions` + `CookiePath` set later by `PostConfigure`, `KnownDevicesOptions`, `EmailTokensOptions`, `ExternalLoginOptions`, `AuditOptions`, `LegalOptions`, `AppOptions`), each with a section-name constant and an `IValidateOptions<T>` validator (all `TimeSpan`s > 0, `AbsoluteLifetime` ≥ `InactivityTimeout`, `MaxPerAddressPerHour` ≥ 1, `DocumentsVersion` non-empty and ≤ 32 chars, `FrontendBaseUrl` an absolute https URI). Follow the `IdentitySecurityOptions` pattern
- [X] T020 [P] Create `backend/src/Liftingo.Infrastructure/Identity/JwtOptions.cs` (`Issuer`, `Audience`, `SigningKey`, `AccessTokenLifetime`) with a validator: all required, `SigningKey` Base64 decoding to ≥ 32 bytes, lifetime > 0
- [X] T021 [P] Unit tests `AccountOptionsValidatorTests.cs` and `JwtOptionsValidatorTests.cs` in `backend/tests/Liftingo.Api.UnitTests/Common/`

### Application ports (`backend/src/Liftingo.Application/Common/`)

- [X] T022 [P] Create `Email/IEmailSender.cs` (`Task SendAsync(EmailMessage message, CancellationToken ct)`) and `Email/EmailMessage.cs` (`record EmailMessage(string To, string Subject, string HtmlBody, string TextBody, string TemplateName, Guid UserId)`; `TemplateName` and `UserId` exist so non-dev senders can log without the recipient, FR-028)
- [X] T023 [P] Create `Security/ISecurityAuditLog.cs` (`Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken ct)`) and `Security/SecurityAuditEvent.cs` (`record SecurityAuditEvent(Guid? UserId, SecurityEventType Type, SecurityEventOutcome Outcome, DateTimeOffset OccurredAt, string? IpAddress, string? DeviceType)`; "`null` when `UserId` is `null`")
- [X] T024 [P] Create `Security/ISessionStore.cs` with `CreateAsync(Guid userId, SessionDevice device, CancellationToken)` → `(Guid SessionId, string RefreshToken, TimeSpan RemainingLifetime)`, `RotateAsync(byte[] tokenHash, ct)` → `RotationResult`, `RevokeAsync(Guid sessionId, ct)`, `RevokeByTokenAsync(byte[] tokenHash, ct)` → revoked session or null, `RevokeAllAsync(Guid userId, ct)` → count, `IsActiveAsync(Guid sessionId, ct)`. Add `Security/SessionDevice.cs` (`DeviceId`, `DeviceType`) and `Security/RotationResult.cs` (closed hierarchy `Rotated(SessionInfo, string NewRefreshToken, TimeSpan RemainingLifetime)` / `Reused(SessionInfo)` / `Invalid`). Add `Exceptions/SessionStoreUnavailableException.cs`
- [X] T025 [P] Create `Security/IIdentityService.cs` and `Security/AccountSummary.cs` (`record AccountSummary(Guid Id, string Email, bool EmailVerified, bool HasPassword, IReadOnlyList<string> Providers, DateTimeOffset CreatedAt)`). The port only stages changes and never saves (R2). Methods: `CreateUserAsync(email, password?, emailConfirmed, now)` → `Result<Guid>` (password-policy failures as `ValidationError`); `FindByEmailAsync`; `FindByLoginAsync(provider, key)`; `GetSummaryAsync(userId)`; `CheckPasswordAsync(userId, password)` → `PasswordCheckResult` (`Succeeded` / `Failed` / `LockedOut(retryAfter)` / `LockoutTriggered(retryAfter)`), applying Identity's lockout rules; `GetLockoutEndAsync(userId)`; `VerifyDummyPasswordAsync(password)` (checks the password against a fixed dummy hash so attempts for unknown e-mails take as long as real ones, R8); `AddLoginAsync(userId, provider, key, displayName)`; `ConfirmEmailAsync(userId)`; `RemovePasswordAsync(userId)`; `ResetPasswordAsync(userId, newPassword)` (sets or adds the hash, clears `AccessFailedCount` and `LockoutEnd`, updates the security stamp; returns `Result`)
- [X] T026 [P] Create `Security/IAccessTokenIssuer.cs` (`AccessToken Issue(Guid userId, Guid sessionId, bool emailVerified)` → `record AccessToken(string Value, DateTimeOffset ExpiresAt)`)
- [X] T027 [P] Create `Security/OpaqueToken.cs`: `Generate()` returns 32 random bytes from `RandomNumberGenerator`, Base64Url-encoded, plus its SHA-256 hash; `Hash(string token)` returns the SHA-256 (or null for input that is not valid Base64Url). Create `Security/AuthenticationSchemes.cs` with `External = "Liftingo.External"`. Unit test `backend/tests/Liftingo.Api.UnitTests/Common/Security/OpaqueTokenTests.cs`

### Infrastructure implementations

- [X] T028 Create `backend/src/Liftingo.Infrastructure/Identity/LiftingoUserStore.cs` (subclass of EF `UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>` with `AutoSaveChanges = false`) and `LiftingoPasswordValidator.cs` (subclass of `PasswordValidator<ApplicationUser>` overriding `IsLetterOrDigit` with `char.IsLetterOrDigit`, R13). In `InfrastructureExtensions.AddIdentityServices`, register both in place of the defaults and keep `RequireUniqueEmail`
- [X] T029 Implement `backend/src/Liftingo.Infrastructure/Identity/IdentityService.cs` (`IIdentityService`) over `UserManager<ApplicationUser>`. Set `UserName` to the e-mail and `CreatedAt` to `now`. Map Identity's `PasswordTooShort`, `PasswordRequiresDigit` and `PasswordRequiresNonAlphanumeric` to `Validation.PasswordTooShort`, `Validation.PasswordRequiresDigit` and `Validation.PasswordRequiresSpecial` with Polish messages naming the failed rule. Implement `CheckPasswordAsync` with the `UserManager` lockout methods (`AddIdentityCore` registers `UserManager`; no `SignInManager` is needed): `IsLockedOutAsync` → `LockedOut(GetLockoutEndDateAsync)` without checking the password; otherwise `CheckPasswordAsync`; on a wrong password, or no password at all (provider-only account), `AccessFailedAsync`, then report `LockoutTriggered` if `IsLockedOutAsync` is now true, else `Failed`; on success `ResetAccessFailedCountAsync` (FR-012). These calls only stage changes (`AutoSaveChanges = false`); the handler saves (R8). Implement `VerifyDummyPasswordAsync` with `IPasswordHasher<ApplicationUser>.VerifyHashedPassword` against a hash computed once per process (depends on T025, T028)
- [X] T030 [P] Implement `backend/src/Liftingo.Infrastructure/Identity/JwtAccessTokenIssuer.cs`: HS256 with `JsonWebTokenHandler`, claims `sub`, `sid`, `email_verified`, `jti`, `iat`, plus `exp`, `iss` and `aud` from `JwtOptions`, expiry from the injected `TimeProvider`
- [X] T031 [P] Write the Lua scripts `rotate.lua`, `revoke.lua`, `revoke-by-token.lua` and `revoke-all.lua` in `backend/src/Liftingo.Infrastructure/Sessions/Scripts/` and embed them (`<EmbeddedResource>` in `Liftingo.Infrastructure.csproj`). Keys: `liftingo:session:{id}` (hash `userId, deviceId, deviceType, createdAt, absoluteExpiresAt, lastRenewedAt, currentTokenHash`), `liftingo:rt:{hash}` (hash `sessionId, state` with state `active`/`used`) and `liftingo:user-sessions:{userId}` (set). `rotate.lua` implements R4 steps 1–4: missing rt or session → invalid; `used` → delete the session and its current rt and remove it from the set → reused; otherwise mark the old rt `used`, write the new rt `active`, update `currentTokenHash` and `lastRenewedAt`, and set TTL = `min(InactivityTimeout, absoluteExpiresAt - now)` on the session and both rt keys, extending the user set's TTL to the inactivity timeout
- [X] T032 Implement `backend/src/Liftingo.Infrastructure/Sessions/RedisSessionStore.cs` (`ISessionStore`) over `IConnectionMultiplexer`: `CreateAsync` in one `MULTI` transaction (session hash, active rt, set add, TTLs; `absoluteExpiresAt` = now + `AbsoluteLifetime`, set once), the other operations through the embedded scripts (`ScriptEvaluateAsync`), `IsActiveAsync` = `EXISTS`. Stale ids are dropped when the set is read. Catch `RedisConnectionException` and `RedisTimeoutException` and throw `SessionStoreUnavailableException`. Times come from `TimeProvider` (depends on T024, T031)
- [X] T033 [P] Create `backend/src/Liftingo.Infrastructure/Identity/SessionValidation.cs`: a `JwtBearerEvents.OnTokenValidated` handler that reads `sid` and calls `context.Fail(...)` when `ISessionStore.IsActiveAsync` returns false (FR-015b). On success it adds a `ClaimTypes.NameIdentifier` claim copied from `sub` to the principal's identity, because `UserContext` (and the test startup filter) read `NameIdentifier` and inbound claim mapping is off (T038). A `SessionStoreUnavailableException` must reach the exception handler as a 503: also set `OnAuthenticationFailed` to rethrow it (`ExceptionDispatchInfo.Capture(context.Exception).Throw()` when `context.Exception` is `SessionStoreUnavailableException`), so the outcome doesn't depend on how `JwtBearerHandler` treats exceptions thrown from events
- [X] T034 [P] Create `backend/src/Liftingo.Api/ExceptionHandling/SessionStoreUnavailableExceptionHandler.cs` (`IExceptionHandler`, next to the existing `DomainExceptionHandler`): writes `503` ProblemDetails with code `Service.Unavailable`, Polish description and `Retry-After: 30`. Register it in `backend/src/Liftingo.Api/Extensions/ExceptionHandlingExtensions.cs` before the unhandled-exception logger
- [X] T035 [P] Create `backend/src/Liftingo.Infrastructure/Email/ConsoleEmailSender.cs` (writes recipient, subject and text body through logger category `Liftingo.DevEmail`; the masking enricher (T040) masks the recipient and any address in the body, which is expected: the developer finds the e-mail by subject and template name and copies the link) and `DisabledEmailSender.cs` (drops the message, logs a warning with `TemplateName` and `UserId` only)
- [X] T036 [P] Create in `backend/src/Liftingo.Infrastructure/Audit/`: `AuditRecord.cs` (`EventId`, `UserId`, `EventType`, `Outcome`, `OccurredAt`, `KeyId`, `IpAddressCipher`, `DeviceTypeCipher`), `IAuditSink.cs` (internal), `ConsoleAuditSink.cs` (writes the encrypted record through its **own** Serilog logger, a separate `LoggerConfiguration` with only a console sink and an `[AUDIT]` output template, not the app's `ILogger` pipeline, so the audit stream stays apart from application logs per constitution III and can't reach app log sinks added later; dispose it with the host), `DisabledAuditSink.cs` (logs a warning with the event type only)
- [X] T037 Create `backend/src/Liftingo.Infrastructure/Audit/AuditKeyProvider.cs` (get-or-create the user's 256-bit key, wrapped with `IDataProtector` purpose `Liftingo.AuditKeys`, saved on its own `SaveChangesAsync` since it runs after the handler's commit) and `EncryptingSecurityAuditLog.cs` (`ISecurityAuditLog`: for events with a user, AES-GCM encrypt IP and device type with a fresh 12-byte nonce, Base64(nonce + ciphertext + tag); events without a user get empty `KeyId` and ciphers. Hand the record to `IAuditSink`) (depends on T012, T036)
- [X] T038 Wire everything in `backend/src/Liftingo.Infrastructure/InfrastructureExtensions.cs`: `TimeProvider.System`, `AddMemoryCache`, all options from T019/T020 with `ValidateOnStart`, `IConnectionMultiplexer` singleton from `ConnectionStrings:Redis` with `AbortOnConnectFail = false` (the default is `true`, which would crash startup when Redis is down) and `BacklogPolicy = BacklogPolicy.FailFast` (3.x's default queues commands while disconnected until the async timeout; FailFast fails them at once so FR-015c answers `503` quickly), plus short `ConnectTimeout`/`AsyncTimeout` values, `ISessionStore`, `IIdentityService`, `IAccessTokenIssuer`, `ISecurityAuditLog`, `AuditKeyProvider`, Data Protection with `PersistKeysToDbContext<ApplicationDbContext>()` and application name `Liftingo` (replacing the bare `AddDataProtection()`), console e-mail and audit sinks **only** when `IHostEnvironment.IsDevelopment()` and the disabled ones otherwise (change the signature to take the environment). Register authentication: JwtBearer as the default scheme (validate issuer, audience, lifetime and signing key from `JwtOptions`, `MapInboundClaims = false` so `sub`, `sid` and `email_verified` keep their JWT names, `OnTokenValidated` from T033), plus the `Liftingo.External` cookie scheme (`HttpOnly`, `Secure`, `SameSite=Lax`, expiry `ExternalLogin:PendingTimeout`). Add `AddAuthorization()` (depends on T028–T037)
- [X] T039 Create `backend/src/Liftingo.Api/Extensions/ForwardedHeadersExtensions.cs` (`X-Forwarded-For`/`-Proto`, known proxies from `ForwardedHeaders:KnownProxies`) and `AuthenticationExtensions.cs` (`PostConfigure<SessionsOptions>` sets `CookiePath = "/api/v1" + RouteConsts.Auth.SessionPrefix` from the version prefix). In `backend/src/Liftingo.Api/Program.cs`, pass `builder.Environment` to `AddInfrastructure`, add `UseForwardedHeaders()` first, `UseHsts()` outside Development, and `UseAuthentication()` **before** `UseRateLimiting()` with `UseAuthorization()` after it (R18)
- [X] T040 [P] Add sensitive-data masking to Serilog in `backend/src/Liftingo.Api/Extensions/SerilogExtensions.cs`: `.Enrich.WithSensitiveDataMasking(options => ...)` in code (not in appsettings, so configuration can't turn it off) for both the bootstrap logger and the app logger, with the default e-mail operator plus a custom `IpAddressMaskingOperator` (a `RegexMaskingOperator` for IPv4 and IPv6 addresses) in `backend/src/Liftingo.Api/Logging/IpAddressMaskingOperator.cs`. The enricher masks properties and the rendered message. It is a safety net for FR-030 and constitution III; code still logs user ids only. Unit test `backend/tests/Liftingo.Api.UnitTests/Logging/SensitiveDataMaskingTests.cs`: an e-mail, an IPv4 and an IPv6 address are masked both as a property and inside the message; a GUID user id, a time like `10:15:00` and an ISO timestamp are left alone

### Shared Account helpers (`backend/src/Liftingo.Application/Features/Account/Shared/`)

- [X] T041 [P] Create `DeviceDescriptor.cs`: a static User-Agent classifier returning "{browser}, {OS}, {form factor}" with form factor `telefon` / `tablet` / `komputer`, browser families Edge, Chrome, Firefox, Safari, Opera or `inna przeglądarka`, OS families Windows, macOS, iOS, Android, Linux or `inny system`, capped at 100 chars (e.g. "Chrome, Windows, komputer"). Create `ClientInfo.cs`, built from `HttpContext`: `IpAddress` from `Connection.RemoteIpAddress`, `DeviceType` from `DeviceDescriptor`, `DeviceId` from `X-Device-Id` (or a given value), replaced by `Guid.NewGuid()` when missing or not a GUID (R6). Unit test `backend/tests/Liftingo.Api.UnitTests/Features/Account/DeviceDescriptorTests.cs` with real UA strings for each form factor
- [X] T042 [P] Create `RefreshTokenCookie.cs`: `Append(HttpResponse, token, maxAge)` and `Delete(HttpResponse)` for cookie `liftingo_rt` with "`HttpOnly; Secure; SameSite=Lax; Path=/api/v1/auth/session; Max-Age=<session's remaining TTL, ≤ 2592000>`" (path from `SessionsOptions.CookiePath`); `Read(HttpRequest)`
- [X] T043 [P] Create `AuthSessionResponse.cs` with the shared contract records `AuthSessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AccountResponse Account)` and `AccountResponse(Guid Id, string Email, bool EmailVerified, bool HasPassword, IReadOnlyList<string> Providers)`, plus `AccountResponse.From(AccountSummary)`
- [X] T044 Create `SessionIssuer.cs` with two explicit steps around the handler's commit (SQL first, Redis second, R4). `Task<bool> StageDeviceAsync(Guid userId, ClientInfo client, SignInKind kind, CancellationToken ct)` runs **before** `SaveChangesAsync`: it stages the `KnownDevice` upsert and returns `isNewDevice` (`SignInKind.Registration` inserts the first device and always returns `false`; `SignInKind.SignIn` inserts an unknown pair and returns `true`, or calls `RecordSignIn` on a known one and returns `false`). `Task<AuthSessionResponse> IssueAsync(HttpContext http, Guid userId, ClientInfo client, CancellationToken ct)` runs **after** the commit: it creates the Redis session, issues the JWT with the account's current `EmailVerified`, appends the refresh cookie and returns the response. `SessionIssuer` never calls `SaveChangesAsync`; the handler does, so the commit stays visible in the slice (constitution II) (depends on T041–T043)
- [X] T045 [P] Create `SecurityAudit.cs`: builds `SecurityAuditEvent`s from `ClientInfo` and `TimeProvider` (IP and device only when `UserId` is set, FR-014), writes them through `ISecurityAuditLog` and catches and logs failures (event type and user id only), and also writes a Serilog `Information` line with event type, outcome and user id (R14)
- [X] T046 [P] Create `AccountEmails.cs`: builds Polish `EmailMessage`s with links from `AppOptions.FrontendBaseUrl` and sends them through `IEmailSender` inside try/catch, logging failures with template name and user id only (FR-029). Start with the **verification + welcome** template (`{Frontend}/auth/verify-email?token=…`); later stories add the others
- [X] T047 [P] Create `EmailTokenIssuer.cs`: `IssueAsync(userId, purpose, ct, provider?)` → token string or `null` when the user already has `EmailTokens:MaxPerAddressPerHour` tokens of that purpose with "`CreatedAt > now - 1h`" (FR-037). Otherwise it supersedes all usable tokens of that user and purpose, stages the new `EmailToken` with the purpose's lifetime from `EmailTokensOptions`, and leaves saving to the caller. Add `FindAsync(token, purpose, ct)` that looks up by hash whatever the state
- [X] T048 [P] Create `ConsentRecorder.cs`: `Stage(userId, acceptTermsAndAge, acceptHealthData, acceptMarketing)` adds exactly three `UserConsent` rows with `Legal:DocumentsVersion` and `TimeProvider` time (marketing `false` when unticked, FR-004); `SupersedeCurrentAsync(userId, ct)` calls `Supersede(now)` on every row where `SupersededAt` is null. Add a shared FluentValidation rule-set extension for the two required consents (`Validation.TermsRequired`, `Validation.HealthDataRequired` with the US1-2 body-weight explanation) and the trimmed e-mail rule (`EmailAddress()`, `MaximumLength(256)`)
- [X] T049 Create `AccountTakeover.cs` (R10/R7): `StageAsync(userId, consents, PasswordMode mode, ct)` with `PasswordMode.Remove` (provider takeovers) or `Keep` (password reset). It calls `ConfirmEmailAsync`, `RemovePasswordAsync` if the mode is `Remove`, `ConsentRecorder.SupersedeCurrentAsync` + `Stage`, and supersedes every usable `EmailToken` of the user. Callers consume their own token **before** calling it, so that token is no longer usable and is skipped (no `keepToken` parameter). It does not save. `CompleteAsync(userId, ct)` runs after the commit and calls `ISessionStore.RevokeAllAsync` (depends on T048)

### Test infrastructure (`backend/tests/Liftingo.Api.IntegrationTests/Infrastructure/`)

- [X] T050 Extend `SqlServerFixture.cs` to also start a `RedisContainer` built with `new RedisBuilder("redis:latest").Build()` (the current API takes the image in the constructor; use the same tag as `docker-compose.yml`) and expose its connection string and a `FlushRedisAsync()` that `IntegrationTestBase.cs` calls before each test next to the Respawn reset (Respawn must not drop `DataProtectionKeys` or `__EFMigrationsHistory`)
- [X] T051 [P] Create `CapturingEmailSender.cs` (thread-safe list of `EmailMessage`, `Clear()`, optional `ThrowOnSend` switch for FR-029 tests) and `CapturingAuditSink.cs` (captures `AuditRecord`s, plus a helper that decrypts a record with the user's key from the database)
- [X] T052 Update `ApiFactory.cs`: set `ConnectionStrings:Redis` from the fixture, a fixed test `Jwt:SigningKey`, `App:FrontendBaseUrl`, `Legal:DocumentsVersion`; replace `TimeProvider` with a shared `FakeTimeProvider`, `IEmailSender` with `CapturingEmailSender` and `IAuditSink` with `CapturingAuditSink`. Expose all of them to tests. Use a cookie-aware `HttpClient` helper (`HandleCookies = true`, base address `https://localhost`) (depends on T050, T051)
- [X] T053 [P] Create `AuthTestClient.cs` with helpers `RegisterAsync(email?, password?, deviceId?)`, `LoginAsync`, `RefreshAsync`, `GetMeAsync(accessToken)`, `ReadRefreshCookie(response)` (parses the `Set-Cookie` attributes) and `ExtractToken(EmailMessage)`, plus `Fakers/RegisterRequestFaker.cs` (Bogus, valid password like `Sztanga1!`)
- [X] T054 [P] Extend `backend/tests/Liftingo.ArchitectureTests/DependencyGuardTests.cs`: Domain must not depend on `Microsoft.EntityFrameworkCore`; Application must not depend on `StackExchange.Redis`
- [X] T055 [P] Unit tests in `backend/tests/Liftingo.Api.UnitTests/Infrastructure/`: `Identity/LiftingoPasswordValidatorTests.cs` (`zażółć12` rejected, `zażółć1!` accepted, `abc 1234` accepted because a space is special) and `Audit/EncryptingSecurityAuditLogTests.cs` (round-trip decrypt with the user's key, fresh nonce per field, user-less event has empty key id and ciphers; use the ephemeral Data Protection provider)
- [X] T056 Integration test `backend/tests/Liftingo.Api.IntegrationTests/Infrastructure/IdentityServiceTests.cs` that confirms R2's *verify* item: with `AutoSaveChanges = false`, `CreateUserAsync` runs the validators and leaves the user `Added` and unsaved until `SaveChangesAsync`. If this fails, switch to an explicit transaction in multi-write handlers as plan.md says
- [X] T057 Integration test `backend/tests/Liftingo.Api.IntegrationTests/Infrastructure/RedisSessionStoreTests.cs` against the Redis container: create sets the keys and TTLs; rotate returns a new token and the old one becomes `used`; replaying the old token returns `Reused` and deletes the session; two concurrent rotations of one token → exactly one `Rotated`; near the 90-day cap the TTL is capped; `RevokeAllAsync` removes every session of the user and leaves others alone

**Checkpoint**: `dotnet build` and every existing test suite pass, and the migration applies. User stories can start.

---

## Phase 3: User Story 1 - Register with e-mail and password (Priority: P1) 🎯 MVP

**Goal**: a visitor creates an account with e-mail, password and consents, is signed in, and receives one verification + welcome e-mail. `GET /auth/me` shows the signed-in account, and it replaces the Ping placeholder.

**Independent test**: `POST /api/v1/auth/register` with valid data → `201`, access token in the body, `liftingo_rt` cookie, 3 consent rows with document version, one captured verification + welcome e-mail, no new-device e-mail; `GET /api/v1/auth/me` with the access token → `200`.

### Tests for User Story 1 ⚠️ (write first, see them fail)

- [X] T058 [P] [US1] Unit test `backend/tests/Liftingo.Api.UnitTests/Features/Account/RegisterValidatorTests.cs`: e-mail trimmed before the format and 256-char checks; password required and ≤ 128 (`Validation.PasswordTooLong`); `acceptTermsAndAge` false → `Validation.TermsRequired`; `acceptHealthData` false → `Validation.HealthDataRequired` with the body-weight message
- [X] T059 [P] [US1] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/RegisterTests.cs`: `Register_ValidRequest_Returns201WithSessionAndCookie` (cookie `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/api/v1/auth/session`, `Max-Age` ≤ 2592000; token only in the body); three consent rows with `Legal:DocumentsVersion` and marketing `false`; exactly one captured e-mail (verification + welcome) with a token that resolves to an `EmailVerification` row; one `KnownDevice` row and no new-device e-mail; missing either required consent → `400` and **no** user row (SC-007); `abcdefgh` → `400` with `Validation.PasswordRequiresDigit` and `Validation.PasswordRequiresSpecial`; existing e-mail in different case → `409 Auth.EmailTaken`; two parallel registrations of one address → one `201`, one `409`; throwing `IEmailSender` → still `201`, log entry has template name and user id only (FR-029); the returned access token works on `GET /auth/me`
- [X] T060 [P] [US1] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/GetCurrentAccountTests.cs`: bearer → `200` with the `account` object (`emailVerified` false after register, `hasPassword` true, `providers` empty); no bearer → `401`

### Implementation for User Story 1

- [X] T061 [US1] Translate unique-index violations: add `backend/src/Liftingo.Application/Common/Exceptions/UniqueConstraintViolationException.cs` and override `SaveChangesAsync` in `backend/src/Liftingo.Infrastructure/Persistence/ApplicationDbContext.cs` to rethrow a `DbUpdateException` caused by `SqlException` numbers 2601/2627 as that exception, carrying the index name (R2)
- [X] T062 [US1] Implement `backend/src/Liftingo.Application/Features/Account/Register.cs`: `RegisterRequest(string Email, string Password, bool AcceptTermsAndAge, bool AcceptHealthData, bool AcceptMarketing)`, validator from T048's shared rules plus the password rules, and a handler that trims the e-mail, returns `409 Auth.EmailTaken` if `FindByEmailAsync` finds it, calls `CreateUserAsync(emailConfirmed: false)`, `ConsentRecorder.Stage`, `SessionIssuer.StageDeviceAsync(Registration)` and `EmailTokenIssuer.IssueAsync(EmailVerification)`, then one `SaveChangesAsync` (catching `UniqueConstraintViolationException` → `409 Auth.EmailTaken`, with a comment that the exception only happens when two registrations race, an unexpected condition the handler turns into a `Result`, constitution II). After that it calls `SessionIssuer.IssueAsync` and `AccountEmails` (verification + welcome) and returns `201` + `AuthSessionResponse` (depends on T061)
- [X] T063 [US1] Implement `backend/src/Liftingo.Application/Features/Account/GetCurrentAccount.cs` (`GET`, `.RequireAuthorization()`): returns `AccountResponse.From(GetSummaryAsync(userContext.UserId))`
- [X] T064 [US1] Map `Register` and `GetCurrentAccount` in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs`, and add register requests (valid, missing consent, weak password) with an `X-Device-Id` header, plus a `GET /auth/me` request, to `backend/src/Liftingo.Api/Liftingo.Api.http`
- [X] T065 [US1] Remove the `Ping` placeholder (it only existed to prove versioned routing and isn't needed any more): delete `backend/src/Liftingo.Application/Features/Common/Ping.cs` and `RouteConsts.Ping`, drop it from `V1Endpoints.cs`, and rewrite `backend/tests/Liftingo.Api.IntegrationTests/ApiVersioningTests.cs` to use `GET /api/v1/auth/me` without a bearer instead: the versioned route answers `401` (it exists, so not `404`) and reports the supported versions, `/auth/me` without the prefix and `/api/v99/auth/me` answer `404`, and `/openapi/v1.json` lists `/api/v1/auth/me`

**Checkpoint**: registration works end to end; `RegisterTests`, `GetCurrentAccountTests` and the rewritten `ApiVersioningTests` pass.

---

## Phase 4: User Story 2 - Log in and stay signed in (Priority: P1)

**Goal**: password login with lockout that doesn't reveal accounts, audit records, silent renewal with rotation and reuse detection, 30-day sliding and 90-day absolute session lifetime.

**Independent test**: log in → `200`; refresh with the cookie → new token and rotated cookie; 5 wrong passwords → 6th gets `429 Auth.LockedOut` with `retryAfter`; the same for an unregistered e-mail.

### Tests for User Story 2 ⚠️

- [X] T066 [P] [US2] Unit test `backend/tests/Liftingo.Api.UnitTests/Features/Account/FailedLoginThrottleTests.cs` with `FakeTimeProvider`: the 5th failure locks for 15 min; attempts during the lockout are refused and do not extend it; the counter does not decay and resets when the lockout triggers; the lock lifts after the duration
- [X] T067 [P] [US2] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/LoginTests.cs`: correct credentials → `200` + session + cookie attributes as in T059; wrong password, unknown e-mail and provider-only account → identical `401 Auth.InvalidCredentials` bodies; 5 failures → 6th `429` with `retryAfter` about 15 min ahead and a `Retry-After` header; the right password during the lockout is refused and does not move `retryAfter`; after `LockoutEnd` is moved into the past **in the database** (Identity uses the system clock) login succeeds and `AccessFailedCount` is 0; unknown e-mail ×5 → the same `429` shape, then expiry through `FakeTimeProvider`; audit: one `Login` record per attempt, plus `LockoutTriggered` on the 5th failure, records for a known user decrypt to IP + device type, unknown-e-mail records have no user, key, IP or device; the e-mail is trimmed and case-insensitive; a known `X-Device-Id` updates `LastSeenAt` and an unknown one inserts a `KnownDevice` row
- [X] T068 [P] [US2] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/RefreshSessionTests.cs`: valid cookie → `200`, new access token, rotated cookie, session TTL reset to 30 days; no cookie, unknown cookie or deleted session key → `401 Auth.SessionInvalid` with the cookie cleared; replaying the previous cookie → `401`, the session is gone (newer cookie fails too), a `RefreshTokenReuse` audit record is written, and the unexpired access token of that session gets `401` on `GET /auth/me` (FR-015b); another device's session is unaffected; with `FakeTimeProvider` near the 90-day cap the TTL and cookie `Max-Age` are capped, and past the cap refresh → `401` despite daily renewals; no audit record for a routine refresh

### Implementation for User Story 2

- [X] T069 [P] [US2] Create `backend/src/Liftingo.Application/Features/Account/Shared/FailedLoginThrottle.cs`: an `IMemoryCache` entry keyed by the SHA-256 of the upper-invariant trimmed e-mail, with `IsLockedOut(email, out retryAfter)` and `RegisterFailure(email)` → `LockoutTriggered(retryAfter)?`, using `Identity:Lockout` values and `TimeProvider` (R8). Unknown-e-mail attempts call `IIdentityService.VerifyDummyPasswordAsync` (T025) so they take as long as a real check
- [X] T070 [US2] Implement `backend/src/Liftingo.Application/Features/Account/Login.cs`: `LoginRequest(string Email, string Password)` (e-mail trimmed, both required). For a known user: `CheckPasswordAsync`, which gives `LockedOut` → `LockedOutError` and audit `Login`/`LockedOut`; `LockoutTriggered` → `SaveChangesAsync` (persists `LockoutEnd` and the reset counter), audit `Login`/`Failed` + `LockoutTriggered`, then `LockedOutError`; `Failed` → `SaveChangesAsync` (persists the higher `AccessFailedCount`), audit, `401`; success → `SessionIssuer.StageDeviceAsync(SignIn)`, one `SaveChangesAsync` (which also persists the counter reset from `ResetAccessFailedCountAsync`), `IssueAsync`, audit `Login`/`Succeeded`, `200`. Keep the `IsNewDevice` flag for US7 (T112). For an unknown e-mail: check the throttle first, run the dummy password check, register the failure, write the audit record without user, return the same `401` or `429`. With `AutoSaveChanges = false` every path that changes the user must save; `LockedOut` changes nothing (depends on T069)
- [X] T071 [US2] Implement `backend/src/Liftingo.Application/Features/Account/RefreshSession.cs` (`POST`, no body): read the cookie, hash it, call `ISessionStore.RotateAsync`. `Rotated` → reload `AccountSummary` for the current `emailVerified`, issue the JWT, append the cookie with `RemainingLifetime`, `200` + `AuthSessionResponse`. `Reused` → audit `RefreshTokenReuse` with the session's user and device, delete the cookie, `401 Auth.SessionInvalid`. `Invalid` → delete the cookie, `401`
- [X] T072 [US2] Map `Login` and `RefreshSession` in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs`, and add login and refresh requests to `backend/src/Liftingo.Api/Liftingo.Api.http`
- [X] T073 [US2] Add a test to `backend/tests/Liftingo.Api.IntegrationTests/RateLimitingTests.cs` showing that a signed-in auth call (`GET /auth/me` with a real bearer) falls into the per-user bucket now that `UseAuthentication` runs before the limiter (FR-040)

**Checkpoint**: US1 and US2 together: register, log in, stay signed in through refresh, lockout without enumeration.

---

## Phase 5: User Story 3 - Log out (Priority: P1)

**Goal**: log out of the current device using only the cookie, or sign out of all devices. Revoked sessions reject their access tokens at once.

**Independent test**: log in on devices A and B; log out on A → A's refresh and bearer fail, B works; `logout-all` from B → B fails as well.

### Tests for User Story 3 ⚠️

- [X] T074 [P] [US3] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/LogoutTests.cs`: logout → `204`, cookie cleared, `Logout` audit record; afterwards refresh with that cookie → `401` and the old access token on `GET /auth/me` → `401` (SC-003, FR-015b); the other device still refreshes; logout with an expired access token and only the cookie still works; logout without a cookie or with an unknown cookie → `204` and no audit record (idempotent)
- [X] T075 [P] [US3] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/LogoutAllDevicesTests.cs`: three sessions → `logout-all` → `204`, every refresh and bearer fails, including the caller's; `KnownDevices` rows of the user are gone; the next login from a previously known device inserts a new row with `IsNewDevice` (the e-mail is asserted in US7); `LogoutAllDevices` audit record; other users' sessions remain; no bearer → `401`

### Implementation for User Story 3

- [X] T076 [P] [US3] Implement `backend/src/Liftingo.Application/Features/Account/Logout.cs` (`POST`, anonymous): if the cookie hashes to an active session, call `RevokeByTokenAsync` and audit `Logout` with that session's user and device; always delete the cookie and return `204`
- [X] T077 [P] [US3] Implement `backend/src/Liftingo.Application/Features/Account/LogoutAllDevices.cs` (`POST`, `.RequireAuthorization()`): delete the user's `KnownDevices` rows with `ExecuteDeleteAsync` (the only SQL write in this handler, so no transaction), then `RevokeAllAsync(userId)`, audit `LogoutAllDevices`, delete the cookie, `204`
- [X] T078 [US3] Map `Logout` and `LogoutAllDevices` in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs` and add both requests to `backend/src/Liftingo.Api/Liftingo.Api.http`

**Checkpoint**: the P1 scope (US1–US3) is complete and can be demoed.

---

## Phase 6: User Story 4 - Verify e-mail address (Priority: P2)

**Goal**: single-use 24 h verification links, resend with supersede and the 3-per-hour limit, and an "already verified" answer.

**Independent test**: register, post the captured token to `/auth/email/verify` → `200 verified`; post it again → `200 alreadyVerified`; `GET /auth/me` after refresh shows `emailVerified: true`.

### Tests for User Story 4 ⚠️

- [X] T079 [P] [US4] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/VerifyEmailTests.cs`: valid token → `200 {"status":"verified"}`, `EmailConfirmed` true, token consumed; same token again → `200 alreadyVerified`; token older than 24 h (`FakeTimeProvider`) of an unverified account → `400 Auth.VerificationLinkInvalid`; superseded token of an unverified account → `400`; unknown or malformed token → `400`; request sent with another user's bearer verifies the token's account, not the signed-in one; after refresh the new JWT has `email_verified=true`; for an account verified by a provider takeover or by a password reset, its earlier verification links answer `200 alreadyVerified`
- [X] T080 [P] [US4] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/ResendVerificationEmailTests.cs`: resend → `202` and one captured verification-only e-mail (no welcome text); earlier tokens become superseded; registration + 2 resends = 3 e-mails, a 3rd resend within the hour → `202` and no e-mail (FR-037); after the hour (`FakeTimeProvider`) it sends again; an already verified account → `202` and no e-mail; no bearer → `401`

### Implementation for User Story 4

- [X] T081 [US4] Add the **verification-only** template (no welcome text, FR-018) to `backend/src/Liftingo.Application/Features/Account/Shared/AccountEmails.cs`
- [X] T082 [P] [US4] Implement `backend/src/Liftingo.Application/Features/Account/VerifyEmail.cs` (`POST`, anonymous): `VerifyEmailRequest(string Token)` (required); look up with `EmailTokenIssuer.FindAsync(token, EmailVerification)`; unknown → `400 Auth.VerificationLinkInvalid`; the token's user already verified → `200 alreadyVerified` whatever the token state; unusable → `400`; otherwise `Consume`, `ConfirmEmailAsync`, `SaveChangesAsync`, `200 {"status":"verified"}`. Response `VerifyEmailResponse(string Status)` with values `verified` / `alreadyVerified`
- [X] T083 [P] [US4] Implement `backend/src/Liftingo.Application/Features/Account/ResendVerificationEmail.cs` (`POST`, `.RequireAuthorization()`): if the user is not verified and `EmailTokenIssuer.IssueAsync` returns a token, `SaveChangesAsync` and send the verification-only e-mail; always `202` (depends on T081)
- [X] T084 [US4] Map `VerifyEmail` and `ResendVerificationEmail` in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs` and add requests to `backend/src/Liftingo.Api/Liftingo.Api.http`

**Checkpoint**: verification works on its own on top of US1.

---

## Phase 7: User Story 5 - Register or log in with an external provider (Priority: P2)

**Rev. f**: the redirect flow built here is replaced by Phase 12 (client-supplied provider tokens). The tasks below are kept as the record of what was built first.

**Goal**: Google, Facebook and X sign-in through server-side redirects, a consent interstitial before any account is created, linking by e-mail, pre-hijack takeover, link confirmation for typed-in e-mails, and lockout plus e-mail-conflict refusals.

**Independent test**: with the fake external handler, a first sign-in → redirect to `/auth/consent`; `pending` → provider data; `complete` with consents → `201`, verified account, 3 consents; a second sign-in → redirect to `/auth/callback` with a refresh cookie.

### Test infrastructure and tests for User Story 5 ⚠️

- [X] T085 [US5] Create `backend/tests/Liftingo.Api.IntegrationTests/Infrastructure/FakeExternalAuthHandler.cs`: a remote-authentication-style handler registered in `ApiFactory` under the scheme name `Google` (and a second one, `Facebook`, that returns no e-mail). The test sets the identity (provider key, e-mail or none, display name) or a failure per request. On challenge it redirects straight to its callback path, which signs the principal into `Liftingo.External` with the original `AuthenticationProperties` (device id, `RedirectUri`), as the real handlers do. Have `ApiFactory` register providers through the same `ExternalProviderOptions` path, using test client ids (skipped at the user request: external-provider flows are tested manually, not against mocks) (superseded by Phase 12, rev. f)
- [X] T086 [P] [US5] Unit test `backend/tests/Liftingo.Api.UnitTests/Infrastructure/Identity/ExternalProviderOptionsTests.cs`: X options contain scope `users.email`, `UserFields` contains `confirmed_email`, and a claim action maps `ClaimTypes.Email` from `data.confirmed_email` (R9); a provider without `ClientId`/`ClientSecret` is not registered
- [X] T087 [P] [US5] Unit test `backend/tests/Liftingo.Api.UnitTests/Features/Account/CompleteExternalLoginValidatorTests.cs` and `ConfirmProviderLinkValidatorTests.cs`: shared consent rules; `email` trimmed, valid and ≤ 256 when present
- [X] T088 [P] [US5] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/HandleExternalLoginCallbackTests.cs`: already linked → `302 {Frontend}/auth/callback`, refresh cookie set, external cookie deleted, `ExternalLogin` audit; e-mail of a **verified** account → linked, signed in, `ProviderLinked` + `ExternalLogin` audit; e-mail of an **unverified** account → `302 /auth/consent`, nothing persisted, external cookie kept; new user → `302 /auth/consent`, no `AspNetUsers` row; matched account locked → `302 /login?error=lockedOut&retryAfter=<ISO>`, `AccessFailedCount` unchanged; linked to A while the provider e-mail belongs to B → `302 /login?error=emailConflict`, both accounts unchanged, `ExternalLogin` `Failed` for A; linked login whose provider e-mail belongs to nobody → normal sign-in, account e-mail unchanged; provider failure/cancel → `302 /login?error=external`, nothing persisted, one audit record without user, IP or device (FR-020); `GET /auth/external/providers` lists only the configured providers; `start` for an unconfigured provider → `404 Auth.ProviderUnavailable` (skipped at the user request: external-provider flows are tested manually, not against mocks) (superseded by Phase 12, rev. f)
- [X] T089 [P] [US5] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/CompleteExternalLoginTests.cs`: `pending` returns provider, e-mail, `emailRequired`, `accountExists`; `complete` for a new provider e-mail → `201`, `EmailConfirmed` true, login added, 3 consent rows, `KnownDevice` row, **no** verification e-mail; missing consent → `400` and no user (SC-007); `cancel` → `204`, then `pending` → `400 Auth.ExternalLoginExpired` and no user; 10 min later (`FakeTimeProvider`) → `400 Auth.ExternalLoginExpired`; takeover of an unverified account → `200`, verified, password no longer works, old sessions and their bearer fail, old consents have `SupersededAt`, 3 new rows, open e-mail tokens superseded, `ProviderLinked` audit; cancel at the takeover consent step → account unchanged, password still works; takeover of a locked account → `429 Auth.LockedOut`; no provider e-mail + new typed e-mail → `201`, unverified, verification + welcome e-mail; typed e-mail of an existing account → `202 {"status":"linkConfirmationSent"}`, a `ProviderLinkConfirmation` token with `LoginProvider` and `ProviderKey`, no user or login added, no consents recorded, one link-confirmation e-mail (skipped at the user request: external-provider flows are tested manually, not against mocks) (superseded by Phase 12, rev. f)
- [X] T090 [P] [US5] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/ConfirmProviderLinkTests.cs`: verified account + token → `200` + session, login added, token consumed, `ProviderLinked` audit; unverified account without consents → `400 Auth.ConsentRequired`, token still usable, nothing changed; with consents → takeover as in T089 and `200`; provider pair already linked → `409 Auth.ProviderAlreadyLinked`, token consumed; expired, used or unknown token → `400 Auth.ProviderLinkInvalid`; locked account → `429 Auth.LockedOut`, token still usable, counter unchanged; a 4th link-confirmation request within an hour → `202` and no e-mail (FR-037) (skipped at the user request: external-provider flows are tested manually, not against mocks) (superseded by Phase 12, rev. f)

### Implementation for User Story 5

- [X] T091 [US5] Create `backend/src/Liftingo.Infrastructure/Identity/ExternalProviderOptions.cs` and call it from `InfrastructureExtensions`: register Google (`Authentication:Google:ClientId/ClientSecret`), Facebook (`AppId/AppSecret`) and X (`AspNet.Security.OAuth.Twitter`, scheme `X`, `CallbackPath = "/signin-x"`, `Scope.Add("users.email")`, `UserFields.Add("confirmed_email")`, `ClaimActions.MapJsonSubKey(ClaimTypes.Email, "data", "confirmed_email")`) **only** when both values are set, each with `SignInScheme = AuthenticationSchemes.External`. Expose the registered names through a small `IExternalProviderCatalog` port in `backend/src/Liftingo.Application/Common/Security/IExternalProviderCatalog.cs` (route name `google`/`facebook`/`x` ↔ scheme name)
- [X] T092 [US5] Create `backend/src/Liftingo.Application/Features/Account/Shared/PendingExternalLogin.cs`: reads the `Liftingo.External` principal through `HttpContext.AuthenticateAsync(AuthenticationSchemes.External)` into `record PendingExternalLogin(string Provider, string ProviderKey, string? Email, string? DisplayName, Guid? DeviceId)` (null when missing or expired), and `ClearAsync(HttpContext)` that signs out of the scheme
- [X] T093 [US5] Add the **provider link confirmation** template (`{Frontend}/auth/confirm-link?token=…`, valid 24 h) to `backend/src/Liftingo.Application/Features/Account/Shared/AccountEmails.cs` (the welcome-only template is US7, T110)
- [X] T094 [P] [US5] Implement `backend/src/Liftingo.Application/Features/Account/GetExternalProviders.cs` (`GET`, anonymous) → `200 {"providers":[...]}` with route names of configured providers
- [X] T095 [P] [US5] Implement `backend/src/Liftingo.Application/Features/Account/StartExternalLogin.cs` (`GET {provider}/start?deviceId=`): unknown or unconfigured provider → `404 Auth.ProviderUnavailable`; otherwise `Results.Challenge` with `RedirectUri = "/api/v1" + RouteConsts.Auth.ExternalLoginCallback` and the parsed device id in `AuthenticationProperties.Items`
- [X] T096 [US5] Implement `backend/src/Liftingo.Application/Features/Account/HandleExternalLoginCallback.cs` (`GET`, always `302` to `App:FrontendBaseUrl`), following the contract's callback table row by row. Order: no or invalid external principal → audit failure without user, clear, `error=external`. `FindByLoginAsync` hit → if a provider e-mail belongs to a different account → audit `ExternalLogin` `Failed` for the linked user, clear, `error=emailConflict`; if locked → clear, `error=lockedOut&retryAfter=`; else sign in (`SignIn` kind), audit `ExternalLogin`, clear, `/auth/callback`. E-mail matches a verified account → lock check, `AddLoginAsync`, device, save, issue, audit `ProviderLinked` + `ExternalLogin`, clear, `/auth/callback`. E-mail matches an unverified account, or no match → keep the cookie, `/auth/consent`. Never call `AccessFailedAsync`. Keep `IsNewDevice` for US7 (T112) (depends on T092)
- [X] T097 [P] [US5] Implement `backend/src/Liftingo.Application/Features/Account/GetPendingExternalLogin.cs` (`GET`): no pending → `400 Auth.ExternalLoginExpired`; else `200 {provider, email, emailRequired, accountExists}`, where `accountExists` is true when the provider e-mail belongs to an unverified account (depends on T092)
- [X] T098 [P] [US5] Implement `backend/src/Liftingo.Application/Features/Account/CancelExternalLogin.cs` (`POST`): `PendingExternalLogin.ClearAsync`, `204`
- [X] T099 [US5] Implement `backend/src/Liftingo.Application/Features/Account/CompleteExternalLogin.cs` (`POST`): `CompleteExternalLoginRequest(bool AcceptTermsAndAge, bool AcceptHealthData, bool AcceptMarketing, string? Email)`; `email` required only when the provider gave none. No pending → `400 Auth.ExternalLoginExpired`. Re-check the e-mail against existing accounts and branch as in the contract's `complete` table: **new address** → `CreateUserAsync(password: null, emailConfirmed: provider e-mail present)`, `AddLoginAsync`, `ConsentRecorder.Stage`, device (`Registration`), verification token only for a typed e-mail, save, issue, clear, `201`, verification + welcome e-mail for a typed e-mail (the welcome-only e-mail for a provider e-mail is T111); **provider e-mail of an unverified account** → lock check (`429`), `AddLoginAsync`, `AccountTakeover.StageAsync(Remove)`, device (`SignIn`), save, `AccountTakeover.CompleteAsync` (revoke all), audit `ProviderLinked`, issue, clear, `200`; **typed e-mail of an existing account** → `EmailTokenIssuer.IssueAsync(ProviderLinkConfirmation, provider, key)`, save, link-confirmation e-mail if a token was issued, clear, `202 {"status":"linkConfirmationSent"}`, consents not recorded (depends on T049, T092, T093)
- [X] T100 [US5] Implement `backend/src/Liftingo.Application/Features/Account/ConfirmProviderLink.cs` (`POST`, anonymous): `ConfirmProviderLinkRequest(string Token, bool? AcceptTermsAndAge, bool? AcceptHealthData, bool? AcceptMarketing)`. Follow R17 steps 1–4 in order: unusable → `400 Auth.ProviderLinkInvalid`; pair already linked → consume, save, `409 Auth.ProviderAlreadyLinked`; locked → `429` (token kept); unverified without both consents → `400 Auth.ConsentRequired` (token kept); otherwise `Consume` first (so the takeover doesn't supersede this token), `AddLoginAsync`, takeover (`Remove`) if unverified, device (`SignIn`, `X-Device-Id`), save, revoke all if a takeover happened, audit `ProviderLinked`, issue, `200` + `AuthSessionResponse`. Keep `IsNewDevice` for US7 (T112) (depends on T049)
- [X] T101 [US5] Map the seven external slices in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs` and add `providers`, `pending`, `complete`, `cancel` and `confirm-link` requests to `backend/src/Liftingo.Api/Liftingo.Api.http` (start and callback are browser navigations; add a comment with the start URL)

**Checkpoint**: provider sign-in works with the fake handler. Manual quickstart scenarios 8–10 work with real provider credentials.

---

## Phase 8: User Story 6 - Reset a forgotten password (Priority: P2)

**Goal**: an identical response for known and unknown e-mails, single-use 1 h links, reset that revokes every session and starts none, clears the lockout and known devices, and turns a reset of an unverified account into a takeover with consents.

**Independent test**: `forgot` for a known and an unknown e-mail → identical `202`, one captured e-mail; `reset` with the token → `204` and no `Set-Cookie`; old sessions fail, the old password fails, the new one works.

### Tests for User Story 6 ⚠️

- [X] T102 [P] [US6] Unit test `backend/tests/Liftingo.Api.UnitTests/Features/Account/ResetPasswordValidatorTests.cs` and `ForgotPasswordValidatorTests.cs`: token required; `newPassword` required and ≤ 128; e-mail trimmed, valid, ≤ 256
- [X] T103 [P] [US6] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/ForgotPasswordTests.cs`: registered and unregistered e-mail → identical status (`202`) and body; e-mail sent only for the registered one, with a 1 h token; a 4th request within an hour → `202`, no e-mail (FR-037); a new request supersedes the earlier token; throwing `IEmailSender` → still `202`, log holds template name and user id only; malformed e-mail → `400`
- [X] T104 [P] [US6] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/ResetPasswordTests.cs`: valid token + good password → `204`, no `Set-Cookie: liftingo_rt`; old password fails, new succeeds; every earlier session's refresh and bearer → `401`, including the caller's cookie; `KnownDevices` rows gone; `PasswordResetCompleted` audit; token consumed (second use → `400 Auth.ResetLinkInvalid`); expired (> 1 h, `FakeTimeProvider`), superseded or unknown token → `400 Auth.ResetLinkInvalid`; a policy-breaking password → `400` validation errors, token still usable, password unchanged; a locked account → reset works and clears `LockoutEnd` and `AccessFailedCount`; a provider-only account → can log in with e-mail and password afterwards (FR-036); **unverified** account without consents → `400 Auth.ConsentRequired`, password unchanged, token usable; with both consents → `204`, `EmailConfirmed` true, old consents superseded, 3 new rows, open verification tokens superseded; a verified account ignores the consent fields

### Implementation for User Story 6

- [X] T105 [US6] Add the **password reset** template (`{Frontend}/auth/reset-password?token=…`, valid 1 h) to `backend/src/Liftingo.Application/Features/Account/Shared/AccountEmails.cs`
- [X] T106 [P] [US6] Implement `backend/src/Liftingo.Application/Features/Account/ForgotPassword.cs` (`POST`, anonymous): `ForgotPasswordRequest(string Email)`; for a registered address, `EmailTokenIssuer.IssueAsync(PasswordReset)`, save, send the reset e-mail if a token was issued; always `202` with an empty body (SC-008) (depends on T105)
- [X] T107 [US6] Implement `backend/src/Liftingo.Application/Features/Account/ResetPassword.cs` (`POST`, anonymous): `ResetPasswordRequest(string Token, string NewPassword, bool? AcceptTermsAndAge, bool? AcceptHealthData, bool? AcceptMarketing)`. Unusable token → `400 Auth.ResetLinkInvalid`. Unverified account without both consents `true` → `400 Auth.ConsentRequired` before touching the password. Otherwise `ResetPasswordAsync` (policy errors → `400`, nothing consumed). Then, inside `db.BeginTransactionAsync`: `Consume` first, then `AccountTakeover.StageAsync(Keep)` for an unverified account, `SaveChangesAsync`, `ExecuteDeleteAsync` on the user's `KnownDevices`, commit. After the commit: `RevokeAllAsync`, audit `PasswordResetCompleted`, delete the refresh cookie, `204` with no session (depends on T049)
- [X] T108 [US6] Map `ForgotPassword` and `ResetPassword` in `backend/src/Liftingo.Api/Endpoints/V1Endpoints.cs` and add requests (including the consent variant) to `backend/src/Liftingo.Api/Liftingo.Api.http`

**Checkpoint**: password recovery works on its own on top of US1 and US2.

---

## Phase 9: User Story 7 - Welcome and new-device e-mails (Priority: P3)

**Goal**: exactly one welcome message per account, and a security notice on a successful sign-in from an unknown device.

**Independent test**: log in with a new `X-Device-Id` → one new-device e-mail; again with the same id → none; a provider sign-up with a provider e-mail → one welcome-only e-mail.

### Tests for User Story 7 ⚠️

- [X] T109 [P] [US7] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/AccountEmailTests.cs`: e-mail registration → exactly one e-mail, verification + welcome (US7-1); provider sign-up with a provider e-mail → exactly one welcome-only e-mail without a link (US7-2); provider sign-up with a typed e-mail → exactly one verification + welcome e-mail (FR-025); login from device B after device A → one new-device e-mail containing the sign-in time, the device description and advice to sign out of all devices and reset the password (US7-3); login again from B → no e-mail (US7-4); failed login from a new device → no e-mail (US7-5); the first sign-in right after registration (same device) → no new-device e-mail (US7-6); a provider sign-in into an existing account and a confirmed provider link from a new device → new-device e-mail; after `logout-all` or a password reset, the next login from a formerly known device → new-device e-mail; refresh never sends one **Left out: external-provider flows are tested manually** (no fake provider handler): provider sign-up with a provider e-mail (welcome-only, US7-2), provider sign-up with a typed e-mail (FR-025), and provider sign-in into an existing account from a new device. The confirmed provider link from a new device is covered by staging the `ProviderLinkConfirmation` token directly with `EmailToken.IssueProviderLink`.

### Implementation for User Story 7

- [X] T110 [US7] Add the **welcome-only** and **new device** templates to `backend/src/Liftingo.Application/Features/Account/Shared/AccountEmails.cs`. The new-device e-mail takes the sign-in time (formatted for `Europe/Warsaw`) and the device type, and links to sign-out-of-all-devices and password reset in the frontend (FR-026)
- [X] T111 [US7] Send the welcome-only e-mail after a successful `complete` for a new account created from a provider-supplied e-mail in `backend/src/Liftingo.Application/Features/Account/CompleteExternalLogin.cs`
- [X] T112 [US7] Send the new-device e-mail when `SessionIssuer` reports `IsNewDevice` in `backend/src/Liftingo.Application/Features/Account/Login.cs`, `HandleExternalLoginCallback.cs` (existing-account sign-ins) and `ConfirmProviderLink.cs`, after the session is issued

**Checkpoint**: every user story is implemented and its tests pass.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: privacy and availability sweeps across all stories, docs and manual validation.

- [X] T113 [P] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/SecurityAuditTests.cs`: one captured record per login attempt and per listed event type (FR-014, SC-005) across all flows; records for known users decrypt to the request IP and device type with the user's key; records without a user carry no key, IP or device; no record contains an e-mail, password or token; routine refresh writes none. External-provider flows are tested manually, not here
- [X] T114 [P] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/AuditEncryptionTests.cs`: after the user row is deleted, the `AuditEncryptionKeys` row is gone through the cascade and the captured records can no longer be decrypted (crypto-shredding, US-18 groundwork)
- [X] T115 [P] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/LogPrivacyTests.cs`: run register, login (good, bad, unknown), refresh, logout, verify, forgot, reset and an external sign-in, then assert that no event from `LogEventCollector` contains the test e-mail, its `@` part, the password, any issued token or the client IP (FR-030, R15). Check Identity's and the external handlers' log output at `Information` and above too (R15 *verify*). External-provider sign-in is not covered here (tested manually). The masking enricher (T040) runs before the collector, so this test proves the combined result; add one case that logs a test e-mail on purpose and finds it masked, which shows the enricher is active in the test host
- [X] T116 [P] Integration test `backend/tests/Liftingo.Api.IntegrationTests/Features/Account/SessionStoreUnavailableTests.cs`: with the Redis container paused through `PauseAsync()` and resumed with `UnpauseAsync()` in a `finally` block (or a dedicated factory whose connection string points at a closed port), login, register, refresh and `GET /auth/me` with a valid bearer return `503` with code `Service.Unavailable` and `Retry-After: 30`; accounts and consents are unaffected (FR-015c). Run this class in its own collection so other tests keep a working Redis
- [X] T117 [P] Update `backend/AGENTS.md` common commands with `docker compose up -d` for Redis and the `Jwt:SigningKey` user-secret step from quickstart.md, and run the humanizer skill on the changed text
- [X] T118 Finish `backend/src/Liftingo.Api/Liftingo.Api.http` so it covers every endpoint in the contract, with variables for the access token, device ids and e-mail tokens
- [ ] T119 Run `dotnet build` and all three test projects from `backend/`, then walk through quickstart.md manual scenarios 1–9 (and 10 if a phone-only Facebook account is available; since rev. f scenarios 8–10 run on the development provider sign-in page, T145) against `dotnet run`. Scenarios 8–10 need real provider credentials and are left out. Automated part done (build clean; unit 252, architecture 7, integration 158 pass); the manual scenarios are still to do
- [ ] T120 Run quickstart scenario 11 (SC-011): 1 minute of logins and 1 minute of refreshes against a local Release run with raised `RateLimiting:Anonymous` limits; record p95 (≤ 1 s login, ≤ 300 ms refresh) in the PR description

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup. Blocks every story.
- **US1 (Phase 3)**: after Foundational.
- **US2 (Phase 4)**: after Foundational. Its tests register through `POST /auth/register` (US1) to create users, so in practice it follows US1.
- **US3 (Phase 5)**: needs login and refresh from US2, and `GET /auth/me` from US1.
- **US4 (Phase 6)**: needs US1 (the registration token) and refresh from US2 for the claim check.
- **US5 (Phase 7)**: needs Foundational, `GET /auth/me` from US1 and refresh from US2 for assertions. Independent of US4 and US6.
- **US6 (Phase 8)**: needs US1 and US2. Independent of US4 and US5 (`AccountTakeover` is foundational).
- **US7 (Phase 9)**: adds e-mails to slices from US2 and US5, so it comes after both.
- **Polish (Phase 10)**: after the stories it sweeps (T115 needs every slice).

### Story dependency graph

```text
Setup → Foundational → US1 → US2 → US3
                              ├──→ US4
                              ├──→ US5 ──┐
                              ├──→ US6   ├──→ US7 → Polish
                              └──────────┘
```

### Within each story

- Tests first; they must fail before implementation.
- Shared templates in `AccountEmails.cs` before the slice that sends them.
- Slices before mapping in `V1Endpoints.cs`. `V1Endpoints.cs`, `AccountEmails.cs` and `Liftingo.Api.http` are shared files, so tasks that touch them are never `[P]` with each other.

### Key task-level dependencies

- T013 → T015 (configurations before migration); T014 → T015.
- T024, T031 → T032 (port and scripts before the Redis store).
- T028–T037 → T038 (implementations before DI wiring) → T039 (pipeline).
- T041–T043 → T044 (`SessionIssuer`); T048 → T049 (`AccountTakeover`).
- T050, T051 → T052 (test fixtures before `ApiFactory`).
- T061 → T062; T069 → T070; T081 → T083; T092 → T096/T097/T099; T105 → T106.

---

## Parallel Examples

### Foundational

```text
# Domain, all different files:
T006 UserConsent   T007 KnownDevice   T008 EmailToken   T009 enums
# Application ports once Domain compiles:
T022 IEmailSender  T023 ISecurityAuditLog  T024 ISessionStore  T025 IIdentityService  T026 IAccessTokenIssuer  T027 OpaqueToken
# Infrastructure pieces with no shared file:
T030 JwtAccessTokenIssuer  T031 Lua scripts  T033 SessionValidation  T034 503 handler  T035 e-mail senders  T036 audit sinks
# Shared helpers:
T040 Serilog masking  T041 DeviceDescriptor  T042 RefreshTokenCookie  T043 AuthSessionResponse  T045 SecurityAudit  T046 AccountEmails  T047 EmailTokenIssuer  T048 ConsentRecorder
```

### User Story 2

```text
T066 FailedLoginThrottleTests  T067 LoginTests  T068 RefreshSessionTests
then T069 FailedLoginThrottle, followed by T070 and T071 (separate slice files) and T072 mapping
```

### User Story 5

```text
T086 ExternalProviderOptionsTests  T087 validator tests  T088 callback tests  T089 complete tests  T090 confirm-link tests
then T094 GetExternalProviders  T095 StartExternalLogin  T097 GetPendingExternalLogin  T098 CancelExternalLogin  (parallel)
```

### Across stories (after US2)

```text
Developer A: US4 (T079–T084)
Developer B: US5 (T085–T101)
Developer C: US6 (T102–T108)
```

---

## Implementation Strategy

### MVP first

1. Phase 1 and Phase 2.
2. Phase 3 (US1): registration with consents and sign-in.
3. **Stop and validate**: `RegisterTests` pass, quickstart scenario 1 works.

US1 alone creates accounts but cannot log back in. The smallest useful slice is the P1 set: US1 + US2 + US3 (register, log in, stay signed in, log out).

### Incremental delivery

1. Setup + Foundational → foundation ready.
2. US1 → US2 → US3 → the P1 release (FR-01, FR-02).
3. US4, US5, US6 in any order or in parallel → the P2 release (FR-03, provider sign-in, verification).
4. US7 → notification e-mails.
5. Polish → privacy and availability sweeps, manual quickstart and performance check.

### Out of scope (do not implement)

Log Analytics audit sink, Azure e-mail sender, retention and purge jobs, account deletion, the Angular frontend and the NSwag client (plan.md, Constitution Check VIII).

---

## Notes

- `[P]` tasks touch different files and have no unfinished dependency.
- Identity's lockout uses the system clock. Lockout-expiry tests move `LockoutEnd` in the database; everything else uses `FakeTimeProvider`.
- Commit after each task or logical group, with no `Co-Authored-By: Claude` trailer (project memory).
- Each checkpoint should end with green `dotnet build` and test runs.

---

## Phase 11: Convergence

- [X] T121 CRITICAL: Add the Api HTTP adapters and the plain inputs Application will take instead of `HttpContext`. In `backend/src/Liftingo.Api/Endpoints/Account/Shared/`, create `RefreshTokenCookie.cs` (read, append, delete `liftingo_rt` with unchanged attributes), `RefreshCookieOptions.cs` (`Path` set from `V1Endpoints.PathPrefix + RouteConsts.Auth.SessionPrefix`, replacing the `SessionsOptions.CookiePath` `PostConfigure` in `AuthenticationExtensions`), `ExternalLoginCookie.cs` (authenticates `Liftingo.External` into an `ExternalIdentity?` and clears it), `ClientInfoReader.cs` (`HttpContext` → `ClientInfo.Create`) and `FrontendRedirects.cs` (moved from Application). In Application, add `Common/Security/ExternalIdentity.cs` and `Common/Security/IssuedSession.cs` (`AccessToken`, `AccessTokenExpiresAt`, `AccountSummary Account`, `RefreshToken`, `RefreshTokenLifetime`), replace `ClientInfo.From(HttpContext)` with a pure `ClientInfo.Create(ip, userAgent, deviceIdHeader, deviceIdOverride)` plus `backend/tests/Liftingo.Api.UnitTests/Features/Account/Shared/ClientInfoTests.cs`, and move `AppOptions` to `backend/src/Liftingo.Application/Common/Configuration/AppOptions.cs` per Constitution I, plan R19 (contradicts)
- [X] T122 CRITICAL: Move `UserContext` from `backend/src/Liftingo.Infrastructure/Identity/UserContext.cs` to `backend/src/Liftingo.Api/Security/UserContext.cs` (the `IUserContext` port stays in Application), register it from Api, and move `UserContextTests.cs` to mirror the new path per Constitution I, plan R19 (contradicts)
- [X] T123 CRITICAL: Move authentication scheme registration to Api. `backend/src/Liftingo.Api/Extensions/AuthenticationExtensions.cs` calls `AddAuthentication`, registers JwtBearer (default) and the `Liftingo.External` cookie scheme (`HttpOnly`, `Secure`, `SameSite=Lax`, `ExternalLogin:PendingTimeout`) and `AddAuthorization`. Move `AuthenticationSchemes.cs` and `ExternalLoginPaths.cs` from `Application/Common/Security/` to `backend/src/Liftingo.Api/Security/`. In Infrastructure, replace `AddAuthenticationSchemes` with `AuthenticationBuilder.AddExternalProviders(configuration, signInScheme, callbackPath)` in `Identity/ExternalProviderOptions.cs` and an `IConfigureNamedOptions<JwtBearerOptions>` in `Identity/JwtBearerOptionsSetup.cs` (signing key, issuer, audience, `MapInboundClaims = false`, `SessionValidation` events); Api passes the scheme name and callback path as arguments. Update `ExternalProviderOptionsTests.cs` so it still asserts the X e-mail configuration (R9) per Constitution I, plan R20 (contradicts)
- [X] T124 CRITICAL: Move e-mail templates out of Application behind a record-based port. Change `IEmailSender` to `SendAsync(OutgoingEmail email, CancellationToken ct)` with abstract record `backend/src/Liftingo.Application/Common/Email/OutgoingEmail.cs` (`UserId`, `To`); add the six records in `backend/src/Liftingo.Application/Features/Account/Shared/Emails/` (`VerificationAndWelcomeEmail`, `VerificationEmail`, `PasswordResetEmail`, `ProviderLinkConfirmationEmail`, `WelcomeEmail`, `NewDeviceEmail(UserId, To, SignedInAt, DeviceType)`; data only). In `backend/src/Liftingo.Infrastructure/Email/` create `EmailSender.cs` (renders, hands `EmailMessage` to the transport, catches every non-cancellation exception and logs only e-mail type and user id, FR-029), `AccountEmailRenderer.cs` (the six Polish templates from `AccountEmails`, HTML-encoded, links from `AppOptions.FrontendBaseUrl`), `IEmailTransport.cs`, and rename `ConsoleEmailSender`/`DisabledEmailSender` to `ConsoleEmailTransport`/`DisabledEmailTransport` (FR-028 unchanged); move `EmailMessage` there. Point every handler at the records, sent after the save, and delete `Shared/AccountEmails.cs`. Rename the test double to `backend/tests/Liftingo.Api.IntegrationTests/Infrastructure/CapturingEmailTransport.cs` and swap it in `ApiFactory` per Constitution I, plan R24 (contradicts)
- [X] T125 CRITICAL: Replace the in-memory unknown-e-mail lockout with a Redis-backed one. Add port `backend/src/Liftingo.Application/Common/Security/ILoginThrottle.cs` (`GetLockoutEndAsync(email, ct)`, `RegisterFailureAsync(email, ct)` → `LockoutTriggered?`), implement `backend/src/Liftingo.Infrastructure/Sessions/RedisLoginThrottle.cs` with embedded `Scripts/login-throttle-fail.lua`: key `liftingo:login-throttle:{hex SHA-256 of trimmed upper-cased e-mail}`, hash `failures`/`lockoutEnd`; refuse without counting during a lockout, no TTL while `failures > 0` (no decay), reset `failures` and set `PEXPIRE` = lockout duration on trigger, current time passed from `TimeProvider`, Redis connection/timeout errors → `SessionStoreUnavailableException`. Use it in the Login handler, then delete `Shared/FailedLoginThrottle.cs`, `FailedLoginThrottleTests.cs` and `AddMemoryCache` per FR-013, Constitution I, plan R21 (contradicts)
- [X] T126 CRITICAL: Convert `GetCurrentAccount`, `Register`, `Login` and `RefreshSession` to slice folders `backend/src/Liftingo.Application/Features/Account/{UseCase}/` with one type per file (`{UseCase}Command`/`Query`, `…CommandHandler`/`QueryHandler`, `…CommandValidator` where a validator exists today, `{UseCase}Response`). Validators become injected classes called first in `HandleAsync` (no `static readonly s_validator`). Commands carry `ClientInfo Client` and `string? RefreshToken`; `SessionIssuer.IssueAsync` returns `IssuedSession` and no longer touches cookies; responses hold `IssuedSession`. Add `backend/src/Liftingo.Api/Endpoints/Account/{UseCase}Endpoint.cs` for each, which reads cookies and client info, calls the handler, writes or deletes the refresh cookie and returns the unchanged body. Move `AuthSessionResponse`/`AccountResponse` to `backend/src/Liftingo.Api/Endpoints/Account/Shared/`. Rename validator unit tests to `Features/Account/{UseCase}/{UseCase}CommandValidatorTests.cs`. Integration tests must pass unchanged per Constitution II, plan R22, R23 (contradicts)
- [X] T127 CRITICAL: Convert `Logout`, `LogoutAllDevices`, `VerifyEmail`, `ResendVerificationEmail`, `ForgotPassword` and `ResetPassword` the same way as T126, with Api endpoints in `backend/src/Liftingo.Api/Endpoints/Account/`. Logout and logout-all endpoints always delete the refresh cookie, and reset deletes it on success. `ResetPassword` keeps its explicit transaction in the handler. Rename the validator unit tests to mirror the folders per Constitution II, plan R22, R23 (contradicts)
- [X] T128 CRITICAL: Convert the external slices `GetExternalProviders`, `StartExternalLogin` (query that resolves the scheme; the Api endpoint issues `Results.Challenge`), `HandleExternalLoginCallback`, `GetPendingExternalLogin`, `CompleteExternalLogin` and `ConfirmProviderLink` the same way, taking `ExternalIdentity? External` from `ExternalLoginCookie`. `HandleExternalLoginCallbackResponse(Outcome, IssuedSession?)` with `Outcome` = `SignedIn` | `ConsentRequired`; refusals return `LockedOutError` or the new `Auth.ExternalLoginFailed` / `Auth.ProviderEmailConflict` codes in `Shared/ExternalLoginErrors.cs`, and the endpoint maps each to the redirect in the contract's callback table and clears the external cookie for everything except `ConsentRequired`. Replace `CancelExternalLogin.cs` with an Api-only `backend/src/Liftingo.Api/Endpoints/Account/CancelExternalLoginEndpoint.cs` and delete `Shared/PendingExternalLogin.cs`. Redirect URLs and status codes must not change per Constitution I, II, plan R19, R22 (contradicts)
- [X] T129 CRITICAL: Close the Application boundary. Move `IEndpoint.cs` and `RouteConsts.cs` to `backend/src/Liftingo.Api/Endpoints/`, `ResultExtensions.cs` (`ToProblem`, `RetryAfterProblemResult`) to `backend/src/Liftingo.Api/Http/` and its tests to `backend/tests/Liftingo.Api.UnitTests/Http/ResultExtensionsTests.cs`; `V1Endpoints.cs` maps the Api endpoints. Delete the remaining Application HTTP types (`Shared/RefreshTokenCookie.cs`, `Shared/FrontendRedirects.cs`, `Shared/AuthSessionResponse.cs`, `SessionsOptions.CookiePath`). Switch `ApplicationExtensions.AddApplication` to scan the assembly for classes ending in `CommandHandler`/`QueryHandler` and every `IValidator<T>` (scoped, no Scrutor). Remove `<FrameworkReference Include="Microsoft.AspNetCore.App" />` from `Liftingo.Application.csproj`, and add `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.Options` to `backend/Directory.Packages.props` (pinned to the 10.0.x patch EF Core 10.0.11 pulls in; check with `dotnet list package --include-transitive`) and to the csproj per Constitution I, plan R25 (contradicts)
- [X] T130 Add `backend/src/Liftingo.Infrastructure/Email/EmailOptions.cs` (section `Email`, `DisplayTimeZone` default `Europe/Warsaw`) with an `IValidateOptions` that resolves the id with `TimeZoneInfo.FindSystemTimeZoneById`, registered with `ValidateOnStart`; add `"Email": { "DisplayTimeZone": "Europe/Warsaw" }` to `appsettings.json`; make `AccountEmailRenderer` convert `NewDeviceEmail.SignedInAt` with the configured zone and remove the silent UTC fallback (today in `AccountEmails.FindDisplayZone`). Unit test `backend/tests/Liftingo.Api.UnitTests/Infrastructure/Email/EmailOptionsValidatorTests.cs` (unknown zone fails) per FR-026a, FR-031 (contradicts)
- [X] T131 Unit test `backend/tests/Liftingo.Api.UnitTests/Infrastructure/Email/AccountEmailRendererTests.cs`: a July and a January sign-in show Warsaw summer and winter time; the provider name and device type are HTML-encoded; links are built from `App:FrontendBaseUrl` with the token per FR-026a, plan R24 (missing)
- [X] T132 Integration tests for the Redis lockout: `backend/tests/Liftingo.Api.IntegrationTests/Infrastructure/RedisLoginThrottleTests.cs` against the Redis container (no count during lockout, no decay, reset on trigger, the entry expires only after the lockout, the key holds no e-mail), and a `LoginTests` case where an unknown address locked out through one `ApiFactory` is still refused, with the same `retryAfter`, through a second `ApiFactory` on the same Redis per FR-013, plan R21 (missing)
- [X] T133 Create `backend/tests/Liftingo.ArchitectureTests/LayerResponsibilityTests.cs`: (1) the Application assembly's `GetReferencedAssemblies()` is a subset of the R25 allowlist (`Liftingo.Domain`, `System.*`, `netstandard`, `Microsoft.EntityFrameworkCore` + `.Abstractions`, the three `Microsoft.Extensions` packages, `Microsoft.Extensions.Primitives`, `FluentValidation`); (2) no Application type depends on `Microsoft.AspNetCore.*`, `Microsoft.Extensions.Caching.*`, `StackExchange.Redis` or `Microsoft.EntityFrameworkCore.SqlServer`; (3) no Application type ends in `Endpoint`, and every `*Handler` ends in `CommandHandler`/`QueryHandler` and sits in a `Features.{Module}.{UseCase}` namespace; (4) no handler depends on another handler; (5) Api types other than `Program` and `Liftingo.Api.Extensions` do not depend on `Liftingo.Infrastructure`, and no Api type depends on `IApplicationDbContext` per Constitution I, II, VI, plan R25 (missing)
- [X] T134 After T121–T133, run `dotnet build` and all three test projects from `backend/`. Confirm the integration tests pass with no assertion changes beyond test-infrastructure type renames, that `/openapi/v1.json` lists the same paths and schemas as before the refactor, and that starting with `Email__DisplayTimeZone=Mars/Olympus` stops the app with an options validation error naming `Email:DisplayTimeZone` per plan constraint "no route, status code, header, cookie or body may change", Constitution VII, quickstart rev. e checks (partial)
- [X] T135 Update the Testing section of `backend/AGENTS.md` for the renamed and new test types (`CapturingEmailTransport`, `RedisLoginThrottleTests`, `LayerResponsibilityTests`, unit tests mirroring slice folders), and run the humanizer skill on the changed text per plan delivery step 6 (missing)

---

## Phase 12: Client-supplied provider tokens (rev. f)

**Goal**: replace the server-side redirect flow of Phase 7 with `POST /auth/external/{provider}/login`, which takes a Google ID token or a Facebook access token, verifies it with the provider, and applies the R10 account rules; consents come with that request or with a repeat of it (research R9, R10, contract rev. f). Twitter (X) is deferred.

**Independent test**: on the development page (quickstart scenarios 8–10), a first Google sign-in without consents → `400 Auth.ConsentRequired`; sending the same token again with both consents → `201` and a verified account; a later sign-in → `200`. No integration tests drive this endpoint (decided 2026-10-14).

- [X] T136 Remove the `Microsoft.AspNetCore.Authentication.Google`, `Microsoft.AspNetCore.Authentication.Facebook` and `AspNet.Security.OAuth.Twitter` entries from `backend/Directory.Packages.props` and `backend/src/Liftingo.Infrastructure/Liftingo.Infrastructure.csproj`, and add `Google.Apis.Auth` 1.77.0 to both
- [X] T137 Reshape the provider ports in `backend/src/Liftingo.Application/Common/Security/`: `ExternalIdentity(Provider, ProviderKey, Email?, DisplayName?)` without the device id; new `IExternalIdentityVerifier.VerifyAsync(loginProvider, token, ct)` → `ExternalIdentity?`; `IExternalProviderCatalog` lists `ExternalProviderInfo(RouteName, LoginProvider, ClientId)` and finds one by route name
- [X] T138 Create `backend/src/Liftingo.Infrastructure/Identity/ExternalProviders/`: `ExternalProviderOptions` (`Authentication:Google:ClientId`; `Authentication:Facebook:AppId`, `AppSecret`, `GraphApiVersion` default `v26.0`), `GoogleIdTokenVerifier` (`GoogleJsonWebSignature.ValidateAsync` with `Audience = [ClientId]`; `EmailVerified = false` → no e-mail), `FacebookTokenVerifier` (typed `HttpClient`: `debug_token` with `is_valid` and `app_id`, then `/me?fields=id,name,email` with `appsecret_proof` and `id == user_id`; failures logged with the provider name only), the catalog, and the verifier that dispatches by login provider. Register a provider in `AddInfrastructure` only when its keys are set (R9)
- [X] T139 [P] Unit tests `backend/tests/Liftingo.Api.UnitTests/Infrastructure/Identity/ExternalProviders/GoogleIdTokenVerifierTests.cs` (the validation settings always carry the configured client id as the only audience), `FacebookTokenVerifierTests.cs` (valid token → identity with e-mail; another app's `app_id`, `is_valid: false`, a `/me` id that differs from `user_id`, or an error status → `null`; no e-mail → identity without e-mail; `appsecret_proof` sent) against a stubbed `HttpMessageHandler`, and `ExternalProviderRegistrationTests.cs` (only configured providers are listed, with their client ids; lookup by route name ignores case), replacing `ExternalProviderOptionsTests.cs`
- [X] T140 Create the `ExternalLogin` slice in `backend/src/Liftingo.Application/Features/Account/ExternalLogin/` (command with `Provider`, `Token`, nullable consents, `Email`, `ClientInfo`; handler following the R10 table row by row; validator for `Token` and an optional `Email`; `ExternalLoginResponse(Outcome, IssuedSession?)` with `AccountCreated`, `SignedIn`, `LinkConfirmationSent`), and `ConsentRequiredError(AccountExists, EmailRequired)` in `Features/Account/Shared/`. Move `Auth.ProviderUnavailable` into `ExternalLoginErrors` and drop `Auth.ExternalLoginExpired`. `GetExternalProviders` returns names with client ids. Delete `StartExternalLogin`, `HandleExternalLoginCallback`, `GetPendingExternalLogin` and `CompleteExternalLogin`
- [X] T141 In `backend/src/Liftingo.Api/`: add `ExternalLoginEndpoint.cs` and `ExternalLoginRequest.cs` (`201` for `AccountCreated`, `200` for `SignedIn`, `202 {"status":"linkConfirmationSent"}`); map `ConsentRequiredError` to `accountExists` and `emailRequired` problem extensions in `Http/ResultExtensions.cs`; update `RouteConsts`; delete the start, callback, pending, complete and cancel endpoints, `ExternalLoginCookie`, `FrontendRedirects`, `Security/AuthenticationSchemes.cs`, `Security/ExternalLoginPaths.cs`, the `Liftingo.External` cookie scheme, and the device-id override in `ClientInfoReader` and `ClientInfo.Create`
- [X] T142 Remove `ExternalLoginOptions`, its validator, its registration, the `ExternalLogin` section in `appsettings.json` and its unit tests; rename `CompleteExternalLoginCommandValidatorTests` to `ExternalLogin/ExternalLoginCommandValidatorTests.cs` for the new validator; add a `ResultExtensionsTests` case for the consent extensions
- [X] T143 Add the Development-only page `backend/src/Liftingo.Api/DevPages/provider-sign-in.html`, served at `/dev/provider-sign-in` only when the environment is Development, with the route string in `RouteConsts` (mapped outside the `/api/v1` group): reads `GET /api/v1/auth/external/providers`, loads Google Identity Services and the Facebook JavaScript SDK for the configured providers, keeps the last token in memory and in an editable field, sends it with the consent boxes, e-mail and a `localStorage` device id, offers "Send again", and prints each response
- [X] T144 Replace the external requests in `backend/src/Liftingo.Api/Liftingo.Api.http` with `providers`, the provider login (token placeholder, with and without consents) and `confirm-link`; point to the dev page for real tokens
- [ ] T145 Run `dotnet build` and all three test projects from `backend/`, check `/openapi/v1.json` lists `POST /api/v1/auth/external/{provider}/login` and none of the removed paths, and walk through quickstart scenarios 8–10 with real Google and Facebook credentials (the scenarios need the developer's provider apps)
