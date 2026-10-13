# Liftingo

## Overview

Liftingo is a workout tracking app with AI assistance for beginners and returning lifters. It combines an offline-capable workout log, an AI plan generator, rule-based progression advice, plan adaptation, and an opt-in social layer. It's a **monolith**: one ASP.NET Core Web API backend and one Angular frontend. See `docs/Liftingo_PRD.md` for full product requirements.

## Project structure

Backend and frontend are separate top-level folders:

```
Liftingo/
├── AGENTS.md                # project context for AI coding assistants (source of truth)
├── CLAUDE.md                # imports AGENTS.md
├── backend/
│   ├── AGENTS.md
│   ├── CLAUDE.md
│   ├── Liftingo.sln
│   ├── src/
│   │   ├── Liftingo.Domain/
│   │   │   ├── Account/            # entities, value objects, domain events, grouped by module
│   │   │   ├── Plans/
│   │   │   ├── WorkoutLog/
│   │   │   ├── ExerciseLibrary/
│   │   │   ├── Statistics/
│   │   │   ├── CardioMobility/
│   │   │   ├── Gamification/
│   │   │   ├── Social/
│   │   │   ├── Privacy/
│   │   │   └── Common/             # base entity, domain exceptions, shared value objects
│   │   ├── Liftingo.Application/
│   │   │   ├── Features/
│   │   │   │   ├── Account/
│   │   │   │   │   ├── Login/
│   │   │   │   │   │   ├── LoginCommand.cs
│   │   │   │   │   │   ├── LoginCommandHandler.cs
│   │   │   │   │   │   ├── LoginCommandValidator.cs
│   │   │   │   │   │   └── LoginResponse.cs
│   │   │   │   │   ├── GetCurrentAccount/
│   │   │   │   │   │   ├── GetCurrentAccountQuery.cs
│   │   │   │   │   │   ├── GetCurrentAccountQueryHandler.cs
│   │   │   │   │   │   └── GetCurrentAccountResponse.cs
│   │   │   │   │   ├── Shared/        # code used by two or more Account slices
│   │   │   │   │   └── ...
│   │   │   │   ├── Plans/
│   │   │   │   ├── WorkoutLog/
│   │   │   │   ├── ExerciseLibrary/
│   │   │   │   ├── Statistics/
│   │   │   │   ├── CardioMobility/
│   │   │   │   ├── Gamification/
│   │   │   │   ├── Social/
│   │   │   │   └── Privacy/
│   │   │   └── Common/
│   │   │       ├── Result/            # Result<T> pattern
│   │   │       ├── Persistence/       # IApplicationDbContext
│   │   │       └── ...                # other ports (IAiClient, IEmailSender, ICurrentUser, ...)
│   │   ├── Liftingo.Infrastructure/
│   │   │   ├── Persistence/        # ApplicationDbContext (implements IApplicationDbContext), EF configurations, migrations
│   │   │   ├── Identity/
│   │   │   ├── Jobs/               # Hangfire job definitions, thin wrappers over Application handlers
│   │   │   ├── Ai/                 # Microsoft Foundry client
│   │   │   ├── Email/              # Azure Communication Services, e-mail templates
│   │   │   └── Storage/            # Azure Blob Storage, QuestPDF
│   │   └── Liftingo.Api/
│   │       ├── Program.cs          # composition root, DI wiring, middleware
│   │       └── Endpoints/
│   │           ├── RouteConsts.cs
│   │           ├── Account/
│   │           │   ├── LoginEndpoint.cs
│   │           │   ├── GetCurrentAccountEndpoint.cs
│   │           │   └── ...         # {UseCase}Request.cs only when the HTTP shape differs
│   │           └── ...
│   └── tests/
│       ├── Liftingo.Api.UnitTests/
│       └── Liftingo.Api.IntegrationTests/
├── frontend/
│   ├── AGENTS.md
│   ├── CLAUDE.md
│   └── (Angular workspace)
└── docs/
    └── Liftingo_PRD.md
```

Backend follows vertical slice architecture inside a Clean Architecture project split, with dependencies pointing inward: `Domain` holds business rules, `Application` holds use cases (CQRS without a mediator) and ports, `Infrastructure` implements the ports, and `Api` maps HTTP to use cases. See `backend/AGENTS.md` for details.

## Common commands

Backend: see `backend/AGENTS.md`.
Frontend: see `frontend/AGENTS.md`.
