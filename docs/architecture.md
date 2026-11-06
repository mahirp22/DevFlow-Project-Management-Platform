# Architecture

## System boundary

DevFlow is a modular monolith: one independently deployed browser frontend, one API service and one relational database. A monorepo keeps changes to contracts and consumers reviewable together. Modules follow workspace, project, task and collaboration use cases.

~~~text
Browser -- HTTPS --> CloudFront --> private frontend S3 bucket
Browser -- authorization code + PKCE --> Cognito
Browser -- REST / SignalR --> HTTPS load balancer --> ECS Fargate API
API -- EF Core / Npgsql --> private RDS PostgreSQL
API -- IAM task role --> private attachment S3 bucket
Browser -- authorized presigned URL --> attachment S3 bucket
API -- structured logs --> CloudWatch
~~~

ECR stores container images. Secrets Manager holds runtime secrets. Deployment infrastructure is defined in Phase 8; no cloud resources exist yet.

## Backend boundaries

Domain contains entities and business rules with no infrastructure dependencies. Application contains use cases, DTOs, validation and focused interfaces. Infrastructure implements persistence and external services. API handles HTTP, identity, authorization policies, SignalR transport and dependency injection composition.

References: Application -> Domain; Infrastructure -> Application and Domain; API -> Application and Infrastructure. The API's Infrastructure reference exists for composition. Controllers call use cases rather than querying the database. Phase 2 adds core domain entities, EF mappings and health infrastructure; business use cases remain for later phases.

Use scoped services for request use cases and EF DbContext when introduced. Avoid generic repositories that merely duplicate DbSet and SaveChanges. Add narrowly scoped query/storage interfaces when they clarify a boundary. No mediator framework or message broker is required at this stage.

## Request lifecycle

Validate DTO -> identify caller -> verify workspace/project access -> execute use case -> save transactional changes -> map response DTO. A task mutation and its activity entry commit together. Return 201 plus Location for creation, 204 for deletion, 400 for validation, 401 for missing/invalid identity, 403 for denied access, 404 for missing resources and 409 for concurrency conflicts. Resource-disclosure policy will be consistent across endpoints.

Use Problem Details for errors and structured logs with trace/request identifiers. Never log access tokens, invitation tokens or secrets. Development OpenAPI describes controller contracts; it currently has no business operations because none exist yet.

## Identity and tenant isolation

Cognito owns registration, credentials and login. The SPA uses authorization code + PKCE with a public client. The backend validates access-token signature, issuer, lifetime, token_use and intended client/resource before authorizing application data. Cognito subject maps to UserProfile; email is not the identity key. Phase 4 uses an in-memory OIDC user store, sessionStorage only for authorization transaction state, and no browser client secret. Reload/expiration requires sign-in again; query data clears on logout/expiry. See [Phase 4](phase-4.md) for offline JWT revocation limitations.

Workspace is the tenant boundary. Project membership gates collaboration. Workspace owners/admins can administer projects; project managers manage membership/content; members collaborate; viewers read. Every data operation and SignalR group subscription checks access server-side. URL identifiers and UI visibility are never authorization.

## Frontend

React renders UI; strict TypeScript checks source contracts. Runtime responses still need validation where appropriate. React Router handles navigation; TanStack Query owns server-state caching and mutations. Feature-local components and hooks keep code near its use case. Shared UI is extracted when genuinely reused. Tailwind and responsive layout were added in Phase 3. Phase 4 adds protected routes and Cognito session handling; server authorization remains authoritative.

## Real-time and consistency

REST persists mutations; SignalR notifies authorized project subscriptions after commit. Phase 6 uses a per-connection registry and rechecks database access before notification; it does not rely on potentially stale group membership. Reconnects refetch authoritative state. See [Phase 6](phase-6.md) for bounded ordering, revision checks, draft preservation and single-instance delivery limitations. Optimistic moves roll back on error; concurrency tokens prevent silent overwrites. Best-effort notifications can be missed between commit and broadcast, so reconnect/refocus refetch is essential. An outbox is a future option if durable event delivery becomes necessary.

Start with one ECS task. Multiple replicas require a shared SignalR backplane and connection affinity as appropriate to transport configuration. Do not claim horizontal real-time scaling without implementing that infrastructure.

## Attachments

API authorizes an upload, creates a pending record and issues a short-lived presigned URL for a server-generated object key. The browser uploads directly; finalization verifies object metadata and size. Only finalized objects are downloadable through authorized short-lived links. Define file limits, safe download headers and abandoned-upload cleanup in Phase 7. Store object keys, not permanent public URLs.

## Database and deployment

EF Core migrations live in Infrastructure. No browser connects to PostgreSQL. Local PostgreSQL runs in Docker starting in Phase 2; RDS runs privately in AWS. Migrations execute as an explicit deployment step, not independently on every API replica. GitHub Actions will use OIDC for AWS access rather than long-lived access keys.

## Phase 5 use cases

Application/Projects defines validated requests, response DTOs and use-case interfaces. Infrastructure/Projects implements permission-checked EF Core queries and transactions. Thin API controllers supply the authenticated subject. The React projects feature owns forms, runtime response parsing and TanStack Query invalidation. Tasks use xmin for stale-write detection; row locks protect numbering and invitation acceptance. See [Phase 5](phase-5.md) for the concrete routes, deletion model and tests.

## Phase 7 attachments

The storage interface isolates S3 signing/metadata/deletion from task authorization and persistence. Direct browser POST uploads have exact size, type and checksum conditions. Finalization checks S3 metadata; only ready records can issue a 60-second download grant. Dedicated unversioned SSE-S3 storage is required for this milestone. An hourly bounded worker cleans abandoned uploads and retains tombstones for retries. See [Phase 7](phase-7.md) for IAM, CORS, login, limits and live setup.
