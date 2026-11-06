# DevFlow

A cloud-based project management platform built incrementally as a software engineering portfolio project.

**Current milestone: Phase 9 started: automated verification and delivery preparation; paid AWS hosting deferred.**

Implemented: responsive React/TypeScript shell, Tailwind styling, React Router, TanStack Query, API/database connection checks, Vitest/React Testing Library tests, layered ASP.NET Core backend, PostgreSQL/EF Core persistence, initial core-schema migration, liveness/readiness endpoints, development OpenAPI, safe Problem Details responses, structured logs, and xUnit unit/integration tests.

Implemented locally in Phase 4: Cognito PKCE sign-in integration, protected routes, verified profile bootstrap and database-backed resource authorization. Follow [Phase 4 setup](docs/phase-4.md) to create your user pool and verify real login.

Phase 5 adds workspace/project/task CRUD, team invitations, roles, comments, activity history, task filters and analytics. Follow [the Phase 5 guide](docs/phase-5.md) to restart the API and try the workflows.

Phase 6 adds a drag-and-drop Kanban board, persisted ordering, conflict handling and authorized SignalR updates. See [Phase 6 setup and walkthrough](docs/phase-6.md).

Phase 7 adds private S3 uploads, metadata/checksum verification, authorized downloads and abandoned-upload cleanup. Start with [the step-by-step AWS setup guide](docs/phase-7.md). Phases 5 and 6 were committed together as `ec1f54f`; Phase 7 was committed as `272644e`. Phase 8 was committed as `98ad466`; see [Phase 8](docs/phase-8.md) for the local-container closeout and [Phase 9](docs/phase-9.md) for CI and local verification.

Hosting: React, the API and PostgreSQL run locally. AWS Cognito and S3 integrations have been live-tested. ECS/RDS/CloudFront deployment definitions are prepared but have not been deployed; cloud mutation stages are disabled by default. Existing AWS integrations are not a guarantee of zero AWS charges.

## Start here

**For authentication, start with [the Cognito setup guide](docs/phase-4.md).** Account, Projects and My tasks now require sign-in; the overview and connection checks remain public.

1. Read [development setup](docs/development.md).
2. Review [architecture](docs/architecture.md) and [data model](docs/data-model.md).
3. Follow the [phase plan](docs/roadmap.md).
4. Follow [Phase 2 database setup, migration and testing](docs/phase-2.md).
5. Follow [Phase 3 frontend setup, architecture and tests](docs/phase-3.md).
6. The [Phase 1 report](docs/phase-1.md) preserves the earlier milestone.

## Quick start

Requires .NET SDK 10.0.403, Node.js 24.22.1 with npm 11.19.1, and PostgreSQL 18.6. Run from this repository root. **First start PostgreSQL and set `ConnectionStrings__DevFlow` using the [Phase 2 instructions](docs/phase-2.md).** That guide includes the existing workspace SDK/database setup and Docker instructions.

Terminal 1:

~~~powershell
dotnet restore backend/DevFlow.slnx --locked-mode
dotnet tool restore
dotnet build backend/DevFlow.slnx --no-restore
dotnet ef database update --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api --no-build
dotnet run --project backend/src/DevFlow.Api --launch-profile http
~~~

Terminal 2:

~~~powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
~~~

Open http://localhost:5173 and open **Connection**, then select **Check api** or **Check database**. The API listens on http://127.0.0.1:5080. The frontend development proxy forwards /api requests without requiring permissive CORS.

## Verify

~~~powershell
dotnet build backend/DevFlow.slnx --no-restore
Set-Location frontend
npm.cmd test
npm.cmd run lint
npm.cmd run build
~~~

Backend: GET /api/health checks liveness. GET /api/health/ready checks database connectivity and migration state. GET /openapi/v1.json serves the development OpenAPI document and includes both health endpoints.

Run `dotnet test --solution backend/DevFlow.slnx --no-build --no-restore` with Docker running. In this workspace, set `DEVFLOW_TEST_POSTGRES` using the Phase 2 guide to test against the local PostgreSQL binary instead. Tests create isolated temporary databases and do not modify application data.

## Intended stack

React, TypeScript, Vite, Tailwind CSS, React Router, TanStack Query, dnd-kit; ASP.NET Core, EF Core, SignalR; PostgreSQL; Cognito, S3, RDS, ECS Fargate and CloudFront; Docker and GitHub Actions; xUnit, Vitest and React Testing Library.

Only dependencies needed for the current milestone are installed. Feature libraries and test infrastructure arrive with their phases.
