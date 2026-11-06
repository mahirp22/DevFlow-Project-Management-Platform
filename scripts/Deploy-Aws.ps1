[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Check','Plan','Apply','Publish','Migrate','Start','Frontend','Status')][string]$Stage,
    [string]$ImageTag,
    [string]$PostgresVersion,
    [string]$ChangeSetName
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
Set-Location $repo
$config = Get-Content infrastructure/aws/deployment.json -Raw | ConvertFrom-Json
# Fail closed before AWS login, local output files, or resource changes.
# The user deferred paid hosting; changing this requires a deliberate scope decision.
if ($Stage -notin @('Check','Status') -and
    (!$config.PSObject.Properties['AllowCloudDeployment'] -or $config.AllowCloudDeployment -ne $true)) {
    throw 'Cloud deployment is disabled for the local-hosting milestone. No AWS command was run. See docs/phase-8.md.'
}
$env:AWS_PAGER = ''
$local = Join-Path $repo '.deployment'
New-Item -ItemType Directory -Force $local | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Save-Json($name, $value) {
    $path = Join-Path $local $name
    [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 30), $utf8)
    return ('file://' + ($path -replace '\\','/'))
}
function Invoke-AwsJson {
    param([string[]]$Arguments)
    $result = & aws @Arguments --profile $config.Profile --region $config.Region --no-cli-pager --output json
    if ($LASTEXITCODE -ne 0) { throw "AWS command failed: $($Arguments[0]) $($Arguments[1]). Stop here; do not continue to the next stage." }
    if ($result) { return ($result -join "`n" | ConvertFrom-Json) }
}
function Native {
    param([string]$command, [string[]]$arguments)
    & $command @arguments
    if ($LASTEXITCODE -ne 0) { throw "$command failed. Stop here." }
}
function Outputs {
    $stack = Invoke-AwsJson @('cloudformation','describe-stacks','--stack-name',$config.StackName)
    $values = @{}
    foreach ($item in $stack.Stacks[0].Outputs) { $values[$item.OutputKey] = $item.OutputValue }
    return $values
}
function Update-Stack([hashtable]$changes) {
    $current = Invoke-AwsJson @('cloudformation','describe-stacks','--stack-name',$config.StackName)
    $parameters = @($current.Stacks[0].Parameters | ForEach-Object {
        if ($changes.ContainsKey($_.ParameterKey)) { @{ ParameterKey=$_.ParameterKey; ParameterValue=[string]$changes[$_.ParameterKey] } }
        else { @{ ParameterKey=$_.ParameterKey; UsePreviousValue=$true } }
    })
    $parametersFile = Save-Json 'update-parameters.json' $parameters
    Invoke-AwsJson @('cloudformation','update-stack','--stack-name',$config.StackName,'--use-previous-template','--parameters',$parametersFile,'--capabilities','CAPABILITY_IAM') | Out-Null
    Invoke-AwsJson @('cloudformation','wait','stack-update-complete','--stack-name',$config.StackName) | Out-Null
}
$identity = Invoke-AwsJson @('sts','get-caller-identity')
if ($identity.Account -ne $config.AccountId) { throw 'Wrong AWS account. Check deployment.json and your login before continuing.' }
Write-Host "Account verified: $($identity.Account); region: $($config.Region)."
switch ($Stage) {
    'Check' {
        Native 'docker' @('version')
        $versioning = Invoke-AwsJson @('s3api','get-bucket-versioning','--bucket',$config.AttachmentBucket)
        if ($versioning -and $versioning.PSObject.Properties['Status']) { throw 'Attachments require a bucket that has never had versioning enabled.' }
        $versions = Invoke-AwsJson @('rds','describe-db-engine-versions','--engine','postgres','--query','DBEngineVersions[?starts_with(EngineVersion, `18.`)].EngineVersion')
        Write-Host ('Available PostgreSQL 18 versions: ' + ($versions -join ', '))
        Invoke-AwsJson @('cloudformation','validate-template','--template-body','file://infrastructure/aws/devflow.template.json') | Out-Null
        Write-Host 'Preflight passed. Review costs and permissions before Plan/Apply.'
    }
    'Plan' {
        if (!$PostgresVersion) { throw 'Provide -PostgresVersion from the Check output.' }
        $options = Invoke-AwsJson @('rds','describe-orderable-db-instance-options','--engine','postgres','--engine-version',$PostgresVersion,'--db-instance-class','db.t4g.micro')
        if (@($options.OrderableDBInstanceOptions).Count -eq 0) { throw 'This RDS version/class combination is unavailable in the selected region.' }
        $lists = Invoke-AwsJson @('ec2','describe-managed-prefix-lists','--filters','Name=prefix-list-name,Values=com.amazonaws.global.cloudfront.origin-facing')
        if (@($lists.PrefixLists).Count -ne 1) { throw 'Cannot resolve CloudFront origin prefix list.' }
        $parameters = @(
            @{ParameterKey='AttachmentBucket';ParameterValue=$config.AttachmentBucket},
            @{ParameterKey='UserPoolId';ParameterValue=$config.UserPoolId},
            @{ParameterKey='ClientId';ParameterValue=$config.ClientId},
            @{ParameterKey='CognitoDomain';ParameterValue=$config.CognitoDomain},
            @{ParameterKey='CloudFrontPrefixListId';ParameterValue=$lists.PrefixLists[0].PrefixListId},
            @{ParameterKey='PostgresVersion';ParameterValue=$PostgresVersion},
            @{ParameterKey='ImageTag';ParameterValue='bootstrap'},
            @{ParameterKey='DesiredCount';ParameterValue='0'}
        )
        $file = Save-Json 'create-parameters.json' $parameters
        $name = 'initial-' + (Get-Date -Format 'yyyyMMddHHmmss')
        Invoke-AwsJson @('cloudformation','create-change-set','--stack-name',$config.StackName,'--change-set-name',$name,'--change-set-type','CREATE','--template-body','file://infrastructure/aws/devflow.template.json','--parameters',$file,'--capabilities','CAPABILITY_IAM') | Out-Null
        Invoke-AwsJson @('cloudformation','wait','change-set-create-complete','--stack-name',$config.StackName,'--change-set-name',$name) | Out-Null
        $plan = Invoke-AwsJson @('cloudformation','describe-change-set','--stack-name',$config.StackName,'--change-set-name',$name)
        $plan.Changes | ForEach-Object { Write-Host "$($_.ResourceChange.Action) $($_.ResourceChange.LogicalResourceId) $($_.ResourceChange.ResourceType)" }
        Write-Host "No resources provisioned yet. After cost/resource review, run -Stage Apply -ChangeSetName $name"
    }
    'Apply' {
        if (!$ChangeSetName) { throw 'Provide the exact change set name printed by Plan.' }
        Invoke-AwsJson @('cloudformation','execute-change-set','--stack-name',$config.StackName,'--change-set-name',$ChangeSetName) | Out-Null
        Write-Host 'Provisioning billable infrastructure. RDS and CloudFront can take 20 minutes or longer.'
        Invoke-AwsJson @('cloudformation','wait','stack-create-complete','--stack-name',$config.StackName) | Out-Null
        Outputs
    }
    'Publish' {
        if ($ImageTag -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$') { throw 'Provide a new unique -ImageTag, e.g. phase8-20261105-01. Existing tags cannot be overwritten.' }
        $values = Outputs
        $registry = $values.RepositoryUri.Split('/')[0]
        $loginPassword = & aws ecr get-login-password --profile $config.Profile --region $config.Region
        if ($LASTEXITCODE -ne 0) { throw 'ECR login failed.' }
        try {
            $loginPassword | & docker login --username AWS --password-stdin $registry
            if ($LASTEXITCODE -ne 0) { throw 'Docker registry login failed.' }
        } finally { $loginPassword = $null }
        $image = "$($values.RepositoryUri):$ImageTag"
        Native 'docker' @('build','--platform','linux/amd64','-f','backend/Dockerfile','-t',$image,'.')
        Native 'docker' @('push',$image)
        # Stop the demo service during schema changes; no overlapping SignalR instances.
        Update-Stack @{ImageTag=$ImageTag;DesiredCount='0'}
        Write-Host 'Image published; service is stopped. Run Migrate, then Start.'
    }
    'Migrate' {
        $values = Outputs
        $network = Save-Json 'migration-network.json' @{awsvpcConfiguration=@{subnets=@($values.PublicSubnets.Split(','));securityGroups=@($values.TaskSecurityGroup);assignPublicIp='ENABLED'}}
        $run = Invoke-AwsJson @('ecs','run-task','--cluster',$values.Cluster,'--task-definition',$values.MigrationTask,'--launch-type','FARGATE','--platform-version','1.4.0','--network-configuration',$network)
        if (@($run.failures).Count -ne 0 -or @($run.tasks).Count -ne 1) { throw 'Migration task could not start. Check ECS failures in the console.' }
        $task = $run.tasks[0].taskArn
        Invoke-AwsJson @('ecs','wait','tasks-stopped','--cluster',$values.Cluster,'--tasks',$task) | Out-Null
        $result = Invoke-AwsJson @('ecs','describe-tasks','--cluster',$values.Cluster,'--tasks',$task)
        $finished = $result.tasks[0].containers[0]
        if (!$finished.PSObject.Properties['exitCode'] -or $finished.exitCode -ne 0) { throw 'Migration did not succeed. Inspect the migration CloudWatch log; do not start the API.' }
        [IO.File]::WriteAllText((Join-Path $local 'successful-migration.txt'), $values.MigrationTask, $utf8)
        Write-Host 'Migration completed successfully. Start is now permitted for this task revision.'
    }
    'Start' {
        $values = Outputs
        $marker = Join-Path $local 'successful-migration.txt'
        if (!(Test-Path $marker) -or (Get-Content $marker -Raw) -ne $values.MigrationTask) { throw 'Run Migrate successfully for the current task revision first.' }
        Update-Stack @{DesiredCount='1'}
        $health = Invoke-RestMethod "$($values.SiteUrl)/api/health/ready"
        Write-Host "Readiness: $($health.status). Next: Frontend and Cognito/CORS setup."
    }
    'Frontend' {
        $values = Outputs
        $keys = @('VITE_API_BASE_URL','VITE_COGNITO_AUTHORITY','VITE_COGNITO_CLIENT_ID','VITE_COGNITO_DOMAIN')
        $previous = @{}
        foreach ($key in $keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key,'Process') }
        Push-Location frontend
        try {
            $env:VITE_API_BASE_URL='/api'
            $env:VITE_COGNITO_AUTHORITY="https://cognito-idp.$($config.Region).amazonaws.com/$($config.UserPoolId)"
            $env:VITE_COGNITO_CLIENT_ID=$config.ClientId
            $env:VITE_COGNITO_DOMAIN=$config.CognitoDomain
            Native 'npm.cmd' @('ci')
            Native 'npm.cmd' @('run','build')
        } finally {
            Pop-Location
            foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
        }
        # Keep older hashed assets for clients with an earlier index.html; no --delete.
        Invoke-AwsJson @('s3','sync','frontend/dist',"s3://$($values.FrontendBucket)",'--exclude','index.html','--exclude','assets/*','--cache-control','no-cache','--only-show-errors') | Out-Null
        Invoke-AwsJson @('s3','sync','frontend/dist/assets',"s3://$($values.FrontendBucket)/assets",'--cache-control','public,max-age=31536000,immutable','--only-show-errors') | Out-Null
        Invoke-AwsJson @('s3','cp','frontend/dist/index.html',"s3://$($values.FrontendBucket)/index.html",'--cache-control','no-cache','--content-type','text/html','--only-show-errors') | Out-Null
        Invoke-AwsJson @('cloudfront','create-invalidation','--distribution-id',$values.DistributionId,'--paths','/*') | Out-Null
        $cors = @(@{AllowedOrigins=@('http://localhost:5173',$values.SiteUrl);AllowedMethods=@('POST','GET','HEAD');AllowedHeaders=@('*');ExposeHeaders=@('ETag');MaxAgeSeconds=300})
        [IO.File]::WriteAllText((Join-Path $local 'attachment-cors.json'), (ConvertTo-Json -InputObject $cors -Depth 5), $utf8)
        Write-Host "Frontend uploaded: $($values.SiteUrl). Add this origin to Cognito and attachment CORS using docs/phase-8.md."
    }
    'Status' { Outputs }
}
