# ADR 0001: Modular monolith in a monorepo

Status: accepted for Phase 1.

Context: A student portfolio project needs credible engineering boundaries and a feasible deployment/learning path.

Decision: One ASP.NET Core service with Domain, Application, Infrastructure and API projects; one React frontend; one PostgreSQL database; one repository. Organize application/frontend features by use case. Add focused interfaces when they provide a useful dependency boundary. Avoid a generic repository layer and premature distributed services.

Consequences: Simple local debugging and deployment; compile-time backend dependency direction; straightforward cross-stack reviews. Deployment scales the backend as a unit. Multi-instance SignalR requires additional infrastructure. Domain and Application begin empty rather than carrying invented abstractions.
