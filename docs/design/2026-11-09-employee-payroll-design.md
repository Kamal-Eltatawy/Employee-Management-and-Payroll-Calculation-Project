# Employee Management & Payroll Calculation — Design Specification

| | |
|---|---|
| **Date** | 2026-11-09 |
| **Status** | Approved by Kamal on 2026-11-09, including the §0 details S1–S12 |
| **Source** | *Interface — Full-Stack Code Assessment (2026)* PDF |
| **Stack** | ASP.NET Core Web API (.NET 10 LTS), EF Core 10, SQL Server, Angular (latest stable), xUnit |
| **Decisions** | Every decision below is traceable to an entry `D1`–`D50` in [§20 Decision log](#20-decision-log). |

> **Review first:** [§0](#0-spec-level-details-not-discussed-explicitly) lists the details this spec had to fix that were **not** discussed one by one. Please confirm or change them before implementation starts.

---

## Table of contents

0. [Spec-level details not discussed explicitly](#0-spec-level-details-not-discussed-explicitly)
1. [Scope](#1-scope)
2. [Working agreements](#2-working-agreements)
3. [Engineering principles](#3-engineering-principles)
4. [Architecture overview](#4-architecture-overview)
5. [Backend solution structure](#5-backend-solution-structure)
6. [Domain model](#6-domain-model)
7. [Database design](#7-database-design)
8. [API contracts](#8-api-contracts)
9. [Error handling](#9-error-handling)
10. [Validation strategy](#10-validation-strategy)
11. [Payroll calculation design](#11-payroll-calculation-design)
12. [Authentication & authorization](#12-authentication--authorization)
13. [Angular architecture](#13-angular-architecture)
14. [Testing strategy](#14-testing-strategy)
15. [Configuration, secrets & seed data](#15-configuration-secrets--seed-data)
16. [Observability](#16-observability)
17. [Dependencies and their justification](#17-dependencies-and-their-justification)
18. [Implementation phases](#18-implementation-phases)
19. [Assumptions & known limitations](#19-assumptions--known-limitations)
20. [Decision log](#20-decision-log)

---

## 0. Spec-level details not discussed explicitly

These follow directly from approved decisions but were not individually discussed. **Confirm or change.**

| # | Detail | Proposed value | Follows from |
|---|---|---|---|
| S1 | Solution / project names | `Payroll.slnx` (the .NET 10 default solution format); `Payroll.Domain`, `Payroll.Application`, `Payroll.Infrastructure`, `Payroll.Api` | D13 |
| S2 | Repository layout | `src/` (backend), `tests/` (backend tests), `client/` (Angular), `docs/`, `ai-usage/` | D13, D40 |
| S3 | Max lengths not given by the PDF | Job title 100; Email 254 (RFC 5321 practical limit); Department / pay component name 100 | D15, D16 |
| S4 | `PUT /employees/{id}` response | `200 OK` + body (so the client receives the new `rowVersion`), not `204` | D12 |
| S5 | `Kind` on pay component types | Present only in the **create** request. The update request has no `Kind` field, so the type cannot be changed by construction | D2 |
| S6 | Database errors become application exceptions | In **one place**: `AppDbContext.SaveChangesAsync` (the `IUnitOfWork` implementation) converts concurrency errors and unique-index violations into `ConflictException` | D14, D17 |
| S7 | Ownership rule ("an Employee sees only their own payslip") | Enforced in `PayslipService` through an `ICurrentUser` abstraction (implemented in Api from the JWT claims) | D9, D19, D39 |
| S8 | Development networking | Angular dev-server **proxy** (`/api` → API), so no CORS configuration is needed | D21 |
| S9 | OpenAPI | Built-in `Microsoft.AspNetCore.OpenApi` document + `Swashbuckle.AspNetCore.SwaggerUI` for the UI (Development only), with a JWT bearer scheme | PDF §5 |
| S10 | Seed strategy | **Reference data** (departments, pay component types, payroll policies) via EF `HasData`, i.e. part of the migrations. **Demo data** (employees, users) via a seeder that runs only in Development, with passwords read from `.env` | D3, D21 |
| S11 | Ordering of payslip checks | Authorization → employee exists → Active → month ≥ hire month → effective policy exists → calculate | D25, D29 |
| S12 | Where the login rule lives | `AuthService` is in **Application** (same as the other services, D19). It depends on `IUserCredentialStore`, a narrow interface implemented in Infrastructure by `IdentityCredentialStore`, which wraps `UserManager`. This keeps the D25 rule unit-testable with fakes | D19, D25, D39 |

---

## 1. Scope

### 1.1 Committed (D49: about 12.25 h accepted and documented in the README)

- **Employee CRUD:**
  - Validation on both client and server
  - RFC 7807 Problem Details
  - Global error handling
  - Pagination, search, filters and sorting
  - Soft delete
  - Optimistic concurrency
- **Pay components:**
  - Master table of allowance/deduction types with a full CRUD API (no admin screen)
  - Assigned to employees with amounts
- **Departments:** master table, seeded, read-only API.
- **Payroll:**
  - Progressive tax
  - Social insurance with an optional cap
  - Taxable / non-taxable allowances
  - Hire-month proration
  - Effective-dated payroll policies with an append-only write API
  - Payslip API and UI
- **Auth:** ASP.NET Core Identity + JWT, with roles `Hr` and `Employee`, enforced on the server.
- **Health check** (`/health`) and **structured JSON logging**.
- **Tests:**
  - Backend unit tests (Domain, Application, Api)
  - Frontend logic tests
- **Delivery:**
  - README covering setup, architecture, assumptions and limitations, and a *Scope & time* section
  - `/ai-usage` folder

### 1.2 Optional phases (only if time remains, in this order)

1. Integration tests with Testcontainers (D47).
2. Full `docker-compose` (API + client) + CI pipeline (D10).

### 1.3 Out of scope (README → "Next steps")

- Admin screens for pay component types and payroll policies (API only).
- Salary history and stored payroll runs.
- Refresh tokens and token revocation.
- Restore endpoint for soft-deleted employees.
- Department CRUD.

---

## 2. Working agreements

| Rule | Source |
|---|---|
| Discussion happens in Arabic; **every file is written in English**. | D1 |
| Every decision is asked as one question with options and a recommendation, then recorded. | D1 |
| **Claude never runs `git commit` or `git push`.** Kamal owns all Git operations. Claude only ran `git init` and added `.gitignore` / `.gitattributes`. | D50 |
| No application code before this spec is approved (Mode 1 → Mode 2). | Kickoff |

---

## 3. Engineering principles

1. **Simplest design that correctly solves the current problem while allowing reasonable extension.**
2. **Interfaces (revised by D39):**

   > *Every class that carries behaviour is consumed through an interface (DIP). Data types (entities, value objects, DTOs) are concrete. This keeps every collaborator substitutable in tests and lets a rule be replaced (e.g. proration by working days) without touching its consumers.*

   Interfaces exist for:
   - application services
   - domain calculators
   - repositories and the unit of work
   - `ICurrentUser` and `ITokenGenerator`

   They do **not** exist for:
   - entities, value objects and DTOs
   - validators (FluentValidation already exposes `IValidator<T>`)
   - controllers
3. **Open/Closed through data first.** New allowance or deduction types, tax exemptions and new tax or insurance rules are **data** (`PayComponentType`, `IsTaxable`, `PayrollPolicy`). Code changes only for genuinely new calculation kinds.
4. **Encapsulation.** Entities have private setters, intention-revealing methods, guard clauses and read-only collections (D16).
5. **Full precision for computed values (D32).** Every computed amount keeps all of its decimals during calculation and storage. Rounding happens **only for display** (D33).
6. **No library for something trivial.** Every package is justified in [§17](#17-dependencies-and-their-justification).
7. **Comments only where code is not self-explanatory.**

---

## 4. Architecture overview

```
┌──────────────────────────── client/ (Angular) ────────────────────────────┐
│ core (auth, http/errors, layout) · shared (pipes, form helpers, ui)        │
│ features/employees · features/payslip                                      │
└───────────────────────────────┬────────────────────────────────────────────┘
                                │ HTTPS / JSON (Bearer JWT), dev proxy /api
┌───────────────────────────────▼────────────────────────────────────────────┐
│ Payroll.Api            controllers · IExceptionHandler · JWT · DI root     │
├────────────────────────────────────────────────────────────────────────────┤
│ Payroll.Infrastructure EF Core · Identity · repositories · UoW · seeding   │
├────────────────────────────────────────────────────────────────────────────┤
│ Payroll.Application    services (+interfaces) · validators · DTOs ·        │
│                        Mapster config · repository/UoW/user abstractions   │
├────────────────────────────────────────────────────────────────────────────┤
│ Payroll.Domain         entities · value objects · calculators · rules      │
│                        (no package references at all)                      │
└────────────────────────────────────────────────────────────────────────────┘
Reference direction:  Api → Infrastructure → Application → Domain
                      Api → Application (contracts)
```

**What each layer may know (D13):**

| Project | Contains | Knows |
|---|---|---|
| Domain | Entities, value objects, domain calculators + interfaces, `DomainException` | nothing |
| Application | Use-case services + interfaces, repository/UoW/current-user/token abstractions, validators, DTOs, Mapster config, application exceptions | Domain |
| Infrastructure | `AppDbContext` (Identity), EF configurations, migrations, repositories, `IdentityCredentialStore`, `JwtTokenGenerator`, seeders | Application, Domain |
| Api | Controllers, `ApiExceptionHandler`, `CurrentUser`, auth setup, OpenAPI, health checks, `Program.cs` | all (composition root) |

**Why 4 projects (D13):** the compiler enforces that payroll rules cannot reference EF Core, Identity or ASP.NET. It also enforces that the Domain knows nothing about repositories or DTOs, so domain rules are tested with plain `new`, without any doubles.

---

## 5. Backend solution structure

```
Payroll.slnx
src/
  Payroll.Domain/
    Common/              DomainException.cs, Guard.cs
    Employees/           Employee.cs, EmployeeStatus.cs, EmployeePayComponent.cs
    Departments/         Department.cs
    PayComponents/       PayComponentType.cs, PayComponentKind.cs
    Payroll/
      PayrollMonth.cs                 (value object, yyyy-MM)
      PayrollPolicy.cs, TaxBracket.cs (aggregate + invariants)
      Payslip.cs, PayslipLine.cs      (value objects, result)
      ITaxCalculator.cs               ProgressiveTaxCalculator.cs
      ISocialInsuranceCalculator.cs   SocialInsuranceCalculator.cs
      IProrationCalculator.cs         CalendarDayProrationCalculator.cs
      IPayrollCalculator.cs           PayrollCalculator.cs
  Payroll.Application/
    Abstractions/        IEmployeeRepository, IDepartmentRepository,
                         IPayComponentTypeRepository, IPayrollPolicyRepository,
                         IUnitOfWork, ICurrentUser, ITokenGenerator, IUserCredentialStore
    Common/              NotFoundException, ConflictException, ForbiddenException,
                         PagedResult<T>, MappingConfig (Mapster), Roles
    Employees/           IEmployeeService, EmployeeService, EmployeeQuery,
                         Create/UpdateEmployeeRequest (+ validators), EmployeeResponse,
                         EmployeeListItem, PayComponentAssignmentDto
    Departments/         IDepartmentService, DepartmentService, DepartmentResponse
    PayComponents/       IPayComponentTypeService, PayComponentTypeService, requests,
                         validators, responses
    Payroll/             IPayslipService, PayslipService, PayslipResponse,
                         IPayrollPolicyService, PayrollPolicyService, requests,
                         validators, responses
    Auth/                IAuthService, AuthService, LoginRequest (+ validator), LoginResponse
    DependencyInjection.cs   AddApplication() — also registers domain calculators
  Payroll.Infrastructure/
    Persistence/         AppDbContext (IdentityDbContext + IUnitOfWork),
                         Configurations/*.cs, Migrations/, ReferenceData.cs (HasData)
    Repositories/        EmployeeRepository, DepartmentRepository,
                         PayComponentTypeRepository, PayrollPolicyRepository
    Identity/            ApplicationUser, IdentityCredentialStore, JwtTokenGenerator, JwtOptions
    Seeding/             DevelopmentDataSeeder (employees + users)
    DependencyInjection.cs   AddInfrastructure(configuration)
  Payroll.Api/
    Controllers/         AuthController, EmployeesController, DepartmentsController, PayslipController,
                         PayComponentTypesController, PayrollPoliciesController
    ErrorHandling/       ApiExceptionHandler
    Auth/                CurrentUser (ICurrentUser from claims), AuthSetup
    OpenApi/             BearerSecurityTransformer
    Program.cs
tests/
  Payroll.Domain.UnitTests/
  Payroll.Application.UnitTests/   Fakes/ (in-memory repositories, FakeUnitOfWork, FakeCurrentUser)
  Payroll.Api.UnitTests/
  Payroll.Api.IntegrationTests/    (optional phase, D47)
client/                             (Angular — §13)
docs/design/
ai-usage/
docker-compose.yml  .env.example  README.md
```

---

## 6. Domain model

### 6.1 `Employee` (aggregate root, rich entity — D16)

| Member | Type | Rule |
|---|---|---|
| `Id` | `int` | identity |
| `FullName` | `string` | required, trimmed, ≤ 100 |
| `Email` | `string` | required, **trimmed + lower-cased** in the entity, ≤ 254 |
| `JobTitle` | `string` | required, ≤ 100 |
| `DepartmentId` | `int` | > 0 (existence checked in Application) |
| `HireDate` | `DateOnly` | ≤ today (today is passed in, coming from `TimeProvider`) |
| `BaseMonthlySalary` | `decimal` | > 0 |
| `Status` | `EmployeeStatus` | `Active = 1`, `Inactive = 2` (explicit values, D23) |
| `PayComponents` | `IReadOnlyCollection<EmployeePayComponent>` | no duplicate type, amount > 0 |
| `RowVersion` | `byte[]` | concurrency token (D12) |
| `CreatedAtUtc`, `UpdatedAtUtc`, `DeletedAtUtc?` | `DateTime` | `DeletedAtUtc != null` means soft deleted (D5) |

**Methods:**

- `Employee(fullName, email, jobTitle, departmentId, hireDate, salary, status, today)`
- `UpdateDetails(..., today)`
- `SetPayComponents(IEnumerable<(int typeId, decimal amount)>)`
- `MarkDeleted(DateTime utcNow)`
- `EnsureEligibleForPayslip(PayrollMonth month)` throws `DomainException` when the employee is Inactive or `month` is before the hire month.

A violated guard throws `DomainException`. The validators (§10) catch input errors first, so a `DomainException` reaching the handler means a business rule was broken.

### 6.2 `EmployeePayComponent` (part of the Employee aggregate)

`EmployeeId`, `PayComponentTypeId`, `Amount` (`decimal`, > 0), and a navigation to `PayComponentType` (loaded for payroll).

### 6.3 `PayComponentType`

| Member | Rule |
|---|---|
| `Id`, `Name` | name required, ≤ 100, unique |
| `Kind` | `Allowance = 1`, `Deduction = 2`; **immutable after creation** (D2) |
| `IsTaxable` | may be `true` only when `Kind = Allowance` (D26); may be changed (policy change) |

Methods: `Rename(name)`, `SetTaxable(bool)`. The second throws if the type is a Deduction.

### 6.4 `Department`

`Id`, `Name`. Read-only master data (D8).

### 6.5 `PayrollPolicy` (aggregate root) + `TaxBracket`

| Member | Rule |
|---|---|
| `EffectiveFrom` | `DateOnly`, **first day of a month**, unique (D29) |
| `InsuranceRatePercent` | 0–100, up to 2 decimals |
| `InsuranceCap?` | `null` = no cap; otherwise ≥ 0 (a cap of 0 is a valid "0 insurance" cap, not "no cap") |
| `TaxBrackets` | ≥ 1; ordered by `SortOrder`; `UpperBound` strictly ascending and > 0; **exactly the last** has `UpperBound = null`; `RatePercent` 0–100 |

- **Immutability (D31):** `EnsureModifiable(PayrollMonth currentMonth)` is called before any update or delete. A policy whose `EffectiveFrom` ≤ the first day of the current month is **in effect or past**, so changing or deleting it throws `DomainException` (→ 409, see §9).
- **Why `UpperBound + Rate` (D27):** each bracket's lower bound is the previous bracket's upper bound, so gaps and overlaps cannot be represented at all.

### 6.6 Value objects

- `PayrollMonth`:
  - parses `yyyy-MM` (strict: two-digit month 01–12)
  - exposes `Year`, `Month`, `FirstDay`, `DaysInMonth`
  - is comparable
- `Payslip` / `PayslipLine`: the calculation result (§11.4).

### 6.7 Domain calculators (D38/D39)

| Interface | Implementation | Responsibility |
|---|---|---|
| `IProrationCalculator` | `CalendarDayProrationCalculator` | factor for a month given the hire date |
| `ITaxCalculator` | `ProgressiveTaxCalculator` | tax on a taxable amount for a set of brackets |
| `ISocialInsuranceCalculator` | `SocialInsuranceCalculator` | insurance from base, rate, cap and factor |
| `IPayrollCalculator` | `PayrollCalculator(ITax…, ISocialInsurance…, IProration…)` | orchestration → `Payslip` |

They are all pure (no I/O, no clock) and registered in DI by `AddApplication()`.

---

## 7. Database design

SQL Server running in Docker (D21). EF Core 10 migrations. Conventions (D23):

- money is `decimal(18,2)` for **entered** amounts
- rates are `decimal(5,2)`
- dates are `date`
- timestamps are `datetime2` in UTC
- enums are `int` with explicit values
- keys are `int` identity

```
Departments              (Id PK, Name nvarchar(100) UNIQUE)

PayComponentTypes        (Id PK, Name nvarchar(100) UNIQUE,
                          Kind int NOT NULL, IsTaxable bit NOT NULL,
                          CreatedAtUtc, UpdatedAtUtc)

Employees                (Id PK, FullName nvarchar(100), Email nvarchar(254) UNIQUE*,
                          JobTitle nvarchar(100), DepartmentId FK→Departments RESTRICT,
                          HireDate date, BaseMonthlySalary decimal(18,2),
                          Status int, RowVersion rowversion,
                          CreatedAtUtc, UpdatedAtUtc, DeletedAtUtc NULL)
                          IX(DepartmentId), IX(FullName), IX(Status)

EmployeePayComponents    (Id PK, EmployeeId FK→Employees CASCADE,
                          PayComponentTypeId FK→PayComponentTypes RESTRICT,
                          Amount decimal(18,2),
                          UNIQUE(EmployeeId, PayComponentTypeId))

PayrollPolicies          (Id PK, EffectiveFrom date UNIQUE,
                          InsuranceRatePercent decimal(5,2), InsuranceCap decimal(18,2) NULL,
                          CreatedAtUtc)

TaxBrackets              (Id PK, PayrollPolicyId FK→PayrollPolicies CASCADE,
                          SortOrder int, UpperBound decimal(18,2) NULL, RatePercent decimal(5,2),
                          UNIQUE(PayrollPolicyId, SortOrder))

AspNetUsers (+ EmployeeId int NULL FK→Employees RESTRICT), AspNetRoles, AspNetUserRoles,
AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens   (Identity, D24)
```

`*` **Email uniqueness covers all rows, including soft-deleted ones (D6).** The index is a plain unique index with no filter.

- **Soft delete (D5):** `HasQueryFilter(e => e.DeletedAtUtc == null)` on `Employee`.
  - The Employee cascade only fires on a real delete. That never happens through the API, so the pay components of soft-deleted employees are preserved.
  - "Type in use" checks query `EmployeePayComponents` directly, so they count deleted employees too (D7).
- **Computed values are never stored** in this version. If payroll runs are stored later, computed amounts use `decimal(19,6)` or higher (D32).

---

## 8. API contracts

All routes are prefixed with `/api`.

**Common rules:**

- JSON uses camelCase.
- Enums are serialized as **strings** in JSON (`JsonStringEnumConverter`). They are stored as `int` (D23).
- Every error response is `application/problem+json` (§9).
- **Roles:** `Hr` has full access. `Employee` can only log in and read their own payslip.

### 8.1 Auth

| Method & route | Access | Success | Errors |
|---|---|---|---|
| `POST /auth/login` `{ email, password }` | anonymous | `200 { accessToken, expiresAtUtc, role, employeeId? }` | 400, 401 (generic "Invalid credentials", also for an Employee user whose employee is Inactive or deleted, D25) |

### 8.2 Employees (`Hr`)

| Method & route | Success | Errors |
|---|---|---|
| `GET /employees?search=&departmentId=&status=&sortBy=&sortDir=&page=&pageSize=` | `200 PagedResult<EmployeeListItem>` | 400 |
| `GET /employees/{id}` | `200 EmployeeResponse` | 404 |
| `POST /employees` | `201` + `Location` + `EmployeeResponse` | 400, 409 (email, with `errors.email`) |
| `PUT /employees/{id}` (body includes `rowVersion`) | `200 EmployeeResponse` (new `rowVersion`) | 400, 404, 409 (email / concurrency) |
| `DELETE /employees/{id}` (soft) | `204` | 404 |

**List rules (D22):**

- `search` is a case-insensitive *contains* on full name **or** email.
- `sortBy` must be one of `name | hireDate | salary` (default `name`). `sortDir` is `asc | desc`.
- `page` ≥ 1. `pageSize` is 1–100, default 10.
- Invalid values return 400.

```jsonc
// EmployeeResponse
{ "id": 7, "fullName": "Ali Hassan", "email": "ali@demo.local", "jobTitle": "Developer",
  "departmentId": 1, "departmentName": "Engineering", "hireDate": "2024-06-01",
  "baseMonthlySalary": 17000.00, "status": "Active",
  "payComponents": [ { "payComponentTypeId": 1, "name": "Transport", "kind": "Allowance",
                       "isTaxable": false, "amount": 1000.00 } ],
  "rowVersion": "AAAAAAAAB9E=" }

// CreateEmployeeRequest (UpdateEmployeeRequest = same + "rowVersion")
{ "fullName": "...", "email": "...", "jobTitle": "...", "departmentId": 1,
  "hireDate": "2024-06-01", "baseMonthlySalary": 17000.00, "status": "Active",
  "payComponents": [ { "payComponentTypeId": 1, "amount": 1000.00 } ] }

// PagedResult<EmployeeListItem>
{ "items": [ { "id", "fullName", "email", "jobTitle", "departmentName",
               "hireDate", "baseMonthlySalary", "status" } ],
  "page": 1, "pageSize": 10, "totalCount": 37 }
```

### 8.3 Payslip

| Method & route | Access | Success | Errors |
|---|---|---|---|
| `GET /employees/{id}/payslip?month=yyyy-MM` | `Hr` (any employee); `Employee` (only when `id` = own `employee_id`) | `200 PayslipResponse` | 400 (month), 403, 404, 422 (inactive / before hire month / no effective policy) |

```jsonc
// PayslipResponse — every amount at FULL precision (D32/D33)
{ "employeeId": 7, "employeeName": "Ali Hassan", "month": "2026-03",
  "policyId": 1, "policyEffectiveFrom": "2025-01-01",
  "prorationFactor": 1, "daysPaid": 31, "daysInMonth": 31,
  "baseSalary": 17000,
  "allowances": [ { "name": "Transport", "amount": 1000, "isTaxable": false },
                  { "name": "Housing",   "amount": 2000, "isTaxable": true } ],
  "grossPay": 20000, "taxableIncome": 19000, "incomeTax": 1800,
  "socialInsurance": 1500,
  "deductions": [ { "name": "Loan Repayment", "amount": 750 } ],
  "otherDeductionsTotal": 750,
  "netPay": 15950, "isNetNegative": false }
```

### 8.4 Reference & admin data

| Method & route | Access | Notes |
|---|---|---|
| `GET /departments` | authenticated | read-only (D8) |
| `GET /pay-component-types`, `GET /pay-component-types/{id}` | `Hr` | |
| `POST /pay-component-types` `{ name, kind, isTaxable }` | `Hr` | 400 if `isTaxable` is true for a Deduction; 409 on a duplicate name |
| `PUT /pay-component-types/{id}` `{ name, isTaxable }` | `Hr` | no `kind` field (S5) |
| `DELETE /pay-component-types/{id}` | `Hr` | 409 if any employee record references it, including deleted ones (D7) |
| `GET /payroll-policies`, `GET /payroll-policies/{id}` | `Hr` | |
| `GET /payroll-policies/effective?month=yyyy-MM` | `Hr` | 422 if none |
| `POST /payroll-policies` `{ effectiveFrom, insuranceRatePercent, insuranceCap?, taxBrackets: [ { upperBound?, ratePercent } ] }` | `Hr` | `effectiveFrom` must be the 1st of a month **after** the current month |
| `PUT /payroll-policies/{id}`, `DELETE /payroll-policies/{id}` | `Hr` | only for future policies, otherwise 409 (D31) |
| `GET /health` | anonymous | outside `/api` (§16) |

---

## 9. Error handling

There is a single `ApiExceptionHandler : IExceptionHandler`, registered with `AddProblemDetails()` (D17). There are **no try/catch blocks in controllers or services** for flow control.

| Source | Status | Problem Details |
|---|---|---|
| FluentValidation `ValidationException` | 400 | `title: "One or more validation errors occurred."`, `errors: { "fullName": [...], "payComponents[0].amount": [...] }` (camelCase JSON paths) |
| Model binding / malformed JSON (`[ApiController]`) | 400 | same shape |
| `NotFoundException` | 404 | `detail: "Employee 42 was not found."` |
| `ConflictException` (duplicate email or name, type in use, policy already in effect) | 409 | optional `errors` for field mapping, e.g. `errors.email` (D42) |
| `ConflictException` produced from a concurrency failure (S6) | 409 | `detail: "The record was modified by another user."` |
| `DomainException` (business rule) | 422 | `detail` explains the rule |
| Authentication / authorization | 401 / 403 | produced by the auth middleware, plus `ForbiddenException` from S7 |
| Anything else | 500 | generic message only. **The full exception is logged with `traceId`; no internals in the body.** |

Every response carries `traceId` (from `Activity.Current` or `HttpContext.TraceIdentifier`).

---

## 10. Validation strategy

There are two levels with different purposes (D15, D16):

- **Input validation** (FluentValidation, in Application):
  - The service calls it explicitly (no auto-validation magic).
  - It returns **all** field errors at once, so the UI can show them.
- **Domain invariants** (guards in entities and the policy aggregate):
  - They are the last line of defence.
  - The small overlap with the validators is intentional: the validators serve UX, the guards serve correctness.

| Field / rule | Validator (400) | Domain guard |
|---|---|---|
| Full name required, ≤ 100 | ✓ | ✓ |
| Email required, valid format, ≤ 254 | ✓ | required only |
| Email unique (across all rows, D6) | service check → 409 + `errors.email`; the DB unique index is the final guarantee (S6) | — |
| Job title required, ≤ 100 | ✓ | ✓ |
| Department required and existing | ✓ (> 0) + service existence check → 400 field error | > 0 |
| Hire date required, ≤ today (`TimeProvider`) | ✓ | ✓ |
| Base salary > 0, at most 2 decimals | ✓ (`PrecisionScale(18,2)`) | > 0 |
| Status is a defined enum value | ✓ | — |
| Pay components: known type, amount > 0 with at most 2 decimals, no duplicate type | ✓ + service existence check | duplicates, > 0 |
| `month` query matches `yyyy-MM` | ✓ (`PayrollMonth.TryParse`) | — |
| Paging / sort whitelist | ✓ | — |
| Policy: `EffectiveFrom` is the 1st of a future month; brackets valid; rates 0–100; cap ≥ 0 | ✓ | ✓ (aggregate) |
| Pay component type: name required ≤ 100; `IsTaxable` only for an Allowance | ✓ | ✓ |
| Login: email and password required | ✓ | — |

The Angular forms mirror the validator rules (§13.5). The server never trusts the client.

---

## 11. Payroll calculation design

### 11.1 Formula (per employee, per `PayrollMonth`)

```
f            = proration factor                                    (§11.2)
base         = BaseMonthlySalary × f
allowance_i  = amount_i × f
gross        = base + Σ allowance_i
taxable      = base + Σ allowance_i   where type.IsTaxable          (D26)
incomeTax    = Σ_k  max(0, min(taxable, upper_k) − lower_k) × rate_k / 100
               lower_0 = 0, lower_k = upper_(k−1), upper_last = +∞   (D27)
insurance    = base × InsuranceRatePercent / 100
               if InsuranceCap ≠ null: min(insurance, InsuranceCap × f)   (D36)
deductions   = Σ deduction amounts            (NOT prorated, D36)
net          = gross − incomeTax − insurance − deductions
isNetNegative = net < 0                                             (D37)
```

- **Social insurance is a percentage of the base salary only** (PDF), never of allowances. It is **not** deducted from the tax base (D26).
- **The brackets are monthly** and are applied to the (prorated) taxable income. The brackets themselves are not prorated (D35).
- **The policy used** is the one with the latest `EffectiveFrom` ≤ `month.FirstDay`. If no later policy exists, the latest one stays in force indefinitely (D29).

### 11.2 Proration (D35/D36)

```
if month == hire month:  daysPaid = DaysInMonth − HireDate.Day + 1   (the hire day counts)
                         f = daysPaid / DaysInMonth                  (full precision, no rounding)
else:                    f = 1
```

### 11.3 Eligibility & errors (S11)

| Order | Check | Result |
|---|---|---|
| 1 | `month` is valid `yyyy-MM` | else 400 |
| 2 | Caller is `Hr`, or `Employee` with `employee_id == id` | else 403 |
| 3 | Employee exists (not soft-deleted) | else 404 |
| 4 | Employee is Active | else 422 "Employee is inactive." |
| 5 | `month` ≥ hire month | else 422 "Employee was hired in 2026-03." |
| 6 | A policy is effective for `month` | else 422 "No payroll policy is effective for 2024-12." |
| — | Future months are **allowed** (preview with the policy effective then, D34) | 200 |

### 11.4 Precision & rounding (D32/D33)

- `decimal` is used everywhere, never `double`. `0.1 + 0.2 != 0.3` in binary floating point, which is unacceptable for money. `decimal` holds 28–29 significant digits.
- **The Domain never rounds.** Every computed value keeps all its decimals. Entered values (salary, amounts, rates) are limited to 2 decimals because they are inputs, not computed values.
- **The API returns full precision.** The **Angular `MoneyPipe` rounds for display only**: 2 decimals, half away from zero (`Intl.NumberFormat` default `halfExpand`).
- **Documented consequence:** because each line is rounded independently for display, the displayed lines can differ from the displayed net by 0.01. The net shown is always the true net rounded once.

### 11.5 Worked examples (become unit tests)

**Policy v1:**

- Tax brackets: 0% up to 5,000; 10% up to 15,000; 20% above that.
- Social insurance: 11%, capped at 1,500.

**Employee:**

- Base salary: 17,000.
- Transport allowance: 1,000 (`IsTaxable = false`).
- Housing allowance: 2,000 (taxable).
- Loan repayment deduction: 750.

| Line | Full month (`f = 1`) | Hired 2026-03-20 (`f = 12/31`) |
|---|---|---|
| Base | 17,000 | 6,580.645161290322580645161290… |
| Gross | 20,000 | 7,741.935483870967741935483871 |
| Taxable | 19,000 | 7,354.838709677419354838709677 |
| Income tax | 0 + 1,000 + 800 = **1,800** | 0 + 235.4838709677419354838709677 |
| Insurance | min(1,870, 1,500) = **1,500** | min(723.87…, 580.6451612903225806451612902) |
| Deductions | 750 | 750 |
| **Net** | **15,950** | **6,175.806451612903225806451613** |
| Displayed net | 15,950.00 | 6,175.81 |

The values were verified with arbitrary-precision decimal arithmetic.

### 11.6 Extensibility (OCP)

| Change | Where |
|---|---|
| New allowance / deduction type | data: `PayComponentType` |
| Exempt an allowance from tax | data: `IsTaxable` |
| New brackets, insurance rate or cap | data: a new `PayrollPolicy` with a future `EffectiveFrom` |
| Proration by working days | new `IProrationCalculator` implementation; consumers unchanged |
| A new earning kind (e.g. overtime from an Attendance module) | new domain calculator + one extra input to `PayrollCalculator` |

---

## 12. Authentication & authorization

- **ASP.NET Core Identity (D24):**
  - `ApplicationUser : IdentityUser<int>` with `int? EmployeeId`, located in **Infrastructure** so the Domain stays free of Identity.
  - `AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>, IUnitOfWork`.
- **Roles:** `Hr`, `Employee` as constants in `Roles` (Application). A user with the `Employee` role must have an `EmployeeId`.
- **Login (`IAuthService` → `AuthService` in Application, S12):**
  - Checks the password through `IUserCredentialStore` (implemented with `UserManager`).
  - For `Employee` users, requires the linked employee to exist (not soft-deleted) and be Active.
  - Any failure returns the **same** 401 "Invalid credentials" (D25).
- **JWT (`ITokenGenerator` → `JwtTokenGenerator`):**
  - Algorithm: HS256.
  - Signing key, issuer and audience come from configuration (`.env`); the key is at least 32 bytes.
  - Lifetime: 60 minutes, no refresh tokens.
  - Claims: `sub`, `email`, `role`, and `employee_id` when present.
- **Server-side enforcement:**
  - A fallback policy requires authentication everywhere except `POST /api/auth/login` and `/health`.
  - The `HrOnly` policy is applied to the Employees, PayComponentTypes and PayrollPolicies controllers.
  - The payslip ownership rule is enforced in `PayslipService` via `ICurrentUser` (S7) and returns 403.
- **Known limitation:** a token issued before an employee was deactivated stays valid until it expires (at most 60 minutes).
- **Client storage (D45):**
  - The token is kept in `sessionStorage` plus an in-memory signal. It survives a refresh and is cleared when the tab closes.
  - Angular's built-in sanitization is relied on, and `innerHTML` is never used.
  - The HttpOnly-cookie alternative is documented as a next step.

---

## 13. Angular architecture

### 13.1 Structure (D40) — standalone components, lazy feature routes

```
client/src/
  styles/            _tokens.scss (Bootstrap variable overrides + CSS custom properties),
                     styles.scss (selective Bootstrap SCSS imports, global form/table tweaks)
  app/
    core/
      auth/          auth.service.ts (currentUser signal, sessionStorage),
                     auth.guard.ts, role.guard.ts, auth.interceptor.ts, login.page.ts
      http/          problem-details.interceptor.ts, api-error.ts (ApiError + toApiError)
      notifications/ notification.service.ts, toast.component.ts
      layout/        shell.component.ts (nav + router-outlet)
    shared/
      pipes/         money.pipe.ts
      forms/         validators.ts (notInFuture, noDuplicateType), apply-server-errors.ts
      ui/            field-errors.component.ts, pagination.component.ts,
                     confirm-dialog.component.ts (native <dialog>)
    features/
      employees/
        data/        employees-api.service.ts, reference-data-api.service.ts, employee.models.ts
        pages/       employee-list.page.ts, employee-form.page.ts
        ui/          pay-components-editor.component.ts (FormArray)
        employees.routes.ts
      payslip/
        data/        payslip-api.service.ts, payslip.models.ts
        pages/       payslip.page.ts (used by both HR and Employee routes)
        payslip.routes.ts
    app.routes.ts, app.config.ts
  proxy.conf.json    /api → backend (S8)
```

### 13.2 Routing (D44)

| Route | Guard | Page |
|---|---|---|
| `/login` | — | Login |
| `/employees` | auth + role `Hr` | List (search, department and status filters, sort, pagination; all filters live in query params) |
| `/employees/new`, `/employees/:id/edit` | auth + role `Hr` | Single create/edit form |
| `/employees/:id/payslip?month=` | auth + role `Hr` | Payslip |
| `/my-payslip?month=` | auth + role `Employee` | Payslip for the `employee_id` claim |
| `''` | — | redirects by role |

The guards exist for UX only. **The server is the authority.**

### 13.3 State (D41)

- Thin API services return typed `Observable`s.
- Pages hold `signal`s for data, loading and error.
- List filters are synchronised with the URL.
- The only shared state is `AuthService.currentUser`.
- No store library.

### 13.4 Errors (D42)

- **`problemDetailsInterceptor`** converts every `HttpErrorResponse` into a typed `ApiError { status, title, detail, fieldErrors }`.
  - 401 → logout and redirect to `/login`.
  - 0 / 5xx → `NotificationService.error(...)`.
- **Pages:**
  - 400, and a 409 that carries field errors, go through `applyServerErrors(form, apiError)`, which sets a `server` error on each control.
  - Other 409 / 422 errors are shown as a page or form banner.
- **`<app-field-errors [control]>`** renders client and server messages in the same way.

### 13.5 Forms (D44)

- **Typed reactive forms** (`fb.nonNullable`), with validators that mirror §10:
  - `required`, `maxLength(100)`, `email`
  - `notInFuture`
  - `min(0.01)` for the salary
  - `noDuplicateType` on the `payComponents` FormArray
- **Edit:** loads the employee, keeps its `rowVersion`, and sends it back on save.
- **Delete:** triggered from the list through a confirm dialog.
- **Month picker:** `input type="month"`.
- **Negative net:** shows a warning banner (D37).
- **Money:** every amount is rendered through `MoneyPipe` (D33).

### 13.6 Styling (D43)

- Bootstrap is consumed **from SCSS source**:
  - `_tokens.scss` overrides variables (`$primary`, fonts, spacing).
  - `styles.scss` imports only functions/variables/mixins/root/reboot/grid/forms/buttons/tables/alerts/utilities.
- No `bootstrap.js`, no ng-bootstrap.
- Hand-written component SCSS is used for the payslip, toast, dialog and layout.

---

## 14. Testing strategy

### 14.1 Projects & doubles (D46)

| Project | Doubles | Scope |
|---|---|---|
| `Payroll.Domain.UnitTests` | none (calculators built with real implementations) | all domain rules |
| `Payroll.Application.UnitTests` | hand-written in-memory fakes (they behave like the DB: query filter, email normalisation, row versions, "in use" checks) + `FakeTimeProvider` + `FakeCurrentUser` | services, validators, Mapster config |
| `Payroll.Api.UnitTests` | NSubstitute substitutes for `I*Service` | controller results, exception → status mapping |
| `Payroll.Api.IntegrationTests` *(optional)* | Testcontainers SQL Server + `WebApplicationFactory` | unique index, rowversion, soft-delete filter, auth end-to-end |
| `client` (Angular CLI default runner) | none | `notInFuture`, `noDuplicateType`, `MoneyPipe`, `toApiError`, `applyServerErrors` (no DOM) |

**Assertions use xUnit `Assert`.** Tests check **outcomes and state, not calls** (no `Received()` on repositories). Unit tests use no database and no network (PDF).

### 14.2 Test catalogue (approved)

**Domain:**

- **Tax:**
  - taxable 0
  - exactly on a boundary (5,000 → 0)
  - boundary + 0.01
  - spans all brackets
  - single open bracket
  - long fractional inputs (no rounding)
- **Insurance:**
  - no cap
  - below the cap
  - exactly at the cap
  - above the cap
  - cap with f < 1
  - cap = 0
- **Proration:**
  - hired on the 1st → 1
  - hired on the 20th of a 31-day month → 12/31
  - hired on the last day
  - February 28 and 29
  - a later month → 1
- **PayrollCalculator:**
  - both §11.5 examples
  - zero allowances
  - exempt vs taxable allowance
  - negative net → flag
- **PayrollPolicy:**
  - non-ascending brackets
  - last bracket not open
  - `EffectiveFrom` not the 1st of a month
  - modifying or deleting an in-effect policy
- **Employee:**
  - guards
  - duplicate pay component type
  - email normalisation
  - eligibility (Inactive, month before hire)
- **PayrollMonth:** parses `2026-03`; rejects `2026-3`, `2026-13`, `abc`.

**Application:**

- **Validators:** every PDF rule plus boundaries:
  - name of exactly 100 characters vs 101
  - hire date today vs tomorrow
  - salary with 3 decimals
  - unknown pay component type
  - paging and sort whitelist
- **EmployeeService:**
  - duplicate email → Conflict (including an email held by a deleted employee)
  - a deleted employee disappears from get and list
  - update of a deleted employee → NotFound
- **PayslipService:**
  - Inactive → 422
  - before hire → 422
  - no policy → 422
  - the effective policy is chosen correctly
  - future month allowed
  - Employee requesting another employee's payslip → Forbidden
- **PayComponentTypeService:**
  - delete of a type in use (also by a deleted employee) → Conflict
  - `IsTaxable` on a Deduction → rejected
- **PayrollPolicyService:** create for the current or a past month → rejected; update or delete of an in-effect policy → Conflict.
- **AuthService** (with a fake `IUserCredentialStore`, S12):
  - wrong password → 401
  - Employee user whose employee is inactive or deleted → 401
  - HR user → token issued
  - Employee user → token contains `employee_id`
- **Mapster:** `TypeAdapterConfig` compiles and validates (D20).

**Api:**

- HR → 200 payslip.
- `ForbiddenException` → 403.
- Each exception type → its expected status (D17).

---

## 15. Configuration, secrets & seed data

- **Nothing secret is committed.**
  - `.env` is gitignored; `.env.example` is committed with placeholder values.
  - ASP.NET reads environment variables (`ConnectionStrings__Default`, `Jwt__SigningKey`, …).
  - `dotnet user-secrets` is an alternative for running outside Docker.
- **`.env.example` keys:**
  - `MSSQL_SA_PASSWORD`
  - `ConnectionStrings__Default`
  - `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience`
  - `Seed__HrPassword`, `Seed__EmployeePassword`
- **`appsettings.json`:**
  - holds only non-secret defaults (logging, `Jwt:LifetimeMinutes = 60`)
  - **no payroll rules**, because they live in the database (D27/D30)
- **Reference data (`HasData`, part of the migrations, S10):**
  - Departments: Engineering, Finance, Human Resources, Operations, Sales.
  - Pay component types:
    - Transport (Allowance, non-taxable)
    - Housing (Allowance, taxable)
    - Meal (Allowance, taxable)
    - Loan Repayment (Deduction)
    - Medical Plan (Deduction)
  - Payroll policies:
    - **v1**, `EffectiveFrom 2025-01-01`: brackets 5,000 @ 0%, 15,000 @ 10%, open @ 20%; insurance 11%, cap 1,500.
    - **v2**, `EffectiveFrom 2027-01-01`: brackets 6,000 @ 0%, 18,000 @ 10%, open @ 22%; insurance 12%, cap 1,800. This one exists to demonstrate effective dating (D29) and future previews (D34).
- **Demo data (`DevelopmentDataSeeder`, Development only, idempotent):**
  - about 8 employees, including one Inactive, one hired in the current month (proration) and one whose deductions exceed earnings in the hire month (negative net)
  - user `hr@payroll.local` (`Hr`)
  - two `Employee` users linked to employees
- **Development startup** applies migrations and runs the seeder automatically (D21). Other environments apply migrations explicitly.

---

## 16. Observability

- **`GET /health`** (anonymous) uses `AddHealthChecks().AddDbContextCheck<AppDbContext>()`. It returns `Healthy` or `Unhealthy` without leaking details.
- **Structured logging** uses the built-in JSON console formatter (`AddJsonConsole`), with no extra library. Unhandled exceptions are logged once, in `ApiExceptionHandler`, together with the `traceId` that is also returned to the client.

---

## 17. Dependencies and their justification

| Package | Project | Why |
|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer`, `.Design` | Infrastructure | ORM + migrations (stack choice) |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Infrastructure | users, roles and password hashing without writing our own crypto (D24) |
| `Microsoft.IdentityModel.JsonWebTokens` | Infrastructure | issuing JWTs |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Api | validating JWTs |
| `FluentValidation` (+ `DependencyInjectionExtensions`) | Application | complete field-level errors; rules that need `TimeProvider`; validators testable in isolation (D15) |
| `Mapster` | Application | entity → response mapping as modules grow; the configuration is validated by a unit test (D20) |
| `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore.SwaggerUI` | Api | OpenAPI document + UI (PDF §5, S9) |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | Api | DB health check |
| `xunit` (v3), `Microsoft.NET.Test.Sdk` | tests | test framework (stack choice) |
| `NSubstitute` | Api.UnitTests | substitutes for service interfaces in controller tests (D46) |
| `Microsoft.Extensions.TimeProvider.Testing` | tests | deterministic "today" (`FakeTimeProvider`) |
| `Testcontainers.MsSql`, `Microsoft.AspNetCore.Mvc.Testing` | IntegrationTests *(optional)* | real SQL Server in tests (D47) |
| `bootstrap` (SCSS only) | client | consistent, accessible forms and tables (D43) |

**Deliberately not used:**

- **AutoMapper and MediatR:** commercial licences, and no need for them here.
- **FluentAssertions:** commercial licence from v8.
- **Moq**
- **NgRx:** no shared state that justifies it.
- **EF Core InMemory provider:** gives false confidence (no constraints, no rowversion).

---

## 18. Implementation phases

Each phase ends in a **working, buildable state** that Kamal can commit (D10, D50). The estimates add up to about **12.25 h committed** (D49).

> **D51 update:** tests are written **TDD inside each phase**. The separate "Backend unit tests" phase (4 below) is dissolved: its time and catalogue move into Phases 2, 3 and 5, and the frontend logic tests move into the Angular phase. The authoritative phase list and numbering is `docs/plans/00-master-plan.md`. The table below keeps the original numbering for traceability.

| # | Phase | Est. | Acceptance criteria |
|---|---|---|---|
| 1 | **Setup & skeleton** | 0.75 h | The solution and its 4 projects + test projects build. Project references enforce §4. `docker compose up -d db` starts SQL Server. `.env.example` is present. The API starts and serves Swagger. `ApiExceptionHandler` + `AddProblemDetails` are wired. The Angular app builds with Bootstrap SCSS and the shell layout. The dev proxy works. |
| 2 | **Employee CRUD (backend)** | 2.25 h | Domain `Employee`, `EmployeePayComponent`, `Department` and `PayComponentType` (entity). Validators, `EmployeeService`, the repositories, the UoW with exception translation (S6), migrations + reference seed + demo seeder. `EmployeesController` and `DepartmentsController` match §8. Soft delete, reserved email and rowversion all behave as specified when checked through Swagger. |
| 3 | **Payroll (backend)** | 1.75 h | `PayrollPolicy` aggregate, the 4 calculators, `PayrollMonth`, `PayslipService` with the S11 ordering, the policy repository + seed v1/v2, the payslip endpoint and the policy GET endpoints. The §11.5 examples return the documented values. |
| 4 | **Backend unit tests** | 1.75 h | The Domain and Application parts of the §14.2 catalogue are implemented and green. No DB and no network are used. |
| 5 | **Angular** | 2.0 h | Core and shared pieces, employee list (filters in the URL, pagination, sorting, delete with confirmation), the single create/edit form with the pay-components FormArray, and the payslip page. Client and server errors are displayed as in §13.4, and money through `MoneyPipe`. |
| 6 | **Admin APIs** | 0.75 h | PayComponentType CRUD and PayrollPolicy write API (future-only rule), with their tests. |
| 7 | **Health + logging** | 0.25 h | `/health` reports the DB status; logs are JSON; a 500 is logged with its `traceId`. |
| 8 | **Identity + JWT** | 1.75 h | Identity tables migration, roles and seeded users, login, policies, `ICurrentUser`, the ownership rule, and the inactive/deleted login rule. Angular gets the login page, guards, auth interceptor and `/my-payslip`. `Payroll.Api.UnitTests` and the auth-related Application tests are green. |
| 9 | **Frontend tests + docs** | 1.0 h | Angular logic tests are green. The README covers setup in a few commands, architecture and reasoning, assumptions, limitations, next steps, and *Scope & time*. `/ai-usage` contains the exported transcript and the list of skills and tools. |
| 10 | *Optional:* integration tests | 1.0 h | The Testcontainers suite covers the unique index, rowversion, the soft-delete filter and auth end-to-end. |
| 11 | *Optional:* full Docker + CI | 1.0 h | `docker compose up` runs db + api + client. A GitHub Actions workflow builds and tests the backend and the client. |

**Overall acceptance criteria:**

- **Build & tests:**
  - The solution and the client build cleanly.
  - All tests pass.
- **Real implementation:**
  - SQL Server persistence through EF Core migrations, with no fake implementations.
  - Every PDF field rule is enforced on the server and mirrored in the UI.
  - Errors are always Problem Details.
- **Payroll and authorization:**
  - Payroll matches §11, including the documented rounding rule.
  - Authorization is enforced on the server.
- **Code hygiene:**
  - No secrets are committed.
  - No unexplained TODOs in mandatory functionality.
- **Explainability:** Kamal can explain every line, and the README records which parts were AI-generated.

---

## 19. Assumptions & known limitations

**Assumptions (README):**

- **Tax:**
  - Tax brackets are monthly.
  - The tax base is the base salary plus taxable allowances. Social insurance is not deducted from the tax base.
- **Social insurance:** it is a percentage of the base salary only, with an optional cap.
- **Proration and deductions:**
  - The hire month is prorated by calendar days, counting the hire day.
  - The insurance cap is prorated; deductions are not.
- **Payslip calculation:**
  - Payslips are computed on demand and not stored.
  - They use the employee's **current** salary and pay components, together with the payroll policy effective for the requested month. A past payslip therefore mixes historical policy with current salary data.
  - Future months are allowed as previews.
- **Negative net:** it is returned as is, with `isNetNegative`.

**Known limitations / next steps:**

- **No history:** there is no salary history and payroll runs are not stored.
- **Missing admin screens:** pay component types and payroll policies have an API but no UI. Departments are read-only.
- **Auth:** there are no refresh tokens, and tokens are not revoked when an employee is deactivated (60-minute window).
- **Soft delete:** there is no restore endpoint.
- **Proration:** only for the hire month (no termination-month proration, because there is no termination date).
- **Precision in JavaScript clients:** JS clients parse JSON numbers as IEEE-754 doubles. That is irrelevant for 2-decimal display, and other clients receive full precision.
- **Display rounding:** displayed lines may differ from the displayed net by 0.01 (§11.4).

---

## 20. Decision log

| ID | Decision | Status |
|---|---|---|
| D1 | Discussion in Arabic; all files in English; one question at a time with options | Active |
| D2 | `PayComponentType` (Name, Kind, IsTaxable) + `EmployeePayComponent` (Amount); unique (EmployeeId, TypeId); FK type Restrict, FK employee Cascade; `Kind` immutable | Active |
| D3 | Full CRUD API for `PayComponentType` + seed | Active |
| D4 | ~~Simple admin screen for types~~ → **no screen; API + Swagger only** | Revised |
| D5 | Employee soft delete (`DeletedAtUtc` + query filter); Inactive ≠ Deleted | Active |
| D6 | Email stays reserved after soft delete (unique over all rows) | Active |
| D7 | Deleting a type referenced by any employee record (including deleted) → 409 | Active |
| D8 | Department master table, seeded, read-only (`GET /departments`) | Active |
| D9 | JWT with two roles (`Hr`, `Employee`) | Active |
| D10 | Phase order; each phase ends in a working state; Docker/CI only if time remains | Active (phases detailed in §18) |
| D11 | .NET 10 LTS + EF Core 10 | Active |
| D12 | Optimistic concurrency with `rowversion` → 409 | Active |
| D13 | 4 projects: Domain / Application / Infrastructure / Api | Active |
| D14 | Repository per aggregate + `IUnitOfWork` (implemented by the DbContext); justified by SRP (data access vs transaction boundary) | Active |
| D15 | FluentValidation in Application, invoked explicitly | Active |
| D16 | Rich entities with guards and `DomainException` | Active |
| D17 | `IExceptionHandler` + `AddProblemDetails`; mapping table in §9 | Active |
| D18 | Controllers | Active |
| D19 | Interface per application service | Active (see D39) |
| D20 | Mapster, entity → response only; config validated by a test | Active |
| D21 | SQL Server in Docker; secrets in `.env`; migrate + seed automatically in Development | Active |
| D22 | Offset paging + search + filters + whitelisted sort | Active |
| D23 | Enums stored as `int` with explicit values; DB conventions in §7 | Active (`UserRole` enum dropped by D24) |
| D24 | ASP.NET Core Identity with `ApplicationUser.EmployeeId` in Infrastructure | Active |
| D25 | Employee user whose employee is Inactive or deleted cannot log in (generic 401) | Active |
| D26 | ~~Tax on full gross~~ → **`IsTaxable` per allowance type** | Revised |
| D27 | Tax brackets stored in the database as `UpperBound + RatePercent` | Active (shaped by D30) |
| D28 | ~~Separate insurance settings table~~ | Superseded by D30 |
| D29 | Effective dating: the latest `EffectiveFrom` ≤ month start applies; with no later version it stays in force | Active |
| D30 | Single `PayrollPolicy` aggregate (insurance rate + cap + brackets) | Active |
| D31 | Policies append-only; only future policies can be edited or deleted; no UI | Active |
| D32 | **Standing rule:** computed values keep full precision; rounding is for display only | Active |
| D33 | API returns full precision; Angular `MoneyPipe` rounds for display | Active |
| D34 | Future months allowed as previews | Active |
| D35 | Hire-month proration by calendar days | Active |
| D36 | Prorate base, allowances and insurance cap; deductions in full | Active |
| D37 | Negative net returned with `isNetNegative` + UI warning | Active |
| D38 | Calculators behind interfaces (SOLID/DIP) | Active |
| D39 | Interfaces for behaviour classes only; the principle is re-worded (§3) | Active |
| D40 | Angular standalone + core/shared/features + lazy routes | Active |
| D41 | Thin API services + signals in pages + filters in the URL | Active |
| D42 | Problem-details interceptor + `ApiError` + `applyServerErrors` + `<app-field-errors>`; 409 email carries `errors.email` | Active |
| D43 | Bootstrap from SCSS source, selective imports, no JS | Active |
| D44 | Single create/edit form, typed reactive forms, role guards, routes in §13.2 | Active |
| D45 | JWT in `sessionStorage` | Active |
| D46 | Hand-written fakes; NSubstitute only for controller tests; outcome-based assertions | Active |
| D47 | Testcontainers integration tests as an optional phase | Active |
| D48 | Frontend logic tests only (no DOM) | Active |
| D49 | Accept about 12.25 h committed scope; document it transparently in the README | Active |
| D50 | Claude never commits or pushes; Kamal owns Git; Claude only ran `git init` | Active |
| D51 | TDD inside every phase; the separate backend-tests phase is dissolved (updates D10 / §18) | Active |
