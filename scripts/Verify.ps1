[CmdletBinding()]
param([switch]$IncludeContainers)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path $PSScriptRoot -Parent
$originalLocation=Get-Location
$names=@('DOTNET_ROOT','PATH','DOTNET_CLI_HOME','NUGET_PACKAGES','TESTINGPLATFORM_TELEMETRY_OPTOUT','DOTNET_CLI_TELEMETRY_OPTOUT','DEVFLOW_TEST_POSTGRES','DOCKER_HOST')
$previous=@{}
foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
function Invoke-Check {
    param([string]$Command,[string[]]$Arguments)
    & $Command @Arguments
    if($LASTEXITCODE -ne 0){throw "Check failed: $Command $($Arguments -join ' ')"}
}
try {
    Set-Location $repo
    if(!(Get-Command dotnet -ErrorAction SilentlyContinue)){
        $tools=[IO.Path]::GetFullPath((Join-Path $repo '../../work'))
        if(!(Test-Path (Join-Path $tools 'dotnet/dotnet.exe'))){throw 'Install the SDK specified in global.json, then reopen the terminal.'}
        $env:DOTNET_ROOT=Join-Path $tools 'dotnet'
        $env:PATH="$env:DOTNET_ROOT;$env:PATH"
        $env:DOTNET_CLI_HOME=Join-Path $tools 'dotnet-home'
        $env:NUGET_PACKAGES=Join-Path $tools 'nuget-packages'
    }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    $env:TESTINGPLATFORM_TELEMETRY_OPTOUT='1'
    $npm=if($env:OS -eq 'Windows_NT'){'npm.cmd'}else{'npm'}
    Invoke-Check 'node' @('--test','infrastructure/aws/template.test.mjs')
    # Separate process isolates mocked native commands and their exit codes.
    $shell=if($env:OS -eq 'Windows_NT'){'powershell'}else{'pwsh'}
    Invoke-Check $shell @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/Test-DeploymentScripts.ps1')
    Push-Location frontend
    try {
        Invoke-Check $npm @('ci')
        Invoke-Check $npm @('audit','--audit-level=high')
        Invoke-Check $npm @('run','lint')
        Invoke-Check $npm @('test','--','--pool=threads','--maxWorkers=2')
        Invoke-Check $npm @('run','build')
    } finally {Pop-Location}
    Invoke-Check 'dotnet' @('restore','backend/DevFlow.slnx','--locked-mode','-p:NuGetAudit=true','-p:NuGetAuditMode=all','-p:NuGetAuditLevel=high')
    # Use separate project outputs to avoid locking the running Debug API's DLLs.
    foreach($project in @('backend/tests/DevFlow.UnitTests/DevFlow.UnitTests.csproj','backend/tests/DevFlow.IntegrationTests/DevFlow.IntegrationTests.csproj','backend/src/DevFlow.Migrator/DevFlow.Migrator.csproj')){
        Invoke-Check 'dotnet' @('build',$project,'--configuration','Verification','--no-restore')
    }
    Invoke-Check 'dotnet' @('backend/tests/DevFlow.UnitTests/bin/Verification/net10.0/DevFlow.UnitTests.dll')
    if($IncludeContainers){
        # Force Testcontainers to use a fresh database, never the developer's database.
        $env:DEVFLOW_TEST_POSTGRES=$null
        try {
            # Docker.DotNet requires a normalized Windows pipe URI; Docker CLI does not.
            $endpoint=$env:DOCKER_HOST
            if(!$endpoint){
                $endpoint=& docker context inspect --format '{{.Endpoints.docker.Host}}'
                if($LASTEXITCODE -ne 0){throw 'Unable to resolve the Docker engine. Open Docker Desktop first.'}
                $endpoint=($endpoint | Out-String).Trim()
            }
            if($endpoint -match '^npipe:/+([^/]+)/pipe/([^/]+)/?$'){
                $pipeServer=$Matches[1]
                if($pipeServer -eq '.'){$pipeServer='localhost'}
                $env:DOCKER_HOST="npipe://${pipeServer}/pipe/$($Matches[2])"
            }
            Invoke-Check 'dotnet' @('backend/tests/DevFlow.IntegrationTests/bin/Verification/net10.0/DevFlow.IntegrationTests.dll')
        } finally {$env:DOCKER_HOST=$previous['DOCKER_HOST']}
        Invoke-Check 'docker' @('build','--platform','linux/amd64','-f','backend/Dockerfile','-t','devflow-api:verification','.')
        Invoke-Check $shell @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/Test-Container.ps1','-Image','devflow-api:verification')
    } else {
        Write-Host 'Integration/container execution skipped. Use -IncludeContainers with Docker Desktop running for full checks.'
    }
    Write-Host 'PASS: all selected checks completed. No AWS deployment was performed.'
} finally {
    foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Set-Location $originalLocation
}
