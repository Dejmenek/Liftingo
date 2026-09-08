# Vertical slice + Clean Architecture for the backend

## Context and problem statement

No backend code exists yet. This decision is being made before the first line of production code is written. The team is three people, and that was known from the start, not something that changed partway through.

The initial plan was pure vertical slice architecture (VSA): one project, one file per endpoint grouped by product module (Account, Plans, WorkoutLog, ...), with the handler calling EF Core's `DbContext` directly and no controller, service, or repository layer in between. That plan didn't yet account for what the product needs: rule-based progression, plan adaptation, and AI-assisted plan generation (see `docs/Liftingo_PRD.md`) are real business logic, not CRUD, and pure VSA has no dedicated place for that logic to live. It would end up scattered across whichever handler files happen to need it, with nothing stopping a handler from also reaching directly into EF Core, the AI client SDK, or any other concrete infrastructure dependency.

We need an architecture that keeps what made pure VSA attractive (a feature's code lives in one place, fast to navigate, easy to write), gives the business logic a real home, and gives a three-person team a boundary the compiler enforces instead of one everyone has to remember and catch in review.

## Decision drivers

- **Growing domain complexity**: progression rules, plan adaptation, and AI plan generation are real business logic. They need a dedicated Domain layer from the start instead of being scattered across handler files or re-derived per slice.
- **Enforced layer boundaries for a three-person team**: with three people touching the backend from day one, the boundary between endpoint, business logic, and infrastructure needs to be a compiler-checked project reference, not a convention that only holds if everyone remembers it and code review catches every slip.
- **Long-term maintainability as feature count grows**: more modules are planned (Gamification, Social, Statistics, CardioMobility, ...). Without a clear layer split, related code and inconsistent infrastructure access are harder to keep straight as that list grows.
- **Isolating third-party/infra churn**: the AI provider, email provider, storage, and ORM are all concrete SDKs. The business logic should depend on interfaces it owns, not on those SDKs directly, so a provider swap doesn't ripple into every slice.

## Considered options

- Layered (N-tier) architecture
- Vertical slice architecture only
- Vertical slice architecture + Clean Architecture

## Decision outcome

Chosen option: **vertical slice architecture + Clean Architecture**, split into four projects (`Liftingo.Domain`, `Liftingo.Application`, `Liftingo.Infrastructure`, `Liftingo.Api`) with dependencies pointing inward. Slices stay the unit of organization inside `Liftingo.Application` (one file per endpoint, grouped by module); Clean Architecture's dependency rule is what's added, not a replacement for VSA. See `backend/CLAUDE.md` for the full project layout and conventions.

This keeps a feature's request/response DTOs, validation, and handler in one file (VSA's main benefit), while giving the team a compiler-enforced boundary: `Domain` has no outward dependencies, `Application` defines the interfaces it needs (`IApplicationDbContext`, `IAiClient`, `IEmailSender`, etc.) without referencing `Infrastructure`, and `Infrastructure` implements those interfaces. A repository-per-aggregate is deliberately not part of this: handlers still query `IApplicationDbContext`'s `DbSet<T>` properties directly with LINQ, so the change adds a dependency-inversion seam, not a repository abstraction.

### Consequences

Good:

- Business rules for progression, plan adaptation, and AI plan generation have an explicit home (`Domain`) instead of being duplicated or reinvented per slice.
- The inward dependency rule is enforced by project references, so the boundary holds regardless of who's writing a given slice.
- Swapping the AI provider, email provider, storage, or ORM only touches `Infrastructure` and its DI registration, not the slices that use them.
- Slice handlers are also easier to unit test in isolation, since they depend on `IApplicationDbContext` rather than EF Core's concrete `DbContext`.

Bad:

- Four projects instead of one adds navigation overhead and setup cost before any feature work starts.
- An extra indirection layer (interface before implementation) exists for things that would otherwise be a direct call, more ceremony than pure VSA for simple CRUD slices.
- Onboarding needs to cover the dependency rule between projects, not just the slice-per-endpoint convention.

## Pros and cons of the options

### Layered (N-tier) architecture

Organizes code by technical layer (Controllers, Services, Repositories, DTOs) instead of by feature.

- Good, because it's a familiar, widely documented pattern for .NET teams.
- Good, because separation of concerns by layer is explicit and well understood.
- Bad, because a single feature change typically touches every layer, spreading one unit of work across many files and folders.
- Bad, because it tends toward generic, one-repository/service-per-entity abstractions that don't naturally encode business rules like progression or plan adaptation.
- Bad, because as module count grows, related code for one feature is scattered across layers instead of grouped together, hurting discoverability.

### Vertical slice architecture only

Each endpoint is a self-contained file grouped by module, calling EF Core directly.

- Good, because a feature's whole implementation is in one file: fast to write, fast to find.
- Good, because there's no layering ceremony for simple CRUD slices.
- Bad, because there's no enforced boundary between the endpoint, business rules, and infrastructure: a handler can reach directly into EF Core, the AI client, or any other concrete SDK.
- Bad, because business logic has no dedicated home, risking duplication or inconsistency across slices as rules like progression and plan adaptation grow more complex.
- Bad, because a provider or ORM change ripples into every slice that references it directly, since nothing sits between slices and concrete infrastructure.
- Bad, because with three people writing slices from the start, the missing boundary depends on convention and code review catching violations, rather than the compiler.

### Vertical slice architecture + Clean Architecture (chosen)

- Good, because it keeps VSA's one-file-per-endpoint organization and discoverability.
- Good, because `Domain` gives progression, plan adaptation, and AI plan generation logic an explicit, reusable home.
- Good, because the dependency rule is enforced by project references, holding up regardless of who's writing a given slice.
- Good, because `Application`-owned interfaces isolate the team from third-party/infrastructure churn.
- Bad, because it introduces more projects and indirection than pure VSA, with a real upfront cost before any feature work starts.
