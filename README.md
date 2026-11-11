# Employee Management & Payroll Calculation

A full-stack assessment project: HR manages employees and their allowances/deductions, the pay items themselves and the payroll policies (insurance and tax brackets per tax scheme), and anyone with access can see an exact monthly payslip (progressive, flat or exempt tax, social insurance with a cap, taxable and non-taxable allowances, hire-month proration, effective-dated payroll policies).

- **Backend:** ASP.NET Core Web API on **.NET 10**, EF Core 10 on **SQL Server 2022** (Docker), ASP.NET Core Identity + JWT with rotating refresh tokens, FluentValidation, Mapster.
- **Frontend:** **Angular 22** (standalone components, signals, zoneless, typed reactive forms, `rxResource`), Bootstrap 5.3 from SCSS source, English and Arabic (RTL) with Angular i18n, served by nginx with a strict Content-Security-Policy.
- **Tests:** xUnit v3 unit tests (Domain, Application, Api), integration tests against a real SQL Server in Testcontainers, Angular unit and component tests (Vitest through the Angular CLI), ESLint, and a Playwright smoke test against the full Docker stack. All of it runs in GitHub Actions.

The full design, with every decision and its reasoning (D1–D71), is in [`docs/design/2026-11-09-employee-payroll-design.md`](docs/design/2026-11-09-employee-payroll-design.md).

---

## 1. Run it

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0.100 or later (pinned by `global.json`, roll-forward to the latest feature band) |
| Node.js | 22.22.3+, 24.15+ or 26+ (required by Angular CLI 22) |
| Docker | with Compose v2 |

### Steps (PowerShell, from the repository root)

```powershell
# 1. Secrets: copy the template and change every value. .env is git-ignored.
Copy-Item .env.example .env

# 2. Database (SQL Server 2022 in Docker, published on host port 14333)
docker compose up -d db

# 3. API: in Development it applies the migrations and seeds demo data on startup
dotnet tool restore
dotnet run --project src/Payroll.Api
#    Swagger UI: http://localhost:5080/swagger   Health: http://localhost:5080/health

# 4. Client (second terminal)
cd client
npm install
npm start            # English: http://localhost:4200  (the dev server proxies /api to http://localhost:5080)
npm run start:ar     # Arabic (RTL): http://localhost:4201  (optional, a third terminal)
```

### Or everything in Docker

```powershell
Copy-Item .env.example .env     # once, then set the values
.\start.ps1                     # builds and starts db + API + app, waits until ready, prints every address
# or: docker compose up --build  (the "urls" service prints the addresses once everything is healthy)
```

### Or the images built by CI

Every push to `main` publishes `ghcr.io/<owner>/<repo>-api` and `-web` (tags `latest` and the commit SHA) to GitHub Container Registry. To run exactly what CI tested, without .NET or Node on the machine:

```powershell
# in .env: IMAGE_PREFIX=ghcr.io/<owner>/<repo>   (lower-case)
.\start.ps1 -FromRegistry
```

| What | Address |
|---|---|
| App, English | http://localhost:8080/en/ |
| App, Arabic (RTL) | http://localhost:8080/ar/ |
| Swagger UI | http://localhost:5081/swagger |
| Health | http://localhost:8080/health |
| SQL Server | `localhost,14333` (user `sa`, password from `.env`) |

The API container starts in Development (migrations + demo data + Swagger), and nginx serves the Angular build and proxies `/api` to the API, so the app and API share one origin (no CORS, refresh cookie works). Use only HTTPS outside localhost: the refresh cookie is `Secure`.

> Host port **14333** is used because a local SQL Server often owns 1433 on Windows. Change both `docker-compose.yml` and `ConnectionStrings__Default` if you need another port.
>
> The Compose project name is fixed (`payroll`), so every clone on one machine shares the `payroll_sqldata` volume. SQL Server keeps the `sa` password it was first created with: if the `db` container stays *unhealthy* after you change `MSSQL_SA_PASSWORD` (or run a second clone with other values), reset the demo database with `docker compose down -v`.

### Demo accounts

Passwords come from `.env` (`Seed__HrPassword`, `Seed__EmployeePassword`); nothing secret is in the code.

| Email | Role | What it shows |
|---|---|---|
| `hr@payroll.local` | Hr | Full access: home dashboard, employees, payslips, pay items, payroll policies |
| `ali@demo.local` | Employee | Sees only his own payslip (`/my-payslip`) |
| `youssef@demo.local` | Employee | His employee is **Inactive**, so login is refused with the generic 401 (D25) |

A user can hold several roles (D67); with Hr among them the HR screens are shown. Ten wrong passwords lock an account for five minutes (D66).

Demo employees cover the interesting cases: the spec's worked example (Ali Hassan, net **15,950.00** for a full month), an inactive employee, someone hired this month (proration), deductions larger than earnings (negative-net warning), and employees on the Flat and Exempt tax schemes.

In Swagger: call `POST /api/auth/login`, click **Authorize**, paste the `accessToken`.

### Tests

GitHub Actions (`.github/workflows/ci.yml`) runs on every push and pull request: .NET build with warnings as errors, all unit and integration tests (Testcontainers SQL Server), ESLint, the Angular build in both languages with its unit and component tests, the Docker image build, and a Playwright smoke test (login, employee list, payslip in English and Arabic) against the full Docker stack (it also opens the pay item and payroll policy screens). On `main` the images are also published to GitHub Container Registry.

```powershell
dotnet test --solution Payroll.slnx          # all .NET tests (integration tests need Docker running)
dotnet test --project tests/Payroll.Domain.UnitTests   # one project, e.g. unit tests only (no DB, no network)
cd client; npx ng test --watch=false         # Angular unit + component tests
cd client; npm run lint                       # ESLint (angular-eslint, template accessibility rules)
cd client; $env:E2E_HR_PASSWORD="<Seed__HrPassword from .env>"; npm run e2e   # Playwright smoke test against http://localhost:8080 (stack running)
```

---

## 2. Architecture

```
client/ (Angular)          core (auth, http errors, i18n, layout) · shared (pipes, form helpers, ui)
                           features: home · employees · payslip · pay-component-types · payroll-policies
        │  /api (dev proxy), Bearer JWT + HttpOnly refresh cookie
Payroll.Api                controllers · ApiExceptionHandler (RFC 7807) · JWT auth · OpenAPI · health · composition root
Payroll.Infrastructure     EF Core + migrations · repositories · unit of work · Identity · JWT/refresh tokens · seeding
Payroll.Application        use-case services · validators · DTOs · Mapster config · repository/UoW/current-user ports
Payroll.Domain             entities · value objects · payroll calculators · business rules   (no package references)
```

**Why four projects (D13):** the compiler enforces that payroll rules cannot depend on EF Core, Identity or ASP.NET, so the whole payroll engine is tested with plain `new`, without doubles.

**Key design choices** (details and alternatives in the spec):

| Topic | Choice |
|---|---|
| Domain model | Rich entities with private setters and guard clauses (`DomainException`); validators give field-level UX errors first (D15/D16) |
| Interfaces | Every behaviour class is consumed through an interface (services, calculators, repositories, UoW, current user, token generator); data types are concrete (D39) |
| Pay components | One concept for allowances **and** deductions: a `PayComponentType` master table (`Kind`, `IsTaxable`) plus per-employee amounts. New types are data, not code (D2/D26). A type's kind can change only while no employee uses it, because payslips are recalculated, not stored (D71). HR manages them in the "Pay items" screen (D70) |
| Tax schemes | Each employee belongs to a `TaxScheme` (Progressive / Flat / Exempt). `ITaxCalculator` has one implementation per method; all are registered and `PayrollCalculator` picks by the scheme's method (strategy, D53) |
| Payroll policies | Brackets + insurance rate/cap are effective-dated data per scheme. The latest policy on or before the month applies (D29). Past months are closed; the current month stays open for corrections (D54). The "Payroll policies" screen has one tab per scheme and a tax-bracket editor that checks the same shape rules as the server (D70) |
| Money | `decimal` everywhere, **no rounding in the domain**; the API returns full precision and only the Angular `MoneyPipe` rounds for display (2 decimals, half away from zero) (D32/D33) |
| Errors | One `IExceptionHandler`; every error is `application/problem+json` with `traceId` and camelCase JSON-path field keys such as `payComponents[0].amount` (D17/D42). Every rule a user can reach has an error code, translated with its field names into the request language (D56/D69) |
| Error mapping | One `IProblemMapper` per exception type, picked by `ApiExceptionHandler` (nearest base type first), messages through the injected `IApiText` (OCP/DIP, D62) |
| Scalability | Pooled `DbContext`; cached reference data and closed-month policies (decorators); SQL aggregates and batched payroll for the dashboard; prefix search on indexed columns; expired refresh tokens cleaned up in the background (D62) |
| Security | Rate limit on login/refresh per IP (429); account lockout after 10 wrong passwords and equal timing for unknown emails (D66); access token in memory only; refresh token HttpOnly + rotation + reuse detection (D55/D61/D62/D69); CSP and other security headers in nginx (D69) |
| Database errors | Translated in exactly one place, `AppDbContext.SaveChangesAsync` (unique index → the same coded 409 on the field as the service check, concurrency → 409) (S6/D69) |
| Concurrency | `rowversion` on employees; the client sends it back on update (D12) |
| Soft delete | `DeletedAtUtc` + EF query filter; the email stays reserved (D5/D6) |
| Auth | Identity + 15-minute JWT; a user may hold several roles (`roles` in the login response, Hr wins, D67); rotating refresh token in an HttpOnly, Secure, SameSite=Strict cookie scoped to `/api/auth`, stored only as a SHA-256 hash, with reuse detection and one extra use inside a short grace window (D24/D25/D55/D61/D69) |
| Client state | Thin typed API services + signals in pages; list filters live in the URL; no store library (D41) |
| Home page | HR: head counts, distribution by department and tax scheme, and the current month's payroll totals computed with the real payslip engine (`GET /api/dashboard`). Employee: this month's net pay and a link to the payslip (D59) |
| Languages | English and Arabic (RTL). Official Angular i18n, one build per language; the API answers in the same language through `Accept-Language` and coded, translated error messages (D56) |

### Payroll formula (per employee, per month)

```
f            = proration factor: (days in month − hire day + 1) / days in month in the hire month, else 1
base         = BaseMonthlySalary × f
allowances   = each allowance amount × f
gross        = base + Σ allowances
taxable      = base + Σ taxable allowances
incomeTax    = by the scheme's method: Progressive Σ over monthly bands · Flat one rate · Exempt 0
insurance    = base × rate, capped at cap × f when the policy has a cap
deductions   = Σ deduction amounts (not prorated)
net          = gross − incomeTax − insurance − deductions        (isNetNegative when net < 0)
```

Worked example (spec §11.5, unit-tested): base 17,000, transport 1,000 (non-taxable), housing 2,000 (taxable), loan 750, policy v1 → gross 20,000, taxable 19,000, tax 1,800, insurance 1,500 (capped), **net 15,950**. Hired on 20 March: f = 12/31, **net 6,175.81** on display (full precision in the API).

### API overview (`/api`)

| Area | Endpoints | Access |
|---|---|---|
| Auth | `POST auth/login`, `POST auth/refresh`, `POST auth/logout` | anonymous |
| Employees | `GET employees?search=&departmentId=&status=&sortBy=&sortDir=&page=&pageSize=`, `GET/PUT/DELETE employees/{id}`, `POST employees` | Hr |
| Payslip | `GET employees/{id}/payslip?month=yyyy-MM` | Hr (any), Employee (own only) |
| Reference data | `GET departments`, `GET tax-schemes` (names in the request language, D58) | signed in |
| Dashboard | `GET dashboard` | Hr |
| Pay component types | full CRUD on `pay-component-types`; responses carry `isInUse`; `PUT` may change `kind` only while the type is unused (D71) | Hr |
| Payroll policies | `GET payroll-policies`, `GET payroll-policies/{id}`, `GET payroll-policies/effective?taxSchemeId=&month=`, `POST/PUT/DELETE` | Hr |
| Health | `GET /health` (outside `/api`) | anonymous |

---

## 3. Assumptions

- **Rounding rule:** every amount is computed and returned at full `decimal` precision; it is rounded only for display, to 2 decimals, half away from zero (`MoneyPipe`). The net shown is the true net rounded once, so displayed lines may differ from it by 0.01.
- Tax brackets are **monthly**. The tax base is base salary + taxable allowances; social insurance is not deducted from it.
- Social insurance is a percentage of the **base salary only**, with an optional cap (a cap of 0 means zero insurance; no cap means uncapped).
- The hire month is prorated by calendar days, counting the hire day. Allowances and the insurance cap are prorated; deductions are not.
- Payslips are **computed on demand**, not stored. They use the employee's current salary and pay components with the policy effective for the requested month. Future months are allowed as previews.
- Payroll is paid at month end, so the current month is still open for policy corrections (D54).
- A negative net is returned as is, flagged with `isNetNegative`, and shown as a warning.
- "Today" is the server's local date (`TimeProvider`); the Angular hire-date check uses the browser's local date.

## 4. Known limitations

- No salary history and no stored payroll runs, so a past payslip combines the historical policy with the current salary data.
- Departments and tax schemes are read-only seed data (pay items and payroll policies have HR screens, D70).
- Pay items and payroll policies have no concurrency token: if two HR users edit the same record, the last save wins (employees use `rowVersion`).
- An access token issued before an employee is deactivated stays valid until it expires (at most 15 minutes); the next refresh is refused.
- Employee search matches the start of the name or email (index-friendly), not words in the middle of a name.
- No restore endpoint for soft-deleted employees. Proration only covers the hire month (there is no termination date).
- Displayed lines can differ from the displayed net by 0.01, because each value is rounded once for display.
- Users and roles come from the seed: there is no screen to create users, link them to an employee, assign several roles (D67) or unlock an account.
- Lockout is per account (D66), so someone who knows an email can lock that account for 5 minutes; the per-IP rate limit only slows this down.
- The "accepted once inside the grace window" check for refresh tokens is not atomic: two reuses at exactly the same moment could both pass.
- Error responses (400/404/409/422) are not described in the OpenAPI document, only the success codes.
- Migrations and demo data are applied at API start-up in Development only; there is no production migration step yet.

## 5. Next steps

1. Stored payroll runs with an explicit "close payroll month" step (would replace the current-month rule of D54).
2. Screens for departments and tax schemes, a user screen (create, link to an employee, roles, unlock), and `rowVersion` on pay items and policies.
3. Production readiness: an HTTPS reverse proxy with HSTS, a separate migration step in the pipeline, `ValidateOnStart` for every options class, secrets from a vault, and a deployment stage in CI.
4. Salary history, termination-month proration, restore for deleted employees.
5. Document every error response in OpenAPI (`ProducesProblem`) and add API versioning before a second client depends on the API.
6. Observability: OpenTelemetry traces and metrics next to the existing JSON logs and `traceId`.
7. More end-to-end coverage (create and edit an employee, policy changes, the employee role) and visual checks of the RTL layout; today the Playwright smoke test covers login, the employee list and a payslip in both languages.

---

## 6. Scope & time

**Actual time spent: about 8 hours**, with AI assistance (see section 7 and `/ai-usage`).

Before implementation, the scope was estimated at about 12.25 hours of manual work (D49). That figure was a planning estimate, not the time spent. The work was split into phases that each ended in a working, committed state:

| Phase | Content | Status |
|---|---|---|
| 1 | Solution skeleton, error handling, API host, Docker DB, Angular scaffold | done |
| 2 | Employee CRUD backend + tests | done |
| 3 | Payroll engine, policies, payslip API + tests | done |
| 4 | Angular client (list, form, payslip) + logic tests | done |
| 5 | Admin APIs: pay component types, payroll policies | done |
| 6 | Health check + JSON logging | done |
| 7 | Identity + JWT (backend and client) + tests | done |
| 8 | README + `/ai-usage` | done |
| 9 | *Optional:* integration tests (Testcontainers SQL Server + `WebApplicationFactory`) | done |
| 10 | *Optional:* full `docker compose up` (db + API + client) and a CI pipeline | done |
| — | Review and hardening: coded domain errors, multi-role users, lockout, squashed migrations, ESLint, component and Playwright tests (D65–D69) | done |
| — | HR screens for pay items and payroll policies; pay item kind editable while unused (D70–D71) | done |

Scope added during implementation, at my request: tax schemes per employee with pluggable tax methods (D53), keeping the current month open for policy corrections (D54), rotating refresh tokens (D55), Arabic and English in the UI and the API (D56, D58), a new layout and home dashboards (D57, D59), a one-command Docker run (D60), a CI pipeline (D63), a server-owned session shared by both languages (D61), a SOLID/scalability review (D62), published Docker images (D64), a full review with multi-role users and account lockout (D65–D69), and HR screens for pay items and payroll policies (D70–D71). Each addition is recorded in the spec's decision log with its reasoning.

The assessment suggests 6–8 hours, and the work stayed within about 8 hours: AI assistance made the larger planned scope (D49) and the additions above fit in that time. I chose depth over breadth where it served the evaluation criteria (architecture, validation, tests, auth) and documented every decision so the trade-offs are visible.

## 7. AI usage

This project was built with **Claude** (Anthropic) in the Claude desktop app (Cowork mode). The AI wrote the code, tests and documents; I made the decisions, reviewed the work, ran every build and test, and tested the application by hand. Details, the tools and skills used, and the exported sessions are in [`ai-usage/`](ai-usage/README.md).
