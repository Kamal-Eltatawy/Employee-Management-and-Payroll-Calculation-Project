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
| Node (Kamal) | v26.13.0 (npm 11: install-scripts warnings for esbuild/lmdb/@parcel/watcher/msgpackr-extract are harmless; the build works) |
| Docker (Kamal) | 29.8.0, Compose v5.5.1 |
| Angular CLI / Bootstrap | 22.2.6 / 5.3.8 |
| Pinned NuGet (`Directory.Packages.props`) | EF/ASP.NET 10.0.14, FluentValidation 12.2.0, Mapster 10.0.14, xunit.v3 4.0.3, NSubstitute 6.2.0, TimeProvider.Testing 10.11.0, JsonWebTokens 8.23.0, SwaggerUI 10.3.1, Testcontainers.MsSql 4.16.0 (`Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio` entries are unused) |
| dotnet-ef | 10.0.14 (local tool, `dotnet-tools.json` at the repo root) |

## Status

| Phase (master plan numbering) | Status |
|---|---|
| 1 Setup & skeleton | **done** (waiting for Kamal's last commit) |
| 2 Employee CRUD backend + tests | **done** (112 tests; Swagger verified). Note: a local Windows SQL Server owns 1433, so compose maps **host 14333** → 1433 and `.env` uses `localhost,14333` |
| 3 Payroll backend + tests (+ D53 tax schemes) | **done** (Swagger: spec example net 15,950; tax schemes listed) |
| 4 Angular client + logic tests | **done** (Kamal checked the UI; 23 client tests incl. interceptor) |
| 5 Admin APIs (types CRUD + policy writes) + tests | **done** |
| 6 Health + JSON logging | **done** |
| 7 Identity + JWT + refresh tokens (D55) + tests | done (migration `AddIdentity` created; refresh cookie verified in the browser: HttpOnly, Secure, SameSite=Strict, Path=/api/auth) |
| 8 README + `/ai-usage` | written; **Kamal to review, export session 2 into `ai-usage/`, commit** |
| 9 *(optional)* integration tests | **done** (Kamal committed) |
| 10 *(optional)* Docker + CI | full compose written (`src/Payroll.Api/Dockerfile`, `client/Dockerfile` + `nginx.conf`, `.dockerignore`s, compose services `api` 5081 and `web` 8080). **CI dropped by Kamal.** Waiting for `docker compose up --build` check |

### Phase 1 detail (plan: `docs/plans/01-setup-and-skeleton.md`; code for Tasks 4–6 is already in that file)

| Task | Status |
|---|---|
| 1 Toolchain | done |
| 2 Package pins | done (Kamal to commit) |
| 3 Skeleton | done, build green (Kamal to commit) |
| 4 Error handling | **done**: build green, 22/22 tests pass |
| 5 Host / `.env` / compose | **done**: db container up, API on :5080, unknown route → 404 problem+json with `traceId` |
| 6 Angular scaffold | **done**: `ng serve` runs on Kamal's machine (Node 26.13.0) |

## Next action

1. **Kamal**: `dotnet ef migrations add AddRefreshTokenRotation ...`, `dotnet build`, `dotnet test --solution Payroll.slnx` (unit + integration, Docker running), run API + `npm start` + `npm run start:ar`: log in once, switch language (no new login), log in as another user in one language and check the other follows. Then `.\start.ps1`. Commit.

(All previous commits are done. The git history is clean up to `a1284f3`.)

## Log

- **2026-11-09:** spec approved (D1–D51); master plan + Phase 1 plan written; `git init` done; continuity files created.
- **2026-11-10:**
  - P1 Tasks 1–3 done.
  - `dotnet-tools.json` is at the repo root.
  - xUnit v3 4.x requires Microsoft.Testing.Platform on the .NET 10 SDK: `global.json` got `"test": {"runner": "Microsoft.Testing.Platform"}`, and `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` were removed from the test project.
  - D52 (fast mode, full scope) adopted because of the deadline.
  - P1 Task 6: Angular 22.2.6 scaffold copied into `client/` (no `node_modules`). Plan deviations: Bootstrap partial is `bootstrap/scss/alert` (not `alerts`); `mixed-decls` removed from `silenceDeprecations` (obsolete in current Sass). Scaffold uses Vitest + jsdom for `ng test`.
  - Phase 1 done (client `a07590e4`; API 404 problem+json + Swagger verified).
  - Phase 2 written: Domain (`Employee`, `EmployeePayComponent`, `Department`, `PayComponentType`, `Guard`), Application (DTOs, validators, `EmployeeService`, `DepartmentService`, Mapster config, repository abstractions, `ClockExtensions.Today/UtcNow`, `ConflictMessages`), Infrastructure (`AppDbContext` with S6 translation, configurations + `HasData`, 3 repositories, `DevelopmentDataSeeder`, `AddInfrastructure`, `MigrateAndSeedDevelopmentDataAsync`), Api (`EmployeesController`, `DepartmentsController`, Program wiring), tests (new `Payroll.Domain.UnitTests`, `Payroll.Application.UnitTests` with `FakeDatabase`; controller tests).
  - Notes: `Microsoft.EntityFrameworkCore.Design` is referenced by **Api** (the EF startup project needs it), not Infrastructure as §17 says. `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` so FluentValidation produces all required-field errors (camelCase keys). The employees list endpoint binds individual query params (defaults page=1, pageSize=10) and builds `EmployeeSearchRequest`. `ClockExtensions.CurrentMonth` is deferred to Phase 3 (needs `PayrollMonth`).
  - Phase 2 verified by Kamal (build, 112 tests, migration, Swagger checks all OK).
  - Phase 3 written: Domain `Payroll/` (`PayrollMonth`, `PayrollPolicy` + `TaxBracket` with in-place bracket replacement, the 4 calculators in `Calculators.cs`, `Payslip`), `Employee.EnsureEligibleForPayslip`; Application `IPayrollPolicyRepository`, `PayslipService` (S11 order, missing month → current month, malformed → 400 on `month`), `PayrollPolicyService` (read side only; writes come in Phase 5, interface extended then), `ClockExtensions.CurrentMonth`, Mapster `Payslip → PayslipResponse`; Infrastructure policy/bracket configurations + `HasData` v1/v2 + `PayrollPolicyRepository`; Api `PayslipController`, `PayrollPoliciesController` (GET). Tests for all of it. Ownership check (S7) is added in Phase 7.
  - **D53 (Kamal): tax schemes per employee + tax method strategies.** `TaxScheme`/`TaxMethod` (Domain), `Employee.TaxSchemeId` (ctor/UpdateDetails take `taxSchemeId` after `departmentId`), `PayrollPolicy(TaxScheme, ...)` + `Replace(TaxScheme, ...)`, `ITaxCalculator.Method` + `FlatTaxCalculator` + `ExemptTaxCalculator`, `PayrollCalculator(IEnumerable<ITaxCalculator>, ...)`; DTOs: requests + `EmployeeResponse` (`TaxSchemeId`, `TaxSchemeName`), `PayslipResponse` (`TaxSchemeName`, `TaxMethod`), policy request/response (`TaxSchemeId`); repo methods take `taxSchemeId`; `GET /api/tax-schemes`; `GET /api/payroll-policies/effective?taxSchemeId=&month=`. Seed: schemes 1 Standard, 2 Flat Rate (15 %), 3 Tax Exempt; demo Omar → Exempt, Mona → Flat. **Phase 4 must add a tax scheme dropdown to the employee form and show the scheme on the payslip.**
  - Phase 4 written: core (`api-error`, `problemDetailsInterceptor`, `NotificationService` + toasts), shared (`MoneyPipe` halfExpand, `notInFuture`, `maxTwoDecimals`, `noDuplicateType`, `applyServerErrors`, field-errors, pagination, native `<dialog>` confirm), employees (list with URL filters/sort/paging/delete, create/edit form with tax scheme dropdown + pay-components FormArray + rowVersion + server error mapping), payslip page (month in URL, scheme shown, negative-net and 422 banners). `tsconfig` now has `strict` + `strictTemplates`. Backend: read-only `GET /api/pay-component-types` (+ `{id}`) added early for the form; writes stay in Phase 5. Auth parts of the client wait for Phase 7.
  - Client: a dev-proxy 502/503/504 without a problem body now reads "The server cannot be reached."; interceptor spec added (502 → toast, 4xx → no toast).
  - Phase 5 written: `PayComponentTypeService` create/update/delete (+ validators; taxable deduction → 400 on `isTaxable`, also on update against the stored kind; duplicate name → 409 `errors.name`; in use incl. deleted employees → 409); `PayrollPolicyService` create/update/delete (`PayrollPolicyRequestValidator`: 1st of a future month, rates 0–100, cap ≥ 0, bracket order/open-last on `taxBrackets[i].upperBound`; service: unknown scheme → 400, bracket shape vs. method → 400 on `taxBrackets` via `TaxScheme.DescribeBracketShapeProblem`, duplicate date in scheme → 409 `errors.effectiveFrom`, in effect → 409, moving scheme → 400); `PayrollPolicy.IsModifiable`; controllers POST/PUT/DELETE.
  - **D54 (Kamal):** past months closed, current month open. `PayrollPolicy.IsModifiable` = `EffectiveFrom >= currentMonth.FirstDay`; validator allows the current month; `Replace` cannot move into a closed month; 409 message explains the correction path. Tests updated.
  - Model-binding/JSON errors now use `ModelStateErrors` (`InvalidModelStateResponseFactory`): keys `$.x` → `x` camelCase, `$` → `body`, no internal type names, traceId kept (§9). Found by Kamal posting `"2026-11-1"`.
  - Phase 7 written. Backend: `UnauthorizedException` (→ 401 generic "Invalid credentials."), `ICurrentUser`/`ITokenGenerator`/`IUserCredentialStore`, `AuthService` (+ `LoginRequestValidator`, D25), `PayslipService` ownership check after month parse and before existence (S7/S11); Infrastructure `ApplicationUser`, `AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`, `JwtOptions` (+ `EnsureValid`, key ≥ 32), `JwtTokenGenerator` (HS256, claims sub/email/role/employee_id), `IdentityCredentialStore`, seeder adds roles + `hr@payroll.local`, `ali@demo.local` (Active) and `youssef@demo.local` (Inactive → 401 demo) from `Seed__*`; Api `AuthSetup` (JwtBearer, `MapInboundClaims=false`, fallback policy, `HrOnly` on Employees/PayComponentTypes/PayrollPolicies), `CurrentUser`, `AuthController`, OpenAPI bearer scheme transformer, `/health` + `/openapi` anonymous. Client: `AuthService` (sessionStorage + signal), `authInterceptor`, 401 → logout in `problemDetailsInterceptor`, `authGuard`/`roleGuard`, `LoginPage` (safe returnUrl), role redirect on `/`, `/my-payslip`, shell shows role nav + sign out.
  - **D55 (Kamal): refresh tokens** before testing Phase 7. Application: `IRefreshTokenStore`, `StoredRefreshToken`, `IssuedRefreshToken`, `IUserCredentialStore.FindByIdAsync`, `AuthService` Login/Refresh/Logout returning `AuthResult` (rotation, reuse → family revoked, D25 re-checked on refresh). Infrastructure: `RefreshToken` entity + config (hash unique, FamilyId index, FK user cascade), `RefreshTokenStore` (SHA-256), `JwtOptions.LifetimeMinutes = 15`, `RefreshTokenDays = 7`. Api: `AuthController` login/refresh/logout with cookie `payroll_refresh` (HttpOnly, Secure, SameSite=Strict, Path=/api/auth). Client: `AuthService.refresh()` single-flight, `endSession()`, `authInterceptor` 401 → refresh → retry once, `authGuard` refreshes an expired session; 29 client tests green. Migration `AddIdentity` (not yet created) will include `RefreshTokens`.
  - Session 2 transcript reconstructed into `ai-usage/session-02-implementation.md` (session 1 was already filled, commit `1c20b44`).
  - Payslip review by Kamal: empty taxable badges = missing Bootstrap `helpers` import (fixed); odd November numbers came from his own test policy (data, not a bug).
  - **D56 (Kamal): Arabic + English**, Angular official i18n + server messages. Backend: `ILocalizableError` (Domain), codes on `DomainException`/`NotFoundException`/`ConflictException.Coded`/`ForbiddenException`/`UnauthorizedException`, `ErrorCodes`, FluentValidation `WithErrorCode`, `ApiText` + `Resources/ErrorMessages(.ar).resx` + `FieldNames(.ar).resx`, `ApiExceptionHandler` translates titles/details/field errors, `ModelStateErrors` translated, `UseRequestLocalization` (UI culture only) before the exception handler, FluentValidation `DisplayNameResolver`; tests (`ApiTextTests` checks every code has en + ar text). Client: `@angular/localize` (added in the sandbox copy), `i18n` config + `localize` in production, `ar` serve config on port 4201, 153 messages with `@@` ids, `src/locale/messages(.ar).xlf`, `languageInterceptor` (Accept-Language), `LanguageService` (switch /en ↔ /ar, or port 4200 ↔ 4201 in dev), `formatMoney` with Latin digits; nginx serves /en/ and /ar/ and redirects / by Accept-Language.
  - **D57 (Kamal): new layout**: sidebar shell, page headers, surfaces, list with avatars and icon actions, sectioned form, formal printable payslip, `_rtl.scss` + logical properties. `ng build` (both locales) + 30 client tests green in the sandbox.
  - Login password field (Kamal): own show/hide button with eye icons, Edge's native reveal hidden, no overlap with the invalid state in LTR or RTL; 155 i18n messages.
  - **D58–D60 (Kamal):** Arabic names for master data (`NameAr` on Department / PayComponentType / TaxScheme, `LocalizedName.Pick`, payslip lines localized in `PayslipService`, migration `AddArabicNames` to be created by Kamal); home page (`DashboardService` + `GET /api/dashboard`, `HomePage` for HR and Employee, `/home` for both roles); Docker (`web` health check, `urls` service, `start.ps1`). Client fixes from Kamal's Arabic screenshot: `<bdi>` instead of block-level LTR for ids/dates/emails, own month picker (native `type=month` showed English), responsive top bar with a menu button, columns hidden on small screens. 190 i18n messages; `ng build` (en + ar) and 32 client tests green in the sandbox.
  - **D61 (Kamal):** session owned by the server: access token in memory only, `provideAppInitializer` restores the session via `/api/auth/refresh`, `LoginResponse.email`, `RefreshToken.RotatedAtUtc` (new column → migration `AddRefreshTokenRotation`), reuse grace `Auth:RefreshReuseGraceSeconds` (30 s; integration tests use 0). Arabic: "الراتب" instead of "قسيمة راتبي". 31 client tests green (sessionStorage test removed).
  - **D62 (Kamal: all 8 review items):** `PayslipLine.PayComponentTypeId`, `DomainErrorCodes`, calculators split (`Proration.cs`, `TaxCalculators.cs`, `SocialInsuranceCalculator.cs`, `PayrollCalculator.cs`), `IEmployeeReferenceChecker` (+ `TestData.EmployeeService(db)` factory in tests), `IApiText`/`ResxApiText`, `IProblemMapper` + 6 mappers registered in `AddApiProblemDetails`, `GetHeadcountAsync` (GROUP BY) + `ListPayableBatchAsync` (keyset, 500), `AddAuthRateLimiting` (`[EnableRateLimiting("auth")]`, integration tests set `RateLimiting__AuthPermitLimit=1000`), `AddDbContextPool`, `Cached*Repository` decorators + `AddMemoryCache`, `RefreshTokenCleanupService`, prefix search. New migration needed: `AddRefreshTokenRotation` (RotatedAtUtc).
  - Kamal's run: build green, migration `AddRefreshTokenRotation` created, 284/285 tests. The failure: the option property was `ReuseGraceSeconds` while config/spec use `Auth:RefreshReuseGraceSeconds`, so the test's 0 never bound. Renamed the property to `RefreshReuseGraceSeconds`.
  - NuGet audit (warnings as errors) flagged transitive `System.Security.Cryptography.Pkcs` 9.0.11 (via Microsoft.Data.SqlClient). Fixed with `CentralPackageTransitivePinningEnabled` + a pin to 10.0.14 in `Directory.Packages.props`.
