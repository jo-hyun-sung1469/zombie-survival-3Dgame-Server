param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Invoke-FileWriteWithRetry {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Operation,
        [int]$MaximumAttempts = 8,
        [int]$InitialDelayMilliseconds = 25
    )

    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        try {
            & $Operation
            return $true
        }
        catch [System.IO.IOException] {
            if ($attempt -eq $MaximumAttempts) {
                return $false
            }

            Start-Sleep -Milliseconds ($InitialDelayMilliseconds * $attempt)
        }
    }

    $false
}

function Write-ActivityLog {
    param([AllowEmptyString()][string]$Raw, $Json)
    $messages = New-Object System.Collections.Generic.List[string]
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
    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    $entryJson = $entry | ConvertTo-Json -Compress
    $activityWritten = Invoke-FileWriteWithRetry {
        [System.IO.File]::AppendAllText(
            $logPath,
            $entryJson + [System.Environment]::NewLine,
            $utf8WithoutBom)
    }
    if (-not $activityWritten) {
        $messages.Add("[zombie-hook] PostToolUse activity logging skipped after repeated file-lock conflicts.")
    }

    if ($command -match 'dotnet\s+build' -and ($parsedExit -eq 0 -or $null -eq $parsedExit)) {
        $buildRecord = [pscustomobject]@{
            timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            command = $command
        } | ConvertTo-Json -Compress
        $buildStatePath = Join-Path $stateDir "last-build-success.json"
        $buildStateWritten = Invoke-FileWriteWithRetry {
            [System.IO.File]::WriteAllText($buildStatePath, $buildRecord, $utf8WithoutBom)
        }
        if (-not $buildStateWritten) {
            $messages.Add("[zombie-hook] Last build state update skipped after repeated file-lock conflicts.")
        }
    }

    $messages.ToArray()
}

function Invoke-PostToolAudit {
    $messages = New-Object System.Collections.Generic.List[string]
    $changedFiles = @(Get-ChangedProjectFiles)
    if ($changedFiles.Count -eq 0) { return @() }
    $findings = @(Invoke-ZombieStaticScan $changedFiles)
    if ($findings.Count -eq 0) { return @() }
    $critical = @($findings | Where-Object { $_.Severity -eq "Critical" })
    $warnings = @($findings | Where-Object { $_.Severity -ne "Critical" })
    if ($critical.Count -gt 0) { $messages.Add("[zombie-hook] Critical security/architecture issue detected after tool use:`n$(Format-Findings $critical 8)") }
    if ($warnings.Count -gt 0) { $messages.Add("[zombie-hook] Performance/security review warnings:`n$(Format-Findings $warnings 8)") }
    $messages.ToArray()
}

if ($SelfTest) {
    $stateDir = Get-HookStateDirectory
    if (-not (Test-Path -LiteralPath $stateDir)) { throw "post_tool_audit self-test failed." }
    $retryState = [pscustomobject]@{ AttemptCount = 0 }
    $retried = Invoke-FileWriteWithRetry -InitialDelayMilliseconds 1 {
        $retryState.AttemptCount++
        if ($retryState.AttemptCount -lt 3) {
            throw [System.IO.IOException]::new("Simulated file-lock conflict.")
        }
    }
    if (-not $retried -or $retryState.AttemptCount -ne 3) { throw "post_tool_audit retry self-test failed." }
    $feedbackJson = Write-HookFeedback "PostToolUse" "test feedback" -IncludeAdditionalContext | ConvertFrom-Json
    $feedbackFailures = @(
        $feedbackJson.systemMessage -ne "test feedback"
        $feedbackJson.hookSpecificOutput.hookEventName -ne "PostToolUse"
        $feedbackJson.hookSpecificOutput.additionalContext -ne "test feedback"
    )
    if ($feedbackFailures -contains $true) {
        throw "post_tool_audit feedback self-test failed."
    }
    "post_tool_audit self-test passed."
    exit 0
}

$payload = Read-HookPayload
$feedback = New-Object System.Collections.Generic.List[string]
try {
    foreach ($message in @(Write-ActivityLog $payload.Raw $payload.Json)) {
        if (-not [string]::IsNullOrWhiteSpace($message)) { $feedback.Add($message) }
    }
}
catch {
    $feedback.Add("[zombie-hook] PostToolUse activity logging skipped: $($_.Exception.Message)")
}

try {
    foreach ($message in @(Invoke-PostToolAudit)) {
        if (-not [string]::IsNullOrWhiteSpace($message)) { $feedback.Add($message) }
    }
}
catch {
    $feedback.Add("[zombie-hook] PostToolUse audit skipped: $($_.Exception.Message)")
}

if ($feedback.Count -gt 0) {
    Write-HookFeedback "PostToolUse" ($feedback -join "`n`n") -IncludeAdditionalContext
}

exit 0

