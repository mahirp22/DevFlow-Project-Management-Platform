# DevFlow

[![DevFlow CI](https://github.com/mahirp22/DevFlow-Project-Management-Platform/actions/workflows/ci.yml/badge.svg)](https://github.com/mahirp22/DevFlow-Project-Management-Platform/actions/workflows/ci.yml)

A full-stack team project management application built as a software engineering portfolio project. Organize workspaces and projects, assign tasks, collaborate on a Kanban board and share private attachments.

**Hosting:** the frontend, API and PostgreSQL run locally. AWS Cognito authentication and S3 attachments are working integrations. ECS/RDS/CloudFront infrastructure is defined but not deployed; paid deployment is disabled by default.

## Features

- Cognito sign-in with authorization code + PKCE and server-validated access tokens.
- Workspaces, projects, invitations and project roles enforced by the API.
- Tasks with priorities, deadlines, assignees, filters and progress analytics.
- Four-column Kanban board with drag-and-drop and labelled Move controls.
- SignalR updates, comments and activity history, with reconnect/polling recovery.
- Conflict-aware task edits and board ordering.
- Private S3 upload/finalize/download flow with size/type/checksum checks.
- Responsive layout, keyboard navigation, lazy project routes and recoverable page-load errors.

## Screenshots

Real screenshots from the locally running application with sample project data. The mobile view was captured using browser device emulation.

![DevFlow workspace overview](docs/images/overview.png)

![Portfolio Launch Kanban board with tasks in all four status columns](docs/images/board2.png)

<details>
<summary>Task details, comments and an S3 attachment</summary>

![Task editor with comments, activity history and a finalized attachment](docs/images/task-details.png)

</details>

<details>
<summary>Project analytics</summary>

![Project totals, progress by status and open work by assignee](docs/images/analytics.png)

</details>

<details>
<summary>Task filtering and personal assignments</summary>

![Tasks filtered to To Do and Medium priority](docs/images/task-filters.png)

![My tasks across projects](docs/images/my-tasks.png)

</details>

<details>
<summary>Project team and roles</summary>

![Project members and roles with email addresses redacted](docs/images/team.png)

</details>

<details>
<summary>Mobile layout</summary>

<img src="docs/images/mobile.png" alt="My tasks in a narrow emulated mobile viewport" width="390">

</details>

## Stack and architecture

| Area | Technologies |
| --- | --- |
| Frontend | React, TypeScript, Vite, Tailwind CSS, React Router, TanStack Query, dnd-kit |
| Backend | C#, ASP.NET Core, EF Core, REST, SignalR |
| Persistence and identity | PostgreSQL, AWS Cognito, AWS S3 |
| Verification and delivery | xUnit, Vitest, React Testing Library, Docker, GitHub Actions |
| Prepared infrastructure | CloudFormation, ECS Fargate, RDS, ALB, S3/CloudFront |

The backend is a layered modular monolith: Domain models, Application contracts, Infrastructure persistence/services, and API endpoints. PostgreSQL stores project data and memberships. The browser authenticates with Cognito; the API authorizes every project operation. File bytes go directly to S3 using constrained signed uploads, then the API verifies them before finalizing metadata. SignalR announces changes and clients refetch authorized data.

See [architecture](docs/architecture.md), [data model](docs/data-model.md), and [portfolio case study](docs/portfolio.md) for design decisions, tradeoffs and implementation evidence.

## Run locally

Requirements: .NET SDK **10.0.403**, Node.js **24.22.1**, npm **11.19.1**, PostgreSQL **18.6**, and Docker Desktop with Linux containers for container tests. Versions are pinned in global.json, .node-version and lock files.

1. Follow [development setup](docs/development.md) and [database setup](docs/phase-2.md). Start PostgreSQL and configure the connection string before EF commands.
2. Configure Cognito using [Phase 4](docs/phase-4.md). Copy frontend/.env.example to ignored frontend/.env.local and use your own public Cognito identifiers. Backend credentials belong in User Secrets or process configuration, never Git.
3. From the repository root, restore/build and apply migrations:

```powershell
dotnet restore backend/DevFlow.slnx --locked-mode
dotnet tool restore
dotnet build backend/DevFlow.slnx --no-restore
dotnet ef database update --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api --no-build
dotnet run --project backend/src/DevFlow.Api --launch-profile http --no-build
```

4. In a second terminal:

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
```

Open **http://localhost:5173**. Vite forwards `/api` and WebSockets to the API on 127.0.0.1:5080. For attachments, complete [S3 setup](docs/phase-7.md); the earlier features remain usable without storage configuration. Cognito/S3 usage is not guaranteed to be free.

If dotnet is not on PATH in the original workspace, use the local SDK setup documented in the phase guides. A fresh clone should install the pinned SDK. Stop Vite before npm ci on Windows if native dependencies are locked.

## Test and package

From the repository root, with Docker Desktop running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify.ps1 -IncludeContainers
```

For a local release ZIP and SHA256 manifest, run `scripts/New-LocalRelease.ps1` instead; it performs verification first and writes only to ignored `.artifacts/`. Nothing is uploaded or deployed.

The last published Phase 9 CI run passed frontend, backend and container jobs: [run 42315418271](https://github.com/mahirp22/DevFlow-Project-Management-Platform/actions/runs/42315418271). Phase 10 adds local frontend regression tests and a 125,000-byte gzip initial-JavaScript budget; its hosted CI status requires a subsequent push. Private repositories skip hosted jobs under the current no-paid-runner policy.

## Portfolio and operations

- [Case study and interview preparation](docs/portfolio.md)
- [Five-minute demo and real screenshot checklist](docs/demo.md)
- [Operations, backups and local releases](docs/operations.md)
- [Phase 10 changes and validation](docs/phase-10.md)
- [Milestone history](docs/roadmap.md)

## Known limits

SignalR currently assumes one API instance, the board is bounded to 500 cards, and attachment malware scanning is not implemented. No production uptime, load-test benchmark, WCAG certification or real-user performance score is claimed. Paid AWS hosting is intentionally deferred; its deployment configuration is not proof of a live deployment. See the case study for the reasoning and next steps.
