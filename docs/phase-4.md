# Phase 4: authentication and authorization

## Status

Implementation and local automated verification are complete. On November 3, 2026, the user confirmed that Step 12 of the guided setup worked: registration, email verification, login, profile setup, logout and signing in again. The user created the Cognito resources manually. No AWS access keys are needed in the application.

The frontend shows an explicit setup message until all public Cognito settings are supplied. Development can still serve public overview/health pages without Cognito, but protected API routes fail closed. Production startup requires complete configuration. The user approved the Phase 4 milestone commit after verification. Phase 5 requires separate approval.

## 1. Create Cognito in the AWS console

Use the AWS console's Cognito service in your chosen region, for example `ca-central-1`. Console labels can change; the settings below are the important contract.

1. Create a user pool/application for a **Single-page application (SPA)** named DevFlow. Choose email sign-in and enable self-service sign-up.
2. Require email and enable email verification. Permit email-based password recovery. Use Cognito's password policy; DevFlow never handles passwords.
3. Create a **public app client without a client secret**. If a secret was generated, create a new SPA client instead of embedding that secret in frontend code.
4. Configure a Cognito domain and enable its login pages (managed login or classic hosted UI). This is a separate URL from the pool's issuer.
5. Configure the app client's login pages:
   - Identity provider: Cognito user pool.
   - OAuth grant: **Authorization code grant**. Disable implicit and client-credentials grants for this browser client.
   - OpenID scopes: **openid**, **email**, **profile**.
   - Allowed callback URL: `http://localhost:5173/auth/callback`.
   - Allowed sign-out URL: `http://localhost:5173/sign-in`.
6. Allow the client to read `email`, `email_verified`, and optionally `name`. Set access-token lifetime to **5 minutes** and enable token revocation.
7. Record the region, user pool ID, app client ID and full Cognito domain. These identifiers are public configuration; do not share passwords, tokens or AWS access keys.

Use **localhost consistently** for this flow, not a mix of localhost and 127.0.0.1. OAuth redirect URLs must match exactly, including scheme, port, path and trailing slash. HTTPS is required for deployed callback URLs.

AWS references: [Create a Cognito application](https://docs.aws.amazon.com/cognito/latest/developerguide/getting-started-user-pools-application.html), [managed login configuration](https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-pools-managed-login.html), [access-token lifetime](https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-the-access-token.html).

## 2. Configure the frontend

Create `frontend/.env.local` using `frontend/.env.example`. Replace the angle-bracket placeholders with your public values:

~~~dotenv
VITE_API_BASE_URL=/api
VITE_COGNITO_AUTHORITY=https://cognito-idp.<region>.amazonaws.com/<user-pool-id>
VITE_COGNITO_CLIENT_ID=<public-app-client-id>
VITE_COGNITO_DOMAIN=https://<your-domain>.auth.<region>.amazoncognito.com
~~~

The authority is the **user pool issuer**, not the managed-login domain. Supply no trailing paths in the domain. All `VITE_*` values are included in the browser bundle; never put secrets there.

From the repository root:

~~~powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
~~~

Restart Vite after changing environment values. If 5173 is already occupied by DevFlow, stop that instance before restarting. Open **http://localhost:5173** for authentication.

## 3. Configure the backend

Use the same public values in the terminal that starts the API:

~~~powershell
# From the repository root:
$env:Cognito__Authority = 'https://cognito-idp.<region>.amazonaws.com/<user-pool-id>'
$env:Cognito__ClientId = '<public-app-client-id>'
$env:Cognito__Domain = 'https://<your-domain>.auth.<region>.amazoncognito.com'

# Start PostgreSQL and set ConnectionStrings__DevFlow using the Phase 2 guide.
dotnet restore backend/DevFlow.slnx --locked-mode
dotnet build backend/DevFlow.slnx --no-restore
dotnet run --project backend/src/DevFlow.Api --launch-profile http --no-build
~~~

You may instead store these in ASP.NET Core User Secrets with keys `Cognito:Authority`, `Cognito:ClientId` and `Cognito:Domain`. No database migration is needed for Phase 4: the Phase 2 user/membership tables already support it. AWS credentials and a Cognito identity pool are not needed.

## 4. Verify the real flow

1. Open **Sign in** and continue to Cognito.
2. Create an account, receive the email verification code, and confirm the address.
3. Return to DevFlow after login. Open **Account** if necessary.
4. Select **Set up my profile**. The API retrieves verified attributes from Cognito; the browser supplies no identity or role DTO.
5. Confirm your profile appears. Repeat setup requests must not create duplicate users.
6. Open Projects/My tasks. These are still placeholders; creating an account does not automatically create a workspace or grant memberships.
7. Sign out and confirm protected pages return to sign-in. Test password recovery through the hosted login page.
8. Sign in again, wait for the access token to expire, and confirm the app asks you to sign in again.
9. Refresh the page: tokens are deliberately kept only in memory, so a refresh requires sign-in again. Cognito's existing login cookie can shorten that interaction.
10. Try a modified callback URL or an old authorization code; it should produce a recoverable sign-in failure.

If profile setup returns 403, confirm the email is verified and the app client can read `email_verified`. A 503 means the identity provider cannot be reached; a database outage is a separate backend failure. `redirect_mismatch` usually means the callback URL or browser origin differs from the configured value.

## Security and architecture

The SPA uses `oidc-client-ts` 3.5.0 for authorization-code flow, PKCE and callback state validation. Tokens are stored in an in-memory user store; only short-lived authorization transaction state/PKCE material uses sessionStorage to survive the redirect. No access, ID or refresh token is written to localStorage or sessionStorage. Automatic refresh is intentionally off for this milestone. Logout clears query data, attempts refresh-token revocation, removes local credentials and navigates through Cognito's logout endpoint.

A resource server that validates JWTs locally does not instantly learn about revoked tokens. A copied access token may remain usable until expiration (plus 30 seconds of clock skew), even after logout. Five-minute access tokens bound this interval. Membership removal is different: role checks query the database on every request, so removed memberships stop authorizing subsequent operations immediately.

ASP.NET Core JWT Bearer 10.0.14 validates RS256 signatures through issuer discovery/JWKS, issuer, expiration and not-before. Additional checks require `token_use=access`, the configured `client_id`, a user UUID subject and the `openid` scope. Cognito access tokens use `client_id`; this client check replaces ordinary ID-token audience validation. ID tokens are rejected. We support a single configured user pool and public app client in this milestone.

References: [Cognito token validation](https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html), [userInfo attributes](https://docs.aws.amazon.com/cognito/latest/developerguide/userinfo-endpoint.html), [Cognito logout](https://docs.aws.amazon.com/cognito/latest/developerguide/logout-endpoint.html), [OIDC client documentation](https://github.com/authts/oidc-client-ts/blob/main/docs/index.md).

All API endpoints require authentication by default, with explicit public health and development OpenAPI exceptions. Anonymous unknown URLs now return 401 because of the fallback policy. Authentication errors do not expose validation details.

Profile bootstrap reads verified email and subject from the configured provider over HTTPS. It checks that userInfo's subject matches the validated access token. It stores the initial profile once using a parameterized PostgreSQL insert with conflict handling. Changing profile/email attributes after registration is a later feature. No password, token or membership role is stored during bootstrap.

Tenant roles live in PostgreSQL, not Cognito groups or frontend state:

| Membership | Read project | Write project | Manage project |
| --- | --- | --- | --- |
| Workspace Owner/Admin | Yes | Yes | Yes |
| Workspace Member + Project Manager | Yes | Yes | Yes |
| Workspace Member + Project Member | Yes | Yes | No |
| Workspace Member + Project Viewer | Yes | No | No |
| Workspace Member without project membership | No | No | No |
| Outside workspace | No | No | No |

Workspace administration requires Owner/Admin. The access endpoints hide unknown/foreign resources with 404. The reusable `project:read`, `project:write` and `project:manage` policies enforce permissions against a `ProjectResource`. Phase 5 controllers must call server-side resource authorization before every relevant operation; hiding a button or fetching permission flags is not authorization. Policies deny with 403 when an authenticated caller lacks the required permission. Test-only probe endpoints prove policy enforcement; they are not compiled into the API.

## HTTP contracts

| Endpoint | Purpose |
| --- | --- |
| GET /api/me | Current stored profile; 404 before setup |
| POST /api/me | Idempotent verified profile initialization; no body required |
| GET /api/workspaces/{workspaceId}/access | Current caller's workspace permissions |
| GET /api/workspaces/{workspaceId}/projects/{projectId}/access | Current caller's project permissions |

All require `Authorization: Bearer <access-token>`. The API ignores browser-provided subject/email/role fields on bootstrap. Profile data is private. No project/workspace CRUD was added in this phase. OpenAPI now documents the Bearer security scheme and protected operations.

## Files and boundaries

New backend files:
- `Application/Identity/IdentityContracts.cs`: narrow profile, identity and permission interfaces/DTOs.
- `Infrastructure/Identity/UserProfiles.cs` and `ResourceAccess.cs`: PostgreSQL-backed implementations.
- `Api/Authentication/CognitoSettings.cs`, `CognitoAuthentication.cs`, `CognitoIdentityDirectory.cs`, `ResourcePermissions.cs`, `BearerSecurityDocument.cs`: identity boundary, provider transport, policy handlers and API documentation.
- `Api/Controllers/MeController.cs` and `AccessController.cs`: authenticated HTTP adapters.
- `IntegrationTests/AuthenticationTests.cs`, `IdentityDirectoryTests.cs`, `TestTokens.cs`, `AuthorizationProbeController.cs`: security regression tests and test-only signing/provider adapters.

Modified backend files: Program, API project/package lock, appsettings, health controller, exception handler, Infrastructure DI, integration test factory, API tests, pipeline probe and integration package lock.

New frontend files under `src/features/auth/`: `oidc.ts`, `AuthContext.ts`, `AuthProvider.tsx`, `AuthPages.tsx`, `auth.test.tsx`, `AuthProvider.test.tsx`. Modified frontend files: package/lock, .env.example, main, App/routes/tests, layout, API client and index.html referrer policy. Documentation: this guide, ADR 0004, README, architecture and roadmap.

## Verification

~~~powershell
# Repository root; Docker must be running for the default integration fixture:
dotnet build backend/DevFlow.slnx --no-restore
dotnet test --solution backend/DevFlow.slnx --no-build --no-restore

# Frontend:
Set-Location frontend
npm.cmd test
npm.cmd run lint
npm.cmd run build
~~~

The existing `scripts/Test-Docker.ps1` remains the Windows Docker helper. Test signing keys and provider stubs exist only in the test project. Production token validation is exercised using locally signed test JWTs; the signature/issuer/lifetime/client/type checks are not bypassed.

Local verification:
- 15 backend unit tests passed.
- 46 tests in the integration project passed, including real PostgreSQL persistence/HTTP authorization tests and provider adapter checks.
- 21 frontend tests passed.
- Backend build: zero warnings/errors. Frontend TypeScript/build and lint passed.
- Live local smoke checks without Cognito configuration: API liveness 200, PostgreSQL readiness 200, protected profile 401 through the Vite proxy; development OpenAPI exposes the Bearer scheme.
- Backend dependencies restored in locked mode. The sandbox's .NET TLS transport could not reach NuGet directly, so packages were downloaded from official NuGet URLs using Node's trusted system CAs and restored from an external local feed. Normal repo sources remain unchanged. NuGet vulnerability auditing was unavailable for that local-feed restore.
- Live Cognito setup walkthrough (Step 12): completed and confirmed by the user on November 3, 2026. This is user-reported verification, not an automated browser run. Live password recovery and the timed expiry walkthrough were not separately reported; expiry behavior is covered by local automated tests.
