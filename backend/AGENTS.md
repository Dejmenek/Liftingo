# Liftingo backend

## Tech stack

- .NET 10, ASP.NET Core Web API with Minimal APIs
- EF Core with SQL Server (local dev) / Azure SQL (prod)
- ASP.NET Core Identity + Google/Facebook sign-in (client-supplied provider token, verified by the API) + JWT auth with refresh tokens
- Redis (StackExchange.Redis) for user sessions and refresh tokens; Azure Cache for Redis in prod
- Azure Monitor Log Analytics for the security audit log (own custom table, separate from application logs)
- Hangfire for scheduled/background jobs
- FluentValidation for request validation
- Serilog for structured logging
- OpenAPI for API documentation
- Microsoft Foundry for AI access (plan generation)
- Azure Communication Services Email for transactional email
- Azure Blob Storage + QuestPDF for generated PDF exports
- xUnit + NSubstitute for unit tests
- TestContainers for integration tests
- Bogus for test data seeding
- Docker for local dev and integration test dependencies (SQL Server, Redis)
- Azure App Service for hosting
- GitHub Actions for CI/CD (build, test, deploy)

## Architecture

Backend combines vertical slice architecture with Clean Architecture layering: four projects, dependencies point inward. `Domain` depends on nothing, `Application` depends only on `Domain`, and `Infrastructure`/`Api` depend on `Application`. `Api` also references `Infrastructure`, but only to wire up DI at startup. Inside each project, code is grouped by product module (Account, Plans, WorkoutLog, ExerciseLibrary, Statistics, CardioMobility, Gamification, Social, Privacy).

### `Liftingo.Domain`: business rules

- Entities, aggregates, value objects, domain events, domain services and `DomainException`.
- Pure logic with no I/O lives here too, e.g. the progression rules engine and the methodology validator.
- References nothing beyond the .NET base class library: no EF Core, no ASP.NET Core, no NuGet packages.

### `Liftingo.Application`: use cases

- One slice per use case, in its own folder `Features/{Module}/{UseCase}/`: a command or query, its handler, its validator and its response DTO, one type per file.
- Orchestrates domain objects. Business rules stay in `Domain`; the handler calls them.
- Checks business authorization: does the user own this plan, is this profile visible, is the AI quota used up.
- Defines the ports it needs: `IApplicationDbContext`, `IAiClient`, `IEmailSender`, `ICurrentUser`, `ISecurityAuditLog`, `ISessionStore`, and so on.
- Allowed references: `Liftingo.Domain`, `Microsoft.EntityFrameworkCore` (core package only), `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `FluentValidation`. Anything else needs a constitution amendment.
- Not allowed: the ASP.NET Core shared framework (`Microsoft.AspNetCore.*`), EF Core provider packages, Identity, StackExchange.Redis, Hangfire, Serilog, Azure SDKs, `Infrastructure`, `Api`.
- No HTTP concerns: no `HttpContext`, `IResult`, status codes, cookies, redirects or route mapping. No raw SQL, no EF Core model configuration, no caching code (put caches behind a port).

### `Liftingo.Infrastructure`: implementations of the ports

- `ApplicationDbContext : IApplicationDbContext`, EF Core entity configurations and migrations.
- ASP.NET Core Identity and the external provider token verifiers (Google, Facebook).
- Redis session store, caches and throttles.
- External clients: Microsoft Foundry (AI), Azure Communication Services (e-mail), Azure Blob Storage, QuestPDF.
- E-mail templates.
- Security audit sink (Log Analytics) and the per-user key store used for crypto-shredding.
- Hangfire job definitions. A job is a thin wrapper that calls an `Application` handler; the work itself stays in `Application`.
- Options classes for the above and their binding.
- No business rules.

### `Liftingo.Api`: HTTP mapping and composition root

- One endpoint per use case in `Endpoints/{Module}/{UseCase}Endpoint.cs`. An endpoint only binds the request, calls the handler and maps the `Result` to an HTTP response.
- `RouteConsts`, route groups and the `/api/v1` prefix.
- Mapping `Result` error types to status codes and `ProblemDetails`, plus the global exception handler.
- Authentication schemes and authorization policies ("is the caller signed in", "does the token carry role X"). Ownership and visibility checks belong in `Application`.
- Cookies (refresh token) and the `HttpContext`-based `ICurrentUser` and client info.
- OpenAPI metadata, CORS, HTTP rate limiting.
- `Program.cs`: DI wiring, registering `Infrastructure` implementations against `Application` ports.
- No business logic, no database access.

These rules are enforced by `Liftingo.ArchitectureTests`.

## Use cases: CQRS without a mediator

A **command** changes state. A **query** only reads and never writes. Each has a handler class that is registered in DI and injected straight into its endpoint (or Hangfire job). There is no mediator, dispatcher or pipeline in between.

The interfaces in `Application/Common/Messaging/` fix the shapes:

| Type | Implements | `HandleAsync` returns |
|---|---|---|
| Command with no data back | `ICommand` | |
| Command with a response | `ICommand<TResponse>` | |
| Query | `IQuery<TResponse>` | |
| Handler of `ICommand` | `ICommandHandler<TCommand>` | `Task<Result>` |
| Handler of `ICommand<TResponse>` | `ICommandHandler<TCommand, TResponse>` | `Task<Result<TResponse>>` |
| Handler of `IQuery<TResponse>` | `IQueryHandler<TQuery, TResponse>` | `Task<Result<TResponse>>` |

`AddApplication` registers each handler against its handler interface, and the endpoint injects that interface (`ICommandHandler<RenamePlanCommand, RenamePlanResponse>`). Nothing else is registered against them, so there is no dispatcher and no place for decorators.

The handler is where the whole use case can be read from top to bottom. It calls these explicitly, in the order the use case needs them:

1. its validator,
2. business authorization checks,
3. domain objects (loading aggregates through `IApplicationDbContext` and changing them through domain methods),
4. the transaction: `SaveChangesAsync`, or an explicit transaction from the context when one use case spans several saves,
5. log entries and audit events.

None of this runs in endpoint filters, decorators or pipeline behaviors.

Queries use `AsNoTracking` and project straight to the response DTO with `Select`. Commands load what they change, call domain methods and save.

### Slice layout

Each use case gets a folder. Every type in it is a separate file named after the type:

```
Liftingo.Application/Features/Plans/
├── RenamePlan/
│   ├── RenamePlanCommand.cs
│   ├── RenamePlanCommandHandler.cs
│   ├── RenamePlanCommandValidator.cs
│   └── RenamePlanResponse.cs
├── GetPlan/
│   ├── GetPlanQuery.cs
│   ├── GetPlanQueryHandler.cs
│   └── GetPlanResponse.cs              # no validator: nothing to validate beyond the route id
└── Shared/                             # code used by two or more Plans slices

Liftingo.Api/Endpoints/Plans/
├── RenamePlanEndpoint.cs
├── RenamePlanRequest.cs                # only because the HTTP shape differs from the command
└── GetPlanEndpoint.cs
```

- The `Command`/`Query` suffix shows at a glance which handlers may write. Use cases that look like reads but write something (sessions, tokens, audit records) are commands, e.g. `Login`, `RefreshSession`, `VerifyEmail`.
- Validators are named after what they validate: `{UseCase}CommandValidator`, or `{UseCase}QueryValidator` when a query takes input worth checking (paging limits, filters).
- There is no request type in `Application`. The command or query is the input. `Api` binds it directly, or declares its own `{UseCase}Request` when the HTTP shape differs (e.g. a route value plus a body).
- Never give a type the same name as its folder (no class `RenamePlan` in `RenamePlan/`). C# resolves the name to the namespace, and you get confusing errors.

Sharing between slices:

- Code used by two or more slices of the same module goes in `Features/{Module}/Shared/`. Code used across modules goes in `Application/Common/`. Business rules go in `Domain` no matter how many slices use them.
- Response DTOs are never shared, even when two look identical. Each one is that endpoint's contract and changes on its own schedule.
- A handler never calls another handler. If two use cases need the same steps, move those steps to a class in `Shared/` (e.g. `SessionIssuer`) and call it from both.

### Example

```csharp
// Liftingo.Application/Features/Plans/RenamePlan/RenamePlanCommand.cs
public sealed record RenamePlanCommand(Guid PlanId, string Name) : ICommand<RenamePlanResponse>;

// Liftingo.Application/Features/Plans/RenamePlan/RenamePlanResponse.cs
public sealed record RenamePlanResponse(Guid PlanId, string Name);

// Liftingo.Application/Features/Plans/RenamePlan/RenamePlanCommandHandler.cs
public sealed class RenamePlanCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    RenamePlanCommandValidator validator,
    ILogger<RenamePlanCommandHandler> logger) : ICommandHandler<RenamePlanCommand, RenamePlanResponse>
{
    public async Task<Result<RenamePlanResponse>> HandleAsync(RenamePlanCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<RenamePlanResponse>(validation.ToValidationError());

        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Id == command.PlanId, ct);
        if (plan is null || plan.OwnerId != currentUser.Id)
            return Result.Failure<RenamePlanResponse>(Error.NotFound("Plans.NotFound", "Plan not found."));

        plan.Rename(command.Name);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} renamed plan {PlanId}", currentUser.Id, plan.Id);
        return new RenamePlanResponse(plan.Id, plan.Name);
    }
}
```

```csharp
// Liftingo.Api/Endpoints/Plans/RenamePlanRequest.cs
public sealed record RenamePlanRequest(string Name);

// Liftingo.Api/Endpoints/Plans/RenamePlanEndpoint.cs
app.MapPut(RouteConsts.Plans.Rename, async (
        Guid planId,
        RenamePlanRequest body,
        ICommandHandler<RenamePlanCommand, RenamePlanResponse> handler,
        CancellationToken ct) =>
    {
        var result = await handler.HandleAsync(new RenamePlanCommand(planId, body.Name), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblem();
    })
    .RequireAuthorization();
```

### Data access

Handlers depend on `IApplicationDbContext` and query its `DbSet<T>` properties directly with LINQ. There are no per-aggregate repositories. That makes the EF Core core package (`DbSet<T>`, async LINQ operators, `Include`, `AsNoTracking`, transactions from the context) the one framework dependency allowed in `Application`. It's a deliberate trade-off: the SQL Server provider, entity configuration and migrations stay in `Infrastructure`, and we don't need a repository layer.

## Error handling

All errors leave the API as RFC 9457 `ProblemDetails` with a `traceId`. There are two failure mechanisms:

- **`Result<T>`** for expected failures: validation, not found, conflict, rule violations the caller can fix. Error types are HTTP-agnostic; `Api` maps each one to a status code.
- **`DomainException`** for invariants the domain model refuses to break. The message is returned to clients, so write it for users and keep personal data out of it.

Any other exception becomes a generic 500 with no message.

## File naming

- Use cases are named in PascalCase after what they do (e.g. `CreateWorkoutSession`, `GenerateAiPlan`), not after the HTTP verb.
- One type per file, file named after the type.
- `Liftingo.Application/Features/{Module}/{UseCase}/` holds `{UseCase}Command.cs` or `{UseCase}Query.cs`, `{UseCase}CommandHandler.cs` or `{UseCase}QueryHandler.cs`, `{UseCase}CommandValidator.cs` or `{UseCase}QueryValidator.cs` (when needed) and `{UseCase}Response.cs`. See [Slice layout](#slice-layout).
- Code shared by several slices in a module goes in `Features/{Module}/Shared/`.
- `Liftingo.Api/Endpoints/{Module}/` holds `{UseCase}Endpoint.cs` and, only when the HTTP shape differs from the command or query, `{UseCase}Request.cs`.
- Test files mirror the folder and class under test with a `Tests` suffix (e.g. `Features/Plans/RenamePlan/RenamePlanCommandHandlerTests.cs`, `RenamePlanCommandValidatorTests.cs`).
- Route constants live centrally in `RouteConsts` (`Liftingo.Api/Endpoints/`), not inline as magic strings. They hold the route template without the version prefix (`/auth/me`, not `/api/v1/auth/me`); `Api` adds the prefix once.

## Code conventions

- File-scoped namespaces everywhere.
- Positional records for commands, queries, response DTOs and HTTP request records.
- Primary constructors for dependency injection.
- Nullable reference types enabled.
- Handlers and endpoints are `async` and accept a `CancellationToken`.
- Validators are injected into the handler and called explicitly at the start of `HandleAsync`.
- Comments explain why the code is the way it is. Don't write a comment that repeats the code or the member's name (`/// <param name="Password">The password.</param>`), that describes how the code used to work, or that cites a requirement id (`FR-021`, `R17`, `SC-008`). Ids belong in the spec. Write the reason itself in the comment.
- Analyzer rules: when a rule doesn't fit how this codebase is built, turn it off for the whole backend in `.editorconfig`, with a comment that names the rule and says why. Don't suppress the same rule in place after place. Keep `[SuppressMessage]` (with a `Justification`) or `#pragma warning disable` for a one-off exception to a rule that is worth keeping, e.g. the catch-all in `SecurityAudit.WriteAsync`. A path-scoped section is for a rule that still catches real mistakes elsewhere, e.g. CA1707 only in test files.
- Test naming: `[Method]_[Scenario]_[ExpectedResult]`.
- Log messages must not contain e-mail addresses, names, body data or set values. Log the user id to identify a user.

## Patterns we use

- Clean Architecture layering with vertical slices: `Domain` ← `Application` ← `Infrastructure`/`Api`, grouped by product module.
- CQRS without a mediator: commands, queries and handlers implement the `Common/Messaging` interfaces, and endpoints and jobs inject the handler interface directly.
- Ports and adapters: `Application` defines the interfaces, `Infrastructure` implements them.
- `Result<T>` as the return type for all handlers; no throwing for expected/business failures.
- `IApplicationDbContext` queried directly with LINQ.
- Explicit validator, transaction, logging and audit calls inside the handler.
- `RouteConsts` for centralized route definitions instead of inline route strings.
- Minimal APIs for all HTTP endpoints.
- Bogus for realistic test data and seeding.

## Patterns we don't use

- Per-aggregate repository classes: handlers query `IApplicationDbContext`'s `DbSet<T>` properties directly instead.
- AutoMapper or other mapping libraries: mapping between entities and DTOs is written by hand.
- MediatR or other mediators, dispatchers or pipeline behaviors: the endpoint injects the handler and calls it.
- Implicit cross-cutting behavior: validation, transactions, logging and audit are not done in endpoint filters or decorators.
- Exceptions for business logic flow: expected failures (validation, not found, conflicts, rule violations) use `Result<T>` instead.
- Business logic in `Api` or `Infrastructure`, or HTTP types in `Application`.

## Testing

- **`Liftingo.Api.UnitTests`**: logic that needs no database: validators, domain entities and value objects, the rules engine and methodology validator, `Result<T>`, `UserContext`. The folders mirror the source (`Domain/{Module}/`, `Features/{Module}/`, `Common/`). Use NSubstitute for dependencies and Bogus for data.
- **`Liftingo.Api.IntegrationTests`**: endpoints and handlers against a real SQL Server container. Handlers query `IApplicationDbContext` directly, and mocking `DbSet<T>` is unreliable, so these tests don't use an in-memory provider or mocks.
- **`Liftingo.ArchitectureTests`**: layer and dependency rules.

Integration test infrastructure lives in `tests/Liftingo.Api.IntegrationTests/Infrastructure/`:

- `SqlServerFixture`: starts one `MsSqlContainer` and one Redis container per test run, applies the EF Core migrations once and creates the `ApiFactory`. It also resets the data with Respawn and keeps the migrated schema.
- `ApiFactory`: `WebApplicationFactory<Program>` that runs in the `Testing` environment, points the app at the SQL Server and Redis containers, and swaps in a `FakeTimeProvider`, `CapturingEmailTransport` and `CapturingAuditSink`.
- `CapturingEmailTransport`: replaces the e-mail transport, so tests see the subject, body and links the real renderer produced.
- `IntegrationTestCollectionDefinition` and `IntegrationTestBase`: derive test classes from `IntegrationTestBase`. They join the shared collection, so there is only one container, and the database and Redis are reset before every test.
- `RedisSessionStoreTests` and `RedisLoginThrottleTests`: run the Lua scripts against the Redis container with their own fake clock.
- `Fakers/`: Bogus fakers for test data.

Unit tests mirror the source folders. For example, the validator of `Features/Account/Register/` is tested in `Features/Account/Register/RegisterCommandValidatorTests.cs`.

`Liftingo.ArchitectureTests` has two kinds of rules. `LayerDependencyTests` and `DependencyGuardTests` check which layer may reference which. `LayerResponsibilityTests` checks what each layer may contain: Application references only its allowed assemblies, has no endpoints, and has only command and query handlers in use-case folders that never call each other. Every `…CommandHandler`, `…QueryHandler`, `…Command` and `…Query` implements the interface its name says. Api uses Infrastructure only in its wiring classes and never touches `IApplicationDbContext`.

Test groups (xUnit collections) never run in parallel (`[assembly: Parallelization(Mode = ParallelMode.None)]`), and tests inside a group run one at a time. Integration tests need Docker running.

## Common commands

```bash
cd backend
dotnet build
dotnet test
dotnet run --project src/Liftingo.Api
dotnet ef migrations add <Name> --project src/Liftingo.Infrastructure --startup-project src/Liftingo.Api
dotnet ef database update --project src/Liftingo.Infrastructure --startup-project src/Liftingo.Api
dotnet test tests/Liftingo.Api.UnitTests
dotnet test tests/Liftingo.Api.IntegrationTests
docker compose up -d   # Redis for dotnet run; the sign-in endpoints answer 503 without it
dotnet user-secrets --project src/Liftingo.Api set "Jwt:SigningKey" "<at least 32 random bytes, base64>"   # once, before the first run
```

`dotnet run` needs Redis (`docker compose up -d`) and the `Jwt:SigningKey` user secret, which is not in `appsettings.json`. To make a key, run `[Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }))` in PowerShell or `openssl rand -base64 32`. The integration tests start their own SQL Server and Redis containers and use a fixed test key, so they only need Docker.
