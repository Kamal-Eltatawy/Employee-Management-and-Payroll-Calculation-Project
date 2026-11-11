# Session 3: CI, review and submission prep (Claude, Cowork mode)

> **About this file.** This is the last part of the same desktop conversation as session 2. It is kept in its own file because it covers a different kind of work (CI, a full code review, hardening). Like sessions 1 and 2, it was reconstructed by the assistant from its own session context, because the desktop app offers no usable export.
>
> - **Kamal's messages and answers were written in Egyptian Arabic** (with English technical terms). They are **translated into English by the assistant**, as close to the original meaning as possible; typing slips are not reproduced. Pasted console output and screenshots are summarised in square brackets.
> - **The assistant's replies are condensed and written in English** (they were originally in Arabic): same content, decisions and recommendations.
> - Tool calls (file writes, shell commands in the sandbox, sub-agents) are summarised in square brackets. No secrets appear: `.env` values were only ever checked for presence and consistency, never printed.
> - **Omissions:** personal remarks that are not about building the project (personal accounts, personal study material, private correspondence) are left out. Each place is marked with *[personal remark omitted]* or a bracketed note. Nothing else was omitted.
>
> Dates: 2026-11-10 → 2026-11-11. Working mode: D52 "fast mode".

---

## 1. GitHub and CI (D63)

**Kamal:** [personal remark omitted: setting up his own GitHub account for the repository. The assistant explained a repository-local Git identity and credential setup; Kamal pushed the repository himself.]

> Thanks, all good. Let's do the last point.

**Assistant:** [**D63**: `.github/workflows/ci.yml` with three jobs: `backend` (restore, Release build, `dotnet test --solution Payroll.slnx`, including the Testcontainers integration tests), `frontend` (`npm ci`, `ng build`, `ng test`) and `docker` (builds both images after the other two pass).]

---

## 2. Published images (D64)

**Kamal:**

> Everything is green now. How do I see the production publish from the CI?

**Assistant:** Explained that CI only builds and tests; nothing is deployed yet. Asked: publish the images to GitHub Container Registry (recommended), deploy to a cloud host, or leave CI as build-only.

**Kamal's answer:** "Publish the images to GHCR (Recommended)"

**Assistant:** [**D64**: on a push to `main` the `docker` job logs in with `GITHUB_TOKEN` and pushes `-api` and `-web` images tagged `latest` and the commit SHA (pull requests only build). Added `docker-compose.prod.yml` (images instead of builds), `start.ps1 -FromRegistry`, and `IMAGE_PREFIX` / `IMAGE_TAG` in `.env.example`.] Asked Kamal to make the two packages public.

**Kamal:**

> I made them public. What do I do next?

**Assistant:** [Added `IMAGE_PREFIX` and `IMAGE_TAG` to Kamal's `.env`.] Run `docker compose down`, then `.\start.ps1 -FromRegistry`, and open the printed addresses.

**Kamal:**

> I don't understand what we did. Will it run from the internet or locally?

**Assistant:** Locally. The images are stored on GHCR, and `-FromRegistry` downloads them and runs them on the same machine. A public URL would need a hosting service, which the assessment does not ask for.

**Kamal:**

> I haven't pushed the latest changes yet. If the reviewer has no internet access, what happens when he runs docker compose?

**Assistant:** Any Docker setup needs the internet the first time (base images, NuGet and npm packages). After that, `docker compose up` without `--build` runs offline from the local images. `-FromRegistry` always needs the internet because it pulls first.

---

## 3. A read-only review of the whole solution

**Kamal:** asked for improvement suggestions before submission. [personal remark omitted: a request for personal study material.]

**Assistant:** Asked two format questions about the personal material (answered; not part of the project). [Ran six read-only sub-agents in parallel, one per area (overview, solution + Domain, Application + Infrastructure, Api, Angular, tests + DevOps). Each read the real files and listed findings with what / why / effort / files. Three sub-agents stopped early on a usage limit, so the review lists only came back for Domain, Application + Infrastructure and Angular: 34 suggestions.] Summarised the most visible ones: domain errors without a translation code, the closed-month rule only in the validator, `Employee.Apply` assigning before validating, unique-index races returning a generic 409, a refresh-token comment that did not match the code, the browser tab title "Client", and a "must be greater than zero" message on an empty dropdown. None was applied yet.

**Kamal:**

> There are things in Arabic that shouldn't be in Arabic, and things that shouldn't be written at all. Is that a problem in the session files?

**Assistant:** Asked how to handle the transcripts: keep Kamal's messages verbatim and leave personal remarks out with a visible note (recommended), translate everything into English, or leave the files as they are.

**Kamal's answer:** "Personal things out, and everything in English"

**Assistant:** [Translated Kamal's messages in sessions 1 and 2 into English, marked every omission, replaced "Nothing was omitted" with an accurate note, and updated `ai-usage/README.md`.] Gave a commit message.

---

## 4. Time spent

**Kamal:** [personal remark omitted: private correspondence.] Pointed out that the README implied the project took more than 8 hours, while it actually took about 8.

**Assistant:** The 12.25 hours in D49 was a planning estimate for manual work, made before implementation. [Rewrote README §6 to state the actual time (about 8 hours with AI assistance) and to call 12.25 hours an estimate; added the same note to D49 in the spec.]

**Kamal:**

> Do you think anything is missing, or do we need to do anything?

**Assistant:** Nothing required is missing. [Checked that the committed `.env.example` values work as they are: the SQL password matches the connection string and meets the policy, the JWT key is long enough.] Recommended a final run from a fresh clone, exactly as a reviewer would, and offered two small fixes (the tab title, unused package versions).

---

## 5. Decisions D65–D69: applying the review

**Kamal:** [quoted the seven visible suggestions] "And you also mentioned these." Then: "Apply the suggestions file."

**Assistant:** Asked about scope: only the small and safe items (recommended), all 34, or only the seven quoted.

**Kamal's answer:** "All 34"

**Assistant:** Asked the four points that were real decisions:

| Question | Kamal's answer | Decision |
|---|---|---|
| Lowest allowed hire date | "No limit" | **D65** |
| Account lockout | "10 attempts → 5 minutes" | **D66** |
| A user with both Hr and Employee roles | "Logs in normally as HR, and make the system multi-role" | **D67** |
| Default tax scheme in the employee form | "'Select…' like the department (Recommended)" | **D68** |

**Assistant:** [**D69**, in one batch:

- *Domain:* every rule a user can reach has a code in `DomainErrorCodes`; guard arguments carry the request field name, which the API translates (`FieldNames*.resx`); the closed-month rule moved into the `PayrollPolicy` constructor (`currentMonth` parameter); `Employee.Apply` validates everything before assigning; a soft-deleted employee is read-only; money and percentages allow at most 2 decimals; `default(PayrollMonth)` is rejected; the sorted tax-bracket list is cached; ids are guarded; a new `ReviewRulesTests` class covers all of it.
- *Infrastructure:* unique-index races on pay component names and policy dates return the same coded conflict as the service check; a rotated refresh token is accepted exactly once inside the grace window (`GraceUsedAtUtc`); Identity lockout and a dummy-hash check for unknown emails (same timing); `EnableRetryOnFailure`; `AsNoTracking` for an existence check; the cached policy repository uses the shared "current month"; one query for the dashboard totals; the six migrations were deleted so Kamal could generate one clean `InitialCreate`.
- *Auth:* `roles: string[]` in the login response; one role claim per role in the JWT.
- *Client (a sub-agent, verified in a sandbox copy):* "please select" messages, translated page titles, guest guard on `/login`, no duplicate message for network or 5xx errors, error toasts that stay with `role="alert"`, "Reload latest" after a concurrency conflict, nginx security and caching headers with gzip, `rxResource` loaders, ESLint with angular-eslint, component tests, a Playwright smoke test (login, employee list, payslip in English and Arabic) and a CI `e2e` job that runs it against the Docker stack. It also found and fixed an existing bug: field errors did not show after Save on untouched fields (components are OnPush by default in Angular 22).
- Removed two unused package versions. Recorded D65–D69 in the spec, the README and `PROGRESS.md`.]

Gave one command block: recreate the database, generate the migration, build, test, lint, start.

**Kamal:** [pasted output: `InitialCreate` generated; build succeeded; **309/309** .NET tests passed; `npm ci` OK; lint clean; **81/81** Angular tests passed; both locales built; `.\start.ps1` printed every address.] [screenshot: `http://localhost:5081/swagger` returns a 401 problem+json.]

> Why does Swagger look like this?

**Assistant:** An older bug, from Phase 7, not from this batch. Swagger UI is served by middleware without an endpoint, and it was registered after `UseAuthorization`, so the fallback "authenticated users only" policy answered 401. [Moved `UseSwaggerUI` before authentication (Development only); the API calls it makes still need a token. Added an integration test that `/swagger/index.html` and `/openapi/v1.json` answer 200.] Gave the commit commands.

---

## 6. This file

**Kamal:** asked for a session-3 file in the same style (English only, personal remarks left out), an update of the review list, and an update of the README's next steps.

**Assistant:** [Moved the GitHub/CI part from the end of `session-02-implementation.md` into this file, wrote the rest, refreshed the review list with what is done and what remains, and updated the README's limitations and next steps.]
