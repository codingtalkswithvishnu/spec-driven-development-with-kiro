param([Parameter(Mandatory=$true)][string]$Username,[ValidatePattern('^[A-Za-z0-9-]{1,80}$')][string]$ConsentId='C-001',[switch]$Seed,[int]$ExpectedStatus=200)
. (Join-Path $PSScriptRoot 'Common.ps1')
$taskOut=Read-LabOutputs
$taskPassword=Read-LabPassword
try {
    $taskLogin=Invoke-LabAws cognito-idp initiate-auth @{AuthFlow='USER_PASSWORD_AUTH';ClientId=$taskOut.ClientId;AuthParameters=@{USERNAME=$Username;PASSWORD=$taskPassword}}
    $taskToken=$taskLogin.AuthenticationResult.AccessToken
    if (-not $taskToken) { throw 'Sign-in did not return an access token. Complete any required account challenge.' }
    if ($Seed) {
        $taskIdentity=Invoke-LabAws cognito-idp admin-get-user @{UserPoolId=$taskOut.UserPoolId;Username=$Username}
        $taskSub=($taskIdentity.UserAttributes | Where-Object {$_.Name -eq 'sub'}).Value
        $taskKey=@{PK=@{S="CONSENT#$ConsentId"};SK=@{S='STATE'}}
        $taskExisting=Invoke-LabAws dynamodb get-item @{TableName=$taskOut.TableName;Key=$taskKey;ConsistentRead=$true}
        if (-not $taskExisting.Item) {
            Invoke-LabAws dynamodb put-item @{TableName=$taskOut.TableName;Item=@{PK=@{S="CONSENT#$ConsentId"};SK=@{S='STATE'};Owner=@{S=$taskSub};Purpose=@{S='research-demo'};Status=@{S='Granted'};Version=@{N='1'}};ConditionExpression='attribute_not_exists(PK)'} | Out-Null
        } elseif ($taskExisting.Item.Owner.S -ne $taskSub) { throw 'Fixture belongs to a different application user. Choose a new consent ID.' }
    }
    $taskStatus=200
    try {
        $taskReply=Invoke-RestMethod -Uri ($taskOut.ApiUrl+"/consents/$ConsentId/withdraw") -Method Post -Headers @{Authorization="Bearer $taskToken"} -ContentType 'application/json' -Body '{"expectedVersion":1}'
    } catch {
        if ($_.Exception.Response) { $taskStatus=[int]$_.Exception.Response.StatusCode; $taskReply=$null } else { throw }
    }
    Write-Output "HTTP $taskStatus"
    if ($taskReply) { $taskReply | ConvertTo-Json -Compress }
    if ($taskStatus -ne $ExpectedStatus) { throw "Expected HTTP $ExpectedStatus; received $taskStatus" }
} finally { $taskPassword=$null; $taskToken=$null; $taskLogin=$null }
