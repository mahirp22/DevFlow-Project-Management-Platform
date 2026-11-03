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
| 8 | AWS infrastructure and deployment; backend Docker image must exist here |
| 9 | Harden Docker/CI/CD, expand tests, complete operational documentation |
| 10 | Performance, accessibility, demo polish, portfolio case study |

Tests accompany features; Phase 9 consolidates them. Every major phase requires user approval before starting. Every Git commit requires explicit approval. Each milestone documents changed files, decisions, run commands and actual verification results.

Phase 1 was committed as `2411723` (`chore: establish DevFlow phase 1 foundation`). Phase 2 was committed as 1ce62c6 after local PostgreSQL and Docker verification. See [Phase 2](phase-2.md). Phase 3 implementation and automated checks are complete; real-browser visual review remains outstanding because browser automation was unavailable. See [Phase 3](phase-3.md). The user confirmed the frontend runs and approved the Phase 3 milestone commit after verification. Phase 4 requires separate approval.
