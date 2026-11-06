# Infrastructure

Local PostgreSQL Compose configuration is in docker/compose.yaml; see [Phase 2](../docs/phase-2.md). The Phase 7 attachment bucket was configured and live-tested by the user.

Phase 8 infrastructure is defined by aws/build-template.mjs and its generated aws/devflow.template.json. Public account settings are in aws/deployment.json. Dedicated deployment permissions are split between two IAM policy files. See [Phase 8](../docs/phase-8.md) for costs, local testing, deployment stages, live validation and teardown. Paid AWS hosting is deferred by user request. Phase 8 is closed within the revised local-container/deployment-preparation scope. Cloud mutation stages are disabled by default; read-only diagnostics remain available.

Regenerate: `node infrastructure/aws/build-template.mjs`. Test: `node --test infrastructure/aws/template.test.mjs`. Build the image from the repository root using backend/Dockerfile. Never put credentials in these files.
