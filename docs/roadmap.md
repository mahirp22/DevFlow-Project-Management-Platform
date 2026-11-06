# Development milestones

| Phase | Deliverable and exit check |
| --- | --- |
| 1 | Layered skeleton, tooling, docs; build/lint and HTTP health/proxy checks |
| 2 | PostgreSQL, EF mappings/migrations, API infrastructure, xUnit database integration tests |
| 3 | Responsive shell, Tailwind, Router, Query, API client, Vitest/RTL setup |
| 4 | Cognito, profiles, server-side role and tenant authorization with negative tests |
| 5 | Workspaces, project/task CRUD, invitations, comments/history, search and analytics |
| 6 | Accessible dnd-kit board, concurrency handling, SignalR collaboration |
| 7 | S3 upload/finalize/download with authorization and limits |
| 8 | Revised: verified local Docker deployment and AWS infrastructure preparation; paid hosting deferred |
| 9 | Harden Docker/CI/CD, expand tests, complete operational documentation |
| 10 | Performance, accessibility, demo polish, portfolio case study |

Tests accompany features; Phase 9 consolidates them. Every major phase requires user approval before starting. Every Git commit requires explicit approval. Each milestone documents changed files, decisions, run commands and actual verification results.

Phase 1 was committed as `2411723` (`chore: establish DevFlow phase 1 foundation`). Phase 2 was committed as 1ce62c6 after local PostgreSQL and Docker verification. See [Phase 2](phase-2.md). Phase 3 implementation and automated checks are complete; real-browser visual review remains outstanding because browser automation was unavailable. See [Phase 3](phase-3.md). The user confirmed the frontend runs and approved the Phase 3 milestone commit after verification. Phase 3 was committed as b2e835e. Phase 4 implementation and local tests are complete; the user confirmed the live Cognito setup walkthrough on November 3, 2026. See [Phase 4](phase-4.md). The user approved the Phase 4 milestone commit after verification. Phase 4 was committed as `1d8658f`. Phase 5 was authorized and its implementation and automated checks are complete; see [Phase 5](phase-5.md). The user confirmed the Phase 5 walkthrough and display-name fix on November 5, 2026. Phase 6 was authorized and implemented; see [Phase 6](phase-6.md) for checks and its user-confirmed live browser walkthrough. The user confirmed Phase 6 steps 1?8 and approved the combined Phase 5/6 milestone commit. Phases 5 and 6 were committed as `ec1f54f`. Phase 7 implementation and local verification are complete; the user confirmed all 10 live AWS walkthrough steps passed on November 5, 2026. See [Phase 7](phase-7.md). The user approved committing Phase 7 and starting Phase 8.

Phase 7 was committed as `272644e`. Phase 8 is authorized and started: backend Dockerfile, restricted build context, and deployment plan added. The user confirmed updated container verification; paid infrastructure provisioning is deferred; see [Phase 8](phase-8.md).

Phase 8 deployment implementation: generated CloudFormation infrastructure, dedicated IAM policies, deployment stages, isolated Docker smoke test, RDS TLS configuration and one-off migrations with a restricted runtime database role. Verified: 72 integration tests, 15 unit tests, 6 infrastructure tests; offline PowerShell helper checks; clean backend builds; migrations repeated against disposable PostgreSQL and application DDL denied. The user confirmed the updated container test passes, including migrations, Production readiness and restricted permissions. Paid AWS rollout is deferred by user request. No Phase 8 commit yet.

November 6, 2026 closeout: revised Phase 8 scope is complete (local container verification and AWS deployment preparation). The user deferred paid hosting before Apply. Cloud mutation stages are now blocked before AWS calls and the guard passes offline tests. ECS/RDS/CloudFront live deployment remains unverified and deferred. Phase 8 commit and Phase 9 both await user approval.

Phase 8 was committed as `98ad466` after the user-approved closeout and passing deployment-guard/infrastructure checks. Phase 9 is authorized and started: public-repository-only GitHub Actions, local Verify.ps1 and a pull request template. No remote is configured, no publishing occurred, and hosted CI/container execution remains pending. Paid AWS deployment remains disabled. See [Phase 9](phase-9.md).

Phase 9 completion work: local release packaging and operations documentation added, and dependency-audit gates added to verification/CI. npm audit reported zero vulnerabilities. NuGet audit was blocked by NU1900 in the agent environment; full Docker/package verification and GitHub execution remain pending. Origin now points to the user-created public repository; no project push has occurred. Conditional approval to commit/push has not been exercised.

November 6, 2026: the user supplied successful end-to-end New-LocalRelease.ps1 output including all audits/tests, Docker validation and ZIP creation. Phase 9 local implementation/verification is complete; commit/publication is authorized under the earlier condition. First hosted CI verification remains pending.
