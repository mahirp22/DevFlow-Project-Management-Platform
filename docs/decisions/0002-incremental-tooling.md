# ADR 0002: Pin tooling and add dependencies incrementally

Status: accepted for Phase 1.

Decision: Pin .NET SDK and direct npm dependencies, commit lockfiles, enable strict TypeScript and backend warnings-as-errors. Use the current official Vite React/TypeScript template. Its lint tool is Oxlint; ESLint was not a project requirement. Retain that supported default rather than introducing a second linter.

Dependencies for Tailwind, routing, Query, database access, authentication, drag-and-drop, SignalR and testing are installed when their feature is introduced, after checking current stable versions and compatibility. No runtime credentials are committed.

Consequences: Phase 1 stays readable and small. Versions can be deliberately upgraded later. Compile/lint and HTTP smoke checks establish the baseline; they do not substitute for subsequent unit/integration tests.
