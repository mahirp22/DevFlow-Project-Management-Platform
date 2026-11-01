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

Suggested first commit after review: chore: establish DevFlow phase 1 foundation
