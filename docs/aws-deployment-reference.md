Archived instructions for a future paid deployment. Do not execute rollout stages under the current local-hosting scope. Cloud mutations are blocked by AllowCloudDeployment=false. See [Phase 8 closeout](phase-8.md) for current status. The updated container test has passed.

# AWS deployment reference (deferred)

Phase 7 is committed as `272644e`. Phase 8 deployment code is prepared; AWS deployment and live checks are pending. The user confirmed the first Docker image built. Rebuild now because the image adds the migration runner and RDS certificates.

## 1. Test the updated container locally

Open Docker Desktop and wait for Engine running. In VS Code choose Terminal > New Terminal. Leave existing app/database terminals running. Paste:

```powershell
Set-Location "C:\Users\conta\Documents\Codex\2026-11-01\i-want-to-build-a-professional\outputs\devflow"
docker build --platform linux/amd64 -f backend/Dockerfile -t devflow-api:phase8 .
```

After a successful build, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Container.ps1
```

This starts a disposable PostgreSQL container and API on a separate Docker network, runs migrations twice, checks Production API readiness with restricted database credentials, then removes its own containers/network/temporary volumes. It does not use your existing database or AWS credentials. The execution-policy override applies only to this script process.

Expected message: `PASS: migrations run twice, Production API is ready with application credentials, and application DDL is denied.` If it fails, stop and share the error without secrets. Do not continue until it passes.

## 2. Review configuration and costs

Open `infrastructure/aws/deployment.json`. Verify its public identifiers: account `383354018220`, region `ca-central-1`, bucket `devflow-attachments-mahir-2026`, and your existing Cognito pool/client. Never put credentials in this file.

The stack creates a VPC, four subnets, internal ALB, one Fargate task (0.25 vCPU / 0.5 GB after activation), RDS PostgreSQL db.t4g.micro with 20 GB gp3 and seven-day backups, ECR, two Secrets Manager secrets, CloudWatch logs, private frontend S3 and CloudFront. RDS is single-AZ. The ALB/database use two private subnets; PostgreSQL has no public access. There is no NAT gateway. The Fargate task uses a public IPv4 address for outbound access but accepts inbound connections only from the ALB security group.

Before Step 6, use [AWS Pricing Calculator](https://calculator.aws/) to estimate these resources in Canada (Central), including approximately 730 running hours/month, ALB, Fargate, RDS, public IPv4, storage, secrets and logs. Credits do not guarantee free operation. Check whether your account plan permits the required services.

Open AWS Billing and Cost Management > Budgets and create a monthly cost budget with email alerts at your chosen spending threshold. Alerts are notifications, not spending caps. Stopping ECS alone does not stop RDS/ALB charges. See teardown below.

## 3. Create a dedicated deployment user

The Phase 7 user has S3 attachment access, not infrastructure deployment permissions. Keep these separate. In the account administrator's AWS console session:

1. Open IAM > Policies > Create policy > JSON.
2. Open `infrastructure/aws/deployment-services-policy.json` in VS Code. Copy its entire contents into the JSON editor. Choose Next, name it `DevFlowDeploymentServices`, and create it.
3. Repeat with `infrastructure/aws/deployment-iam-policy.json`, named `DevFlowDeploymentResources`.
4. Open IAM > Users > Create user. Enter `devflow-deployer`.
5. Enable console access. If asked, choose to create an IAM user. Set/save a strong password privately. Complete any required first-login password change in the browser.
6. On permissions, choose Attach policies directly. Attach `DevFlowDeploymentServices`, `DevFlowDeploymentResources`, and AWS-managed `SignInLocalDevelopmentAccess`.
7. Create the user and save its console sign-in URL privately. Do not create access keys.

These are infrastructure-administrator permissions: some provisioning operations require resource wildcards. Role creation/pass-role is restricted to `/devflow/`; existing attachment/Cognito access is scoped to this project. The two policies fit IAM size limits. Do not attach them to ECS or the ordinary development user. They are locally reviewed but still need account validation. For AccessDenied, share the denied action/resource so we can correct the specific permission, not grant blanket AdministratorAccess.

## 4. Log in and run preflight

In the VS Code terminal:

```powershell
aws login --profile devflow-deploy --region ca-central-1
```

In the browser, sign in as `devflow-deployer`. Switch identities if another session is active. Enter `n` for optional AI skills/MCP setup. Never share passwords, tokens or keys.

Then run:

```powershell
aws sts get-caller-identity --profile devflow-deploy --region ca-central-1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Check
```

Preflight checks the account, Docker, attachment versioning, available PostgreSQL 18 versions and AWS template validation. Save the printed PostgreSQL version list. These commands must run in your terminal; the agent's tool environment cannot access your AWS login.

## 5. Prepare the infrastructure plan

```powershell
$pgVersion = Read-Host 'Paste one PostgreSQL 18 version printed by Check'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Plan -PostgresVersion $pgVersion
```

The script verifies the RDS version/class combination and creates a CloudFormation change set. It prints resources and an `initial-...` change set name. Save the name. No running infrastructure is created yet. Inspect it in CloudFormation > Stacks > devflow-demo > Change sets and review the costs from Step 2. Plan is for initial creation, not an existing deployed stack.

The generated template is `infrastructure/aws/devflow.template.json`. To change it, edit `build-template.mjs`, then regenerate with `node infrastructure/aws/build-template.mjs` and run `node --test infrastructure/aws/template.test.mjs`.

## 6. Apply the infrastructure plan

**This begins provisioning billable resources.** After reviewing the plan and costs:

```powershell
$changeSet = Read-Host 'Paste the initial-... name printed by Plan'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Apply -ChangeSetName $changeSet
```

RDS and CloudFront can take 20 minutes or longer. Keep the terminal open. CloudFormation > devflow-demo > Events shows progress. ECS starts with zero tasks, avoiding an absent image or unmigrated database. If creation fails, inspect Events and share the error before deleting anything. Database protection and retained secrets can require deliberate cleanup after a rollback.

## 7. Publish the backend image

```powershell
$imageTag = 'phase8-' + (Get-Date -Format 'yyyyMMddHHmmss')
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Publish -ImageTag $imageTag
```

This uses temporary ECR login credentials, builds/pushes the Linux image and updates the stack with an immutable tag. Never reuse a tag. The service stays stopped until migrations succeed. On subsequent deployments, this deliberately introduces downtime rather than overlapping SignalR instances. Review the enabled ECR scan-on-push findings before making the demo public.

## 8. Migrate the database and start the API

Run separately, continuing only after each succeeds:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Migrate
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Start
```

Migration is a one-off task inside the VPC. It applies EF migrations with administrator credentials, then provisions the application's data-only role. The API receives only the app password, not the administrator password. Both use verified TLS and a pinned public RDS CA bundle. Migrations do not run during API startup. Start checks the successful migration task revision and then expects readiness Healthy.

CloudWatch's stack log group contains migration and api streams. Migration failures log only an exception type, avoiding submitted SQL/secret exposure. The API uses structured logs. Live RDS behavior remains to be verified here.

## 9. Upload the frontend

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Frontend
```

This builds React with public Cognito configuration and API URL `/api`, uploads hashed assets first and index.html last, and invalidates CloudFront. Old hashed assets remain for existing browser tabs. `.env.local` is unchanged. Copy the printed HTTPS CloudFront site URL.

Frontend S3 access is private through OAC. CloudFront routes `/api` and `/api/*` to the internal ALB via a VPC origin, with caching disabled and authorization/query/WebSocket headers forwarded. A frontend-only function rewrites React routes to index.html. API errors retain their status; they never become frontend HTML. Index.html revalidates; hashed assets cache long-term. No purchased domain is required.

## 10. Allow the deployed URL in Cognito and S3

1. In AWS open Cognito > User pools > your existing pool > App clients > DevFlow > Login pages > Edit.
2. Add `https://YOUR-CLOUDFRONT-HOST/auth/callback` to Allowed callback URLs. Keep `http://localhost:5173/auth/callback`.
3. Add `https://YOUR-CLOUDFRONT-HOST/sign-in` to Allowed sign-out URLs. Keep the existing localhost URL. Preserve authorization code flow, identity provider and openid/email/profile scopes. Save.
4. Open S3 > `devflow-attachments-mahir-2026` > Permissions > Cross-origin resource sharing (CORS) > Edit.
5. Open `.deployment/attachment-cors.json` in VS Code, generated by Frontend. Copy the complete JSON array into the editor and save. It includes localhost and your deployed origin. If you independently added other CORS rules, preserve them when merging.

## 11. Verify the live application

Use the CloudFront HTTPS URL, not S3 or ALB URLs:

1. Sign in using Cognito. RDS starts empty: create a workspace/project. Local development data is not automatically copied.
2. Create/edit a task, assign a member and add a comment.
3. Refresh a nested project/task route and confirm it loads.
4. Use a second account/browser and verify live Kanban, status and comment updates.
5. Confirm Viewer permissions deny editing/uploads and allow authorized downloads.
6. Upload/download a small text file; compare the filename and contents.
7. Sign out/in. Confirm unauthorized API requests fail rather than returning index.html.
8. Verify `/api/health/ready` and inspect CloudWatch startup logs. Do not share tokens or signed URLs.

Phase 8 is complete only after these checks pass, resource identifiers are recorded and rollback/cleanup are understood. Phase 9 is separate. No Phase 8 commit has been made.

## Operations, rollback and teardown

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-Aws.ps1 -Stage Status` prints resource identifiers and the site URL, not passwords. Record the deployed image tag, ECR digest, task/migration revision and frontend build commit.
- Keep ECS at one instance. SignalR connection state is process-local; scaling out needs shared messaging first. Deployment interruption is expected, with reconnect/polling recovery.
- To roll back application code, first check database compatibility, then update CloudFormation ImageTag to the previous immutable tag. Never overwrite tags. For incompatible schema changes, restore RDS to a new instance from backup and deliberately update configuration. Do not automatically run destructive down-migrations.
- Before risky schema changes, create a manual RDS snapshot and verify Available. Seven-day automated backups are configured; restoration has not yet been rehearsed. Secret rotation requires restarting tasks because environment injection happens at startup.
- Keep ALB/CloudFront request access logging disabled until query-token redaction exists. SignalR WebSocket URLs can contain access tokens. Application logging suppresses request-URL diagnostics.
- The RDS CA bundle is public and checksum-pinned. If AWS updates it and the build fails checksum validation, verify the official bundle and update the hash deliberately. Never disable TLS verification.
- To retire the demo, save needed data and a final snapshot, disable RDS deletion protection through a reviewed template update, then delete the CloudFormation stack. RDS deletion takes a snapshot. ECR, frontend S3 and database secrets are retained intentionally; delete them separately only after confirming they are no longer needed. Retained snapshots also cost money. Existing attachment S3/Cognito resources are outside this stack. CloudFront VPC origin teardown can take time; inspect deletion events instead of deleting dependencies out of order.
- Remove the deployment user's elevated access when finished managing infrastructure. Never place deployment credentials in ECS, Git or frontend configuration.

## Files and validation

Added: CloudFormation generator/template, two deployment policies, public configuration, infrastructure tests, Deploy-Aws.ps1, Test-Container.ps1, database connection resolver, migration console project/lock file, and configuration tests. Updated: Dockerfile, solution, dependency injection, gitignore and milestone docs. No new third-party application dependency was introduced.

Backend/API/migrator compile with zero warnings/errors in isolated Phase8 outputs. Three database configuration tests and 15 domain unit tests pass. Six infrastructure tests cover database isolation, role separation, bootstrap, API caching, SPA routes and references. Resource properties were checked against the official CloudFormation resource specification. Disposable PostgreSQL verification confirmed repeatable migrations and read/write grants without schema creation. See the roadmap for final regression results.

These checks do not replace AWS template/account/IAM validation, updated Docker runtime testing or live RDS/CloudFront checks. The agent has no usable Docker installation or deployment AWS login. The user-confirmed initial image build predates the migrator; Step 1 validates the updated image.

References: [CloudFront VPC origins](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/private-content-vpc-origins.html), [origin request policies](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/using-managed-origin-request-policies.html), [RDS TLS](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/UsingWithRDS.SSL.html), [AWS CLI temporary login](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sign-in.html).

Final local regression results: all 72 integration tests and 15 unit tests passed; all 6 infrastructure tests passed. Offline script syntax/argument/error propagation checks passed with `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-DeploymentScripts.ps1`. No frontend code changed in this phase. No AWS deployment was attempted from the agent environment.

Container-readiness correction: the application role now has SELECT-only permission on __EFMigrationsHistory because readiness checks pending EF migrations. It still cannot modify migration history or create schema objects. RDS configuration and disposable-container connections explicitly disable unused GSS/Kerberos negotiation while retaining VerifyFull TLS for RDS. Failed container checks now print redacted startup logs before cleanup. Verified the corrected grants against disposable PostgreSQL (history SELECT allowed; INSERT and schema CREATE denied), clean builds, three connection-configuration tests and offline script checks. Updated Docker rerun remains pending.
