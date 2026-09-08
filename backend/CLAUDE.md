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
