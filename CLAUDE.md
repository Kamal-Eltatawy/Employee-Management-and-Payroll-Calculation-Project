# CLAUDE.md — Employee Management & Payroll (assessment project)

Before doing anything, read `docs/plans/PROGRESS.md`. It says exactly where the work stopped and what to do next.

## Key documents

- **Spec (approved, D1–D51):** `docs/design/2026-11-09-employee-payroll-design.md`
- **Master plan** (phases, shared contracts, execution model): `docs/plans/00-master-plan.md`
- **Detailed phase plans:** `docs/plans/NN-*.md`
- **Progress and next action:** `docs/plans/PROGRESS.md`

## Non-negotiable rules

1. **Language:** talk to Kamal in Arabic. Write every file in English.
2. **Decisions:** ask about any undecided point as one question with options and a recommendation, then record it in the spec's decision log. Never silently change an agreed decision. If something conflicts with an earlier decision, say so.
3. **Git:** never run `git commit` or `git push`. Kamal owns Git and commits at each checkpoint.
4. **TDD in every phase.** The spec wins over the plans; the plans win over assumptions.
5. **Execution split:**
   - No .NET SDK, NuGet or Docker in the Claude sandbox. Kamal runs `dotnet` / `docker` commands and pastes the output.
   - Never run `npm install` inside the repo from Linux. Verify Angular in a `/tmp` copy instead.
6. **Keep `docs/plans/PROGRESS.md` updated** after every step that changes state.
