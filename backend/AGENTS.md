# Liftingo backend

## Tech stack

- .NET 10, ASP.NET Core Web API with Minimal APIs
- EF Core with SQL Server (local dev) / Azure SQL (prod)
- ASP.NET Core Identity + external providers (Google/Apple) + JWT auth with refresh tokens
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
- Docker for local dev and integration test dependencies (SQL Server, etc.)
- Azure App Service for hosting
- GitHub Actions for CI/CD (build, test, deploy)

## Architecture

Backend combines vertical slice architecture with Clean Architecture layering: four projects, dependencies point inward. `Domain` depends on nothing, `Application` depends only on `Domain`, and `Infrastructure`/`Api` depend on `Application` (`Api` also references `Infrastructure`, to wire up DI at startup).

- **`Liftingo.Domain`**: entities, value objects, domain events, domain exceptions, grouped by module. No EF Core, no framework types.
- **`Liftingo.Application`**: the vertical slices, one per endpoint, grouped by module (Account, Plans, WorkoutLog, ExerciseLibrary, Statistics, CardioMobility, Gamification, Social, Privacy). Also defines the interfaces it needs (`IApplicationDbContext`, `IAiClient`, `IEmailSender`, etc.); never references `Infrastructure`.
- **`Liftingo.Infrastructure`**: implements those interfaces, e.g. `ApplicationDbContext : IApplicationDbContext` (EF Core), plus Identity, Hangfire jobs, the Microsoft Foundry client, email, and blob storage.
- **`Liftingo.Api`**: Minimal API host. `Program.cs` maps endpoints and wires DI, registering `Infrastructure` implementations against the `Application` interfaces. No business logic here.

Still no controller/service layer and no per-aggregate repositories: a slice handler depends on `IApplicationDbContext` and queries its `DbSet<T>` properties directly with LINQ, same as it would a `DbContext`. The interface exists only so `Application` can avoid referencing EF Core's concrete implementation in `Infrastructure`, not to add a repository abstraction.

Every slice returns `Result<T>` for expected failures (validation, not found, conflict, rule violations); exceptions are for unexpected conditions only, not business logic flow.

## Error handling

All errors leave the API as RFC 9457 `ProblemDetails` with a `traceId`. There are two failure mechanisms:

- **`Result<T>`** for expected failures: validation, not found, conflict, rule violations the caller can fix.
- **`DomainException`** for invariants the domain model refuses to break. The message is returned to clients, so write it for users and keep personal data out of it.

Any other exception becomes a generic 500 with no message.

## File naming

- One file per endpoint, named after the endpoint/use case in PascalCase (e.g. `CreateWorkoutSession.cs`, `GenerateAiPlan.cs`), not after the HTTP verb. Lives in `Liftingo.Application/Features/{Module}/`.
- A slice file contains, in order: request DTO, response DTO, validator, and the endpoint handler/route registration.
- Test files mirror the slice under test with a `Tests` suffix (e.g. `CreateWorkoutSessionTests.cs`).
- Route constants live centrally in `RouteConsts` (`Liftingo.Application/Common/Routes/`), not inline as magic strings in each slice.

## Code conventions

- File-scoped namespaces everywhere.
- Positional records for all request/response DTOs.
- Primary constructors for dependency injection.
- Nullable reference types enabled.
- Endpoint handlers are `async` and accept a `CancellationToken`.
- FluentValidation validators are colocated in the same file as the endpoint they validate.
- Test naming: `[Method]_[Scenario]_[ExpectedResult]`.
- Log messages must not contain e-mail addresses, names, body data or set values. Log the user id to identify a user.

## Patterns we use

- `Result<T>` as the return type for all slice handlers; no throwing for expected/business failures.
- `IApplicationDbContext` injected into slice handlers, queried directly with LINQ; no per-aggregate repositories.
- Vertical slice architecture inside Clean Architecture layering: one file per endpoint, grouped by product module, with `Domain` → `Application` → `Infrastructure`/`Api` dependencies pointing inward.
- `RouteConsts` for centralized, typed route definitions instead of inline route strings.
- Bogus for realistic test data and seeding.
- Minimal APIs for all HTTP endpoints.

## Patterns we don't use

- Per-aggregate repository classes: handlers query `IApplicationDbContext`'s `DbSet<T>` properties directly instead.
- AutoMapper or other mapping libraries: mapping between entities and DTOs is written by hand.
- MediatR or other mediators: the endpoint handler is the entry point and does the work directly.
- Exceptions for business logic flow: expected failures (validation, not found, conflicts, rule violations) use `Result<T>` instead.

## Testing

- **`Liftingo.Api.UnitTests`**: logic that needs no database: validators, domain entities and value objects, the rules engine and methodology validator, `Result<T>`, `UserContext`. The folders mirror the source (`Domain/{Module}/`, `Features/{Module}/`, `Common/`). Use NSubstitute for dependencies and Bogus for data.
- **`Liftingo.Api.IntegrationTests`**: endpoints and handlers against a real SQL Server container. Handlers query `IApplicationDbContext` directly, and mocking `DbSet<T>` is unreliable, so these tests don't use an in-memory provider or mocks.
- **`Liftingo.ArchitectureTests`**: layer and dependency rules.

Integration test infrastructure lives in `tests/Liftingo.Api.IntegrationTests/Infrastructure/`:

- `SqlServerFixture`: starts one `MsSqlContainer` per test run, applies the EF Core migrations once and creates the `ApiFactory`. It also resets the data with Respawn and keeps the migrated schema.
- `ApiFactory`: `WebApplicationFactory<Program>` that runs in the `Testing` environment and replaces `ConnectionStrings:Default` with the container's connection string.
- `IntegrationTestCollectionDefinition` and `IntegrationTestBase`: derive test classes from `IntegrationTestBase`. They join the shared collection, so there is only one container, and the database is reset before every test.
- `Fakers/`: Bogus fakers for test data.

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
docker compose up -d
```
