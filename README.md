# Employee Management & Payroll Calculation

A full-stack assessment project: HR manages employees and their allowances/deductions, and anyone with access can see an exact monthly payslip (progressive tax, social insurance with a cap, taxable and non-taxable allowances, hire-month proration, effective-dated payroll policies per tax scheme).

- **Backend:** ASP.NET Core Web API on **.NET 10**, EF Core 10 on **SQL Server 2022** (Docker), ASP.NET Core Identity + JWT with rotating refresh tokens, FluentValidation, Mapster.
- **Frontend:** **Angular 22** (standalone components, signals, typed reactive forms), Bootstrap 5.3 from SCSS source.
- **Tests:** xUnit v3 unit tests (Domain, Application, Api), integration tests against a real SQL Server in Testcontainers, and Angular logic tests (Vitest through the Angular CLI).

The full design, with every decision and its reasoning (D1–D55), is in [`docs/design/2026-11-09-employee-payroll-design.md`](docs/design/2026-11-09-employee-payroll-design.md).

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
npm start
#    http://localhost:4200  (the dev server proxies /api to http://localhost:5080)
```

### Or everything in Docker

```powershell
Copy-Item .env.example .env     # once, then set the values
docker compose up --build
#    App: http://localhost:8080   Swagger: http://localhost:5081/swagger
```

The API container starts in Development (migrations + demo data + Swagger), and nginx serves the Angular build and proxies `/api` to the API, so the app and API share one origin (no CORS, refresh cookie works). Use only HTTPS outside localhost: the refresh cookie is `Secure`.

> Host port **14333** is used because a local SQL Server often owns 1433 on Windows. Change both `docker-compose.yml` and `ConnectionStrings__Default` if you need another port.

### Demo accounts

Passwords come from `.env` (`Seed__HrPassword`, `Seed__EmployeePassword`); nothing secret is in the code.

| Email | Role | What it shows |
|---|---|---|
| `hr@payroll.local` | Hr | Full access: employees, payslips, admin APIs |
| `ali@demo.local` | Employee | Sees only his own payslip (`/my-payslip`) |
| `youssef@demo.local` | Employee | His employee is **Inactive**, so login is refused with the generic 401 (D25) |

Demo employees cover the interesting cases: the spec's worked example (Ali Hassan, net **15,950.00** for a full month), an inactive employee, someone hired this month (proration), deductions larger than earnings (negative-net warning), and employees on the Flat and Exempt tax schemes.

In Swagger: call `POST /api/auth/login`, click **Authorize**, paste the `accessToken`.

### Tests

```powershell
dotnet test --solution Payroll.slnx          # all .NET tests (integration tests need Docker running)
dotnet test --project tests/Payroll.Domain.UnitTests   # one project, e.g. unit tests only (no DB, no network)
cd client; npx ng test --watch=false         # Angular logic tests
```

---

## 2. Architecture

```
client/ (Angular)          core (auth, http errors, layout) · shared (pipes, form helpers, ui) · features/employees · features/payslip
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
| Pay components | One concept for allowances **and** deductions: a `PayComponentType` master table (`Kind`, `IsTaxable`) plus per-employee amounts. New types are data, not code (D2/D26) |
| Tax schemes | Each employee belongs to a `TaxScheme` (Progressive / Flat / Exempt). `ITaxCalculator` has one implementation per method; all are registered and `PayrollCalculator` picks by the scheme's method (strategy, D53) |
| Payroll policies | Brackets + insurance rate/cap are effective-dated data per scheme. The latest policy on or before the month applies (D29). Past months are closed; the current month stays open for corrections (D54) |
| Money | `decimal` everywhere, **no rounding in the domain**; the API returns full precision and only the Angular `MoneyPipe` rounds for display (2 decimals, half away from zero) (D32/D33) |
| Errors | One `IExceptionHandler`; every error is `application/problem+json` with `traceId` and camelCase JSON-path field keys such as `payComponents[0].amount` (D17/D42) |
| Database errors | Translated in exactly one place, `AppDbContext.SaveChangesAsync` (unique index → 409 on the field, concurrency → 409) (S6) |
| Concurrency | `rowversion` on employees; the client sends it back on update (D12) |
| Soft delete | `DeletedAtUtc` + EF query filter; the email stays reserved (D5/D6) |
| Auth | Identity + 15-minute JWT; rotating refresh token in an HttpOnly, Secure, SameSite=Strict cookie scoped to `/api/auth`, stored only as a SHA-256 hash, with reuse detection (D24/D25/D55) |
| Client state | Thin typed API services + signals in pages; list filters live in the URL; no store library (D41) |

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
| Reference data | `GET departments`, `GET tax-schemes` | signed in |
| Pay component types | full CRUD on `pay-component-types` | Hr |
| Payroll policies | `GET payroll-policies`, `GET payroll-policies/{id}`, `GET payroll-policies/effective?taxSchemeId=&month=`, `POST/PUT/DELETE` | Hr |
| Health | `GET /health` (outside `/api`) | anonymous |

---

## 3. Assumptions

- Tax brackets are **monthly**. The tax base is base salary + taxable allowances; social insurance is not deducted from it.
- Social insurance is a percentage of the **base salary only**, with an optional cap (a cap of 0 means zero insurance; no cap means uncapped).
- The hire month is prorated by calendar days, counting the hire day. Allowances and the insurance cap are prorated; deductions are not.
- Payslips are **computed on demand**, not stored. They use the employee's current salary and pay components with the policy effective for the requested month. Future months are allowed as previews.
- Payroll is paid at month end, so the current month is still open for policy corrections (D54).
- A negative net is returned as is, flagged with `isNetNegative`, and shown as a warning.
- "Today" is the server's local date (`TimeProvider`); the Angular hire-date check uses the browser's local date.

## 4. Known limitations

- No salary history and no stored payroll runs, so a past payslip combines the historical policy with the current salary data.
- Pay component types and payroll policies have an API but no admin screen; departments and tax schemes are read-only seed data.
- An access token issued before an employee is deactivated stays valid until it expires (at most 15 minutes); the next refresh is refused.
- No restore endpoint for soft-deleted employees. Proration only covers the hire month (there is no termination date).
- Displayed lines can differ from the displayed net by 0.01, because each value is rounded once for display.

## 5. Next steps

1. Stored payroll runs with an explicit "close payroll month" step (would replace the current-month rule of D54).
2. Admin screens for pay component types, payroll policies and tax schemes.
3. A CI pipeline (build, unit + integration tests, Angular tests) and an HTTPS reverse proxy for non-local deployments.
4. Salary history, termination-month proration, restore for deleted employees.

---

## 6. Scope & time

The committed scope was agreed up front at about **12.25 hours** (D49), split into phases that each ended in a working, committed state:

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
| 10 | *Optional:* full `docker compose up` (db + API + client) | done; CI pipeline skipped by decision |

Scope added during implementation, at my request: tax schemes per employee with pluggable tax methods (D53), keeping the current month open for policy corrections (D54), and rotating refresh tokens (D55). Each addition is recorded in the spec's decision log with its reasoning.

## 7. AI usage

This project was built with **Claude** (Anthropic) in the Claude desktop app (Cowork mode). The AI wrote the code, tests and documents; I made the decisions, reviewed the work, ran every build and test, and tested the application by hand. Details, the tools and skills used, and the exported sessions are in [`ai-usage/`](ai-usage/README.md).
