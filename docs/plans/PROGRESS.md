# Implementation Progress

> **Resuming in a new session?** Read, in this order:
>
> 1. This file, top to bottom.
> 2. `docs/plans/00-master-plan.md` (Global Constraints, Execution model, Shared contracts).
> 3. The detailed plan of the **current phase** (below).
> 4. The spec, only for the sections the current task needs: `docs/design/2026-11-09-employee-payroll-design.md`.
>
> Then continue from **Next action**. Update this file at the end of every step that changes state.

## Working rules (do not break)

- **Language:** discuss with Kamal in **Arabic**. Every file (code, docs, commit messages) is in **English** (D1).
- **Decisions:** every decision is asked as one question with options plus a recommendation, then recorded in the spec's decision log (§20).
- **Git (D50):** Claude **never** runs `git commit` / `git push`. Kamal commits at every **Checkpoint**.
- **TDD in every phase (D51):** write the failing test → Kamal runs it (red) → write the code → Kamal runs it (green) → checkpoint.
- **Who runs what:**
  - Claude's sandbox has **no .NET SDK, no NuGet, no Docker**. Kamal runs all `dotnet` / `docker` commands in PowerShell at the repo root and pastes the output.
  - Claude verifies Angular builds and tests in a **sandbox copy** (`/tmp/verify-client`) and **never** runs `npm install` inside the repo.

## Environment facts (verified 2026-11-09)

| Item | Value |
|---|---|
| Angular CLI | 22.2.6 (requires Node `^22.22.3 \|\| ^24.15.0`) |
| Bootstrap | 5.3.8 |
| Sandbox Node | v22.23.4 |
| .NET SDK (Kamal) | 10.0.400 (`global.json` pins 10.0.100 with `rollForward: latestFeature`) |
| Node (Kamal) | **v22.22.0: too old for Angular CLI 22.2.6. Kamal must upgrade to ≥ 22.22.3 or 24 LTS before Phase 1 / Task 6, Step 7** |
| Docker (Kamal) | 29.8.0, Compose v5.5.1 |
| Pinned packages | see `Directory.Packages.props`: EF/ASP.NET 10.0.14, FluentValidation 12.2.0, Mapster 10.0.14, xunit.v3 4.0.3, NSubstitute 6.2.0 |
| dotnet-ef | 10.0.14, in a local tool manifest at the repo root (`dotnet-tools.json`) |
| Git | initialised on `main` (all commits by Kamal) |

## Status

| Phase | Plan file | Status |
|---|---|---|
| 1 Setup & skeleton | `01-setup-and-skeleton.md` | **in progress** |
| 2 Employee CRUD backend | `02-employee-crud-backend.md` | plan not written yet |
| 3 Payroll backend | `03-payroll-backend.md` | plan not written yet |
| 4 Angular client | `04-angular-client.md` | plan not written yet |
| 5 Admin APIs | `05-admin-apis.md` | plan not written yet |
| 6 Health + logging | `06-health-and-logging.md` | plan not written yet |
| 7 Identity + JWT | `07-identity-and-jwt.md` | plan not written yet |
| 8 Docs | `08-docs.md` | plan not written yet |
| 9 *(optional)* Integration tests | — | — |
| 10 *(optional)* Docker + CI | — | — |

### Phase 1 tasks

| Task | Status | Notes |
|---|---|---|
| 1 Toolchain check | **done** | Node needs an upgrade (see above) |
| 2 Pin package versions | **done**, reviewed | Kamal to commit the checkpoint |
| 3 Solution skeleton | **done**, build green | Kamal to commit the checkpoint |
| 4 Error-handling foundation (TDD) | Steps 1–2 done (test project + failing tests written) | next: Kamal runs Step 3 (expect RED) |
| 5 API host + `.env` + SQL Server | not started | |
| 6 Angular scaffold | not started | |

## Next action

1. Kamal commits the Task 2 and Task 3 checkpoints.
2. Kamal runs Phase 1 / Task 4 / Step 3 (add the test project to the solution and run `dotnet test`). Expected result: **compile errors** (RED).
3. Claude writes the Task 4 / Step 4 implementation, and Kamal runs the tests again (GREEN).

## Log

- **2026-11-09:**
  - Spec approved (D1–D51).
  - Master plan + Phase 1 plan written.
  - `git init` done by Claude (no commits).
  - Execution started inline in this session.
  - `docs/plans/PROGRESS.md` and the root `CLAUDE.md` created for session continuity.
- **2026-11-10:**
  - P1 Tasks 1–3 done: toolchain checked, packages pinned, skeleton builds green.
  - `dotnet-tools.json` was created at the repo root rather than `.config/`; the plan was updated to match.
  - P1 Task 4: failing tests written.
