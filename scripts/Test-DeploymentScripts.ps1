# Offline checks: load only helper definitions, never the deployment script's stages.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$tokens=$null
$errors=$null
foreach($file in @('Deploy-Aws.ps1','Test-Container.ps1')) {
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$errors)
    if($errors){throw ($errors -join "`n")}
    $definitions=$ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)
    foreach($definition in $definitions){
        if($definition.Name -in @('Invoke-AwsJson','Native','Invoke-Docker')){
            . ([ScriptBlock]::Create($definition.Extent.Text))
        }
    }
}
$config=[pscustomobject]@{Profile='offline-test';Region='ca-central-1'}
$script:captured=@()
$script:mockExit=0
function aws { $script:captured=@($args); $global:LASTEXITCODE=$script:mockExit; '{"Account":"test"}' }
function docker { $script:captured=@($args); $global:LASTEXITCODE=$script:mockExit; 'test-output' }
function Check($condition,$message){if(!$condition){throw $message}}
$result=Invoke-AwsJson @('cloudformation','describe-stacks','--stack-name','name with spaces')
Check ($result.Account -eq 'test') 'AWS JSON result was not parsed.'
Check ($script:captured[3] -eq 'name with spaces') 'AWS argument boundary was lost.'
Check ($script:captured -contains 'offline-test') 'AWS profile was not passed.'
Native 'docker' @('build','directory with spaces') | Out-Null
Check ($script:captured[1] -eq 'directory with spaces') 'Native command argument boundary was lost.'
Invoke-Docker @('inspect','container-id') | Out-Null
Check ($script:captured[0] -eq 'inspect') 'Docker helper did not invoke the command.'
$script:mockExit=7
$failed=$false
try { Invoke-AwsJson @('ecs','run-task') | Out-Null } catch { $failed=$true }
Check $failed 'AWS failures must stop deployment.'
$failed=$false
try { Invoke-Docker @('run','image') | Out-Null } catch { $failed=$true }
Check $failed 'Docker failures must stop validation.'
foreach($stage in @('Plan','Apply','Publish','Migrate','Start','Frontend')) {
    $script:captured=@()
    $blocked=$false
    try { & (Join-Path $PSScriptRoot 'Deploy-Aws.ps1') -Stage $stage | Out-Null }
    catch { $blocked=$_.Exception.Message -like 'Cloud deployment is disabled*' }
    Check $blocked "Deployment stage $stage was not blocked by the local-hosting guard."
    Check ($script:captured.Count -eq 0) "Deployment stage $stage called AWS before rejecting deployment."
}
Write-Host 'PASS: script syntax, argument boundaries, JSON/error handling, and all six cloud mutation stages blocked before AWS calls.'
