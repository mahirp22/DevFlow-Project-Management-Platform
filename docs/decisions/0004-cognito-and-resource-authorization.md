# ADR 0004: Cognito identity and database resource authorization

Status: Implemented; live Cognito setup walkthrough confirmed by the user on November 3, 2026.

Use Cognito managed login with a public SPA client, authorization code and PKCE. Delegate protocol details to oidc-client-ts. Keep tokens in memory and authorization transaction state in sessionStorage. Do not persist tokens or implement custom password forms. Reload/expiration requires another sign-in; refresh-token renewal is a possible later UX enhancement.

Use ASP.NET Core JWT Bearer for signature, issuer and lifetime checks, with explicit access-token type, client_id and scope validation. Configure one issuer/client. Missing development settings expose only explicitly public endpoints; missing production settings stop startup.

Bootstrap profiles through server-to-server userInfo retrieval and verified-email/subject checks. Use a parameterized idempotent PostgreSQL insert keyed on Cognito subject. The HTTP identity adapter lives at the API authentication boundary; Application contracts remain independent of ASP.NET and Infrastructure implements persistence.

Workspace/project roles belong in PostgreSQL and are checked for each operation. Resource policies enforce read/write/manage permissions; tests exercise the production handlers with real relational data. Never use frontend state or Cognito groups as workspace authorization.

No new migration or generic repository is needed. JWT revocation is not immediate at an offline-validating API, so use short access-token lifetimes and document the limitation. Database membership revocation is effective on subsequent requests.
