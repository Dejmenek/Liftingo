# Contributing

## AI-assisted development

### Project context files

`AGENTS.md` (root, `backend/`, `frontend/`) is the source of truth for project context: overview, architecture, conventions, and commands. Each `CLAUDE.md` just imports the sibling `AGENTS.md` via `@AGENTS.md`, so Claude Code reads the same content as any other tool that supports `AGENTS.md`.

The plugins listed below (`context7`, plus `product-management`, `dotnet-test`, `dotnet-aspnetcore`, `dotnet-data`, `humanizer`, `ui-ux-pro-max`) are enabled through `.claude/settings.json` and only work in Claude Code. There's currently no equivalent wiring for other AI coding assistants, so if you're using one of those, expect it to read `AGENTS.md` but not have access to whatever these plugins provide.

### context7 MCP server

context7 is an MCP server that fetches current, version-accurate documentation for libraries, frameworks, SDKs, APIs, and CLI tools directly into Claude's context, instead of relying on training data that may be stale.

We use it because this stack skews recent: Angular 22, .NET 10, EF Core, Hangfire, Signal Forms, Angular Aria and CDK. Training data cutoffs lag behind releases like these, so lookups for API syntax, configuration, version migrations, and setup steps should go through context7 rather than memory, even for well-known libraries.

**Setup.** Enabled for this repo via `.claude/settings.json` (`context7@context7-marketplace`), so it's available automatically when the repo is opened in Claude Code. If the marketplace isn't already installed locally, add it through Claude Code's plugin manager (`/plugin`).

**Usage.** Resolve the library id first, then query the docs with the actual question, not a single keyword:
1. `resolve-library-id` with the library name and the question at hand.
2. `query-docs` with the resolved id and the full question.

Use it for API syntax, configuration, version migration, library-specific debugging, and setup or CLI usage. Skip it for refactoring, writing scripts from scratch, debugging business logic, or code review; none of those need external docs.
