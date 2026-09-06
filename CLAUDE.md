# Liftingo

## Overview

Liftingo is a workout tracking app with AI assistance for beginners and returning lifters. It combines an offline-capable workout log, an AI plan generator, rule-based progression advice, plan adaptation, and an opt-in social layer. It's a **monolith**: one ASP.NET Core Web API backend and one Angular frontend. See `docs/Liftingo_PRD.md` for full product requirements.

## Project structure

Backend and frontend are separate top-level folders:

```
Liftingo/
├── backend/
│   ├── CLAUDE.md
│   ├── Liftingo.sln
│   ├── src/
│   │   └── Liftingo.Api/
│   │       ├── Features/
│   │       │   ├── Account/
│   │       │   │   ├── Register.cs
│   │       │   │   ├── Login.cs
│   │       │   │   └── ...
│   │       │   ├── Plans/
│   │       │   ├── WorkoutLog/
│   │       │   ├── ExerciseLibrary/
│   │       │   ├── Statistics/
│   │       │   ├── CardioMobility/
│   │       │   ├── Gamification/
│   │       │   ├── Social/
│   │       │   └── Privacy/
│   │       ├── Common/
│   │       │   ├── Result/            # Result<T> pattern
│   │       │   ├── Routes/            # RouteConsts
│   │       │   └── ...
│   │       ├── Infrastructure/
│   │       │   ├── Persistence/       # DbContext, EF configurations, migrations
│   │       │   ├── Identity/
│   │       │   ├── Jobs/              # Hangfire job definitions
│   │       │   ├── Ai/                # Microsoft Foundry client
│   │       │   ├── Email/             # Azure Communication Services
│   │       │   └── Storage/           # Azure Blob Storage, QuestPDF
│   │       └── Program.cs
│   └── tests/
│       ├── Liftingo.Api.UnitTests/
│       └── Liftingo.Api.IntegrationTests/
├── frontend/
│   ├── CLAUDE.md
│   └── (Angular workspace)
└── docs/
    └── Liftingo_PRD.md
```

## Common commands

Backend: see `backend/CLAUDE.md`.
Frontend: see `frontend/CLAUDE.md`.
