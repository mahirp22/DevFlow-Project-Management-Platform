[CmdletBinding()]
param([string]$Image='devflow-api:phase8')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Set-Location (Split-Path $PSScriptRoot -Parent)
$config=Get-Content infrastructure/aws/deployment.json -Raw | ConvertFrom-Json
$suffix=[Guid]::NewGuid().ToString('N').Substring(0,12)
$network="devflow-check-$suffix"
$database="devflow-db-$suffix"
$api="devflow-api-$suffix"
$keys=@('POSTGRES_PASSWORD','ConnectionStrings__DevFlow','Database__ApplicationPassword','Cognito__Authority','Cognito__ClientId','Cognito__Domain','AllowedHosts')
$old=@{}
foreach($key in $keys){$old[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
function Invoke-Docker {
    param([string[]]$Arguments)
    $result=& docker @Arguments
    if($LASTEXITCODE -ne 0){throw "Docker $($Arguments[0]) failed."}
    return $result
}
try {
    Invoke-Docker @('network','create',$network) | Out-Null
    # Disposable test credentials: never printed, written to Git, or sent to AWS.
    $env:POSTGRES_PASSWORD=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
    $env:Database__ApplicationPassword=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
    Invoke-Docker @('run','--detach','--name',$database,'--network',$network,'--env','POSTGRES_PASSWORD','--env','POSTGRES_USER=devflow_admin','--env','POSTGRES_DB=devflow','postgres:18.6') | Out-Null
    $ready=$false
    for($attempt=0;$attempt -lt 30;$attempt++){
        & docker exec $database pg_isready -U devflow_admin -d devflow *> $null
        if($LASTEXITCODE -eq 0){$ready=$true;break}
        Start-Sleep -Seconds 2
    }
    if(!$ready){throw 'Temporary PostgreSQL did not become ready.'}
    $env:ConnectionStrings__DevFlow="Host=$database;Database=devflow;Username=devflow_admin;Password=$env:POSTGRES_PASSWORD;GSS Encryption Mode=Disable"
    # Running twice checks both first deployment and a repeat deployment.
    for($run=0;$run -lt 2;$run++){
        Invoke-Docker @('run','--rm','--network',$network,'--env','ConnectionStrings__DevFlow','--env','Database__ApplicationPassword','--entrypoint','dotnet',$Image,'/migration/DevFlow.Migrator.dll') | Out-Null
    }
    $env:ConnectionStrings__DevFlow="Host=$database;Database=devflow;Username=devflow_app;Password=$env:Database__ApplicationPassword;GSS Encryption Mode=Disable"
    $env:Cognito__Authority="https://cognito-idp.$($config.Region).amazonaws.com/$($config.UserPoolId)"
    $env:Cognito__ClientId=$config.ClientId
    $env:Cognito__Domain=$config.CognitoDomain
    $env:AllowedHosts='*'
    Invoke-Docker @('run','--detach','--name',$api,'--network',$network,'--publish','127.0.0.1::8080',
        '--env','ConnectionStrings__DevFlow','--env','Cognito__Authority','--env','Cognito__ClientId','--env','Cognito__Domain','--env','AllowedHosts',$Image) | Out-Null
    $binding=(Invoke-Docker @('port',$api,'8080/tcp') | Select-Object -First 1).Trim()
    $healthy=$false
    $lastFailure='No response received.'
    for($attempt=0;$attempt -lt 30;$attempt++){
        try {
            $result=Invoke-RestMethod "http://$binding/api/health/ready" -TimeoutSec 3
            if($result.status -eq 'Healthy'){$healthy=$true;break}
        } catch { $lastFailure=$_.Exception.Message; Start-Sleep -Seconds 2 }
    }
    if(!$healthy){
        Write-Host "Last readiness failure: $lastFailure"
        # Only these disposable test containers are inspected; redact their generated passwords.
        $previousPreference=$ErrorActionPreference
        try {
            $ErrorActionPreference='Continue'
            $startup=& docker logs --tail 60 $api 2>&1
            foreach($line in $startup){Write-Host (([string]$line).Replace($env:POSTGRES_PASSWORD,'[redacted]').Replace($env:Database__ApplicationPassword,'[redacted]'))}
        } finally { $ErrorActionPreference=$previousPreference }
        throw 'API container did not become ready. Startup output is printed above.'
    }
    # Inspect effective permissions without deliberately emitting native stderr (which
    # Windows PowerShell can treat as terminating even when a nonzero exit is expected).
    $permissionQuery = "SELECT has_schema_privilege('devflow_app','public','CREATE'), has_table_privilege('devflow_app',format('public.%I','__EFMigrationsHistory'),'SELECT'), has_table_privilege('devflow_app',format('public.%I','__EFMigrationsHistory'),'INSERT,UPDATE,DELETE,TRUNCATE');"
    $permissions = (Invoke-Docker @('exec',$database,'psql','-U','devflow_admin','-d','devflow','-v','ON_ERROR_STOP=1','-t','-A','-c',$permissionQuery) | Out-String).Trim()
    if($permissions -ne 'f|t|f'){throw "Unexpected application database permissions: $permissions (expected f|t|f)."}
    Write-Host 'PASS: migrations run twice, Production API is ready with application credentials, and application DDL is denied.'
} finally {
    & docker rm --force --volumes $api $database 2>$null | Out-Null
    & docker network rm $network 2>$null | Out-Null
    foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$old[$key],'Process')}
}
