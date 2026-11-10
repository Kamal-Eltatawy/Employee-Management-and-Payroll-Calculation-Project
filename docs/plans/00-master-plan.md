# Employee Management & Payroll — Master Implementation Plan

> **For agentic workers:** this master plan fixes the **phase order, task boundaries and the shared contracts** (names, signatures, files). Each phase gets its own detailed plan (`docs/plans/NN-*.md`). The detailed plan has full code and exact commands, is written just before that phase starts, and is approved by Kamal. Detailed plans are executed with `superpowers:executing-plans`, task by task, using checkbox (`- [ ]`) steps.

**Goal:** Build the Employee Management & Payroll Calculation application described in `docs/design/2026-11-09-employee-payroll-design.md` (the "spec").

**Architecture:** An ASP.NET Core Web API (.NET 10) in 4 projects: Domain → Application → Infrastructure → Api. It uses EF Core 10 on SQL Server and an Angular 22 standalone client. The payroll rules are pure domain calculators behind interfaces. The payroll policy (brackets, insurance) is effective-dated data in the database.

**Tech Stack:**

- **Backend:** .NET 10 SDK (pinned by `global.json`), ASP.NET Core, EF Core 10 + SQL Server 2022 (Docker), ASP.NET Core Identity + JWT, FluentValidation, Mapster.
- **Backend tests:** xUnit v3, NSubstitute, `FakeTimeProvider`.
- **Frontend:** Angular 22.2.x, Bootstrap 5.3.8 (SCSS source only), Angular CLI default unit-test runner.

## Global Constraints

Copied from the spec. They apply to every task.

**Scope and rules:**

- **Spec authority:** every task implements the spec. Where this plan and the spec differ, the spec wins and the difference is raised with Kamal.
- **Git (D50):** Claude never runs `git commit` or `git push`. Every task ends with a **Checkpoint**, where Kamal reviews and commits using the suggested message.
- **TDD (D51):** for every behaviour: failing test → minimal code → passing test → checkpoint. There is no separate "tests phase".
- **Language (D1):** all files (code, comments, docs, commit messages) are in English.

**Domain and data:**

- **Precision (D32):** `decimal` everywhere for money and rates. The Domain **never rounds**. Rounding happens only in the Angular `MoneyPipe` for display (2 decimals, half away from zero).
- **Enums (D23):** stored as `int` with explicit values. Serialized as strings in JSON.
- **"Today" (D15):** read from an injected `TimeProvider` as the **server local date**, `DateOnly.FromDateTime(clock.GetLocalNow().DateTime)`. The Angular `notInFuture` validator uses the browser's local date.

**Code structure:**

- **Interfaces (D39):** every behaviour class (services, calculators, repositories, UoW, current user, token generator, credential store) is consumed through an interface. Entities, value objects and DTOs are concrete.
- **Layering (D13):** `Payroll.Domain` has **no** package or project references. `Payroll.Application` references only Domain + FluentValidation + Mapster. EF Core and Identity appear only in Infrastructure; ASP.NET only in Api.
- **No try/catch for flow control** in controllers or services (D17). Database exceptions are translated in exactly one place: `AppDbContext.SaveChangesAsync` (S6).
- **Error format:** every error is RFC 7807 `application/problem+json` with `traceId`. Validation errors use camelCase JSON-path keys (`payComponents[0].amount`).

**Security and dependencies:**

- **Secrets:** nothing secret is committed. Secrets live in `.env` (gitignored), with `.env.example` committed.
- **Packages:** only those in spec §17. Versions are pinned centrally in `Directory.Packages.props`. Microsoft ASP.NET Core / EF Core / Extensions packages use the **10.0.x** line.
- **Build hygiene:** `TreatWarningsAsErrors=true` and `Nullable=enable` for all .NET projects.

## Execution model (who runs what)

Claude's sandbox has Node/npm but **no .NET SDK, no NuGet access and no Docker**.

| Action | Who |
|---|---|
| Write and edit source files | Claude |
| `dotnet build`, `dotnet test`, `dotnet ef …`, `docker compose …` | **Kamal** (PowerShell, repo root). Kamal pastes the output back. |
| Angular scaffolding, `ng build`, `ng test` for verification | Claude, in a **sandbox copy** (`/tmp/verify-client`). Never `npm install` inside the repo from Linux: native binaries (esbuild/rollup) would be Linux builds. |
| `npm install` / `npm start` for actual use | Kamal (Windows) |
| `git add` / `git commit` | Kamal only |

---

## Phases

Estimates come from spec §18, adjusted by D51 (test time folded into the feature phases). Committed total: about **12.25 h**.

| # | Phase | Est. | Detailed plan |
|---|---|---|---|
| 1 | Setup & skeleton | 0.75 h | `01-setup-and-skeleton.md` |
| 2 | Employee CRUD backend (+ tests) | 3.0 h | `02-employee-crud-backend.md` |
| 3 | Payroll backend (+ tests) | 2.5 h | `03-payroll-backend.md` |
| 4 | Angular client (+ logic tests) | 2.5 h | `04-angular-client.md` |
| 5 | Admin APIs: pay component types + policy writes (+ tests) | 0.75 h | `05-admin-apis.md` |
| 6 | Health check + JSON logging | 0.25 h | `06-health-and-logging.md` |
| 7 | Identity + JWT, backend and client (+ tests) | 1.75 h | `07-identity-and-jwt.md` |
| 8 | README + `/ai-usage` | 0.75 h | `08-docs.md` |
| 9 | *Optional:* integration tests (Testcontainers) | 1.0 h | `09-integration-tests.md` |
| 10 | *Optional:* full docker-compose + CI | 1.0 h | `10-docker-and-ci.md` |

---

## Shared contracts

These are the names and signatures that tasks in different phases rely on. A detailed plan may **add** private members, but must not rename or change anything listed here without updating this file.

### Domain — `src/Payroll.Domain`

```csharp
// Common/DomainException.cs
public class DomainException(string message) : Exception(message);

// Employees/EmployeeStatus.cs
public enum EmployeeStatus { Active = 1, Inactive = 2 }

// Employees/PayComponentAssignment.cs
public readonly record struct PayComponentAssignment(int PayComponentTypeId, decimal Amount);

// Employees/Employee.cs  (aggregate root)
public sealed class Employee
{
    public const int FullNameMaxLength = 100, EmailMaxLength = 254, JobTitleMaxLength = 100;
    public int Id { get; }
    public string FullName { get; }   public string Email { get; }   public string JobTitle { get; }
    public int DepartmentId { get; }  public Department? Department { get; }
    public DateOnly HireDate { get; } public decimal BaseMonthlySalary { get; }
    public EmployeeStatus Status { get; }
    public IReadOnlyCollection<EmployeePayComponent> PayComponents { get; }
    public byte[] RowVersion { get; }
    public DateTime CreatedAtUtc { get; } public DateTime UpdatedAtUtc { get; } public DateTime? DeletedAtUtc { get; }
    // (all setters private)

    public Employee(string fullName, string email, string jobTitle, int departmentId, DateOnly hireDate,
                    decimal baseMonthlySalary, EmployeeStatus status, DateOnly today, DateTime utcNow);
    public void UpdateDetails(string fullName, string email, string jobTitle, int departmentId, DateOnly hireDate,
                              decimal baseMonthlySalary, EmployeeStatus status, DateOnly today, DateTime utcNow);
    public void SetPayComponents(IReadOnlyCollection<PayComponentAssignment> assignments, DateTime utcNow);
    public void MarkDeleted(DateTime utcNow);
    public void EnsureEligibleForPayslip(PayrollMonth month);   // DomainException: inactive / before hire month
    public static string NormalizeEmail(string email);          // trim + ToLowerInvariant
}

// Employees/EmployeePayComponent.cs
public sealed class EmployeePayComponent
{
    public int Id { get; } public int EmployeeId { get; } public int PayComponentTypeId { get; }
    public PayComponentType? PayComponentType { get; } public decimal Amount { get; }
}

// Departments/Department.cs
public sealed class Department { public int Id { get; } public string Name { get; } public Department(int id, string name); }

// PayComponents/PayComponentKind.cs
public enum PayComponentKind { Allowance = 1, Deduction = 2 }

// PayComponents/PayComponentType.cs
public sealed class PayComponentType
{
    public const int NameMaxLength = 100;
    public int Id { get; } public string Name { get; } public PayComponentKind Kind { get; } public bool IsTaxable { get; }
    public DateTime CreatedAtUtc { get; } public DateTime UpdatedAtUtc { get; }
    public PayComponentType(string name, PayComponentKind kind, bool isTaxable, DateTime utcNow); // DomainException if Deduction && isTaxable
    public void Update(string name, bool isTaxable, DateTime utcNow);                             // Kind cannot change (D2)
}

// Payroll/PayrollMonth.cs
public readonly record struct PayrollMonth(int Year, int Month) : IComparable<PayrollMonth>
{
    public DateOnly FirstDay { get; }  public int DaysInMonth { get; }
    public static bool TryParse(string? value, out PayrollMonth month);   // strict "yyyy-MM"
    public static PayrollMonth From(DateOnly date);
    public override string ToString();                                    // "yyyy-MM"
}

// Payroll/TaxBracket.cs, Payroll/TaxBracketDefinition.cs
public sealed class TaxBracket { public int Id { get; } public int PayrollPolicyId { get; } public int SortOrder { get; }
                                 public decimal? UpperBound { get; } public decimal RatePercent { get; } }
public sealed record TaxBracketDefinition(decimal? UpperBound, decimal RatePercent);

// Payroll/PayrollPolicy.cs  (aggregate root)
public sealed class PayrollPolicy
{
    public int Id { get; } public DateOnly EffectiveFrom { get; }
    public decimal InsuranceRatePercent { get; } public decimal? InsuranceCap { get; }
    public IReadOnlyList<TaxBracket> TaxBrackets { get; }   // ordered by SortOrder
    public DateTime CreatedAtUtc { get; }
    public PayrollPolicy(DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
                         IReadOnlyList<TaxBracketDefinition> taxBrackets, DateTime utcNow);
    public void Replace(DateOnly effectiveFrom, decimal insuranceRatePercent, decimal? insuranceCap,
                        IReadOnlyList<TaxBracketDefinition> taxBrackets, PayrollMonth currentMonth);
    public void EnsureModifiable(PayrollMonth currentMonth);  // DomainException when EffectiveFrom <= currentMonth.FirstDay
}

// Payroll/ProrationResult.cs, Payroll/IProrationCalculator.cs → CalendarDayProrationCalculator
public readonly record struct ProrationResult(decimal Factor, int DaysPaid, int DaysInMonth);
public interface IProrationCalculator { ProrationResult Calculate(DateOnly hireDate, PayrollMonth month); }

// Payroll/ITaxCalculator.cs → ProgressiveTaxCalculator
public interface ITaxCalculator { decimal Calculate(decimal taxableIncome, IReadOnlyList<TaxBracket> brackets); }

// Payroll/ISocialInsuranceCalculator.cs → SocialInsuranceCalculator
public interface ISocialInsuranceCalculator
{ decimal Calculate(decimal proratedBaseSalary, decimal ratePercent, decimal? cap, decimal prorationFactor); }

// Payroll/PayslipLine.cs, Payroll/Payslip.cs
public sealed record PayslipLine(string Name, decimal Amount, bool IsTaxable);
public sealed record Payslip(
    int EmployeeId, string EmployeeName, PayrollMonth Month, int PolicyId, DateOnly PolicyEffectiveFrom,
    ProrationResult Proration, decimal BaseSalary, IReadOnlyList<PayslipLine> Allowances,
    decimal GrossPay, decimal TaxableIncome, decimal IncomeTax, decimal SocialInsurance,
    IReadOnlyList<PayslipLine> Deductions, decimal OtherDeductionsTotal, decimal NetPay)
{ public bool IsNetNegative => NetPay < 0; }

// Payroll/IPayrollCalculator.cs → PayrollCalculator(ITaxCalculator, ISocialInsuranceCalculator, IProrationCalculator)
public interface IPayrollCalculator { Payslip Calculate(Employee employee, PayrollPolicy policy, PayrollMonth month); }
```

### Application — `src/Payroll.Application`

```csharp
// Common/
public sealed class NotFoundException(string resource, object key) : Exception($"{resource} {key} was not found.");
public sealed class ConflictException : Exception
{
    public ConflictException(string message, IReadOnlyDictionary<string, string[]>? errors = null);
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public static ConflictException ForField(string field, string message);   // Errors = { field: [message] }
}
public sealed class ForbiddenException(string message) : Exception(message);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public static class Roles { public const string Hr = "Hr"; public const string Employee = "Employee"; }
public static class ClockExtensions
{ public static DateOnly Today(this TimeProvider clock); public static PayrollMonth CurrentMonth(this TimeProvider clock); }

// Abstractions/
public interface IUnitOfWork { Task SaveChangesAsync(CancellationToken ct); }
public enum EmployeeSortBy { Name, HireDate, Salary }
public enum SortDirection { Asc, Desc }
public sealed record EmployeeQuery(string? Search, int? DepartmentId, EmployeeStatus? Status,
                                   EmployeeSortBy SortBy, SortDirection SortDirection, int Page, int PageSize);
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(int id, CancellationToken ct);   // excludes deleted; includes Department, PayComponents(+Type)
    Task<PagedResult<Employee>> SearchAsync(EmployeeQuery query, CancellationToken ct);
    Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeEmployeeId, CancellationToken ct); // includes deleted (D6)
    void Add(Employee employee);
}
public interface IDepartmentRepository
{ Task<IReadOnlyList<Department>> ListAsync(CancellationToken ct); Task<bool> ExistsAsync(int id, CancellationToken ct); }
public interface IPayComponentTypeRepository
{
    Task<IReadOnlyList<PayComponentType>> ListAsync(CancellationToken ct);
    Task<PayComponentType?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PayComponentType>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct);
    Task<bool> IsInUseAsync(int id, CancellationToken ct);         // counts deleted employees too (D7)
    void Add(PayComponentType type); void Remove(PayComponentType type);
}
public interface IPayrollPolicyRepository
{
    Task<IReadOnlyList<PayrollPolicy>> ListAsync(CancellationToken ct);
    Task<PayrollPolicy?> GetByIdAsync(int id, CancellationToken ct);
    Task<PayrollPolicy?> GetEffectiveForAsync(PayrollMonth month, CancellationToken ct); // latest EffectiveFrom <= month.FirstDay
    Task<bool> EffectiveFromExistsAsync(DateOnly effectiveFrom, int? excludeId, CancellationToken ct);
    void Add(PayrollPolicy policy); void Remove(PayrollPolicy policy);
}
public interface ICurrentUser { bool IsInRole(string role); int? EmployeeId { get; } }
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
public interface ITokenGenerator { AccessToken Generate(int userId, string email, string role, int? employeeId); }
public sealed record UserCredentials(int UserId, string Email, string Role, int? EmployeeId);
public interface IUserCredentialStore { Task<UserCredentials?> ValidateAsync(string email, string password, CancellationToken ct); }

// Services (interface + implementation each, D19)
public interface IEmployeeService
{
    Task<EmployeeResponse> GetAsync(int id, CancellationToken ct);
    Task<PagedResult<EmployeeListItem>> SearchAsync(EmployeeSearchRequest request, CancellationToken ct);
    Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct);
    Task<EmployeeResponse> UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
public interface IDepartmentService { Task<IReadOnlyList<DepartmentResponse>> ListAsync(CancellationToken ct); }
public interface IPayslipService { Task<PayslipResponse> GetAsync(int employeeId, string? month, CancellationToken ct); }
public interface IPayComponentTypeService
{
    Task<IReadOnlyList<PayComponentTypeResponse>> ListAsync(CancellationToken ct);
    Task<PayComponentTypeResponse> GetAsync(int id, CancellationToken ct);
    Task<PayComponentTypeResponse> CreateAsync(CreatePayComponentTypeRequest request, CancellationToken ct);
    Task<PayComponentTypeResponse> UpdateAsync(int id, UpdatePayComponentTypeRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
public interface IPayrollPolicyService
{
    Task<IReadOnlyList<PayrollPolicyResponse>> ListAsync(CancellationToken ct);
    Task<PayrollPolicyResponse> GetAsync(int id, CancellationToken ct);
    Task<PayrollPolicyResponse> GetEffectiveAsync(string? month, CancellationToken ct);
    Task<PayrollPolicyResponse> CreateAsync(PayrollPolicyRequest request, CancellationToken ct);
    Task<PayrollPolicyResponse> UpdateAsync(int id, PayrollPolicyRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
public interface IAuthService { Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct); }

// DTOs (records; JSON shapes exactly as spec §8)
public sealed record PayComponentAssignmentDto(int PayComponentTypeId, decimal Amount);
public sealed record CreateEmployeeRequest(string FullName, string Email, string JobTitle, int DepartmentId,
    DateOnly HireDate, decimal BaseMonthlySalary, EmployeeStatus Status, IReadOnlyList<PayComponentAssignmentDto> PayComponents);
public sealed record UpdateEmployeeRequest(string FullName, string Email, string JobTitle, int DepartmentId,
    DateOnly HireDate, decimal BaseMonthlySalary, EmployeeStatus Status, IReadOnlyList<PayComponentAssignmentDto> PayComponents,
    string RowVersion);                                         // base64
public sealed record EmployeeSearchRequest(string? Search, int? DepartmentId, EmployeeStatus? Status,
    string? SortBy, string? SortDir, int Page = 1, int PageSize = 10);
public sealed record EmployeePayComponentResponse(int PayComponentTypeId, string Name, PayComponentKind Kind, bool IsTaxable, decimal Amount);
public sealed record EmployeeResponse(int Id, string FullName, string Email, string JobTitle, int DepartmentId, string DepartmentName,
    DateOnly HireDate, decimal BaseMonthlySalary, EmployeeStatus Status, IReadOnlyList<EmployeePayComponentResponse> PayComponents, string RowVersion);
public sealed record EmployeeListItem(int Id, string FullName, string Email, string JobTitle, string DepartmentName,
    DateOnly HireDate, decimal BaseMonthlySalary, EmployeeStatus Status);
public sealed record DepartmentResponse(int Id, string Name);
public sealed record PayslipLineResponse(string Name, decimal Amount, bool IsTaxable);
public sealed record PayslipResponse(int EmployeeId, string EmployeeName, string Month, int PolicyId, DateOnly PolicyEffectiveFrom,
    decimal ProrationFactor, int DaysPaid, int DaysInMonth, decimal BaseSalary, IReadOnlyList<PayslipLineResponse> Allowances,
    decimal GrossPay, decimal TaxableIncome, decimal IncomeTax, decimal SocialInsurance,
    IReadOnlyList<PayslipLineResponse> Deductions, decimal OtherDeductionsTotal, decimal NetPay, bool IsNetNegative);
public sealed record CreatePayComponentTypeRequest(string Name, PayComponentKind Kind, bool IsTaxable);
public sealed record UpdatePayComponentTypeRequest(string Name, bool IsTaxable);
public sealed record PayComponentTypeResponse(int Id, string Name, PayComponentKind Kind, bool IsTaxable);
public sealed record TaxBracketDto(decimal? UpperBound, decimal RatePercent);
public sealed record PayrollPolicyRequest(DateOnly EffectiveFrom, decimal InsuranceRatePercent, decimal? InsuranceCap,
    IReadOnlyList<TaxBracketDto> TaxBrackets);
public sealed record PayrollPolicyResponse(int Id, DateOnly EffectiveFrom, decimal InsuranceRatePercent, decimal? InsuranceCap,
    IReadOnlyList<TaxBracketDto> TaxBrackets, bool IsModifiable);
public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string Role, int? EmployeeId);

// DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services); // services, validators, Mapster, domain calculators
```

### Infrastructure — `src/Payroll.Infrastructure`

```csharp
public sealed class AppDbContext : DbContext, IUnitOfWork            // becomes IdentityDbContext<ApplicationUser, IdentityRole<int>, int> in Phase 7
public sealed class ApplicationUser : IdentityUser<int> { public int? EmployeeId { get; set; } }      // Phase 7
public sealed class EmployeeRepository : IEmployeeRepository                       // + Department/PayComponentType/PayrollPolicy repositories
public sealed class IdentityCredentialStore : IUserCredentialStore                 // Phase 7
public sealed class JwtTokenGenerator : ITokenGenerator                            // Phase 7, options: JwtOptions { Issuer, Audience, SigningKey, LifetimeMinutes }
public sealed class DevelopmentDataSeeder { public Task SeedAsync(CancellationToken ct); }
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration);
```

Migrations, in order: `InitialCreate` (Phase 2), `AddPayrollPolicies` (Phase 3), `AddIdentity` (Phase 7).

### Api — `src/Payroll.Api`

| Class | Phase |
|---|---|
| `ErrorHandling/ApiExceptionHandler : IExceptionHandler` and `ErrorHandling/ProblemDetailsSetup.AddApiProblemDetails()` | 1 |
| `ErrorHandling/JsonPath.ToCamelCase(string)` | 1 |
| `Configuration/DotEnvFile` (`Parse`, `FindUpwards`, `LoadFromAncestors`) | 1 |
| `Controllers/EmployeesController`, `DepartmentsController` | 2 |
| `PayslipController` (route `api/employees/{id}/payslip`) | 3 |
| `PayrollPoliciesController` (GET endpoints) | 3 |
| `PayComponentTypesController`, `PayrollPoliciesController` (write endpoints) | 5 |
| `AuthController`, `Auth/CurrentUser : ICurrentUser` | 7 |
| `public partial class Program;` (for `WebApplicationFactory`) | 1 |

Dev URL: `http://localhost:5080`.

### Angular — `client/src/app`

| File / symbol | Phase |
|---|---|
| `core/layout/shell.component.ts` (`ShellComponent`) | 1 |
| `core/http/api-error.ts`: `interface ApiError { status: number; title: string; detail?: string; fieldErrors: Record<string, string[]> }`, `function toApiError(e: unknown): ApiError` | 4 |
| `core/http/problem-details.interceptor.ts` (`problemDetailsInterceptor: HttpInterceptorFn`) | 4 |
| `core/notifications/notification.service.ts` (`NotificationService.error(msg)`, `.success(msg)`), `toast.component.ts` | 4 |
| `shared/pipes/money.pipe.ts` (`MoneyPipe`, name `money`) | 4 |
| `shared/forms/validators.ts` (`notInFuture(today?: () => Date)`, `noDuplicateType(key = 'payComponentTypeId')`) | 4 |
| `shared/forms/apply-server-errors.ts` (`applyServerErrors(form: AbstractControl, error: ApiError): boolean`) | 4 |
| `shared/ui/field-errors.component.ts` (`<app-field-errors [control]>`), `pagination.component.ts`, `confirm-dialog.component.ts` | 4 |
| `features/employees/data/employees-api.service.ts` (`EmployeesApiService`), `reference-data-api.service.ts` (`ReferenceDataApiService`: `departments()`, `payComponentTypes()`), `employee.models.ts` | 4 |
| `features/employees/pages/employee-list.page.ts`, `employee-form.page.ts`; `ui/pay-components-editor.component.ts` | 4 |
| `features/payslip/data/payslip-api.service.ts`, `payslip.models.ts`; `pages/payslip.page.ts` | 4 |
| `core/auth/*` (`AuthService` with `currentUser` signal; `authGuard`, `roleGuard(role)`, `authInterceptor`, `LoginPage`) | 7 |

---

## Tasks per phase

Each detailed plan expands these tasks into TDD steps with full code.

### Phase 1 — Setup & skeleton

1. **Toolchain check** (Kamal): .NET 10 SDK, Node ≥ 22.22.3, Docker, `dotnet-ef` local tool.
2. **Package version resolution**: a one-off script prints the pinned versions, then `Directory.Packages.props` is written.
3. **Solution skeleton**: `global.json`, `Directory.Build.props`, `Payroll.slnx`, the 4 src projects + `Payroll.Api.UnitTests`, references enforcing §4.
4. **Error-handling foundation (TDD)**: `DomainException`, the Application exceptions, `JsonPath`, `ApiExceptionHandler`, `AddApiProblemDetails`.
5. **API host (TDD for `DotEnvFile`)**: `Program.cs` (controllers, enum JSON, OpenAPI + Swagger UI), `launchSettings.json`, appsettings, `docker-compose.yml` (db), `.env.example`.
6. **Angular scaffold**: Angular 22 app in `client/`, Bootstrap SCSS, shell layout, dev proxy; `ng build` verified in the sandbox.

**Acceptance:** spec §18, Phase 1.

### Phase 2 — Employee CRUD backend

1. `ClockExtensions` + `Department`, `PayComponentType` and `PayComponentKind` entities (TDD guards).
2. `Employee` + `EmployeePayComponent` (TDD: guards, email normalisation, duplicate types, soft delete).
3. Employee validators (TDD: every PDF rule + boundaries).
4. `EmployeeService` + `DepartmentService` with fakes (TDD: duplicate email incl. deleted, unknown department/type → 400, rowVersion mismatch → 409, deleted → 404). Includes the Mapster config + config test.
5. Infrastructure: `AppDbContext` (configurations, query filter, S6 translation), repositories, `HasData` reference data, `DevelopmentDataSeeder` (employees), `AddInfrastructure`, migration `InitialCreate` (Kamal runs `dotnet ef`).
6. `EmployeesController` + `DepartmentsController` + controller tests (NSubstitute); manual Swagger check against SQL Server (Kamal).

**Acceptance:** spec §18, Phase 2.

### Phase 3 — Payroll backend

1. `PayrollMonth` (TDD parse/format/compare).
2. `PayrollPolicy` + `TaxBracket` (TDD invariants + `EnsureModifiable`).
3. `CalendarDayProrationCalculator` (TDD).
4. `ProgressiveTaxCalculator` (TDD boundaries).
5. `SocialInsuranceCalculator` (TDD cap cases).
6. `PayrollCalculator` + `Employee.EnsureEligibleForPayslip` (TDD: both §11.5 examples, zero allowances, negative net).
7. `PayslipService` + `PayrollPolicyService` (read side) with fakes (TDD: S11 ordering, effective-policy choice, future month).
8. Infrastructure: policy configuration + repository + `HasData` v1/v2 + migration `AddPayrollPolicies`; `PayslipController` + `PayrollPoliciesController` (GET) + controller tests.

### Phase 4 — Angular client

1. HTTP foundation (TDD: `toApiError`) + `problemDetailsInterceptor` + `NotificationService`/toast + `provideHttpClient`.
2. Shared pieces (TDD: `MoneyPipe`, `notInFuture`, `noDuplicateType`, `applyServerErrors`) + `FieldErrorsComponent`, `PaginationComponent`, `ConfirmDialogComponent`.
3. Employees data layer + list page (filters in query params, sort, pagination, delete with confirmation).
4. Employee form page (create/edit, typed form, pay-components editor, server-error mapping, rowVersion).
5. Payslip page (month picker, money pipe, negative-net banner, 422 banner).

### Phase 5 — Admin APIs

1. `PayComponentTypeService` + validators (TDD: name unique → 409, IsTaxable on Deduction → 400, in use → 409) + controller.
2. `PayrollPolicyService` writes + validator (TDD: future-only create, update/delete of in-effect → 409, duplicate EffectiveFrom → 409) + controller endpoints.

### Phase 6 — Health + logging

1. `AddHealthChecks().AddDbContextCheck<AppDbContext>()`, `MapHealthChecks("/health")`, JSON console logging, a 500 logged with `traceId`.

### Phase 7 — Identity + JWT

1. Identity in Infrastructure (`ApplicationUser`, `IdentityDbContext`, roles, migration `AddIdentity`, seeded users from `Seed__*` env).
2. `JwtTokenGenerator` + `IdentityCredentialStore`.
3. `AuthService` (TDD with a fake credential store: D25 rules) + `LoginRequest` validator + `AuthController`.
4. Server enforcement: fallback policy, `HrOnly` policy, `CurrentUser`, ownership rule in `PayslipService` (TDD: Employee → other's payslip → 403) + controller tests.
5. Angular: `AuthService` (sessionStorage + signal), `authInterceptor`, `authGuard`/`roleGuard`, `LoginPage`, `/my-payslip`, role-based redirect, logout in the shell.

### Phase 8 — Docs

1. `README.md` (setup, architecture & reasoning, assumptions, limitations, next steps, *Scope & time*, AI usage summary).
2. `ai-usage/README.md` (tools, skills, MCP/plugins used and why; which parts were AI-generated; pointer to the exported transcript placed there by Kamal).

### Phases 9–10 (optional)

These are planned only if time remains, in this order: Testcontainers integration tests, then full docker-compose + GitHub Actions CI.
