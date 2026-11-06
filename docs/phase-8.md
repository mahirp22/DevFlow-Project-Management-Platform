# Phase 8 closeout: local containers and AWS deployment preparation

Status on November 6, 2026: complete within the revised scope. The user requested no paid hosting and stopped after preparing the deployment plan, before Apply. ECS Fargate, RDS, ALB and CloudFront rollout is deferred, not completed. Phase 8 changes remain uncommitted; Phase 9 requires separate approval.

## What was completed

- Multi-stage, non-root backend Docker image with a separate migration runner and pinned RDS trust bundle.
- Local container verification: the user confirmed PASS for repeat migrations, Production API readiness using application credentials, and denied application DDL permissions.
- Database permission separation: the runtime user can read/write application data and read migration history, but cannot change migration history or create schema objects.
- CloudFormation infrastructure definitions, IAM policies, configuration and staged deployment scripts, retained for portfolio review and possible future deployment.
- Local verification: 72 integration tests, 15 unit tests, 6 infrastructure tests, clean backend builds and offline PowerShell checks. Subsequent readiness fixes were checked with targeted tests, database permissions and the user-confirmed Docker run.
- Existing Cognito sign-in and private S3 uploads/downloads were live-tested in earlier phases.

## No paid hosting rollout

`infrastructure/aws/deployment.json` now sets `AllowCloudDeployment` to `false`. The deployment script rejects Plan, Apply, Publish, Migrate, Start and Frontend before calling AWS. Check and Status remain available for read-only diagnostics. This is a guard in the project script, not an AWS billing cap or IAM restriction; manually using the AWS console/CLI can still create resources.

No AWS cleanup or provisioning was performed by the agent. Based on the reported stopping point, the planned hosting resources were not created. A prepared change set is not evidence of a running deployment. Existing Cognito/S3 usage is separate and is not guaranteed to cost zero.

The original deployment walkthrough is preserved in [AWS deployment reference](aws-deployment-reference.md). Those instructions are deferred. Re-enabling deployment should be a deliberate future decision with a cost review.

## Continue running locally

Keep using the current local PostgreSQL, ASP.NET Core and Vite setup described in [development](development.md). Cognito and S3 remain the existing external integrations. The container smoke test is temporary validation, not a persistent installation: it removes its own test containers and data when finished.

To repeat the container check from the repository root with Docker Desktop running:

```powershell
docker build --platform linux/amd64 -f backend/Dockerfile -t devflow-api:phase8 .
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Container.ps1
```

Run the second command only after the build succeeds. Your existing local database is not used by this test.

To check infrastructure and the deployment guard without contacting AWS:

```powershell
node --test infrastructure/aws/template.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-DeploymentScripts.ps1
```

## Optional account housekeeping

No cloud commands are needed to finish this revised phase. If you want to remove the unused deployment preparation:

1. In CloudFormation, inspect devflow-demo and its change set. Only if it is the unexecuted plan (ExecutionStatus AVAILABLE, stack REVIEW_IN_PROGRESS), delete that change set. Do not execute it. If any resources or CREATE/UPDATE status are present, investigate before deleting the stack.
2. Keep the deployment policy JSON files in Git. In IAM you can detach DevFlowDeploymentServices and DevFlowDeploymentResources from devflow-deployer until you need deployment permissions again. Leave the working Phase 7 development user's permissions alone.
3. Continue reviewing account billing for the existing Cognito/S3 integrations. Do not delete either service just to remove the unused hosting plan; that would break sign-in or attachments.

## Accurate portfolio description

Use: "Built a React/ASP.NET Core project management platform with PostgreSQL, AWS Cognito authentication, private S3 attachments and real-time SignalR collaboration. Containerized the backend and authored CloudFormation deployment definitions for ECS, RDS and CloudFront."

Do not claim the application is hosted on ECS/RDS/CloudFront. Their definitions are prepared and locally checked, but AWS execution, IAM compatibility and end-to-end hosted behavior remain unverified.

## Files changed for closeout

Updated README, roadmap, infrastructure README, deployment configuration, Deploy-Aws.ps1, and offline script tests. Replaced the Phase 8 guide with this closeout and retained the original deployment walkthrough as aws-deployment-reference.md. No dependencies or cloud resources were added for this closeout.

The user approved committing this revised Phase 8 milestone and starting Phase 9 on November 6, 2026. Cloud hosting remains deferred.
