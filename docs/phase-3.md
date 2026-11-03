# Phase 3: React frontend foundation

## Delivered

A responsive workspace shell with a teal visual theme, desktop sidebar, mobile menu, overview, project/task empty states, connection page, and a helpful 404 route. Links use React Router; navigation updates the document title and moves keyboard focus to the main content. A skip link, visible focus indicators, status announcements and reduced-motion styling support accessibility.

Project/task creation and authentication remain in their approved future phases. The overview illustration is decorative, not fabricated application data. No accounts, projects or tasks are persisted by this phase.

## Architecture and decisions

- `src/main.tsx`: one QueryClient per application session and a BrowserRouter around the app.
- `src/App.tsx`: route definitions, independent from providers so tests can use MemoryRouter.
- `src/components/AppLayout.tsx`: shared navigation, responsive layout, route title/focus handling.
- `src/pages/WorkspacePages.tsx`: overview, project/task placeholders and not-found screen.
- `src/features/connection/ConnectionPage.tsx`: explicit API liveness and database readiness requests. Responses are checked at runtime before displaying success.
- `src/lib/api.ts`: small fetch client returning `unknown`, bounded eight-second timeout, caller cancellation, safe HTTP errors and Problem Details trace IDs. TypeScript alone cannot validate server JSON, so features narrow the response before use. Future mutations/authentication extend this client when needed.
- `src/lib/queryClient.ts`: shared caching policy. Automatic retries are off for now so explicit connection checks have predictable behavior. Future queries can override defaults.
- `src/index.css`: Tailwind integration, shared component styles and responsive breakpoints. No extra component framework.
- `src/test/setup.ts`, `src/App.test.tsx`, `src/lib/api.test.ts`: isolated RTL/Vitest tests with mocked HTTP boundaries and cleanup after every test.

The browser sends `/api/...` to Vite on port 5173; Vite forwards it to ASP.NET Core on port 5080. Only the backend accesses PostgreSQL. The database readiness endpoint checks both connectivity and pending migrations. A healthy API alone does not mean the database is ready.

`VITE_API_BASE_URL` defaults to `/api`. Values are public and compiled into the bundle: never put secrets in them. A separate API origin would require explicit backend CORS configuration. The planned production deployment should route `/api` to the backend and frontend routes to the SPA. CloudFront must support direct navigation to routes such as `/projects` without rewriting API errors to HTML.

## Dependency compatibility

Verified npm stable versions and engine/peer constraints before installation on Node 24.22.1 with React 19.3.1 and Vite 8.3.5:

| Dependency | Version |
| --- | --- |
| React Router | 8.5.1 |
| TanStack Query | 5.105.3 |
| Tailwind CSS / Vite plugin | 4.3.4 |
| Vitest | 5.1.0 |
| React Testing Library | 16.3.3 |
| user-event | 14.6.8 |
| jest-dom | 7.0.1 |
| jsdom | 30.1.3 |

Exact direct versions and the npm lockfile make installs reproducible. No peer conflicts or vulnerabilities were reported during installation. References: [Tailwind Vite integration](https://tailwindcss.com/docs/installation/using-vite), [TanStack Query installation](https://tanstack.com/query/latest/docs/framework/react/installation), [Vitest documentation](https://vitest.dev/guide/).

## Run and test

From the repository root, in a VS Code terminal:

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
```

Open http://127.0.0.1:5173. Using `npm.cmd` avoids PowerShell's blocked `npm.ps1` without changing your execution policy. The frontend can be explored without a running backend. For successful connection checks, start PostgreSQL, apply migrations and start the API using [Phase 2](phase-2.md).

```powershell
# In frontend/
npm.cmd test
npm.cmd run lint
npm.cmd run build
# Optional interactive test runner:
npm.cmd run test:watch
```

Manual walkthrough:

1. Follow Explore projects, My tasks and Connection; use browser Back/Forward.
2. Open `/projects` directly and refresh. Open an unknown route and return home.
3. Resize below 768px; expand Menu, follow a link and confirm the menu closes.
4. Tab through links and buttons; check visible focus and the skip link.
5. On Connection, check API and Database independently. Stop a service, retry, then restart it and retry again.

## Verification and limits

- All 11 frontend tests passed: routing/title/focus, mobile menu state, pending requests, independent checks, retry recovery, unexpected payloads, HTTP errors, malformed JSON, network failures and cancellation.
- Strict TypeScript and production Vite build passed; lint passed.
- Local Vite server started and `/projects` returned HTTP 200.
- Live `/api/health` through Vite returned `Healthy`. Database readiness returned HTTP 503 while the local database was unavailable. Frontend retry/error behavior is covered by automated tests; the earlier Phase 2 database verification remains documented separately.
- Browser automation was unavailable, so rendered desktop/mobile appearance and a full real-browser accessibility walkthrough remain manual review items. jsdom does not verify CSS layout.

Modified existing files: `frontend/package.json`, `frontend/package-lock.json`, `frontend/vite.config.ts`, `frontend/.env.example`, `frontend/src/App.tsx`, `frontend/src/main.tsx`, `frontend/src/index.css`, `README.md`, `docs/development.md`, `docs/roadmap.md`. New source/test files are listed in the architecture section above, plus this guide.

Phase 3 implementation is complete with the visual-review limitation above. The user confirmed the frontend runs and approved the milestone commit after checks passed. Phase 4 requires separate approval.
