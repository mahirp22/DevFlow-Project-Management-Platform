# Phase 9: automated verification and delivery preparation

Started with user approval on November 6, 2026, after Phase 8 commit `98ad466`. Paid AWS hosting remains deferred. Phase 9 implementation and the full local release workflow are verified; the first hosted CI run remains pending publication.

## Added in this increment

- `.github/workflows/ci.yml`: frontend install/lint/tests/build, backend locked restore/build/unit/database integration tests, infrastructure/guard tests, and a non-root Docker image build with migration/readiness checks. The container job runs only after frontend/backend checks pass.
- GitHub Actions are pinned to verified release commit SHAs. Workflows have read-only repository permissions, no AWS secrets, no deployment or registry push, bounded timeouts and cancellation of superseded runs. Pull requests use `pull_request`, not privileged `pull_request_target`.
- Hosted jobs run only when repository visibility is public, on standard ubuntu-24.04 runners. Private/internal repositories skip all jobs to avoid consuming paid runner minutes. Caching and artifact uploads are omitted to avoid introducing hosted storage usage. This workflow verifies build outputs but does not claim automated production delivery.
- `scripts/Verify.ps1`: repeatable local checks with SDK discovery, isolated Verification build outputs and optional Docker integration/container testing. It never calls AWS. It reports explicitly when container checks are skipped.
- A pull request template asks for the behavior change, actual validation, and relevant deployment/data concerns.

No new application dependencies were installed. Existing versions and lock files remain in use. The CI database is a disposable Testcontainers PostgreSQL instance, with generated test credentials. AWS Cognito/S3 behavior is mocked in integration tests; the pipeline does not use your real account.

## Run locally

In a new VS Code PowerShell terminal at the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify.ps1
```

This installs locked frontend dependencies, checks frontend/infrastructure/scripts, builds backend projects and runs domain unit tests. It compiles integration tests but does not run database/container tests unless requested. Installation needs internet access; stop Vite if npm reports locked dependency files, then restart it afterward.

For full validation, start Docker Desktop and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify.ps1 -IncludeContainers
```

Tests build to separate Verification directories, preserving your running Debug backend. Testcontainers and the container smoke script use disposable databases; do not supply your real PostgreSQL credentials. SDK fallback recognizes the current workspace's `../../work/dotnet` installation; a fresh checkout should install the SDK from global.json.

## GitHub setup remains pending

The user created the public repository https://github.com/mahirp22/DevFlow-Project-Management-Platform. It is configured as origin and currently contains its initial README commit. No project code has been pushed by the agent. The initial remote history must be preserved when combining it with the local project. A public repository is suitable for a portfolio only after you review the files/history; ignored environment files and local AWS credentials must remain private.

Once you approve publishing to a public GitHub repository, push the reviewed commits. In its Actions tab, open DevFlow CI and inspect frontend, backend and container jobs. All three must pass before claiming CI verification. GitHub settings must permit Actions; organization policies may restrict which actions can run. Branch protection should then require these three checks and a pull request before merging.

GitHub's standard hosted runner usage for public repositories is free under its current policy; see [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions). This does not promise that existing AWS services or unrelated workflows are free. No private repository runner execution is enabled here.

## Remaining Phase 9 work

1. Validate the new full local verification script with Docker on the user's machine and run the workflow on GitHub after publication is authorized.
2. Run the implemented scripts/New-LocalRelease.ps1 end to end with Docker; the local-only packaging workflow and docs/operations.md are now written.
3. Complete the NuGet audit in the user environment. The npm audit reported zero vulnerabilities; the agent NuGet audit failed to load its service index (NU1900), so no clean backend audit is claimed.
4. Finalize documentation and obtain approval before committing Phase 9 or starting Phase 10.

The earlier user-confirmed Docker smoke test and Phase 8 regression results remain valid evidence for those features, but do not prove this new GitHub workflow has run successfully.

Validation this increment: actionlint 1.7.12 accepted the workflow (binary checksum verified against the official release; optional shellcheck integration disabled). Six infrastructure tests, deployment guard/offline PowerShell tests, Verify.ps1 syntax and frontend lint passed. The complete new local verification script and GitHub-hosted jobs have not yet run end to end.

Frontend verification: all 37 tests and the production build passed. The default fork worker pool could not start in the agent environment; thread workers with a maximum of two passed. CI and Verify.ps1 now use that tested command while retaining test-file isolation. No application code changed.

Latest changes: CI and Verify.ps1 now gate high/critical frontend and backend advisories. scripts/New-LocalRelease.ps1 runs full verification before creating a local ZIP/checksum and source manifest under ignored .artifacts/. docs/operations.md covers startup, health, backups, release requirements and rollback. New scripts passed syntax checks; updated CI passed actionlint. Packaging and the full Docker verification remain unexecuted here because Docker is inaccessible, even after a filesystem permission grant. No tests or audits were bypassed to mark the phase complete.

The user authorized commit/push only if Phase 9 is complete and issue-free. That condition has not yet been established; no Phase 9 commit, merge or push has been performed.

## User-confirmed end-to-end verification (November 6, 2026)

The user supplied the complete New-LocalRelease.ps1 output: six infrastructure tests, deployment-guard checks, npm audit (zero vulnerabilities), frontend lint/build and 37 tests, successful audited locked NuGet restore, 15 unit tests, 72 Docker-backed integration tests, image build, repeat migrations, Production readiness and denied application DDL all passed. API/migrator publishing and ZIP creation also succeeded. The package's uncommitted-changes warning is accurate source provenance, not a test failure. This supersedes the earlier pending local checks and agent-only NU1900 limitation. No paid AWS deployment occurred.

The user's conditional approval to commit and publish is now being applied to this locally verified increment. Hosted CI still requires checking after the first push; it is not claimed to have passed yet.
