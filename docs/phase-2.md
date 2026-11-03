# Phase 2: PostgreSQL and API foundation

This milestone connects ASP.NET Core to real PostgreSQL, introduces the core schema, and establishes tested HTTP conventions. It does not expose project/task CRUD before authentication and resource authorization are implemented.

## What changed

- Domain: six entities (UserProfile, Workspace, WorkspaceMembership, Project, ProjectMembership, TaskItem), four enums, and construction-time validation.
- Application: a bounded PageRequest query DTO for future list endpoints.
- Infrastructure: DevFlowDbContext, one mapping per entity, dependency injection, a design-time context factory, and an initial migration/model snapshot.
- API: health controller/DTO, PostgreSQL readiness check, consistent validation responses, safe centralized exception handling, and environment-based connection configuration.
- Tests: xUnit domain tests; real PostgreSQL persistence tests; API integration tests using WebApplicationFactory.
- Tooling: pinned local dotnet-ef tool, PostgreSQL Compose configuration, dependency lockfiles, and this guide.

## Run in the existing workspace

Docker was not installed on this computer. A PostgreSQL 18.6 binary distribution from EDB was downloaded into the workspace's `work/` directory for validation. This is an alternative to Docker, not a Windows service or a production deployment. Its local cluster listens only on 127.0.0.1:5432 and requires a randomly generated password. Neither the binary distribution nor credentials are inside the Git repository.

From the DevFlow repository root, load the workspace-only SDK and connection configuration:

```powershell
. ../../work/env.ps1
$env:ConnectionStrings__DevFlow = [System.IO.File]::ReadAllText((Resolve-Path ../../work/postgres-connection.txt))
```

If PostgreSQL has stopped, run this in a separate terminal and leave it open:

```powershell
& ../../work/postgresql/pgsql/bin/postgres.exe -D "$PWD/../../work/pgdata" -p 5432 -h 127.0.0.1 -c timezone=UTC
```

Then restore, build and apply the migration explicitly:

```powershell
dotnet restore backend/DevFlow.slnx --configfile NuGet.Config --locked-mode -m:1
dotnet tool restore --configfile NuGet.Config
dotnet build backend/DevFlow.slnx --no-restore -m:1
dotnet ef database update --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api --no-build
```

Start the API (stop an older API instance before building on Windows):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
dotnet backend/src/DevFlow.Api/bin/Debug/net10.0/DevFlow.Api.dll --contentRoot "$PWD/backend/src/DevFlow.Api"
```

Normal installations can use `dotnet run --project backend/src/DevFlow.Api --launch-profile http` after configuring the same connection string.

## Standard development using Docker

On another machine, install Docker Desktop with Linux containers and the pinned .NET SDK. Do not run Docker PostgreSQL and the workspace binary on port 5432 at the same time.

Create `infrastructure/docker/.env` from `.env.example`, fill in a strong local password, and leave it out of Git. Then:

```powershell
docker compose --env-file infrastructure/docker/.env -f infrastructure/docker/compose.yaml up -d --wait
```

The database and user are both named `devflow`. Set the API connection in the current terminal without writing the password into shell history:

```powershell
$securePassword = Read-Host 'Database password from your local .env' -AsSecureString
$localPassword = [System.Net.NetworkCredential]::new('', $securePassword).Password
$env:ConnectionStrings__DevFlow = "Host=127.0.0.1;Port=5432;Database=devflow;Username=devflow;Password=$localPassword"
Remove-Variable localPassword, securePassword
```

If you change POSTGRES_PORT, use that same port in the connection string. ConnectionStrings:DevFlow may alternatively be stored in .NET User Secrets for API runtime. The design-time factory intentionally uses `ConnectionStrings__DevFlow` for EF CLI operations, so set that environment variable for migrations even when runtime uses User Secrets.

Compose uses a named volume. `docker compose --env-file infrastructure/docker/.env -f infrastructure/docker/compose.yaml down` stops the services without deleting data. Changing the password variable does not rotate an existing database password. Do not delete volumes as a routine startup step.

## HTTP behavior

| Request | Expected result |
| --- | --- |
| GET /api/health | 200 Healthy while the process is alive, regardless of PostgreSQL |
| GET /api/health/ready | 200 only when PostgreSQL connects and all known migrations are applied; otherwise 503 |
| GET /openapi/v1.json | Development-only OpenAPI with health response contracts |
| Unknown route | 404 application/problem+json with a trace ID |
| Invalid query DTO on a future API controller | 400 validation Problem Details with errors and trace ID |
| EF stale-update exception | 409 Problem Details; caller must reload |
| Unexpected exception | 500 Problem Details without provider details |

```powershell
Invoke-RestMethod http://127.0.0.1:5080/api/health
Invoke-RestMethod http://127.0.0.1:5080/api/health/ready
Invoke-RestMethod http://127.0.0.1:5080/openapi/v1.json
```

The existing frontend connection button still checks liveness. Database readiness can be checked using the second URL. No database records are returned by health endpoints. Test-only probe controllers exercise validation and exception handling; they are not compiled into the production API.

## Tests

Run unit tests without PostgreSQL:

```powershell
dotnet test --project backend/tests/DevFlow.UnitTests/DevFlow.UnitTests.csproj --no-build --no-restore
```

With Docker running, integration tests automatically create a PostgreSQL container using Testcontainers:

```powershell
dotnet test --project backend/tests/DevFlow.IntegrationTests/DevFlow.IntegrationTests.csproj --no-build --no-restore
```

In the current workspace, use the local PostgreSQL server instead:

```powershell
$env:DEVFLOW_TEST_POSTGRES = [System.IO.File]::ReadAllText((Resolve-Path ../../work/postgres-connection.txt))
dotnet test --solution backend/DevFlow.slnx --no-build --no-restore
```

External test mode requires a disposable local server and a role with CREATEDB permission. Tests create randomly named `devflow_test_<guid>` databases, run actual migrations, and drop only databases created by that fixture. They never call EnsureDeleted on the supplied database or migrate the application's devflow database. Do not point tests at RDS or a shared/production server.

`global.json` selects Microsoft Testing Platform for .NET 10, and the test projects explicitly use xUnit's MTP integration. Run test commands from the repository root so this configuration is discovered. In the restricted agent environment the solution-level runner stalled; the compiled xUnit executables were used directly for verification instead:

```powershell
dotnet backend/tests/DevFlow.UnitTests/bin/Debug/net10.0/DevFlow.UnitTests.dll
dotnet backend/tests/DevFlow.IntegrationTests/bin/Debug/net10.0/DevFlow.IntegrationTests.dll
```

The same `DEVFLOW_TEST_POSTGRES` variable is required for external PostgreSQL mode. A failed test run exits nonzero. No database test is silently skipped when Docker or PostgreSQL is unavailable.

Persistence tests cover foreign keys across workspaces, project membership and task assignment, unique Cognito subjects/project keys/task numbers, date/enum mapping, restrictive deletion and optimistic concurrency. API tests cover readiness, pending migrations, database failure, validation, error redaction, unknown routes and OpenAPI visibility. These tests do not substitute for authorization tests in Phase 4.

## Architecture decisions

**EF Core is the persistence boundary.** No generic repository is added. Feature-specific query/storage interfaces will be introduced with application use cases.

**Relationships enforce membership consistency.** ProjectMembership includes WorkspaceId so composite foreign keys can prove that both the project and user belong to that workspace. A task's optional assignee references the same project's membership. Reporters reference UserProfile because historical authorship must survive leaving a project; the future create-task use case must check current reporter membership.

**Deletion is deliberate.** Restrictive foreign keys prevent accidental data cascades. Membership removal must first handle assigned tasks and nested membership. Ownership preservation and resource access are application authorization rules still to be implemented.

**Concurrency is database-backed.** EF maps TaskItem.Version to PostgreSQL xmin. Two loaded copies cannot silently overwrite one another. Future update DTOs must carry the version and apply it as the original concurrency value; Phase 2 tests the storage mechanism, not a complete edit endpoint.

**Migrations are explicit.** The API never calls Migrate or EnsureCreated on startup. This keeps schema changes reviewable and makes deployment safe to coordinate later. Readiness is a connectivity/migration-history check, not a full schema-drift validator.

**The core schema is incremental.** Comments, invitations, activity, and attachments are designed but not migrated yet. UpdatedAt and board Position are stored now; their mutation behavior belongs to the future task use cases.

## Migration workflow

After a future approved model change, set the connection environment variable and run:

```powershell
dotnet ef migrations add MeaningfulChangeName --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api
```

Review generated operations before applying them. Never edit an already-shared migration to conceal a schema change. The initial migration can be reapplied safely through the history table; tests verify no pending model changes.

## Version sources

EF Core and dotnet-ef 10.0.14; Npgsql EF provider 10.0.3; PostgreSQL 18.6; xunit.v3.mtp-v2 4.0.2 using Microsoft Testing Platform; MVC Testing 10.0.14; Testcontainers.PostgreSql 4.16.0. Versions and compatibility were checked against official NuGet metadata before installation.

- [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html)
- [PostgreSQL Windows distributions](https://www.postgresql.org/download/windows/)
- [EDB binary downloads](https://www.enterprisedb.com/download-postgresql-binaries)
- [Testcontainers PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/)
- [xUnit getting started](https://xunit.net/docs/getting-started/v3/getting-started)

## Verification results

- Backend solution build: passed with zero warnings and errors.
- InitialCoreSchema migration: generated, reviewed and applied to local PostgreSQL 18.6.
- Domain unit tests: 15 passed, zero failures or skips.
- PostgreSQL/API integration tests: 22 passed, zero failures or skips.
- Tests verified migration/model consistency, relational constraints, optimistic concurrency, readiness, validation, safe error responses and development-only OpenAPI.
- Tests ran as compiled xUnit executables against the local PostgreSQL binary. The solution-level runner stalled in the restricted agent environment.
- Docker verification passed on November 2, 2026, from the user's VS Code terminal. The saved log confirms Testcontainers connected to Docker Desktop's Linux engine, started PostgreSQL, passed all 22 integration tests with zero failures/skips, and removed its PostgreSQL container. Reaching this test stage also confirms the script's preceding Compose startup, health check and PostgreSQL query succeeded.
- Run `./scripts/Test-Docker.ps1` in a normal VS Code PowerShell terminal to repeat verification. The script builds the backend, uses a temporary Compose project with a random password/free port, clears the external database override for Testcontainers, and cleans up its own Compose container/volume afterward. Test output is saved in the Git-ignored `backend/tests/TestResults/docker-integration.log`. The agent session itself cannot access Docker's Windows named pipe, so verification was confirmed by reading the saved output from the user's run.
- The verification script adds Docker's executable directory to its temporary PATH so Docker Desktop's credential helper can be found. It restores the original PATH afterward. On Compose failure, it prints container status/logs before cleanup.
- The script normalizes only the test-process endpoint from Docker CLI's `npipe:////./pipe/...` form to `npipe://localhost/pipe/...`, matching the installed Docker.DotNet client's URI validation, and restores the original endpoint before Compose cleanup.

Phase 2 implementation, local PostgreSQL verification and Docker integration verification are complete. The user approved the Phase 2 milestone commit after verification. Phase 3 requires separate approval.
