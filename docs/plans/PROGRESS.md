# Implementation Progress

> **Resuming in a new session?**
>
> 1. Read this file, then `docs/plans/00-master-plan.md` (Global Constraints, Execution model, **Shared contracts**).
> 2. Read the spec sections that the current phase needs: `docs/design/2026-11-09-employee-payroll-design.md`.
> 3. Continue from **Next action**. Update this file after every state change.

## Working mode — FAST MODE (D52, deadline 2026-11-10, full scope)

- **No per-phase detailed plans.** Build directly from the spec + master plan Shared contracts.
- **One batch per phase.** Each phase is written in one go: production code **and** its tests (the catalogue in spec §14.2).
- **One verification per phase.** Kamal runs one PowerShell block (build + test) and pastes the output; Claude fixes what it shows.
- **Language:** discuss in **Arabic**; every file is in **English** (D1).
- **Git (D50):** Claude **never** commits or pushes. At the end of each phase, Claude gives Kamal the `git add` / `git commit` commands.
- **Execution split:** the Claude sandbox has no .NET/NuGet/Docker, so Kamal runs `dotnet` / `docker`. Claude verifies Angular in a `/tmp` copy and **never** runs `npm install` inside the repo.
- **Tests:** `dotnet test` uses **Microsoft.Testing.Platform** (set in `global.json`). Test projects reference only `xunit.v3` (+ NSubstitute / TimeProvider.Testing as needed), **not** `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`.
  - Run all tests: `dotnet test --solution Payroll.slnx`
  - Run one project: `dotnet test --project tests/<Project>`

## Environment facts

| Item | Value |
|---|---|
| .NET SDK (Kamal) | 10.0.400 (`global.json`: 10.0.100 + `latestFeature`, test runner = MTP) |
| Node (Kamal) | **v22.22.0: must upgrade to ≥ 22.22.3 or 24 LTS before running the Angular client** |
| Docker (Kamal) | 29.8.0, Compose v5.5.1 |
| Angular CLI / Bootstrap | 22.2.6 / 5.3.8 |
| Pinned NuGet (`Directory.Packages.props`) | EF/ASP.NET 10.0.14, FluentValidation 12.2.0, Mapster 10.0.14, xunit.v3 4.0.3, NSubstitute 6.2.0, TimeProvider.Testing 10.11.0, JsonWebTokens 8.23.0, SwaggerUI 10.3.1, Testcontainers.MsSql 4.16.0 (`Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio` entries are unused) |
| dotnet-ef | 10.0.14 (local tool, `dotnet-tools.json` at the repo root) |

## Status

| Phase (master plan numbering) | Status |
|---|---|
| 1 Setup & skeleton | **in progress** (see below) |
| 2 Employee CRUD backend + tests | todo |
| 3 Payroll backend + tests | todo |
| 4 Angular client + logic tests | todo |
| 5 Admin APIs (types CRUD + policy writes) + tests | todo |
| 6 Health + JSON logging | todo |
| 7 Identity + JWT (backend + client) + tests | todo |
| 8 README + `/ai-usage` | todo |
| 9–10 *(optional)* integration tests, Docker + CI | only if time remains |

### Phase 1 detail (plan: `docs/plans/01-setup-and-skeleton.md`; code for Tasks 4–6 is already in that file)

| Task | Status |
|---|---|
| 1 Toolchain | done |
| 2 Package pins | done (Kamal to commit) |
| 3 Skeleton | done, build green (Kamal to commit) |
| 4 Error handling | tests written in `tests/Payroll.Api.UnitTests` and added to the solution. **The implementation (Task 4 Step 4 in the plan file) is not written yet.** |
| 5 Host / `.env` / compose | todo (code in the plan file, Task 5) |
| 6 Angular scaffold | todo (steps in the plan file, Task 6) |

## Next action

1. **Kamal** makes the pending commits:
   - `docs: add approved design spec, implementation plans and repo settings`
   - `build: pin NuGet package versions centrally`
   - `build: add layered solution skeleton (...)` (this one includes `global.json` with the MTP test runner)
2. **Claude** (fast mode) writes the Phase 1 Task 4 Step 4 implementation **and** all of Task 5 (`DotEnvFile` + tests, `Program.cs`, launchSettings, appsettings, `docker-compose.yml`, `.env.example`), copying the code from `01-setup-and-skeleton.md`.
3. **Kamal** runs `dotnet build Payroll.slnx` and `dotnet test --solution Payroll.slnx` (expect 22 passed), then the Task 5 Step 7 checks.
4. **Claude** does Task 6 (Angular scaffold in `/tmp`, copied without `node_modules`, `ng build` verified), then moves on to Phase 2.

## Log

- **2026-11-09:** spec approved (D1–D51); master plan + Phase 1 plan written; `git init` done; continuity files created.
- **2026-11-10:**
  - P1 Tasks 1–3 done.
  - `dotnet-tools.json` is at the repo root.
  - xUnit v3 4.x requires Microsoft.Testing.Platform on the .NET 10 SDK: `global.json` got `"test": {"runner": "Microsoft.Testing.Platform"}`, and `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` were removed from the test project.
  - D52 (fast mode, full scope) adopted because of the deadline.
