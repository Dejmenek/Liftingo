# Liftingo Constitution

## Core Principles

### I. Vertical Slice on Clean Architecture
The backend is a monolith of four projects with dependencies pointing inward: `Domain` depends on
nothing, `Application` depends only on `Domain`, and `Infrastructure` and `Api` depend on
`Application` (`Api` also references `Infrastructure` for DI wiring only).

- Every endpoint MUST be one vertical slice in one file under
  `Liftingo.Application/Features/{Module}/`, named after the use case in PascalCase (not the HTTP
  verb) and containing, in order: request DTO, response DTO, validator, handler/route registration.
- `Application` MUST NOT reference `Infrastructure`. It defines the interfaces it needs
  (`IApplicationDbContext`, `IAiClient`, `IEmailSender`, ...); `Infrastructure` implements them.
- `Domain` MUST NOT reference EF Core or any framework type.
- `Api` MUST NOT contain business logic.
- Route strings MUST come from `RouteConsts`, never inline literals.

Rationale: slices keep each feature self-contained and reviewable; inward dependencies keep the
domain testable and the infrastructure replaceable.

### II. Explicit Results, No Hidden Abstractions
Handlers MUST return `Result<T>` for every expected failure (validation, not found, conflict,
rule violation). Exceptions are reserved for unexpected conditions and MUST NOT carry business
flow.

- Handlers MUST query `IApplicationDbContext` `DbSet<T>` properties directly with LINQ.
- Per-aggregate repositories, AutoMapper (or any mapping library) and MediatR (or any mediator)
  MUST NOT be introduced. Entity-to-DTO mapping is written by hand.
- Request/response DTOs MUST be positional records; DI MUST use primary constructors; file-scoped
  namespaces and nullable reference types MUST be enabled; handlers MUST be `async` and accept a
  `CancellationToken`; FluentValidation validators MUST be colocated with their endpoint.

Rationale: the endpoint handler is the single, obvious entry point for each use case, and failure
modes stay visible in method signatures.

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
- Application and error logs MUST NOT contain personal data (no e-mail, name, body data or set
  values). Retention periods in the PRD retention schedule (NFR-40) MUST be enforced by daily,
  logged jobs.
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
  dependencies (SQL Server); test data uses Bogus. Tests MUST be named
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
  Azure SQL (prod), ASP.NET Core Identity with Google/Apple external providers and JWT auth with
  refresh tokens, Hangfire for background jobs, FluentValidation, Serilog structured logging,
  OpenAPI, Microsoft Foundry (AI), Azure Communication Services Email, Azure Blob Storage with
  QuestPDF (exports).
- **Frontend**: Angular 22 + TypeScript, Angular service worker PWA, Dexie.js (IndexedDB) for
  offline storage, Tailwind, Angular Aria + CDK, NSwag-generated client, Playwright.
- **Hosting and delivery**: Azure App Service (backend), Azure Static Web Apps (frontend), GitHub
  Actions for build, test and deploy, Docker for local dependencies.
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

**Version**: 1.0.1 | **Ratified**: 2026-10-06 | **Last Amended**: 2026-10-07
