# DevFlow

A cloud-based project management platform built incrementally as a software engineering portfolio project.

**Current milestone: Phase 1 — project setup and architecture.**

Implemented: strict React/TypeScript starter, API connection check, ASP.NET Core liveness endpoint, development OpenAPI document, Problem Details handling, structured console logs, layered solution, pinned dependencies, and engineering documentation.

Not yet implemented: persistence, authentication, workspace/project/task features, Kanban, real-time updates, uploads, or cloud deployment. This starter is for local development and must not be exposed as a production service.

## Start here

1. Read [development setup](docs/development.md).
2. Review [architecture](docs/architecture.md) and [data model](docs/data-model.md).
3. Follow the [phase plan](docs/roadmap.md).
4. See [Phase 1 changes and verification](docs/phase-1.md).

## Quick start

Requires .NET SDK 10.0.403 and Node.js 24.22.1 with npm 11.19.1. Run from this repository root.

Terminal 1:

~~~powershell
dotnet restore backend/DevFlow.slnx --locked-mode
dotnet run --project backend/src/DevFlow.Api --launch-profile http
~~~

Terminal 2:

~~~powershell
Set-Location frontend
npm ci
npm run dev
~~~

Open http://127.0.0.1:5173 and select **Check API connection**. The API listens on http://127.0.0.1:5080. The frontend development proxy forwards /api requests without requiring permissive CORS.

## Verify

~~~powershell
dotnet build backend/DevFlow.slnx --no-restore
Set-Location frontend
npm run lint
npm run build
~~~

Backend: GET /api/health returns HTTP 200 with {"status":"Healthy"}. GET /openapi/v1.json serves the development OpenAPI document; health middleware is deliberately outside its controller contract.

## Intended stack

React, TypeScript, Vite, Tailwind CSS, React Router, TanStack Query, dnd-kit; ASP.NET Core, EF Core, SignalR; PostgreSQL; Cognito, S3, RDS, ECS Fargate and CloudFront; Docker and GitHub Actions; xUnit, Vitest and React Testing Library.

Only dependencies needed for the current milestone are installed. Feature libraries and test infrastructure arrive with their phases.
