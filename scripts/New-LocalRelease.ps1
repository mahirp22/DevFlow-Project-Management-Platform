# Creates a local build package only. Never uploads, pushes, or deploys it.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path $PSScriptRoot -Parent
$originalLocation=Get-Location
$names=@('DOTNET_ROOT','PATH','DOTNET_CLI_HOME','NUGET_PACKAGES')
$previous=@{}
foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
function Run-ReleaseCommand {
    param([string]$Command,[string[]]$Arguments)
    & $Command @Arguments
    if($LASTEXITCODE -ne 0){throw "Release command failed: $Command"}
}
try {
    Set-Location $repo
    $shell=if($env:OS -eq 'Windows_NT'){'powershell'}else{'pwsh'}
    # A failed check prevents packaging. Do not bypass this for a release.
    Run-ReleaseCommand $shell @('-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/Verify.ps1','-IncludeContainers')
    if(!(Get-Command dotnet -ErrorAction SilentlyContinue)){
        $tools=[IO.Path]::GetFullPath((Join-Path $repo '../../work'))
        $env:DOTNET_ROOT=Join-Path $tools 'dotnet'
        $env:PATH="$env:DOTNET_ROOT;$env:PATH"
        $env:DOTNET_CLI_HOME=Join-Path $tools 'dotnet-home'
        $env:NUGET_PACKAGES=Join-Path $tools 'nuget-packages'
    }
    $revision=(& git rev-parse HEAD | Out-String).Trim()
    if($LASTEXITCODE -ne 0){throw 'Cannot identify source commit.'}
    $dirty=@(& git status --porcelain).Count -gt 0
    $id='devflow-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
    $root=Join-Path $repo '.artifacts'
    $staging=Join-Path $root $id
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    foreach($item in @(@{Name='api';Project='DevFlow.Api'},@{Name='migrator';Project='DevFlow.Migrator'})){
        $output=Join-Path $staging $item.Name
        Run-ReleaseCommand 'dotnet' @('publish',"backend/src/$($item.Project)/$($item.Project).csproj",'--configuration','Delivery','--no-restore','--output',$output,'-p:UseAppHost=false','-p:Optimize=true','-p:DebugType=None','-p:DebugSymbols=false')
    }
    Copy-Item -LiteralPath (Join-Path $repo 'frontend/dist') -Destination (Join-Path $staging 'frontend') -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'docs/operations.md') -Destination (Join-Path $staging 'OPERATIONS.md')
    # Reject private file types and local configuration rather than silently packaging them.
    foreach($file in Get-ChildItem -LiteralPath $staging -Recurse -File){
        if($file.Name -match '^\.env($|\.)|^credentials$|^secrets\.json$|^appsettings\.Local\.json$|\.(pem|key|pfx)$'){
            throw "Unexpected private configuration in release output: $($file.Name). No archive created."
        }
    }
    $manifest=[ordered]@{
        commit=$revision
        includesUncommittedChanges=$dirty
        generatedUtc=[DateTime]::UtcNow.ToString('o')
        verification='scripts/Verify.ps1 -IncludeContainers passed before publishing'
        runtime='ASP.NET Core 10; PostgreSQL; same-origin frontend/API reverse proxy needed'
        deployment='Local build package only; no AWS hosting provisioned'
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $staging 'release.json') -Encoding UTF8
    $zip=Join-Path $root "$id.zip"
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal
    $checksum=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$checksum  $id.zip" | Set-Content -LiteralPath "$zip.sha256" -Encoding ASCII
    Write-Host "Local release: $zip"
    if($dirty){Write-Warning 'This package includes uncommitted changes; release.json records that fact.'}
} finally {
    foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Set-Location $originalLocation
}
