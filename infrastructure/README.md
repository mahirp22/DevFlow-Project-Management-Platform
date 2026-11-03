# Infrastructure

Local PostgreSQL Compose configuration is in docker/compose.yaml. See [Phase 2](../docs/phase-2.md) for secrets, startup, migrations, and the current workspace alternative. Docker requires a password and binds the database to loopback only. No cloud resources exist. Container/AWS deployment definitions arrive before Phase 8 deployment; CI/CD hardening follows in Phase 9.
