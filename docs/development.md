# Development setup

## Toolchain

| Tool | Pinned baseline | Purpose |
| --- | --- | --- |
| .NET SDK | 10.0.403 | ASP.NET Core 10, C# 14 |
| Node.js | 24.22.1 | Frontend tooling |
| npm | 11.19.1 | Reproducible dependency installation |
| Git | Supported stable release | Milestone history |
| Docker Desktop | Required in Phase 2 | Local PostgreSQL and integration tests |

global.json permits SDK patch updates in the selected feature band and excludes previews. .node-version records Node; package.json constrains its major version and pins direct dependencies. package-lock.json and NuGet packages.lock.json record resolved dependencies. Review dependency updates deliberately.

Install the .NET **SDK**, not only the runtime. On Windows, Docker Desktop should use Linux containers with WSL 2. No AWS account or credentials are needed for Phase 1.

Official references: [SDK download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [Node releases](https://nodejs.org/en/about/previous-releases), [Vite guide](https://vite.dev/guide/).

## This workspace

The host initially had Node and Git but no .NET or Docker. For validation, a .NET SDK was downloaded to ../../work/dotnet, outside the repository. It is not a distributable project dependency. In the original workspace, this PowerShell setup uses that SDK:

~~~powershell
$env:DOTNET_ROOT = (Resolve-Path ../../work/dotnet).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_HOME = [System.IO.Path]::GetFullPath("$PWD/../../work/dotnet-home")
$env:NUGET_PACKAGES = [System.IO.Path]::GetFullPath("$PWD/../../work/nuget-packages")
$env:NUGET_HTTP_CACHE_PATH = [System.IO.Path]::GetFullPath("$PWD/../../work/nuget-http-cache")
$env:APPDATA = [System.IO.Path]::GetFullPath("$PWD/../../work/dotnet-appdata")
dotnet --info
~~~

On another computer, install the pinned SDK normally and skip this workspace-specific setup.

Inside the restricted agent environment, use the repository NuGet configuration and a single MSBuild worker:

~~~powershell
dotnet restore backend/DevFlow.slnx --configfile NuGet.Config --locked-mode -m:1
dotnet build backend/DevFlow.slnx --no-restore -m:1
~~~

The workspace validation launched the compiled API directly, avoiding the SDK runner's child-process behavior:

~~~powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
dotnet backend/src/DevFlow.Api/bin/Debug/net10.0/DevFlow.Api.dll --contentRoot "$PWD/backend/src/DevFlow.Api"
~~~

These environment changes affect only the current terminal. They keep tooling state inside the workspace; they do not change system configuration.

The host's npm connection required the OS certificate store. If the same verified certificate issue occurs on this machine, run:

~~~powershell
$env:NODE_OPTIONS = '--use-system-ca'
~~~

TLS verification remains enabled. Do not disable strict-ssl.

## Run and verify

Follow the root README commands. npm ci restores the exact lockfile. dotnet restore --locked-mode detects unexpected dependency changes. Use separate terminals for frontend and backend; stop each with Ctrl+C.

Direct checks:

~~~powershell
Invoke-RestMethod http://127.0.0.1:5080/api/health
Invoke-RestMethod http://127.0.0.1:5080/openapi/v1.json
Invoke-RestMethod http://127.0.0.1:5173/api/health
~~~

The third check tests the frontend proxy. Stop the backend and select Check API connection to verify the unavailable state. The button has a five-second timeout and an accessible status announcement.

Ports are fixed to avoid silent port changes. If a port is occupied, stop its owner or deliberately update launchSettings.json and vite.config.ts together.

npm run preview previews static build output only; it is not the development proxy or a production API deployment.

## Configuration and secrets

appsettings.json contains non-secret defaults; appsettings.Development.json overrides logging. Environment variables use double underscores for nested .NET keys. User Secrets will hold local backend secrets when needed. Frontend VITE_* values are shipped to the browser and must never contain credentials.

The local API uses loopback HTTP with no authentication or sensitive data. Production will terminate HTTPS at the AWS load balancer, restrict backend ingress, configure trusted forwarded headers and hostnames, and use resource authorization. There is no wildcard CORS policy.

AWS credentials will use developer profiles locally and IAM task roles in ECS. Database passwords belong in managed secrets. Never commit tokens, private keys, database credentials, or environment files.

## Tests

Phase 1 is validated by compilation, linting and HTTP smoke checks. There is no automated feature test suite yet, and an empty test run must not be reported as passing coverage. Phase 2 adds xUnit integration tests against real PostgreSQL; Phase 3 adds Vitest/React Testing Library for user-visible behavior. Authorization, concurrency, and upload controls receive tests with their features.
