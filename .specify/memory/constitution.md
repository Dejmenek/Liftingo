# Liftingo Constitution

## Core Principles

### I. Clean Architecture Layers, Vertical Slices
The backend is a monolith of four projects with dependencies pointing inward: `Domain` depends on
nothing, `Application` depends only on `Domain`, and `Infrastructure` and `Api` depend on
`Application`. `Api` also references `Infrastructure`, for DI wiring only. Inside each layer, code
is grouped by product module (Account, Plans, WorkoutLog, ...), and each use case is one slice.

- **`Domain`** holds the business rules: entities, aggregates, value objects, domain events,
  domain services and domain exceptions. Pure logic with no I/O (the progression rules engine, the
  methodology validator) MUST live here. `Domain` MUST NOT reference EF Core, ASP.NET Core or any
  other package beyond the .NET base class library.
- **`Application`** holds the use cases. It orchestrates domain objects, checks resource-level
  business authorization (ownership, visibility, per-user quotas) and defines the ports it needs
  (`IApplicationDbContext`, `IAiClient`, `IEmailSender`, `ICurrentUser`, `ISecurityAuditLog`,
  ...). It MUST reference only `Domain` and these packages: `Microsoft.EntityFrameworkCore` (the
  core package, see Principle II), `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` and
  `FluentValidation`. Adding a package to this list requires an amendment.
- `Application` MUST NOT reference `Infrastructure`, `Api`, the ASP.NET Core shared framework,
  EF Core provider packages, Identity, Redis, Hangfire, Serilog or any Azure SDK. It MUST NOT
  contain HTTP concerns (`HttpContext`, `IResult`, status codes, cookies, redirects, route
  mapping), raw SQL, EF Core model configuration or caching code.
- **`Infrastructure`** implements the ports: `ApplicationDbContext`, EF Core configurations and
  migrations, ASP.NET Core Identity and external auth providers, the Redis session store and
  caches, external clients (Microsoft Foundry, e-mail, blob storage, PDF generation), e-mail
  templates, the security audit sink and its key store, and options binding. Hangfire job
  definitions live here and MUST only call `Application` handlers. `Infrastructure` MUST NOT
  contain business rules.
- **`Api`** is the composition root and the HTTP mapping. It owns endpoints (one per use case
  under `Liftingo.Api/Endpoints/{Module}/`), `RouteConsts` and the version prefix, binding
  requests to commands and queries, mapping `Result` errors to status codes and RFC 9457
  `ProblemDetails`, authentication schemes and authorization policies, cookies and redirects, the
  `HttpContext`-based `ICurrentUser`, the global exception handler, OpenAPI metadata, CORS and
  HTTP rate limiting. `Api` MUST NOT contain business logic or query the database.
- Each use case MUST be one slice under `Liftingo.Application/Features/{Module}/{UseCase}/`,
  named after the use case in PascalCase (not the HTTP verb).
- Route strings MUST come from `RouteConsts`, never inline literals.
- The layer and package rules above MUST be enforced by `Liftingo.ArchitectureTests`.

Rationale: slices keep each feature self-contained and reviewable; inward dependencies keep the
domain testable and the infrastructure replaceable; keeping HTTP out of `Application` lets the
same use case run from an endpoint, a background job or a test.

### II. Explicit Use Cases, No Hidden Pipeline
Use cases follow CQRS without a mediator. A **command** changes state; a **query** only reads and
MUST NOT write. Each has a handler class that is registered in DI and injected directly into its
endpoint or job.

- Each slice MUST be a folder `Features/{Module}/{UseCase}/` holding one type per file:
  `{UseCase}Command` or `{UseCase}Query`, `{UseCase}CommandHandler` or `{UseCase}QueryHandler`,
  `{UseCase}CommandValidator` or `{UseCase}QueryValidator` (when the input needs validating) and
  `{UseCase}Response`. HTTP request records MUST NOT live in `Application`; `Api` declares one
  only when the HTTP shape differs from the command or query.
- Code used by two or more slices of a module MUST go in `Features/{Module}/Shared/`; code used
  across modules goes in `Application/Common/`, and business rules go in `Domain`. Response DTOs
  MUST NOT be shared between slices, and a handler MUST NOT call another handler.
- Commands MUST implement `ICommand` or `ICommand<TResponse>` and queries `IQuery<TResponse>`;
  handlers MUST implement `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>` or
  `IQueryHandler<TQuery, TResponse>` from `Application/Common/Messaging`. Endpoints and jobs inject
  the handler interface. Only the handler itself MAY be registered against a handler interface:
  the interfaces fix the handler's shape and MUST NOT become a dispatcher or a place for
  decorators.
- The handler MUST make the use case's flow visible from top to bottom. It MUST explicitly call
  its validator, check business authorization, orchestrate domain objects, own the transaction
  (`SaveChangesAsync`, or an explicit transaction when one use case spans several saves), write
  its log entries and emit its audit events. Endpoint filters, decorators or pipeline behaviors
  MUST NOT do this work implicitly.
- Handlers MUST return `Result<T>` for every expected failure (validation, not found, conflict,
  rule violation). Error types MUST be HTTP-agnostic; `Api` maps them to status codes.
  Exceptions are reserved for unexpected conditions and broken domain invariants and MUST NOT
  carry business flow.
- Handlers MUST query `IApplicationDbContext` `DbSet<T>` properties directly with LINQ. This is
  the one framework exception in `Application`: it may use the EF Core core package for
  `DbSet<T>`, async LINQ operators, `Include`, `AsNoTracking` and transactions from the context.
  Commands load aggregates, change them through domain methods and save; queries use
  `AsNoTracking` and project to DTOs.
- Per-aggregate repositories, AutoMapper (or any mapping library) and MediatR (or any mediator,
  dispatcher or pipeline) MUST NOT be introduced. Entity-to-DTO mapping is written by hand.
- Commands, queries and DTOs MUST be positional records; DI MUST use primary constructors;
  file-scoped namespaces and nullable reference types MUST be enabled; handlers MUST be `async`
  and accept a `CancellationToken`.

Rationale: a reader sees the whole use case, including validation, transaction boundaries and
audit, in one method, with nothing running before or after it unseen. Allowing EF Core's core
package in `Application` is a deliberate trade-off that avoids a repository layer, while the
provider, configuration and migrations stay in `Infrastructure`.

### III. Privacy and Compliance by Design (NON-NEGOTIABLE)
Liftingo processes health-category data (body weight, height) and an opt-in social graph, and
MUST comply with GDPR (RODO) and the EU AI Act from the first line of code that touches personal
data.

- Health-data consent (Art. 9(2)(a)), age ≥ 18 acceptance and Terms/Privacy acceptance MUST be
  explicit and logged with timestamp and document version.
- Sensitive and biometric data MUST be encrypted at rest; all traffic MUST use TLS 1.2+.
- Social features MUST be opt-in and off by default. Data listed as non-shareable in NFR-36
  (e-mail, age, sex, body weight, height, goal, notes, individual sets, personal goals) MUST NEVER
  be exposed to other users; leaderboards expose only aggregated, user-chosen metrics.
- Account deletion MUST follow the 7-day grace period, then irreversible removal from live data,
  with backups purged within 30 days. Personal-data export MUST be a complete, documented JSON
  archive.
- Application and error logs MUST NOT contain personal data (no e-mail, name, body data, set
  values or IP addresses). Retention periods in the PRD retention schedule (NFR-40) MUST be
  enforced by daily, logged jobs or by the storing service's configured retention.
- The security audit log (NFR-17) is a separate stream, not part of application logs, and MUST be
  stored apart from them. Its personal fields (IP address, device type) MUST be encrypted with a
  per-user key before they leave the application; deleting that key (crypto-shredding) is how
  audit data is erased on account deletion. Audit events MUST NOT contain e-mail addresses,
  passwords or tokens.
- Limits and retention windows MUST be configuration values, not code constants.

Rationale: the product's legal viability and the thesis's compliance goals depend on privacy being
built in, not bolted on.

### IV. Explainable AI Boundary
AI output and deterministic logic MUST remain separable and traceable.

- The AI plan generator is the only AI component. Every AI-generated plan MUST be labelled as
  AI-generated wherever displayed and in exports, MUST be explainable (inputs used, plan health,
  generation record), and MUST NOT be applied without explicit user acceptance (the documented 24 h
  default reschedule excepted).
- Every generated plan MUST pass the deterministic methodology validator (six rules) before it is
  shown; plans failing after 3 attempts MUST NOT be shown. Starting loads MUST NOT exceed Appendix
  A1 ratios for the user's experience bracket.
- Progression suggestions MUST come from the versioned, deterministic rules engine (no ML in v1):
  same input yields the same output, and the rule-set version is recorded with every suggestion.
- Model/prompt version, input summary and validator result MUST be logged for every generation.
- AI endpoints MUST enforce the NFR-39 limits (per-user quota, one in-flight generation, global
  daily ceiling, 15 s client timeout).
- Generator inputs MUST NOT include injury or medical-condition data.

Rationale: the thesis must demonstrate which outputs are AI-generated and which are rule-based,
and the AI Act Art. 50 transparency obligations depend on it.

### V. Offline-First, Mobile-First PWA
The product ships as an installable PWA, and logging is the core experience.

- All workout-logging functions (strength, cardio, mobility, rest timer) MUST work fully offline
  using local storage and sync automatically on reconnect.
- Sync conflicts MUST be resolved per session with user choice (keep device / keep cloud / merge
  non-overlapping sets) and MUST NOT lose data.
- Saving a set MUST take ≤ 3 taps and complete in ≤ 2 s (p95); core log functions MUST be usable
  one-handed. UI MUST be mobile-first and responsive.
- Home and gym environments are both first-class: no flow may be gym-only.
- All UI strings MUST be externalised for i18n (Polish only in v1).

Rationale: users train in places with poor signal and little attention to spare; friction in the
log directly drives churn.

### VI. Test-Backed Delivery
Behavior MUST be verified by automated tests proportional to its risk.

- Backend unit tests use xUnit and NSubstitute; integration tests use TestContainers against real
  dependencies (SQL Server, Redis); test data uses Bogus. Tests MUST be named
  `[Method]_[Scenario]_[ExpectedResult]`, and test files mirror the slice under test with a
  `Tests` suffix.
- The rules engine and methodology validator MUST have determinism tests; every P0 flow MUST be
  verified for both a home-only and a gym profile.
- Privacy-critical behavior (visibility controls, opt-out propagation, deletion, anonymisation)
  MUST have integration coverage.
- Frontend end-to-end flows MUST be covered with Playwright.
- A change MUST NOT be merged with failing tests or a failing build.

Rationale: verification criteria (PRD section 8) are the thesis's pass/fail evidence.

### VII. Contract-Driven Frontend
The Angular frontend consumes the backend only through the generated contract.

- Components MUST be standalone. State MUST live in signals: component-local by default,
  promoted to a feature-level state service only when genuinely shared. Validated or multi-step
  forms MUST use Signal Forms.
- API calls MUST use the NSwag-generated client from the backend OpenAPI spec; reads MUST use
  `rxResource()`, never `httpResource()`. Hand-written HTTP services or DTOs that duplicate
  generated ones MUST NOT be created, and the client MUST be regenerated after any backend
  endpoint change.
- Styling MUST be Tailwind only; accessible interaction behavior MUST come from Angular Aria and
  Angular CDK. WCAG 2.1 AA is the accessibility target.
- Interceptors and guards MUST use the functional style; files and classes MUST NOT carry type
  suffixes (current Angular style guide).

Rationale: one source of truth for the API contract prevents drift between backend and frontend.

### VIII. Simplicity and Thesis Scope Discipline
Liftingo is an engineering thesis with a single free product and a fixed priority model.

- Work MUST follow the PRD priorities: P0 is the deliverable, P1 is implemented only if time
  allows in the PRD's listed order, P2 is designed for (interfaces, data model) but MUST NOT be
  built.
- Non-goals (ML progression, monetisation, nutrition/wearables, intermediate/advanced content,
  public social features, users under 18, native apps, coach accounts) MUST NOT be implemented.
- New abstractions, projects, dependencies or layers MUST be justified against a concrete current
  requirement; speculative generality (YAGNI) is rejected.
- Backend and frontend remain one monolith each; splitting into services requires an amendment.

Rationale: the scope is intentionally bounded; complexity beyond it threatens delivery.

## Technology Stack and Constraints

The following stack is binding; deviations require a constitution amendment.

- **Backend**: .NET 10, ASP.NET Core Web API (Minimal APIs), EF Core on SQL Server (local) /
  Azure SQL (prod), ASP.NET Core Identity with Google/Facebook/Twitter (X) external providers and JWT auth with
  refresh tokens, Redis (StackExchange.Redis) as the store for user sessions and refresh tokens,
  Hangfire for background jobs, FluentValidation, Serilog structured logging, Azure Monitor Log
  Analytics for the security audit log (dedicated custom table, 90-day retention, KQL queries by
  user id for export; per-user encryption keys kept in SQL Server), OpenAPI, Microsoft Foundry
  (AI), Azure Communication Services Email, Azure Blob Storage with QuestPDF (exports).
- **Frontend**: Angular 22 + TypeScript, Angular service worker PWA, Dexie.js (IndexedDB) for
  offline storage, Tailwind, Angular Aria + CDK, NSwag-generated client, Playwright.
- **Hosting and delivery**: Azure App Service (backend), Azure Static Web Apps (frontend), Azure
  Cache for Redis, Azure Monitor Log Analytics workspace, GitHub Actions for build, test and
  deploy, Docker for local dependencies (SQL Server, Redis).
- **Security**: passwords ≥ 8 chars with ≥ 1 digit and ≥ 1 special character; login attempt
  lockout (15 min after 5 consecutive failures) and audit logging; no account enumeration on recovery flows.
- **Performance targets**: first load ≤ 3 s on 4G (p75), AI plan generation ≤ 10 s (p95), set
  save ≤ 2 s (p95).
- **Compatibility**: current and two previous major versions of Chrome, Safari, Firefox, Edge.

## Development Workflow and Quality Gates

- Work happens on feature branches linked to an issue and merged via pull request into `main`.
- Specifications, plans and tasks are produced with Spec Kit; each plan MUST include a
  Constitution Check against the principles above before implementation begins.
- A pull request MUST pass CI (build and all tests) before merge. The author MUST check the
  change against the constitution; review by a teammate is optional.
- Backend changes that alter endpoints MUST be followed by NSwag client regeneration in the same
  change set or a linked one.
- EF Core schema changes MUST be delivered as migrations from `Liftingo.Infrastructure`.
- Written project text (issues, PR descriptions, docs) SHOULD be run through the humanizer skill.
- Code comments explain why the code is the way it is. They MUST NOT repeat what the code says,
  describe how the code used to work, or cite requirement ids (`FR-…`, `SC-…`, research `R…`);
  ids belong in the spec, and the comment states the reason itself.
- An analyzer rule that does not fit the codebase's design MUST be turned off in `.editorconfig`
  for the whole backend, with a comment naming the rule and the reason, instead of being
  suppressed at each occurrence. `[SuppressMessage]` (with a justification) or a `#pragma` is
  for a one-off exception to a rule worth keeping; a path-scoped `.editorconfig` section is for a
  rule that still catches real mistakes elsewhere.
- `AGENTS.md` files hold day-to-day commands and conventions; if they conflict with this
  constitution, the constitution prevails and the `AGENTS.md` MUST be corrected.

## Governance

This constitution supersedes other practices and guidance documents for the Liftingo repository.
`docs/Liftingo_PRD.md` is the source of product requirements; `AGENTS.md` files (root, backend,
frontend) are the source of runtime development guidance.

- **Amendments**: proposed by pull request modifying this file, with a Sync Impact Report, a
  rationale, and a migration plan for any affected code, specs or templates. The author MUST
  verify consistency with the PRD and AGENTS files. Review by another team member is encouraged
  but not required.
- **Versioning**: semantic versioning. MAJOR for backward-incompatible principle removals or
  redefinitions; MINOR for new principles/sections or materially expanded guidance; PATCH for
  clarifications and wording fixes.
- **Compliance check**: the author of every pull request and every Spec Kit plan MUST check it
  against these principles; violations MUST be fixed or explicitly justified in the plan's complexity tracking.
  Principle III violations are never waivable.
- **Dependency drift**: when a PRD requirement or tech-stack choice changes, this constitution and
  the relevant `AGENTS.md` MUST be updated in the same change.

**Version**: 2.1.0 | **Ratified**: 2026-10-06 | **Last Amended**: 2026-10-13
