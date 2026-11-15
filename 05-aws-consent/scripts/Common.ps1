$ErrorActionPreference = 'Stop'
function Read-LabOutputs {
    param([string]$Path = (Join-Path $PSScriptRoot '../infra/outputs.json'))
    if (-not (Test-Path -LiteralPath $Path)) { throw 'Deploy the lab with --outputs-file outputs.json first.' }
    (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json).ConsentWorkshop
}
function Invoke-LabAws {
    param([string]$Service,[string]$Action,[hashtable]$InputObject)
    $taskTemp = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText($taskTemp,($InputObject | ConvertTo-Json -Depth 20 -Compress),(New-Object Text.UTF8Encoding($false)))
        $taskText = & aws $Service $Action --cli-input-json "file://$taskTemp" --output json --no-cli-pager
        if ($LASTEXITCODE -ne 0) { throw "AWS operation failed: $Service $Action" }
        if ($taskText) { ($taskText -join "`n") | ConvertFrom-Json }
    } finally { Remove-Item -LiteralPath $taskTemp -ErrorAction SilentlyContinue }
}
function Read-LabPassword {
    $taskSecure = Read-Host 'Enter a unique lab password (14+ characters, mixed case, number, symbol)' -AsSecureString
    $taskPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($taskSecure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($taskPointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($taskPointer) }
}
