param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Write-ActivityLog {
    param([AllowEmptyString()][string]$Raw, $Json)
    $stateDir = Get-HookStateDirectory
    $logPath = Join-Path $stateDir "activity.jsonl"
    $commands = @()
    $tools = @()
    $exitValues = @()
    if ($null -ne $Json) {
        $commands += Get-JsonValuesByName $Json @("command", "cmd", "script")
        $tools += Get-JsonValuesByName $Json @("tool_name", "tool", "name")
        $exitValues += Get-JsonValuesByName $Json @("exit_code", "exitCode", "status")
    }
    $firstCommand = $commands | Select-Object -First 1
    $firstTool = $tools | Select-Object -First 1
    $command = if ($null -eq $firstCommand) { "" } else { [string]$firstCommand }
    $tool = if ($null -eq $firstTool) { "" } else { [string]$firstTool }
    if ([string]::IsNullOrWhiteSpace($tool)) { $tool = "unknown" }
    $parsedExit = $null
    $exitRaw = ($exitValues | Select-Object -First 1)
    if ($null -ne $exitRaw) {
        $tmp = 0
        if ([int]::TryParse([string]$exitRaw, [ref]$tmp)) { $parsedExit = $tmp }
    }
    $entry = [pscustomobject]@{
        timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        event = "PostToolUse"
        tool = $tool
        command = $command.Substring(0, [Math]::Min(240, $command.Length))
        exitCode = $parsedExit
    }
    $entry | ConvertTo-Json -Compress | Add-Content -LiteralPath $logPath
    if ($command -match 'dotnet\s+build' -and ($parsedExit -eq 0 -or $null -eq $parsedExit)) {
        [pscustomobject]@{
            timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            command = $command
        } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $stateDir "last-build-success.json")
    }
}

function Invoke-PostToolAudit {
    $changedFiles = @(Get-ChangedProjectFiles)
    if ($changedFiles.Count -eq 0) { return }
    $findings = @(Invoke-ZombieStaticScan $changedFiles)
    if ($findings.Count -eq 0) { return }
    $critical = @($findings | Where-Object { $_.Severity -eq "Critical" })
    $warnings = @($findings | Where-Object { $_.Severity -ne "Critical" })
    if ($critical.Count -gt 0) { Write-HookWarning "[zombie-hook] Critical security/architecture issue detected after tool use:`n$(Format-Findings $critical 8)" }
    if ($warnings.Count -gt 0) { Write-HookWarning "[zombie-hook] Performance/security review warnings:`n$(Format-Findings $warnings 8)" }
}

if ($SelfTest) {
    $stateDir = Get-HookStateDirectory
    if (-not (Test-Path -LiteralPath $stateDir)) { throw "post_tool_audit self-test failed." }
    "post_tool_audit self-test passed."
    exit 0
}

$payload = Read-HookPayload
Write-ActivityLog $payload.Raw $payload.Json
Invoke-PostToolAudit

