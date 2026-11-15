param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9-]{1,80}$')][string]$ConsentId,
    [Parameter(Mandatory=$true)][ValidateRange(1,2147483647)][int]$Version
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$taskOut = Read-LabOutputs
$taskPk = "OUTBOX#${ConsentId}:$Version"
$taskItem = Invoke-LabAws dynamodb get-item @{
    TableName=$taskOut.TableName
    Key=@{PK=@{S=$taskPk};SK=@{S='EVENT'}}
    ConsistentRead=$true
}
if (-not $taskItem.Item -or $taskItem.Item.Kind.S -ne 'Outbox') { throw 'No matching outbox record exists.' }
if ($taskItem.Item.Delivery.S -eq 'Sent') { Write-Output 'Already Sent; inspect the consumer path instead.'; return }
if ($taskItem.Item.Delivery.S -ne 'Pending') { throw 'Unexpected delivery state; investigate before replaying.' }

# Administrative replay uses the stored outbox detail, not caller-supplied events.
$taskPayload = @{Records=@(@{
    eventName='INSERT'
    dynamodb=@{SequenceNumber='manual-replay';NewImage=@{PK=@{S=$taskPk};Kind=@{S='Outbox'}}}
})}
$taskInputFile = [IO.Path]::GetTempFileName()
$taskReplyFile = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($taskInputFile,($taskPayload | ConvertTo-Json -Depth 12 -Compress),(New-Object Text.UTF8Encoding($false)))
    $taskMetadata = & aws lambda invoke --function-name $taskOut.PublisherName --cli-binary-format raw-in-base64-out --payload "file://$taskInputFile" --output json --no-cli-pager $taskReplyFile
    if ($LASTEXITCODE -ne 0) { throw 'Publisher invocation failed.' }
    $taskInvocation = ($taskMetadata -join "`n") | ConvertFrom-Json
    if ($taskInvocation.FunctionError) { throw 'Publisher returned a function error; inspect its sanitized logs.' }
    $taskReply = Get-Content -LiteralPath $taskReplyFile -Raw | ConvertFrom-Json
    if ($null -eq $taskReply.batchItemFailures) { throw 'Publisher response did not contain a batch result.' }
    if (@($taskReply.batchItemFailures).Count -ne 0) { throw 'Publisher reported a failed replay. Investigate before retrying.' }
    Write-Output 'Replay accepted. Verify the outbox state and eventual consumer view separately.'
} finally {
    Remove-Item -LiteralPath $taskInputFile -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $taskReplyFile -ErrorAction SilentlyContinue
}
