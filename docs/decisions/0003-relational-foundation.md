# ADR 0003: Core schema, explicit migrations and real database tests

Status: accepted and implemented in Phase 2.

Context: Tenant boundaries, valid assignments and concurrent edits are central to DevFlow. An in-memory database would not verify PostgreSQL constraints, dates or xmin behavior.

Decision: Start with users, workspaces, projects, memberships and tasks. Use composite foreign keys to keep project membership in the correct workspace and task assignment in the correct project. Use restrictive deletes, PostgreSQL xmin for optimistic concurrency, explicit EF migrations and a scoped DbContext. No repository abstraction is needed until application use cases establish a useful interface.

Integration tests run migrations against real PostgreSQL. Testcontainers is the default lifecycle manager. An explicit DEVFLOW_TEST_POSTGRES override supports a disposable external development server; tests create and clean up only randomly named databases they own. No production CRUD is exposed before resource authorization.

Consequences: Strong database-level relationship guarantees and reproducible schema changes. User removal and deletion workflows must deliberately handle relationships. Foreign keys do not implement authorization or preserve the last workspace owner; these remain application responsibilities. A current row version must be supplied by future edit DTOs for API-level optimistic concurrency. Readiness checks connectivity and known migration history, not arbitrary schema drift.

Initial verification used a workspace-local PostgreSQL binary because Docker was not installed. After Docker Desktop and WSL were installed, the user's verification-script run also passed Compose startup and all 22 integration tests using Testcontainers. The saved test log was inspected to confirm the result. Both database execution paths are now verified.
