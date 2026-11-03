# Run from a normal VS Code PowerShell terminal, not an administrator terminal.
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$originalLocation = Get-Location
$names = @('POSTGRES_PASSWORD', 'POSTGRES_PORT', 'DEVFLOW_TEST_POSTGRES', 'DOCKER_HOST', 'DOTNET_ROOT', 'PATH', 'NUGET_PACKAGES', 'TESTINGPLATFORM_TELEMETRY_OPTOUT')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$projectName = 'devflow-check-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$composeStarted = $false

try {
    Set-Location $repoPath
    $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCommand) {
        $dockerPath = $dockerCommand.Source
    } else {
        $dockerPath = @(
            "$env:LOCALAPPDATA/Programs/DockerDesktop/resources/bin/docker.exe",
            "$env:ProgramFiles/Docker/Docker/resources/bin/docker.exe"
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (-not $dockerPath) { throw 'Docker CLI not found. Install Docker Desktop and reopen your terminal.' }
    # Docker launches credential helpers by name, even when the CLI uses an absolute path.
    # Keep the bundled helpers available to both Compose and Testcontainers.
    $dockerBin = Split-Path $dockerPath -Parent
    $env:PATH = "$dockerBin;$env:PATH"
    $engine = & $dockerPath info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker engine is unavailable. Open Docker Desktop and wait for Engine running.' }
    if ($engine -ne 'linux') { throw 'Switch Docker Desktop to Linux containers.' }

    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $dotnetPath = $dotnetCommand.Source
    } else {
        $localTools = [System.IO.Path]::GetFullPath((Join-Path $repoPath '../../work'))
        $dotnetPath = Join-Path $localTools 'dotnet/dotnet.exe'
        if (-not (Test-Path -LiteralPath $dotnetPath)) { throw 'Install the .NET SDK specified in global.json.' }
        $env:DOTNET_ROOT = Join-Path $localTools 'dotnet'
        $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
        $env:NUGET_PACKAGES = Join-Path $localTools 'nuget-packages'
    }

    Write-Host 'Restoring and building the backend...'
    & $dotnetPath restore backend/DevFlow.slnx --configfile NuGet.Config --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & $dotnetPath build backend/DevFlow.slnx --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Stop any running DevFlow API before rebuilding.' }

    # Docker CLI and Docker.DotNet accept different named-pipe URI forms.
    # Resolve the same engine now, but leave the CLI environment unchanged for Compose.
    $testcontainersDockerHost = $env:DOCKER_HOST
    if (-not $testcontainersDockerHost) {
        $endpoint = & $dockerPath context inspect --format '{{.Endpoints.docker.Host}}'
        if ($LASTEXITCODE -ne 0) { throw 'Could not determine the Docker engine endpoint.' }
        $testcontainersDockerHost = ($endpoint | Out-String).Trim()
    }
    if ($testcontainersDockerHost -match '^npipe:/+([^/]+)/pipe/([^/]+)/?$') {
        $pipeServer = $Matches[1]
        $pipeName = $Matches[2]
        if ($pipeServer -eq '.') { $pipeServer = 'localhost' }
        $testcontainersDockerHost = "npipe://${pipeServer}/pipe/$pipeName"
    }
    $env:DEVFLOW_TEST_POSTGRES = $null
    $env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
    $env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
    $env:POSTGRES_PORT = '0' # Docker selects a free port; existing local PostgreSQL stays untouched.

    Write-Host 'Checking Compose with a temporary PostgreSQL container...'
    $composeStarted = $true
    & $dockerPath compose -p $projectName -f infrastructure/docker/compose.yaml up -d --wait --wait-timeout 120
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Compose failed. Container status and logs follow before temporary resources are removed:'
        $savedErrorPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & $dockerPath compose -p $projectName -f infrastructure/docker/compose.yaml ps --all
            & $dockerPath compose -p $projectName -f infrastructure/docker/compose.yaml logs --no-color --tail 100
        } finally {
            $ErrorActionPreference = $savedErrorPreference
        }
        throw 'Compose startup or PostgreSQL health check failed. See the Docker error and container logs above.'
    }
    & $dockerPath compose -p $projectName -f infrastructure/docker/compose.yaml exec -T postgres psql -U devflow -d devflow -c 'SELECT version();'
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL query in the Compose container failed.' }

    Write-Host 'Running integration tests with Testcontainers (external database override cleared)...'
    $resultsPath = Join-Path $repoPath 'backend/tests/TestResults'
    New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
    $testLogPath = Join-Path $resultsPath 'docker-integration.log'
    $savedErrorPreference = $ErrorActionPreference
    try {
        $env:DOCKER_HOST = $testcontainersDockerHost
        # Preserve native stderr on Windows PowerShell as well as PowerShell 7.
        $ErrorActionPreference = 'Continue'
        & $dotnetPath backend/tests/DevFlow.IntegrationTests/bin/Debug/net10.0/DevFlow.IntegrationTests.dll 2>&1 |
            Tee-Object -FilePath $testLogPath | Out-Host
        $testExitCode = $LASTEXITCODE
    } finally {
        # Restore the original CLI endpoint before Compose cleanup.
        $env:DOCKER_HOST = $previous['DOCKER_HOST']
        $ErrorActionPreference = $savedErrorPreference
    }
    if ($testExitCode -ne 0) { throw "Docker integration tests failed (exit $testExitCode). Full output saved to $testLogPath" }
    Write-Host 'PASS: Compose PostgreSQL and all Docker integration tests passed.' -ForegroundColor Green
} finally {
    if ($composeStarted) {
        # Only this run's uniquely named project/volume is removed, never the devflow project.
        & $dockerPath compose -p $projectName -f infrastructure/docker/compose.yaml down --volumes
        if ($LASTEXITCODE -ne 0) { Write-Warning "Cleanup failed for temporary project $projectName. Remove only that project after reviewing Docker Desktop." }
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    Set-Location $originalLocation
}
