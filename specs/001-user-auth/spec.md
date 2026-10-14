# Feature Specification: User Authentication (Register, Login, Logout, Password Reset)

**Feature Branch**: `001-user-auth`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "We need to implement login, logout and register features. It should be possible to use external providers like Google, Facebook, Twitter to login or register. We should support JWT access and refresh tokens. It should be also possible to send emails like veryfing an email, welcome email, new device logged in. For now email service should only work for local development and don't include any setup for azure one. We can use for example email service interface and email console logger service."

**Related PRD items**: US-01 (Registration), US-24 (Login and password reset), FR-01, FR-02, FR-03, NFR-13, NFR-15, NFR-17, NFR-21, NFR-22, NFR-40, NFR-41

## Clarifications

### Session 2026-10-12

- Q: Which external providers are in scope? → A: Google, Facebook and Twitter (X). The PRD (change log #12) and the constitution (v1.0.2) were amended to match.
- Q: Should the welcome e-mail be a separate message? → A: No. As in PRD US-01, the verification e-mail doubles as the welcome e-mail. Accounts that skip verification (provider-verified e-mail) get a welcome-only e-mail.
- Q: Is password reset in scope? → A: Yes. "Forgot password" (FR-03, US-24) is part of this feature.
- Q: Which providers count as having confirmed a user's e-mail (skip verification, link to an existing account automatically)? → A: All three: Google, Facebook and Twitter (X).
- Q: Where is the renewal credential (refresh token) kept on the client? → A: In a secure, HttpOnly, SameSite cookie sent only to the renewal and logout endpoints. The access credential is returned in the response body and kept in memory only.
- Q: How many e-mail-sending requests (reset, resend verification) are allowed per address? → A: At most 3 e-mails of each type per address per hour (configurable). Extra requests get the same response, but no e-mail is sent.
- Q: Can users sign out of all devices at once? → A: Yes. A "sign out of all devices" action in account settings revokes every session, including the current one. The new-device e-mail points to it and to password reset.
- Q: Which security events besides login attempts go into the audit log? → A: Logout, sign out of all devices, password reset completed, provider linked, lockout triggered, and refresh-token reuse detected. Routine token renewals are not logged.
- Q: Which sessions does a completed password reset sign out? → A: All of them. The user is not signed in after the reset and logs in with the new password.
- Q: After a session is revoked, does its unexpired access credential keep working? → A: No. Revoking a session (logout, sign out of all devices, password reset, renewal-credential reuse) makes its access credential stop working on the next request.
- Q: Do failed logins for an unregistered e-mail lead to the same lockout response? → A: Yes. Unregistered addresses get the same counter, the same 15-minute lockout and the same retry-time message, so the lockout reveals nothing.
- Q: What happens when the e-mail a user types in at the provider fallback (FR-023) already belongs to an account? → A: A single-use link valid for 24 hours is sent to that address, and the provider is linked to the existing account only when the link is opened. The user is told to check their inbox. This response differs from the new-address case (account created and signed in); that is accepted because e-mail registration already reveals registered addresses (US1 AS-4).
- Q: When a provider sign-in takes over an unverified account (pre-hijack protection), must the owner give the required consents again? → A: Yes. The consent interstitial is shown before they are signed in, their consents are recorded as new records, earlier ones are kept as history marked superseded, and account data is kept.

### Session 2026-10-13

- Q: Where should the failed-login counter and lockout for e-mail addresses that have no account be stored? → A: In the shared session store, not in process memory, so it is the same on every server instance and survives restarts.
- Q: When an action both saves data and sends an e-mail, must the e-mail go out only after the data is saved, and can a lost e-mail just be requested again? → A: Yes. E-mails are sent after the save succeeds, in the same request, best-effort. A failed or lost e-mail is logged without personal data and can be requested again. There is no outbox.
- Q: In which time zone should e-mails show times, such as the sign-in time in the new-device e-mail? → A: Polish time (`Europe/Warsaw`, daylight saving applied), set in configuration. The app refuses to start if the configured zone is unknown on the host; there is no fallback to UTC.

### Session 2026-10-14

- Q: Is Twitter (X) still in scope? → A: No, it is deferred to a later feature. X has no identity token, so the client cannot hand the API a credential the way it does for Google and Facebook. This feature supports Google and Facebook. The PRD and the constitution were amended to match. This replaces the provider answers of 2026-10-12.
- Q: How does a provider sign-in reach the API? → A: The client signs in at the provider itself and sends the provider's credential (a Google identity token, a Facebook access token) to the API, which checks it with the provider and signs the user in. The API keeps no state between the provider and the client: no redirects, no temporary cookie.
- Q: When are the consents for a provider sign-up given? → A: Either before the provider sign-in (the registration page asks for them first) or after it, when the API answers that consents are required (the user started from the login page and has no account yet). In the second case the client sends the same provider credential again together with the consents. Nothing is stored until the consents arrive.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register with e-mail and password (Priority: P1)

A new visitor creates a Liftingo account with an e-mail address and a password. On the registration form they tick the required consents: age 18+ with acceptance of the Terms and Privacy Policy, and explicit consent to processing of health-category data. Marketing e-mails are an optional consent. Once the account exists the user is signed in and can start using the app. A verification e-mail is sent to the address they gave.

**Why this priority**: Without an account nobody can use any other part of Liftingo. This is the entry point to the product and the first place personal data and consents are collected.

**Independent Test**: Submit the registration form with valid data and the required consents. Confirm that the account exists, the user is signed in, the consents are recorded with timestamp and document version, and a verification e-mail shows up in the development e-mail output.

**Acceptance Scenarios**:

1. **Given** a visitor with no account, **When** they submit a valid e-mail, a password that meets the password policy, and both required consents, **Then** an account is created, the user is signed in, and a verification e-mail is sent.
2. **Given** a visitor on the registration form, **When** they submit without ticking the age/Terms consent or the health-data consent, **Then** registration is refused and the message for the health-data consent explains that body weight is needed to compute safe starting loads.
3. **Given** a visitor, **When** they submit a password shorter than 8 characters, or one with no digit or no special character, **Then** registration is refused with a message stating which rule failed.
4. **Given** an e-mail address that already belongs to an account, **When** a visitor tries to register with it, **Then** registration is refused without creating a second account.
5. **Given** a successful registration, **When** consents are stored, **Then** each consent records its type, its value, the time it was given, and the Terms/Privacy Policy version that was shown.

---

### User Story 2 - Log in and stay signed in (Priority: P1)

A returning user signs in with their e-mail and password. They stay signed in across app restarts and do not have to log in again until they log out, go 30 days without using the app, or reach 90 days since that sign-in. Repeated failed attempts lock the account for a while to block password guessing.

**Why this priority**: Returning users need it on every visit. Without login, registration is useless after the first session.

**Independent Test**: Log in with valid credentials, close and reopen the app, and confirm the user is still signed in. Then try 5 wrong passwords and confirm the lockout.

**Acceptance Scenarios**:

1. **Given** a registered user, **When** they log in with the correct e-mail and password, **Then** they are signed in and get access to their account.
2. **Given** a signed-in user, **When** their short-lived access expires while they are still using the app, **Then** access is renewed silently without asking them to log in again.
3. **Given** a signed-in user who has not used the app for more than 30 days, **When** they open it, **Then** they must log in again.
4. **Given** a registered user, **When** they enter a wrong password 5 times in a row, **Then** the account is locked for 15 minutes and they are told when they can try again.
5. **Given** a locked account, **When** the lockout period ends and the user logs in correctly, **Then** login succeeds and the failed-attempt counter resets.
6. **Given** any login attempt for a registered e-mail, successful or not, **When** it completes, **Then** an audit record is stored with the outcome, timestamp, IP address and device type. For an unregistered e-mail the record holds only the outcome and timestamp (FR-014).
7. **Given** an incorrect e-mail or password, **When** login fails, **Then** the message does not reveal whether the e-mail is registered.
8. **Given** an e-mail address that is not registered, **When** 5 consecutive logins fail for it, **Then** the sixth attempt gets the same lockout message and retry time as a registered account would.
9. **Given** a user who has used the app every day since signing in, **When** 90 days have passed since that sign-in, **Then** they must log in again.

---

### User Story 3 - Log out (Priority: P1)

A signed-in user logs out of the current device. After that the session on this device cannot be used or renewed. Sessions on the user's other devices keep working. If the user suspects someone else has access, they can also sign out of all devices at once.

**Why this priority**: Users need to end a session, especially on shared devices. Logout is also required to bound the 30-day persistent session.

**Independent Test**: Log in, log out, then confirm that the old session can no longer be renewed or used to reach protected data.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they log out, **Then** the session on that device is ended and its renewal credential is revoked.
2. **Given** a user who has logged out, **When** someone tries to renew the session with the revoked credential, **Then** the attempt is rejected, and so is any request made with the access credential that session had before logout.
3. **Given** a user signed in on two devices, **When** they log out on one, **Then** the other device stays signed in.
4. **Given** a user signed in on several devices, **When** they choose "sign out of all devices", **Then** every session, including the current one, is revoked and every device must sign in again.

---

### User Story 4 - Verify e-mail address (Priority: P2)

After registering, the user gets an e-mail with a single-use verification link that is valid for 24 hours. Following the link marks the address as verified. Until then the user can use the app with some restrictions and can ask for a new link.

**Why this priority**: Verification protects against typos and fake sign-ups and unlocks social features, export, and the full AI quota. However, the user can start training without it, so it ranks below register, login and logout.

**Independent Test**: Register, take the link from the development e-mail output, open it, and confirm that the account is marked verified and the link cannot be used again.

**Acceptance Scenarios**:

1. **Given** a newly registered, unverified user, **When** they open the verification link within 24 hours, **Then** their e-mail is marked as verified.
2. **Given** an unverified account and a verification link for it that is older than 24 hours or superseded, **When** the link is opened, **Then** verification fails with a clear message and an option to request a new link.
3. **Given** an unverified user, **When** they ask for a new verification e-mail, **Then** a new link is sent and earlier unused links stop working.
4. **Given** an unverified user, **When** they use the app, **Then** the system reports their status as unverified so that other features can restrict social features, data export and AI generation as described in the PRD.
5. **Given** an account that is already verified, **When** one of its verification links is opened again (used, expired or not), **Then** the user sees a success message saying the address is already verified.

---

### User Story 5 - Register or log in with an external provider (Priority: P2)

A visitor or returning user chooses to sign in with Google or Facebook. A new user gives the same consents as on the e-mail registration form, either before going to the provider (registration page) or right after it (login page, when the API says consents are required), and the account is not created until both required consents are given. On later visits the user is signed straight in.

**Why this priority**: It lowers the barrier to sign-up, but e-mail registration already covers the core need, so the product works without it.

**Independent Test**: Sign in with a configured provider for the first time, accept the consents, and confirm an account is created and signed in. Sign out, sign in again with the same provider, and confirm the same account is used.

**Acceptance Scenarios**:

1. **Given** a visitor with no account, **When** they finish sign-in with a supported provider without having given the consents, **Then** they are asked for them, and no account is created until the two required consents are accepted.
2. **Given** a visitor who is asked for the consents after a provider sign-in, **When** they decline or leave, **Then** no account and no personal data from the provider are kept.
3. **Given** a user who has already registered through a provider, **When** they sign in with that provider again, **Then** they are signed in to their existing account without being asked for consents. Consents sent along anyway are ignored.
4. **Given** a supported provider that returns an e-mail address, **When** an account is created from it, **Then** the account's e-mail is treated as verified and no verification e-mail is needed.
5. **Given** a provider sign-in whose e-mail matches an existing e-mail/password account, **When** the user completes it, **Then** the provider is linked to that existing account and the user is signed in to it without a password prompt.
6. **Given** a provider that returns no e-mail address, **When** a new account would be created, **Then** the user is asked for an e-mail address, which then goes through normal verification.
7. **Given** a provider that returns no e-mail address, **When** the user types in an address that already belongs to an account, **Then** no account is created, a link-confirmation e-mail is sent to that address, the user is told to check their inbox, and the provider is linked to the existing account only after the link is opened.
8. **Given** a provider credential that is invalid, expired, or was issued for another application, **When** the client sends it, **Then** sign-in is refused with a generic message, and nothing from the provider is kept.
9. **Given** a visitor with no account who has given the consents on the registration page, **When** they finish sign-in with a supported provider, **Then** the account is created and they are signed in without being asked again.

---

### User Story 6 - Reset a forgotten password (Priority: P2)

A user who has forgotten their password asks for a reset link on the login screen. They receive a single-use link valid for 1 hour, choose a new password, and every session on the account is signed out. They then log in with the new password. The response to the request never reveals whether the e-mail is registered.

**Why this priority**: Without it a user who forgets their password loses access to all their training data. It is P0 in the PRD (FR-03), but it is only needed once accounts exist and users can log in.

**Independent Test**: Request a reset, take the link from the development e-mail output, set a new password, and confirm that the old password no longer works, the new one does, and sessions on other devices are revoked.

**Acceptance Scenarios**:

1. **Given** a registered user, **When** they request a password reset for their e-mail, **Then** a reset e-mail with a single-use link valid for 1 hour is sent.
2. **Given** an e-mail that is not registered, **When** a reset is requested for it, **Then** the response looks exactly the same as for a registered e-mail and no e-mail is sent.
3. **Given** a valid reset link, **When** the user sets a new password that meets the password policy, **Then** the password is changed, the link stops working, all of the user's sessions are revoked, and the user is not signed in until they log in with the new password.
4. **Given** a used, expired or superseded reset link, **When** it is opened, **Then** the reset is refused with a message offering to request a new link.
5. **Given** a valid reset link, **When** the user enters a password that breaks the password policy, **Then** the reset is refused, the link stays valid until it expires, and the password is unchanged.
6. **Given** an account locked after failed login attempts, **When** the user completes a password reset, **Then** the lockout and the failed-attempt counter are cleared.
7. **Given** a user who has only signed in through an external provider, **When** they complete a password reset, **Then** they can from then on also log in with e-mail and password.
8. **Given** an account whose e-mail is not verified yet, **When** the user completes a password reset and gives both required consents, **Then** the e-mail is marked verified, the consents are recorded as new records, and the earlier ones are marked superseded.
9. **Given** an account whose e-mail is not verified yet, **When** the user submits a new password without both required consents, **Then** the reset is refused, the password is unchanged, and the link stays valid until it expires.

---

### User Story 7 - Welcome and new-device e-mails (Priority: P3)

The verification e-mail sent at registration doubles as the welcome e-mail. Users whose e-mail is already verified by their provider skip verification, so they get a welcome-only e-mail instead. When their account is signed in from a device it has not been used on before, they receive a security notice so they can react if it was not them.

**Why this priority**: These messages improve trust and security awareness, but the core flows work without them.

**Independent Test**: Log in from a new device (a different browser) and confirm that a new-device e-mail appears in the development output. Log in again from the same device and confirm that no second notice is sent.

**Acceptance Scenarios**:

1. **Given** a visitor registering with e-mail and password, **When** registration succeeds, **Then** exactly one e-mail is sent, combining the welcome message and the verification link.
2. **Given** a visitor registering through a provider that confirms their e-mail, **When** the account is created, **Then** exactly one welcome e-mail without a verification link is sent.
3. **Given** a user who has only signed in from device A, **When** they sign in successfully from device B, **Then** a new-device e-mail is sent with the time of sign-in and a description of the device type, plus advice to sign out of all devices and reset the password if it was not them.
4. **Given** a user signing in from a device they have used before, **When** login succeeds, **Then** no new-device e-mail is sent.
5. **Given** a failed login attempt from a new device, **When** it is rejected, **Then** no new-device e-mail is sent.
6. **Given** the user's very first sign-in right after registration, **When** it completes, **Then** no new-device e-mail is sent for it.

---

### Edge Cases

- E-mail addresses that differ only in letter case (e.g. `Anna@x.pl` and `anna@x.pl`) count as the same address for registration and login.
- A renewal credential that has already been used once is presented again (possible theft): the request is rejected and every session from that sign-in chain is revoked.
- One device sends two renewals at once (several tabs) or retries a renewal after losing the response: this looks the same as theft, so that device's session ends and it must sign in again. Other devices are not affected (FR-011). The client avoids it by sending one renewal at a time.
- Two registrations for the same e-mail arrive at the same moment: exactly one account is created, and the other request gets the normal "already registered" response.
- A user with only an external provider (no password) tries to log in with e-mail and password: login fails with the same generic message as a wrong password.
- Login is attempted on an account that is scheduled for deletion (7-day grace period): out of scope for this feature and handled by the account-deletion feature.
- An unverified account reaches 7 days without verification: the deletion job belongs to the retention work (NFR-40). This feature only records the verification status and the registration date the job needs.
- E-mail sending fails: registration and login still succeed, the failure is logged without personal data, and the user can request the verification e-mail again.
- Someone registers with another person's e-mail and never verifies it. The real owner later signs in with a provider using that address: the account is linked and marked verified, the unverified password is removed, every earlier session is revoked, and the owner must accept the required consents again before being signed in.
- Someone registers with another person's e-mail and never verifies it. The real owner later resets the password for that address: the reset asks for the required consents, marks the address verified and revokes every earlier session, so the person who registered it loses access (FR-035).
- The user opens a verification link while signed in to another account: the link verifies the account it was issued for, not the signed-in one.
- Someone requests a fourth password reset for the same address within an hour: they get the usual response, but no e-mail is sent, so the inbox cannot be flooded and the response still reveals nothing.
- Several devices renew sessions at the same time: each device's session renews on its own without signing the others out.
- A provider identity is linked to account A, but the provider now returns an e-mail that belongs to account B: sign-in is refused and neither account changes (FR-022a).
- The session store is unavailable: login, renewal and signed-in requests fail with "service temporarily unavailable" (FR-015c); accounts and consents are unaffected.

## Requirements *(mandatory)*

### Functional Requirements

**Registration**

- **FR-001**: System MUST let a visitor create an account with an e-mail address and a password.
- **FR-002**: System MUST enforce the password policy: at least 8 and at most 128 characters, with at least one digit and at least one special character (NFR-15). A special character is any character that is neither a letter nor a digit, spaces included. Letters from any alphabet, including Polish letters such as "ą" or "ż", count as letters, not as special characters.
- **FR-003**: System MUST require two explicit, unticked-by-default consents before creating any account: (a) age 18+ and acceptance of the Terms and Privacy Policy, and (b) consent to processing of health-category data. Marketing consent MUST be optional.
- **FR-004**: System MUST record every consent decision with its type, value, timestamp and the document version shown (NFR-21, NFR-22). Marketing consent defaults to "not given"; when the user leaves it unticked, System MUST record it as an explicit refusal (value false).
- **FR-005**: System MUST treat e-mail addresses case-insensitively and allow at most one account per address. Before comparing or storing an address, System MUST trim surrounding whitespace and check that it has a valid e-mail format and at most 256 characters. No other normalisation applies (for example, dots in Gmail addresses are significant). Refusing a registration for an address that already has an account (US1 AS-4) reveals that the address is registered; this is accepted, because hiding it would mean not signing the user in right away (FR-006).
- **FR-006**: System MUST sign the user in right after successful registration.

**Login and sessions**

- **FR-007**: System MUST authenticate users with e-mail and password.
- **FR-008**: System MUST issue a short-lived access credential and a longer-lived renewal credential on successful login or registration. The access credential MUST expire 15 minutes after it is issued (default, configurable per FR-031).
- **FR-008a**: The renewal credential MUST be delivered and stored only as a secure, HttpOnly cookie with `SameSite=Lax` that page scripts cannot read and that the browser sends only to the renewal and logout endpoints. The frontend and the API are served from the same site. The access credential MUST be returned in the response body, and the client MUST keep it in memory only, never in persistent browser storage.
- **FR-009**: System MUST let a client exchange a valid renewal credential for a new access credential and a new renewal credential. The old renewal credential becomes invalid after one use (rotation).
- **FR-010**: System MUST keep a session renewable for 30 days of inactivity. Each successful renewal restarts the 30-day window.
- **FR-010a**: A session MUST also end when it reaches its maximum absolute lifetime of 90 days (default, configurable per FR-031), counted from the sign-in that started it, however active the user is. Renewals do not extend it. The user must then sign in again.
- **FR-011**: System MUST detect reuse of a renewal credential that has already been rotated, reject it, and revoke all sessions from that sign-in chain. A chain is the series of renewal credentials that started with one sign-in on one device, so reuse ends only that device's session; the user's other devices stay signed in. This also applies when the same device presents an old credential by mistake (two renewals at once, or a retry after a lost response); the client is expected to send one renewal at a time.
- **FR-012**: System MUST lock an account for 15 minutes after 5 consecutive failed login attempts, tell the user when they can retry, and reset the counter after a successful login. Further rules:
  - Attempts made while the account is locked MUST be rejected without being checked or counted, and MUST NOT extend the lockout.
  - The counter MUST NOT decay over time. It resets only on a successful login, when a lockout is triggered, and on a completed password reset (FR-035).
  - While the account is locked, sign-in with an external provider (FR-020) MUST also be refused with the same retry-time message. A refused or cancelled provider sign-in does not count as a failed attempt.
  - A locked account MAY request and complete a password reset; completing it clears the lockout (FR-035).
- **FR-013**: System MUST return the same generic error for an unknown e-mail and for a wrong password. The lockout rules of FR-012 MUST apply to an unregistered address in the same way, so a lockout response never reveals whether the address is registered. The counter and lockout for an unregistered address MUST be kept in the shared session store, never in process memory, so they are the same on every server instance and survive a restart; the store keys them by a hash of the address, never the address itself. An entry is deleted only once its counter is zero and its lockout has ended. If the store is unavailable, FR-015c applies.
- **FR-014**: System MUST write an audit record for every login attempt with outcome, timestamp, IP address and device type (NFR-17). It MUST also write an audit record, with event type, timestamp, IP address and device type, for: logout, sign out of all devices, completed password reset, provider linked to an account, lockout triggered, and renewal-credential reuse detected. Routine renewals MUST NOT be audited. When the attempt or event concerns an e-mail address that belongs to no account, or a provider sign-in that fails before any account is known, the record MUST hold only the event type, outcome and timestamp: no user, e-mail, IP address or device type. Audit records MUST be retained for 90 days (NFR-40); the purge job itself can be built by the retention work.
- **FR-014a**: Audit records MUST be kept apart from application and error logs (constitution III). They MUST NOT contain e-mail addresses, passwords or credentials. The IP address and device type in them MUST be stored protected per user, so that deleting the account makes that user's audit data unreadable even before the 90 days have passed.
- **FR-014b**: The device type in audit records and e-mails is a short description derived from the client's browser identification: form factor (phone, tablet or computer), browser family and operating-system family, for example "Chrome, Windows, komputer".
- **FR-015**: System MUST let a signed-in user log out, which revokes the renewal credential of the current session only and clears the renewal cookie. Logout MUST work with the renewal cookie alone, even when the access credential has already expired.
- **FR-015a**: System MUST let a signed-in user sign out of all devices, which revokes every one of their sessions, including the current one, and clears their known-device list (FR-026), so the next sign-in on any device triggers a new-device e-mail.
- **FR-015b**: When a session is revoked for any reason (logout, sign out of all devices, password reset, renewal-credential reuse, pre-hijack linking in FR-022), every request made with that session's access credential MUST be rejected from then on, even if the credential has not expired yet.
- **FR-015c**: If the session store is unavailable, login, renewal and every request that needs a session check MUST fail with a "service temporarily unavailable" response (HTTP 503), not a generic server error.

**E-mail verification**

- **FR-016**: System MUST send a verification e-mail with a single-use link valid for 24 hours after e-mail/password registration.
- **FR-017**: System MUST mark the account as verified when a valid link is used, and reject used, expired or superseded links with a message that offers a new one. If the account the link was issued for is already verified (for example, through a provider in FR-022 or a password reset in FR-035), opening any of its verification links MUST instead report success with an "address already verified" message. This holds only while the link's record still exists (FR-038); a link whose record has been deleted is reported as invalid.
- **FR-018**: System MUST let an unverified user request a new verification e-mail. Issuing a new link invalidates earlier unused links. A resent verification e-mail contains only the verification link, without the welcome text (FR-025).
- **FR-019**: System MUST expose the account's verification status so other features can apply the restrictions for unverified accounts. A status carried in an access credential issued before verification may stay "unverified" until that credential expires (FR-008). This lag only ever errs on the restrictive side and is accepted; the client renews its access credential right after a successful verification so the new status applies at once.

**External providers**

- **FR-020**: System MUST let users register and log in with Google and Facebook. The client signs in at the provider and sends the provider's credential to System, which MUST check it with the provider before trusting anything in it: that it is genuine, unexpired and issued for Liftingo's own application at that provider. If the check fails, System MUST refuse the sign-in with a generic "sign-in with <provider> did not complete" message, keep no provider data, and audit it as a failed login attempt (FR-014). A sign-in the user cancels at the provider never reaches System and is not audited.
- **FR-021**: System MUST NOT create an account or keep provider data until both required consents are given. The client MAY send the consents together with the provider credential. If a provider sign-in would create an account (or take one over, FR-022) and the consents are missing, System MUST answer that consents are required, store nothing, and let the client send the same credential again with the consents. System keeps no pending sign-in between the two requests; the provider credential's own expiry bounds how long the user can take.
- **FR-022**: System MUST treat an e-mail address returned by any supported provider (Google, Facebook) as confirmed. When it matches an existing account, System MUST link the provider identity to that account automatically. If that account's e-mail was never verified, linking MUST also mark it verified, remove its password and revoke its existing sessions, so that whoever registered the address without proving ownership loses access (protection against account pre-hijacking). Before the user is signed in to such an account, System MUST require the consents as in FR-021, even if the account already has consents, and record them as new consent records. Earlier consent records stay as history, marked superseded. Account data is kept. The user can set a password again through password reset (FR-036).
- **FR-022a**: If a provider identity is already linked to one account and the provider now returns an e-mail address that belongs to a **different** account, System MUST refuse the sign-in with a message, sign nobody in, and change neither account. E-mail addresses stay unique per account (FR-005). A provider e-mail that belongs to no account does not block sign-in to the linked account and does not change that account's e-mail.
- **FR-023**: System MUST ask for an e-mail address when a provider does not supply one, and verify that address as in FR-016.
- **FR-023a**: If the address typed in under FR-023 already belongs to an account, System MUST NOT link or create anything immediately. It MUST send a single-use link-confirmation e-mail, valid for 24 hours, to that address, and link the provider identity to the existing account only when the link is opened. Until then it keeps only the provider name and the provider's user identifier, attached to that link. Opening the link applies the same rules as FR-022 (an unverified account is marked verified, its password removed, its sessions revoked, and the consents asked for again) and signs the user in. The user is told to check their inbox. This reveals that the address has an account, which is accepted because e-mail registration already does (US1 AS-4). FR-037's limit applies to these e-mails as well.
- **FR-024**: Accounts created from a provider-supplied e-mail (FR-022) MUST be marked verified without sending a verification e-mail. An e-mail the user typed in themselves (FR-023) is not provider-supplied and MUST be verified.

**Notification e-mails**

- **FR-025**: The verification e-mail from FR-016 MUST double as the welcome e-mail (PRD US-01). Accounts created with a provider-supplied e-mail (FR-024) MUST instead receive a welcome-only e-mail without a verification link. Accounts created through a provider with an e-mail the user typed in (FR-023) MUST receive the same combined welcome and verification e-mail as e-mail/password registration. A user MUST receive exactly one welcome message.
- **FR-026**: System MUST recognise devices a user has signed in from before and send a new-device e-mail on a successful sign-in from an unrecognised device. The e-mail includes the sign-in time and device type, tells the user to sign out of all devices (FR-015a) and reset their password if it was not them, and MUST NOT be sent for the first sign-in after registration or for failed attempts. A sign-in here means a successful e-mail/password login, a successful provider sign-in, or a confirmed provider link (FR-023a); session renewals are not sign-ins. "Registration" covers every way of creating an account: e-mail/password and provider sign-up, with or without a typed-in e-mail. A known device that has not been used to sign in for 12 months MUST be forgotten (default, configurable per FR-031); the clean-up job itself can be built by the retention work.
- **FR-026a**: Times shown in e-mails (for example the sign-in time in FR-026) MUST be in the configured display time zone, `Europe/Warsaw` by default, with daylight saving applied. If the configured zone is not known on the host, the application MUST refuse to start rather than show times in another zone.
- **FR-027**: All e-mail sending MUST go through one sending abstraction so that the delivery mechanism can be swapped without changing the features that send e-mails.
- **FR-028**: In this feature the only delivery mechanism MUST be a development-only one that writes each outgoing e-mail (recipient, subject, body including links) to the local application output instead of sending it. It MUST NOT be active outside the local development environment. Outside it, each e-mail MUST be discarded without being sent, with a log entry that names only the e-mail type and user id, and the flow that triggered it MUST still succeed. Production delivery is out of scope.
- **FR-029**: A failure to send an e-mail MUST NOT make registration, login, verification or a password-reset request fail, and MUST be logged without personal data. An e-mail MUST be sent only after the data it refers to (account, token, session) has been saved, within the same request; if the save fails, no e-mail is sent. Delivery is best-effort: an e-mail lost after the save is not retried, and the user recovers by requesting it again (resend verification, a new reset request or a new link confirmation).

**Privacy**

- **FR-030**: Application and error logs MUST NOT contain e-mail addresses, passwords, names or credentials, except for the development-only e-mail output in FR-028.
- **FR-031**: All time windows and thresholds in this feature (15 min access lifetime, 30-day inactivity, 90-day absolute session lifetime, 24 h verification and link-confirmation validity, 1 h reset link validity, 3 e-mails per address per hour, 5 attempts, 15 min lockout, 90-day audit retention, 12-month known-device retention, 24 h retention of unusable e-mail tokens) MUST be configuration values. So MUST the display time zone for e-mails (FR-026a).

**Password reset**

- **FR-032**: System MUST let a user request a password reset by e-mail. The request MUST get an identical response whether or not the e-mail is registered, and an e-mail MUST be sent only to registered addresses.
- **FR-033**: System MUST send a reset e-mail with a single-use link valid for 1 hour. Issuing a new link invalidates earlier unused reset links.
- **FR-034**: System MUST let the holder of a valid reset link set a new password that meets the password policy (FR-002), and MUST reject used, expired or superseded links.
- **FR-035**: A successful password reset MUST revoke all of the user's sessions, clear their known-device list (FR-026), clear any lockout and the failed-attempt counter, and invalidate the reset link. If the account's e-mail was not verified yet, the reset MUST mark it verified, because opening the link proved ownership of the address. Because this takes the account over from whoever registered the address, the reset of an unverified account MUST also require both required consents (FR-003) to be given again before it completes, record them as new consent records and mark the earlier ones superseded, as in FR-022; without them the reset is refused and the link stays valid until it expires. It MUST NOT start a new session; the user signs in with the new password.
- **FR-036**: A user who previously had no password (provider-only account) MUST be able to set one through password reset.

**E-mail limits**

- **FR-037**: System MUST send at most 3 password-reset e-mails, at most 3 verification e-mails (including the one sent at registration) and at most 3 link-confirmation e-mails (FR-023a) per address per rolling hour. Requests over the limit MUST get the same response as accepted requests, but no e-mail is sent. Welcome-only and new-device e-mails are not limited.

**Data retention**

- **FR-038**: An e-mail token that can no longer be used (used, expired or superseded) MUST be deleted 24 hours after it stopped being usable (default, configurable per FR-031); the clean-up job itself can be built by the retention work. Revoked and expired sessions MUST be deleted when they are revoked or expire. Known-device retention is in FR-026, audit retention in FR-014.

**Transport and abuse protection**

- **FR-039**: All traffic between client and API MUST use HTTPS with TLS 1.2 or newer (PRD NFR-13). The renewal cookie (FR-008a) MUST carry the `Secure` flag so the browser never sends it over plain HTTP.
- **FR-040**: Every endpoint of this feature MUST be covered by the API's existing application-wide request limit: per IP address for requests without a signed-in user (login, registration, renewal, password reset, verification, provider sign-in) and per user for signed-in requests. Requests over the limit get a "too many requests" response (HTTP 429) with a retry time, which is distinct from the account lockout in FR-012. This feature adds no endpoint-specific limits; the per-account lockout (FR-012) and the per-address e-mail limit (FR-037) cover the auth-specific cases.

### Key Entities

- **User account**: A person registered in Liftingo. Holds the e-mail (unique, case-insensitive), verification status, registration date, optional password, failed-attempt counter, and lockout end time.
- **Consent record**: One consent decision of a user: type (terms-and-age, health data, marketing), value, timestamp, document version, and whether it has been superseded (set when an unverified account is taken over under FR-022 or by a password reset under FR-035).
- **External login**: The link between a user account and an identity at an external provider: provider name and the provider's user identifier.
- **Session / renewal credential**: One signed-in session on one device: owning user, device description, issue time, last renewal time, inactivity expiry, absolute expiry, revoked flag, and the chain it belongs to (for reuse detection).
- **Known device**: A device or browser on which the user has signed in successfully before, with the time of its last sign-in, used to decide whether to send a new-device e-mail. Forgotten after 12 months without a sign-in.
- **Security audit record**: One login attempt or security event: user (if known), event type (login attempt, logout, sign out of all devices, password reset, provider linked, lockout, renewal reuse), outcome, timestamp, and, when the user is known, IP address and device type.
- **E-mail token**: A single-use, time-limited proof sent by e-mail: owning user, purpose (e-mail verification, password reset or provider link confirmation), expiry, used/superseded flag. A link-confirmation token also holds the provider name and the provider's user identifier to link.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user can complete e-mail registration, consents included, in under 1 minute.
- **SC-002**: A signed-in user stays signed in across app restarts without re-entering credentials as long as they use the app at least once every 30 days, up to 90 days after the sign-in that started the session.
- **SC-003**: After logout, 100% of attempts to use or renew that session are rejected.
- **SC-004**: The sixth login attempt after 5 consecutive failures is rejected 100% of the time until 15 minutes have passed.
- **SC-005**: 100% of login attempts and listed security events (FR-014) produce an audit record, and no application log entry contains an e-mail address or password (checked by automated tests).
- **SC-006**: Verification/welcome, welcome-only, password reset and new-device e-mails show up in the local development output within 5 seconds of the action that triggers them.
- **SC-007**: No account is ever created without both required consents recorded with timestamp and document version (checked by automated tests for e-mail and every supported external provider).
- **SC-008**: An attacker cannot tell from the login or password-reset response whether an e-mail address is registered.
- **SC-009**: A user who has forgotten their password can regain access in under 3 minutes, and after the reset 100% of their previous sessions are rejected.
- **SC-010**: No credential that can renew a session is readable by page scripts or stored in persistent browser storage (verified by inspecting the client after login).
- **SC-011**: Under normal load, login and registration complete in ≤ 1 s and session renewal in ≤ 300 ms (p95, measured at the API, matching how PRD NFR-01 states its targets).

This feature delivers the backend only. Criteria that depend on client behaviour are verified by the later frontend feature: SC-001 entirely, the "under 3 minutes" part of SC-009, and the browser inspection in SC-010. Here, the backend's part of each is covered by automated tests (consents and sign-in on registration, session revocation after reset, the renewal cookie's attributes).

## Assumptions

- "JWT access and refresh tokens" from the request map to the short-lived access credential and the rotating renewal credential in FR-008 – FR-011. On app start the client gets a fresh access credential by calling the renewal endpoint with the cookie.
- The provider set (Google, Facebook) matches the PRD (change log #14) and the constitution. Twitter (X) is deferred to a later feature (clarification 2026-10-14). Apple sign-in is not part of this feature.
- FR-023 is a fallback for a provider that returns no e-mail, for example a Facebook account registered with a phone number. Google always returns one.
- E-mail addresses supplied by Google and Facebook are trusted as verified by those providers (confirmed in review on 2026-10-12: both return verified addresses only). Linking by matching e-mail therefore relies on the providers' own checks. This risk is accepted to keep provider sign-in friction-free.
- Withdrawing the health-data consent leads to account deletion (PRD NFR-22). Withdrawal and deletion are out of scope here.
- Re-authentication for account deletion (PRD US-18) will use the same providers. That flow is out of scope here.
- Account deletion, the 7-day deletion of unverified accounts, and the retention purge jobs are out of scope. This feature only stores the data those jobs need.
- The restrictions for unverified users (no social features, no export, AI limited to 1 per 24 h) are enforced by those features. This feature only reports the verification status.
- The onboarding questionnaire and body-data step that follow registration are a separate feature.
- Production e-mail delivery (Azure Communication Services) is out of scope; a later feature will plug it into the sending abstraction from FR-027.
- E-mail content is in Polish to match the Polish-only UI (constitution V). Exact wording can be refined later.
- A "device" is identified by a persistent identifier stored by the client app together with the device type. Clearing browser data makes the device count as new. The identifier is supplied by the client, so it can be faked. This risk is accepted: faking it needs the user's password and the identifier from the victim's browser, and its only effect is that no new-device e-mail is sent.
- External provider credentials for local development are supplied by the developer through local configuration. Providers that are not configured are hidden or turned off instead of failing.
- The client loads each provider's own sign-in script (Google Identity Services, the Facebook JavaScript SDK). Until the frontend feature exists, a development-only page served by the API does this so provider sign-in can be tried by hand.
- The current Terms and Privacy Policy versions come from configuration. A re-acceptance flow for policy updates is out of scope (NFR-21).
