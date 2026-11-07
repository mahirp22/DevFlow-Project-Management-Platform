# DevFlow portfolio case study

## Problem and implementation

DevFlow gives a team a shared place to manage projects, assign tasks, track work across four Kanban statuses and discuss changes. It is a student portfolio application, with a React/TypeScript frontend, an ASP.NET Core API, PostgreSQL persistence and working AWS Cognito/S3 integrations.

Team workspaces and project memberships define access. Managers can invite collaborators; viewers can read project data and download authorized attachments while write operations remain forbidden. Task edits use concurrency checks. SignalR broadcasts change notifications so clients refetch authorized data, with polling/reconnect recovery for missed events.

Hosting is local. CloudFormation definitions for ECS Fargate, RDS, ALB, private frontend S3 and CloudFront are prepared but were not deployed, by choice to avoid paid hosting. AWS authentication and attachment storage were live-tested. Do not describe the app as running on ECS/RDS/CloudFront.

## Engineering decisions to explain

| Decision | Reason and tradeoff |
| --- | --- |
| Layered modular monolith | Clear Domain/Application/Infrastructure/API responsibilities without distributed-service overhead. EF Core provides data access; generic repositories were not added without value. |
| Cognito authorization code + PKCE | Browser sign-in uses a public client without embedding a secret. The API verifies access-token issuer, signature, expiry, token type and client identity. Project roles remain database-backed. |
| Concurrency checks and transactions | A stale task edit or board move is rejected instead of silently overwriting another user's change. Project-level locks serialize ordering changes, trading write concurrency for understandable consistency. |
| SignalR notifications plus refetch | Messages announce changes rather than containing tenant data. Authorization is checked server-side. Polling/reconnect recovers missed notifications; this is not a durable event stream. |
| Signed S3 upload + verification | The API authorizes an upload, constrains its key/size/type/checksum and verifies metadata before finalization. File bytes bypass the API. Short-lived signed URLs remain usable until expiry. |
| Separate migration and application credentials | Only the one-off migration process changes schema. Readiness can read migration history, but the API cannot modify it or create tables. |
| Route code splitting | Project code and drag-and-drop tools load when needed. Loading/error UI keeps navigation available if a download fails. Additional first-visit requests are a tradeoff. |
| Reproducible local and CI checks | Lock files, pinned tooling/actions, Docker-backed PostgreSQL tests and a local release manifest make verification repeatable. CI does not deploy to AWS. |

## Evidence, with limits

- Phase 9 local release passed 37 frontend, 15 domain unit, 72 database/API integration and 6 infrastructure tests, plus audits, lint, build and container smoke checks.
- The first public GitHub Actions run passed frontend, backend and container jobs on commit `2db5321`: [run 42315418271](https://github.com/mahirp22/DevFlow-Project-Management-Platform/actions/runs/42315418271).
- Phase 10 adds two frontend checks for Escape/focus behavior and failed lazy-page recovery: 39 frontend tests now pass locally. This is not evidence of new hosted CI until the changes are pushed.
- The initial production JavaScript bundle fell from 137.72 kB gzip at the Phase 9 baseline to approximately 115.60 kB after route splitting, about 16%. Measurements are Vite production-build output, not Lighthouse, real-user latency or backend throughput. The full codebase still downloads as more routes are visited.
- A 125,000-byte gzip budget checks the entry's full static JavaScript dependency graph using Vite's build manifest. Dynamic route chunks are excluded intentionally.
- Automated keyboard/rendering tests and existing focus/label behavior are evidence of specific accessibility features, not a WCAG compliance certification. A live browser, screen reader and mobile-device review remain useful follow-up work.

## Resume wording

> Built a full-stack project management application with React, TypeScript, ASP.NET Core and PostgreSQL, integrating AWS Cognito authentication, private S3 attachments and SignalR collaboration.

> Implemented server-side role authorization, conflict-aware task updates, Docker-based integration tests and GitHub Actions verification; reduced initial gzipped JavaScript by approximately 16% through route code splitting.

> Authored CloudFormation deployment definitions for ECS Fargate, RDS and CloudFront; retained local hosting to avoid paid infrastructure.

Use only claims you can demonstrate and explain. Do not invent users, uptime, throughput, revenue impact, production scale or cloud deployment experience beyond the implemented integrations and prepared definitions. Be candid about AI assistance and your own implementation/debugging decisions if asked.

## Known limits and next improvements

The in-memory SignalR registry supports one API instance; scaling out needs shared messaging. The board has a 500-card bound. Database reads are paginated and use no-tracking queries where appropriate, but realistic SQL plans and load tests have not been measured. Attachments have size/type/checksum checks but no malware scanning. Files are downloaded rather than previewed. Local data does not automatically migrate to RDS. No production backup-recovery drill or deployed uptime claim is made.

The next performance step would be profiling with a representative dataset, inspecting PostgreSQL query plans and measuring before adding indexes or caching. The next product step would be user testing, accessibility checks with assistive technology, and a shared demo environment if hosting costs are deliberately accepted.

## Interview preparation

Be ready to walk through these concrete paths:

1. Browser sign-in → Cognito → access-token validation → database membership authorization. Explain why authentication does not grant access to every workspace.
2. Two people edit the same task. Explain the version check, conflict response and recovery UI.
3. A board card moves. Explain transaction boundaries, ordering and why the registry cannot simply be scaled to multiple API instances.
4. A file uploads but finalization fails. Explain pending attachment records, retry, cleanup and signed-URL expiry.
5. A deploy is rolled back. Explain why reverting code does not automatically revert a database migration.
6. A project page chunk fails to download. Show the error boundary and explain why navigation resets it while reloading retries a fresh application load.

For recording and screenshots, follow [the demo walkthrough](demo.md).
