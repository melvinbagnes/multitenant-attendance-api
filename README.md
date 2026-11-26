# Multi-Tenant Attendance API

![CI](https://github.com/melvinbagnes/multitenant-attendance-api/actions/workflows/ci.yml/badge.svg)

A small ASP.NET Core Web API for time-and-attendance, built to demonstrate **tenant isolation** done properly: one shared database, many tenants, and no way for one tenant to read or write another's data.

Employees clock in and out, and the API reports daily hours split into regular and overtime.

> Portfolio project. The domain is intentionally generic and was written from scratch; it is not derived from any employer's code.

## Stack

- C# / ASP.NET Core Web API on .NET 10 (controllers)
- Entity Framework Core with PostgreSQL (Npgsql); SQLite for tests
- JWT bearer authentication
- xUnit unit and integration tests (`WebApplicationFactory`)
- Docker, Docker Compose, GitHub Actions CI

## How tenant isolation works

| Layer | What it does |
|---|---|
| **Identity** | The tenant comes only from the `tenant_id` claim of a validated JWT. Request headers are never trusted for tenant selection. |
| **Authorization** | Every API endpoint requires an authenticated user whose token carries a valid `tenant_id`. |
| **Reads** | EF Core global query filters on every tenant-owned entity. If no tenant is resolved the filter matches nothing (fail closed). |
| **Writes** | `SaveChanges` stamps the current tenant on new rows and throws `TenantViolationException` for any write that touches another tenant's row or happens without a tenant. |
| **Database** | Unique indexes are tenant-scoped (`TenantId, EmployeeNumber`), so two tenants can reuse the same employee number. |

Other integrity rules:

- A partial unique index allows **at most one open attendance record per employee**, so two racing clock-in requests cannot both succeed (the service pre-checks, the index is the final guard).
- Timestamps are UTC. A record belongs to the UTC day it was clocked in on.

## Run it

With Docker:

```bash
docker compose up --build
# API on http://localhost:8080 , health check on /health
```

Locally (needs a PostgreSQL instance matching `appsettings.Development.json`):

```bash
dotnet run --project src/AttendanceApi
```

`requests.http` has a ready-made request sequence (VS Code REST Client / JetBrains HTTP client).

### Quick walkthrough

```bash
# 1. Demo token for a tenant (only mapped when ASPNETCORE_ENVIRONMENT=Development)
TOKEN=$(curl -s -X POST localhost:8080/dev/token \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"11111111-1111-1111-1111-111111111111","userName":"melvin"}' | jq -r .accessToken)

# 2. Create an employee, clock in, clock out
EMP=$(curl -s -X POST localhost:8080/api/employees -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"E-001","fullName":"Juan Dela Cruz"}' | jq -r .id)

curl -X POST localhost:8080/api/attendance/clock-in  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d "{\"employeeId\":\"$EMP\"}"
curl -X POST localhost:8080/api/attendance/clock-out -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d "{\"employeeId\":\"$EMP\"}"

# 3. Daily report
curl localhost:8080/api/reports/daily -H "Authorization: Bearer $TOKEN"
```

## API

| Method | Route | Description |
|---|---|---|
| POST | `/api/employees` | Create an employee (409 on duplicate number within the tenant) |
| GET | `/api/employees` | List the tenant's employees |
| GET | `/api/employees/{id}` | Get one employee (404 if it belongs to another tenant) |
| POST | `/api/employees/{id}/deactivate` | Deactivate an employee |
| POST | `/api/attendance/clock-in` | Open an attendance record (409 if already clocked in) |
| POST | `/api/attendance/clock-out` | Close the open record (409 if not clocked in) |
| GET | `/api/attendance?employeeId=&from=&to=` | Records, newest first (max 500) |
| GET | `/api/reports/daily?date=yyyy-MM-dd` | Hours per employee, regular vs overtime (default: today, UTC) |
| GET | `/health` | Liveness (anonymous) |

Errors are returned as RFC 7807 problem responses. The regular-hours threshold is `Attendance:RegularHoursPerDay` (default 8).

## Tests

```bash
dotnet test AttendanceApi.slnx
```

- `OvertimeCalculatorTests`: regular/overtime split, rounding, invalid input
- `TenantIsolationTests`: filtered reads, tenant stamping, rejected cross-tenant and tenant-less writes, tenant-scoped uniqueness
- `AttendanceServiceTests`: clock-in/out rules, inactive and unknown employees, cross-tenant access, daily summary
- `ApiIntegrationTests`: auth (401), validation (400), conflicts (409), the full clock-in/out flow, and tenant isolation over HTTP

## Design decisions and known limits

- **`EnsureCreated`, not migrations.** The schema is created at startup to keep the demo to a single command. For a real deployment I would add EF Core migrations and turn this off (`Database:EnsureCreated=false`).
- **No repository layer on top of EF Core.** `DbContext` already is a unit of work; services use it directly to avoid a pass-through abstraction.
- **`/dev/token` is demo-only.** It exists only in the Development environment. A real system would validate tokens issued by an identity provider.
- **Signing keys in config files are placeholders.** Override `Jwt__SigningKey` (32+ characters) from a secret store outside local development.
- **Not covered yet:** pagination beyond the 500-record cap, shifts and schedules, per-user roles within a tenant, rate limiting.
