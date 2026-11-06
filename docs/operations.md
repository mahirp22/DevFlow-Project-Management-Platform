# DevFlow local operations

The application runs locally. Cognito and S3 are live AWS integrations; ECS/RDS/CloudFront hosting is deferred. The deployment script blocks cloud mutations by default. No CI job has AWS credentials or deploys infrastructure.

## Starting and stopping

Follow the existing environment setup in [development.md](development.md) and [Phase 5](phase-5.md). Start PostgreSQL first, the API second, and Vite third. Browse to http://localhost:5173 to match Cognito callback URLs and S3 CORS. The API listens on 127.0.0.1:5080.

Keep these terminal processes running while using the app. To stop Vite or the API, select that terminal and press Ctrl+C. If Windows asks `Terminate batch job (Y/N)?`, enter Y. Stop PostgreSQL only after the API is stopped. Do not kill unrelated processes just because a port is occupied.

`scripts/Test-Container.ps1` starts disposable containers and deletes its own containers/volumes afterward. It is a test, not your persistent development installation. `scripts/Test-Docker.ps1` similarly uses a uniquely named Compose test project. Do not run `docker compose down --volumes` against your normal development project unless you intend to delete its database.

## Health and diagnostics

Open http://127.0.0.1:5080/api/health for process liveness, and /api/health/ready for database connectivity and migration readiness. Healthy liveness with unhealthy readiness means the API runs but cannot use the expected database schema. Check PostgreSQL, the connection settings and migrations; do not expose credentials when sharing errors.

The API writes structured JSON logs to its terminal. Public error responses intentionally omit provider details. Share the failing operation, HTTP status and trace ID with redacted logs. Never share AWS tokens, database passwords, Cognito authorization codes or presigned attachment URLs. Request URL logging is suppressed because SignalR can use access tokens in the WebSocket query string.

## Backups and recovery

Before a schema change or deleting development data, use PostgreSQL's pg_dump with custom format to create a backup outside the repository. Provide credentials through local environment configuration or a protected PostgreSQL password file, not a command copied into chat. Verify the dump can be listed with pg_restore --list. Restore it into a separate empty test database before relying on it as a backup. A dump file alone is not proof of recovery.

Database backups do not include S3 attachment bytes. The Phase 7 attachment bucket is intentionally unversioned; deleting an object can permanently lose it. Keep separate copies of important demo files. Do not enable bucket versioning without redesigning and testing the attachment implementation, which rejects versioned/suspended buckets.

No production backup/restore rehearsal or disaster-recovery guarantee is claimed. This is a local portfolio environment.

## Dependency updates and verification

Use the pinned Node and .NET versions, npm ci, and locked NuGet restore. Never use npm audit fix --force as an automatic remedy. Review an advisory, select compatible versions, update lock files and rerun meaningful tests before committing.

`scripts/Verify.ps1` checks infrastructure, script guards, frontend dependencies/lint/tests/build, backend dependencies/build and domain unit tests. `-IncludeContainers` additionally runs database integration tests and the Production container smoke test. Audit failures and registry failures stop the script; unavailable audit data must not be reported as a clean audit.

The SDK's NuGet audit checks direct/transitive dependencies at high severity or above. TreatWarningsAsErrors makes those findings fail restore. The npm audit gate also blocks high/critical vulnerabilities; lower-severity findings should still be reviewed. Audit results are time-dependent and not proof that software has no security defects.

## Local release package

With Docker Desktop running, from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-LocalRelease.ps1
```

This first runs all local verification including containers, then publishes optimized framework-dependent API/migrator outputs and packages the frontend build. Output is a uniquely named ZIP and SHA256 file under ignored `.artifacts/`. Nothing is uploaded. release.json records the source commit and whether uncommitted changes were included; a package with dirty sources is not an immutable Git release.

The package contains build outputs, not a configured hosting environment. The API requires ASP.NET Core 10, database/Cognito configuration and AWS credentials for attachments. Serve the frontend and `/api` behind one origin with SPA fallback only for frontend paths and WebSocket forwarding for SignalR. Do not double-click index.html and expect routing/authentication to work. Use the established local development setup for demonstrations until a hosting setup is deliberately tested.

Frontend VITE_* configuration is public and is embedded at build time. Backend credentials must be injected at runtime; they must not be included in packages. The migrator requires administrator database credentials and the application password; the API uses restricted database credentials. For RDS, the certificate path must refer to the verified public RDS CA bundle (the Docker image supplies it); ordinary local PostgreSQL uses the existing local connection string.

## Rollback

Preserve the last tested source commit and database backup. Reverting code does not automatically revert the database. Check compatibility before starting an older API against a newer schema. Restore to a separate database for incompatible changes; never run destructive down-migrations blindly. Keep the existing S3/Cognito resources and local environment configuration when switching source versions.

## GitHub workflow

The public-repository CI runs frontend, backend and container checks with a read-only token and no AWS secrets. Private repositories skip the hosted jobs under the zero-paid-runner policy. Logs are available in GitHub Actions once a run exists. No cache/artifact uploads or production delivery are enabled. A successful local test does not prove the hosted workflow has passed.
