# Phase 6: Kanban and real-time collaboration

Phase 6 implementation and automated verification are complete. The user confirmed Phase 5's two-account walkthrough and display-name fix before authorizing this phase. The user confirmed the live two-account walkthrough (steps 1?8), approved committing Phases 5 and 6, and authorized Phase 7.

## Start the updated app on this computer

1. Keep PostgreSQL running.
2. In VS Code, find the terminal displaying API logs and press **Ctrl+C**. Keep using that same terminal so the working Cognito settings remain available. If port 5080 is occupied, stop the previous DevFlow API before starting another instance.
3. From the repository root (the folder containing backend, frontend and README.md), paste this complete setup block:

```powershell
$env:DOTNET_ROOT = (Resolve-Path "../../work/dotnet").Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_HOME = [System.IO.Path]::GetFullPath("$PWD/../../work/dotnet-home")
$env:NUGET_PACKAGES = [System.IO.Path]::GetFullPath("$PWD/../../work/nuget-packages")
$env:ConnectionStrings__DevFlow = (Get-Content "../../work/postgres-connection.txt" -Raw).Trim()
dotnet build backend/DevFlow.slnx --no-restore --configuration Release
```

4. After **Build succeeded**, run:

```powershell
dotnet run --project backend/src/DevFlow.Api --launch-profile http --configuration Release --no-build
```

5. Leave this terminal open when it says **Now listening on: http://127.0.0.1:5080**.
6. In the frontend terminal, press **Ctrl+C** if Vite is already running. From the repository root, run:

```powershell
Set-Location frontend
npm.cmd run dev
```

7. Open **http://localhost:5173**, sign in, and open your project. Click its new **Board** tab.

No Phase 6 database migration is necessary: the existing task Position and xmin columns support ordering and conflict detection. Phase 5's migration must already be applied. On a different computer, install the pinned .NET SDK and use your own database/Cognito configuration instead of the workspace-specific setup block. Run `npm.cmd ci` in frontend to restore the committed dependency lockfile when checking out the project elsewhere.

## Verify the board and live updates

1. Sign into your original account in a normal browser window. Open **Phase 5 Demo → Board**.
2. Open a private browser window, sign into your second account, and open the same board. Make sure that account has **Member** permission, as configured during Phase 5.
3. Both windows should show **Updates: Live** above the project tabs. Keep them open side by side.
4. Create at least three tasks using the **Tasks** tab, then return to **Board**. The four columns are **To Do**, **In Progress**, **In Review**, and **Completed**.
5. Drag a task using its **⠿ handle**. Drop it onto the **Drop at end** area in another column. An empty column has a **No tasks yet — drop here** target.
6. Confirm the task appears in its new column in both windows without refreshing the page.
7. Put two tasks into one column. Drag the later task onto the **Before DEMO-n** target above the other card. Confirm the new order appears in both windows and survives **Reload board**.
8. Try the non-drag alternative: expand **Move task** on a card, select its destination column and position, then click **Move DEMO-n**. These controls work with keyboard, mouse or touch. You can also focus the drag handle and use the library's keyboard interaction; the explicit move form is the simpler alternative.
9. Open a task in one window. In the other window, change its priority or add a comment. Comments/activity refresh automatically. An open edit form preserves its original draft/version and shows a newer-changes message instead of silently replacing your input. Copy any draft edits before selecting **Reload task**.
10. As the owner, change the second account to **Viewer** under **Team**. The second window should lose editing/move controls after the update. Change it back to **Member** to continue testing.
11. Test reconnect: stop the API with **Ctrl+C**, then restart it with the command above. The indicator should change through offline/reconnecting and return to **Live**. The board refetches on subscription/reconnection, recovering changes missed during disconnection.
12. To test rollback, attempt moves from two clients holding the same old board revision (for example, temporarily disable network access in one browser's developer tools, then restore it just before moving). The stale request returns 409, the optimistic preview is removed, and the board reloads with an explanatory message. Automated tests verify this deterministically; reproducing the timing manually is optional.

Tokens still expire after the short Phase 4 lifetime. If prompted, sign in again. No browser tokens are persisted. Member removal also invalidates an open project view, but reassignment is required before removing a member with assigned tasks.

## Architecture and tradeoffs

- **REST saves; SignalR notifies.** `GET .../projects/{p}/board` returns ordered cards and a revision. `PUT .../projects/{p}/board/{task}` takes `status`, optional `beforeTaskId` (null means end), and `revision`. All writes enforce the same database-backed permissions as Phase 5.
- **Transactional ordering.** A project-row lock serializes task creation, updates, deletion and board moves. Reordering assigns consecutive positions in the destination column. Legacy equal positions use task number as the stable tie-breaker. Direct status changes append the task to its new column.
- **Conflicts are explicit.** The revision hashes task IDs and xmin versions from one task snapshot. A stale snapshot returns 409. The frontend shows an immediate preview, then adopts the saved state or rolls back/refetches on failure. This intentionally detects any task change, not only changes to the card being dragged.
- **Bounded boards.** Boards support up to 500 active tasks per project. Larger projects receive a clear message directing them to the paginated task list, rather than silently hiding cards. Renumbering a bounded column is simpler to understand and maintain than fractional ranks.
- **Authenticated subscriptions.** SignalR uses `/api/hubs/projects`. A connection subscribes to one workspace/project and verifies read permission. Each notification rechecks current access; revoked/deleted projects cause `AccessRevoked` and client cache removal. Expired subscriptions receive no notifications, and transport connections close on authentication expiration.
- **No content broadcasts.** The server sends only `ProjectChanged` or `AccessRevoked`, with no task bodies, names, tokens or email addresses. Clients retrieve fresh data through authorized REST endpoints. MVC's notification filter runs only after successful use-case completion/commit, including comments, role changes, archive/delete, and profile renames. A delivery failure does not turn an already committed write into an HTTP failure.
- **Best-effort delivery.** There is no durable event queue. Subscription/reconnection, window focus and a 30-second visible-page fallback refetch recover missed notifications. Initial connections and exhausted reconnect attempts retry. The hook stops connections and timers when leaving a project or signing out.
- **Drafts stay local.** Live query refreshes never replace an open task editor's draft or silently advance its expected version. Explicit reload is required to adopt another writer's changes.
- **Single API instance.** The in-memory connection registry deliberately targets the initial one-instance deployment. Multiple ECS replicas require a distributed subscription/delivery design and appropriate transport routing before scaling. This phase does not claim multi-instance live delivery.
- **Token logging.** Browser WebSocket connections send an access token in the query string. The JWT handler accepts this only on the exact hub path. Request URL logging is suppressed for ASP.NET Core Hosting.Diagnostics; SignalR client logging is disabled. Future reverse-proxy/load-balancer access logs must also redact query tokens. Production requires HTTPS/WSS.

## Dependencies and files

Installed and pinned `@dnd-kit/react` 0.5.3 and `@microsoft/signalr` 10.0.11. Registry metadata confirms dnd-kit supports React 18/19; SignalR 10 aligns with the existing ASP.NET Core 10 backend. The server's SignalR components are already included in the ASP.NET shared framework, so no backend package was added. The board is lazy-loaded to keep drag-and-drop code out of the initial app bundle.

Created:

- `backend/src/DevFlow.Infrastructure/Projects/BoardService.cs`
- `backend/src/DevFlow.Api/Controllers/BoardController.cs`
- `backend/src/DevFlow.Api/Collaboration/ProjectHub.cs` and `ProjectNotificationFilter.cs`
- `backend/tests/DevFlow.IntegrationTests/BoardTests.cs` and `RealtimeTests.cs`
- `frontend/src/features/projects/KanbanBoard.tsx`, `board.ts`, `useProjectLive.ts`, `board.test.tsx`, `useProjectLive.test.tsx`
- This guide.

Modified: task domain behavior, application contracts/interfaces, Infrastructure DI/TaskService, API Program/JWT setup, ProjectPage/TaskPage/TaskForm, frontend CSS, Vite WebSocket proxy, package/lockfile, README, architecture, data-model and roadmap. Phase 5 changes remain uncommitted alongside these additions.

## Automated verification

From the repository root, with Docker running or the external PostgreSQL test connection configured as in Phase 2:

```powershell
dotnet build backend/DevFlow.slnx --no-restore --configuration Release
dotnet test --solution backend/DevFlow.slnx --configuration Release --no-build --no-restore
Set-Location frontend
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

Verification: 60 integration tests passed against isolated real PostgreSQL databases, including actual SignalR WebSocket handshakes, subscription authorization, post-commit delivery and membership revocation. Board tests cover ordering, persistence, stale revisions, simultaneous writers, invalid destinations, Viewer restrictions and archive behavior. The 33 frontend tests cover move controls, optimistic rollback, draft preservation, reconnect/resubscribe and access-revocation navigation. Backend unit tests: 15 passed. Backend build has zero warnings/errors; frontend lint, TypeScript and production build pass. npm reported zero known dependency vulnerabilities at installation time.

The native computer-use connection was unavailable, so visual pointer/touch/keyboard drag behavior and a live two-account Cognito session remain for the walkthrough above. Component tests mock drag geometry; they do not replace this browser check. No migration, AWS deployment, or Git commit was performed in Phase 6.

References: [dnd-kit React quickstart](https://dndkit.com/react/quickstart/), [SignalR authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0).

Live verification: the user confirmed steps 1?8 of the guided two-account walkthrough passed, including drag-and-drop, saved ordering, live comments and reconnection. This is user-reported browser verification, separate from the automated checks above.
