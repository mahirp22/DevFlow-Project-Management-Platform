# Phase 5: project and task management

Implementation and automated checks are complete. This phase adds real workspace, project and task screens backed by PostgreSQL. The user approved the combined Phase 5/6 milestone commit after verification. Phase 6 was subsequently authorized; see [Phase 6](phase-6.md).

## Start the updated application

Keep PostgreSQL running. In VS Code, open the terminal that was running the Phase 4 API and press **Ctrl+C** to stop that API. Keep its existing database/Cognito environment settings or User Secrets. Run these commands from the `devflow` repository root (the folder containing README.md, backend and frontend):

```powershell
dotnet build backend/DevFlow.slnx --no-restore -c Release
$env:ConnectionStrings__DevFlow = (Get-Content "../../work/postgres-connection.txt" -Raw).Trim()
dotnet ef database update --project backend/src/DevFlow.Infrastructure --startup-project backend/src/DevFlow.Api --configuration Release --no-build
dotnet run --project backend/src/DevFlow.Api --launch-profile http --configuration Release --no-build
```

The migration was already applied to the workspace's local PostgreSQL database during verification. Running the update command again is safe; use it if your terminal points to a different database.

If PowerShell says `dotnet` is not recognized, run this first from the repository root to use the existing local SDK:

```powershell
$env:DOTNET_ROOT = (Resolve-Path ../../work/dotnet).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_HOME = [System.IO.Path]::GetFullPath("$PWD/../../work/dotnet-home")
$env:NUGET_PACKAGES = [System.IO.Path]::GetFullPath("$PWD/../../work/nuget-packages")
dotnet --version
```

Do not change APPDATA in your working API terminal: User Secrets must remain in the same profile used during Phase 4. If configuration is missing, use the existing Phase 2 database and Phase 4 Cognito instructions; do not create new credentials.

In a second terminal, from the repository root:

```powershell
Set-Location frontend
npm.cmd run dev
```

If your frontend is already running on 5173, use that instance. Open **http://localhost:5173** and sign in. A browser refresh requires sign-in again because Phase 4 keeps tokens in memory. Do not run npm from the repository root.

## Walk through the features

1. Open **Account** and set up your profile if you have not already done so.
2. Open **Projects**, create a workspace, and select it. Its creator becomes its Owner.
3. Create a project, for example key `DEV` and name `DevFlow`. Keys contain 2–10 letters/digits, start with a letter, and cannot change later.
4. Open the project. Create a task with a title, description, priority, status, deadline and optional assignee. The creator is automatically a project Manager and can assign a task to themselves.
5. Open the task to edit it and post a comment. Its activity history records creation, changed fields and comments. Task references such as `DEV-1` remain stable.
6. Return to the project and try title search and status, priority, assignee and overdue filters. **My tasks** lists your assigned tasks in active projects.
7. Open **Analytics** for status counts, completion percentage, overdue tasks and open workload. Deadlines are dates; overdue means before today's UTC date and not completed.
8. Open **Team**, enter a teammate's email, select a project role and create an invitation. Copy the code immediately and share it yourself. DevFlow does not send invitation email in this phase.
9. Your teammate signs in with that verified email, creates their Account profile, and uses **Projects → Join with an invitation** to paste the code. Use a separate browser profile to try this with a second account.
10. A Viewer can read tasks/comments/analytics but cannot edit. A Member can edit tasks and comment. A Manager can also edit the project, invite and manage project members. Workspace Owners/Admins can manage all workspace projects.
11. Workspace settings allow renaming; only Owners may change workspace roles. The last Owner cannot be demoted. Reassign a project member's tasks before removing them from the project.
12. Project **Settings** supports rename, description, archive/restore and confirmed deletion. Archived projects remain readable but tasks/comments cannot be changed. Task deletion also requires confirmation.

For a conflict check, open the same task in two separately signed-in browser profiles. Save an edit in the first, then save the old version in the second. The API returns 409 and the second form keeps its draft. Copy your edits before selecting **Reload task**. There are no automatic live updates yet.

## Architectural decisions

- Controllers validate request DTOs and translate HTTP. Application interfaces describe use cases; Infrastructure services implement database queries and transactions. EF Core already supplies change tracking and a unit of work, so a generic repository would add indirection without value here.
- Every use case checks database-backed workspace/project permissions. URL nesting is checked against the actual project/task relationship. Foreign or deleted resources return 404; insufficient permissions within a visible resource return 403.
- Task updates/deletes carry the current PostgreSQL `xmin` version. Conflicting edits return 409. Task changes and their activity entries save atomically.
- Project-row locking allocates task numbers safely during concurrent creation. The migration initializes counters from existing tasks. Deleted numbers are not reused.
- Projects and tasks use soft deletion, preserving history and foreign-key integrity. Deleted tasks clear their assignee. Project keys remain reserved after deletion. This phase does not expose restoration of deleted records or permanent retention cleanup.
- Invitations contain 256 bits of random data; only a SHA-256 hash is stored. They expire after seven days, are bound to the recipient's freshly verified Cognito email, and can be accepted once. Acceptance locks the invitation row and creates memberships transactionally. Existing roles are never elevated through a new invitation. Codes travel in POST bodies, not URLs.
- Tasks/comments/activity use bounded pagination. Search currently matches task titles. Analytics computes current status counts and open workload, not historical completion trends. Project/workspace lists are intended for portfolio-scale usage; broader pagination and performance measurement belong to Phase 10.
- TanStack Query owns server data and invalidation after mutations. Runtime parsers validate response shapes. Accessible labels, pending/error states and inline deletion confirmations support the forms. No packages were added or upgraded.

## Files created or modified

All backend paths below are relative to `backend/src`:

| Area | Files |
| --- | --- |
| Domain | New `Entities/ProjectInvitation.cs`, `TaskComment.cs`, `TaskActivity.cs`; updated Project, TaskItem, Workspace, memberships and DomainGuard |
| Application | New `DevFlow.Application/Projects/Contracts.cs` and `Services.cs` |
| Infrastructure | New `Projects/AccessGuard.cs`, `WorkspaceService.cs`, `ProjectService.cs`, `TaskService.cs`, `InvitationService.cs`; updated DI and Identity/ResourceAccess |
| Persistence | Updated DbContext; new CollaborationConfiguration and `20261104035317_ProjectCollaboration` migration/designer; updated model snapshot |
| API | New Workspaces, Projects, Tasks and Invitations controllers; updated Program and ApiExceptionHandler |
| Backend tests | New `backend/tests/DevFlow.IntegrationTests/ProjectWorkflowTests.cs` |
| Frontend | New `frontend/src/features/projects/` pages, forms, query/DTO helpers, task list, team panel, shared UI and workflow tests |
| Frontend integration | Updated App routes, AppLayout, auth return paths, API client, overview copy and index.css |
| Documentation | This guide, README, roadmap, architecture, data-model and development guide |

## API surface

All routes below start with `/api` and require a Bearer access token. Full schemas are available in development at `/openapi/v1.json`.

| Route | Operations |
| --- | --- |
| `/workspaces` | GET list; POST create |
| `/workspaces/{w}` | PUT rename |
| `/workspaces/{w}/members` | GET members; PUT `/{user}` role |
| `/workspaces/{w}/projects` | GET list; POST create |
| `/workspaces/{w}/projects/{p}` | GET, PUT, DELETE |
| `.../{p}/members` | GET; PUT/DELETE `/{user}` |
| `.../{p}/tasks` | GET filtered page; POST create |
| `.../{p}/tasks/{t}` | GET, PUT; DELETE with `?version=...` |
| `.../{p}/tasks/{t}/comments` | GET page; POST comment |
| `.../{p}/tasks/{t}/activity` | GET page |
| `.../{p}/analytics` | GET counts/workload |
| `.../{p}/invitations` | GET latest 100; POST create; DELETE `/{invitation}` revoke |
| `/invitations/accept` | POST code |
| `/me/tasks` | GET filtered assigned tasks |

Create endpoints return 201, updates returning data use 200, and successful empty mutations use 204. Invalid input returns 400; duplicate project keys, stale versions and invalid state transitions return 409.

## Verification

```powershell
# Repository root, with Docker running (or DEVFLOW_TEST_POSTGRES configured per Phase 2):
dotnet build backend/DevFlow.slnx --no-restore -c Release
dotnet test --solution backend/DevFlow.slnx --configuration Release --no-build --no-restore

Set-Location frontend
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

Results: backend build passed with zero warnings/errors; 15 unit tests and 54 integration tests passed, using isolated databases on real local PostgreSQL. All 25 frontend tests passed, lint passed without warnings, and TypeScript/production build passed. Integration coverage includes the CRUD/comment/history/analytics flow, tenant and Viewer isolation, stale writes, concurrent task numbering, invalid input, last-owner protection, verified-email invitation acceptance, expiry/revocation/reuse and archive/deletion behavior. The application migration applied successfully.

The new UI has automated component coverage, but a live two-account Cognito/browser walkthrough and visual review still need your check. Automated tests use test-only signing/provider adapters, not your real account. No Git commit or cloud deployment has been made for Phase 5.

## Display-name follow-up (November 5, 2026)

The user confirmed that walkthrough steps 1?12 work, including invitations and role changes. They found that accounts without a Cognito name all displayed the fallback ?DevFlow member?. Account now includes a Display name field and Save name button, backed by authenticated PUT /api/me. Each user edits only their own name (1?100 characters); email, identity and roles remain server-controlled. Existing memberships, comments and assignments read this profile name, so no migration or account recreation is needed. Names are application profile data and do not change Cognito attributes. The user confirmed the live name-edit flow works on November 5, 2026.

Changed for this fix: Domain UserProfile, Application IdentityContracts, Infrastructure UserProfiles, API MeController, frontend AuthPages, and backend/frontend regression tests.
