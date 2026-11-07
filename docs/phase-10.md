# Phase 10: performance, usability and portfolio preparation

Implementation and local automated checks are complete. Changes are uncommitted and have not been pushed. Paid cloud deployment remains disabled. No new dependency or database migration was added.

## Improvements

Project, task, workspace, invitation and My tasks pages now load on demand with React.lazy. Suspense displays a loading status inside the existing application shell, preserving navigation. A route-keyed error boundary catches failed page downloads/rendering and offers reload or navigation back to the overview without exposing exception details in the page.

Escape closes the expanded mobile menu and returns focus to its button. Existing skip links, visible focus, route titles/focus, form labels and non-drag board controls remain. The overview now describes available features instead of promising features that already exist.

The README now describes the actual product, supported setup, CI evidence, known limits and hosting status. The portfolio case study includes architecture tradeoffs and truthful resume/interview material. The demo guide provides realistic sample tasks, a five-minute recording outline and a checklist for real screenshots.

## Measured performance

| Metric | Phase 9 baseline | Phase 10 |
| --- | --- | --- |
| Entry JavaScript, Vite gzip estimate | 137.72 kB | 115.60 kB |
| Entry JavaScript, uncompressed | 469.71 kB | 382.85 kB |

The Vite estimate is approximately 16% lower for initial gzipped JavaScript. Project and board code still downloads when visited; this is not a reduction in all application code or a measured page-load speed improvement.

`scripts/check-bundle.mjs` reads the Vite manifest and follows all static imports with deduplication. It sums Node gzip sizes for the initial JavaScript graph and fails above 125,000 bytes. The observed Node measurement is 114,465 bytes, distinct from Vite's gzip estimate; do not mix compression methods in a comparison. Dynamic route imports and CSS are not part of this JavaScript budget. CI and the local verification script enforce it after the production build.

Backend review confirmed existing bounded pagination/no-tracking reads and a 500-card board limit. No speculative indexes or caches were added without query-plan/load evidence. Existing 100 ms notification coalescing and 30-second recovery polling remain unchanged.

## Validation

- Frontend lint: passed.
- Frontend tests: 39 passed, including navigation through lazy routes, Escape focus recovery, and rejected-page-import recovery.
- TypeScript and production build: passed.
- Initial JavaScript budget: passed (114,465 / 125,000 bytes).
- Updated workflow: actionlint accepted it; PowerShell verification script syntax checked.
- Backend code/schema/authentication behavior did not change. The previous published CI run passed its backend/container checks; these are historical evidence, not a claim that the new frontend changes already ran on GitHub.

No screen reader, real mobile-device, visual browser, Lighthouse, production load or hosted Phase 10 CI result is claimed. The [demo checklist](demo.md) makes those manual checks explicit. Screenshots/video are optional portfolio assets to capture from the real application, not fabricated deliverables.

## Run and review

From the repository root:

```powershell
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend test -- --pool=threads --maxWorkers=2
npm.cmd --prefix frontend run build
node scripts/check-bundle.mjs
```

To run the application, keep PostgreSQL/API running and start `npm.cmd run dev` from frontend. Navigate from Overview to Projects and My tasks. At a narrow width, open Menu, tab into it, then press Escape. Confirm focus returns to Menu. The [demo guide](demo.md) covers a fuller walkthrough.

For the complete suite and a new local package, stop Vite first on Windows, keep Docker Desktop running, and run `scripts/New-LocalRelease.ps1`. It now includes the bundle budget. This is optional re-verification of unchanged backend/container features before release, not a claim it was rerun in the agent environment.

## Changed files

- App.tsx: lazy project routes; AppLayout.tsx: Suspense, error boundary and Escape handling.
- PageBoundary.tsx and tests: recoverable page failures; App.test.tsx: asynchronous route assertions and keyboard coverage.
- WorkspacePages.tsx: current feature wording.
- vite.config.ts: build manifest; scripts/check-bundle.mjs: initial-download budget.
- CI workflow and Verify.ps1: budget enforcement.
- README, roadmap, Phase 9 closeout, this report, portfolio.md and demo.md: accurate evidence and presentation guidance.

Next release step: user reviews changes and approves a commit/push. Then verify the new GitHub Actions run before describing the Phase 10 commit as hosted-CI-verified. No further development phase or paid hosting is started automatically.
