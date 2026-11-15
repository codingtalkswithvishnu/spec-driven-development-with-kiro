param([Parameter(Mandatory=$true)][string]$Username)
. (Join-Path $PSScriptRoot 'Common.ps1')
$taskOut = Read-LabOutputs
$taskPassword = Read-LabPassword
try {
    Invoke-LabAws cognito-idp admin-create-user @{UserPoolId=$taskOut.UserPoolId;Username=$Username;MessageAction='SUPPRESS'} | Out-Null
    Invoke-LabAws cognito-idp admin-set-user-password @{UserPoolId=$taskOut.UserPoolId;Username=$Username;Password=$taskPassword;Permanent=$true} | Out-Null
    Write-Output "Created fictional application user: $Username"
} finally { $taskPassword = $null }
