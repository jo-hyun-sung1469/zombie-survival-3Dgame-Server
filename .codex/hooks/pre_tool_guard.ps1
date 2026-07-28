param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Invoke-PreToolGuard {
    param(
        [AllowEmptyString()][string]$Raw,
        $Json,
        [string]$HookEventName = "PreToolUse"
    )

    $commands = @()
    if ($null -ne $Json) {
        $commands += Get-JsonValuesByName $Json @("command", "cmd", "script")
    }
    if ($commands.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($Raw)) {
        $commands += $Raw
    }

    $commandText = $commands -join "`n"
    $lower = $commandText.ToLowerInvariant()
    $blocked = @(
        @{ Pattern = '\bgit(?:\.exe)?\s+checkout\b[^\r\n;&|]*\s--(?:\s|$)'; Reason = 'Path checkout can silently revert user edits.' },
        @{ Pattern = ('\bremove' + '-item\b[^\r\n;|]*(?:\s|^)-(?:r|re|rec|recu|recur|recurs|recurse)(?:\s|$)'); Reason = 'Recursive deletion must be reviewed manually.' },
        @{ Pattern = '\brm\b[^\r\n;&|]*(?:\s|^)(?:-[a-z]*r[a-z]*|--recursive)(?:\s|$)'; Reason = 'Recursive deletion is unsafe.' },
        @{ Pattern = 'git\s+reset\s+--hard'; Reason = 'Hard reset can discard user work.' },
        @{ Pattern = 'git\s+checkout\s+--\s+'; Reason = 'Path checkout can silently revert user edits.' },
        @{ Pattern = 'git\s+clean\s+(-|/)[a-z]*[fdx]'; Reason = 'git clean can delete untracked user files.' },
        @{ Pattern = 'git\s+push\s+(-f|--force|--force-with-lease)'; Reason = 'Force push requires explicit user approval.' },
        @{ Pattern = 'dotnet\s+ef\s+database\s+(drop|update)'; Reason = 'Database mutation requires an explicit migration/database decision.' },
        @{ Pattern = 'dotnet\s+ef\s+migrations\s+remove'; Reason = 'Migration removal is destructive for schema history.' },
        @{ Pattern = 'remove-item\b.*\b-recurse\b'; Reason = 'Recursive deletion must be reviewed manually.' },
        @{ Pattern = '\brm\s+-[^\n]*r[^\n]*f\s+(/|\.|~)'; Reason = 'Recursive force deletion is unsafe.' },
        @{ Pattern = '\brmdir\s+/s\b'; Reason = 'Recursive directory deletion is unsafe.' },
        @{ Pattern = '\b(drop\s+database|drop\s+table|truncate\s+table)\b'; Reason = 'Destructive SQL must not be run through Codex hooks.' },
        @{ Pattern = '\bshutdown\b|\breboot\b|\bformat\b'; Reason = 'System-level destructive command is outside project scope.' }
    )

    $systemCommandPattern = '(?m)(?:^|[;&|({]\s*)(?:shut' + 'down|re' + 'boot|for' + 'mat)(?:\.com|\.exe)?(?:\s|$)'
    foreach ($entry in $blocked) {
        $isSystemCommandRule = $entry.Reason -eq 'System-level destructive command is outside project scope.'
        if ($isSystemCommandRule -and $lower -notmatch $systemCommandPattern) {
            continue
        }
        if ($lower -match $entry.Pattern) {
            return Write-HookDeny $HookEventName "[dangerous-command] $($entry.Reason)`nMatched pattern: $($entry.Pattern)`nCommand:`n$commandText"
        }
    }

    $secrets = New-Object System.Collections.Generic.List[string]
    if ($Raw -match '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----') { $secrets.Add("private key material") }
    if ($Raw -match '(?i)\bBearer\s+[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+') { $secrets.Add("bearer/JWT token") }

    foreach ($value in @(Get-PasswordAssignmentValues $Raw)) {
        if (-not (Test-PlaceholderValue $value)) { $secrets.Add("connection string password") }
    }
    foreach ($assignment in @(Get-SensitiveAssignments $Raw)) {
        if (-not (Test-PlaceholderValue $assignment.Value)) { $secrets.Add("$($assignment.Name) value") }
    }

    if ($secrets.Count -gt 0) {
        $unique = $secrets | Sort-Object -Unique
        return Write-HookDeny $HookEventName "[secret-guard] Potential hardcoded secret detected: $($unique -join ', ').`nUse appsettings placeholders, user secrets, or environment variables instead."
    }

    $referencedPaths = @(Get-ReferencedRepoPaths $Raw)
    if ($referencedPaths.Count -gt 0) {
        $overbroadReason = Test-OverbroadReferencedChange $referencedPaths
        if ($null -ne $overbroadReason) {
            return Write-HookDeny $HookEventName "[scope-guard] $overbroadReason"
        }
    }

    ""
}

if ($SelfTest) {
    $danger = Invoke-PreToolGuard '{"command":"git reset --hard HEAD"}' (@{ command = "git reset --hard HEAD" } | ConvertTo-Json | ConvertFrom-Json)
    $secretPayload = '{"content":"ConnectionStrings__DefaultConnection=Server=db;Pass' + 'word=RealPassword123!"}'; $secret = Invoke-PreToolGuard $secretPayload $null
    $quotedSecretPayload = '{"content":"ConnectionStrings__DefaultConnection=Server=db;Pass' + 'word=\"RealPassword123!\""}'; $quotedSecret = Invoke-PreToolGuard $quotedSecretPayload $null
    $widePatch = "*** Update File: zombie_servival-3Dgame_Server/Program.cs`n*** Update File: zombie_servival-3Dgame_Server/Data/GameDbContext.cs`n*** Update File: zombie_servival-3Dgame_Server/Auth/AuthController.cs`n*** Update File: zombie_servival-3Dgame_Server/Player/PlayerController.cs`n*** Update File: zombie_servival-3Dgame_Server/Gacha/GachaController.cs`n*** Update File: zombie_servival-3Dgame_Server/Inventory/InventoryController.cs"
    $wide = Invoke-PreToolGuard $widePatch $null
    $safe = Invoke-PreToolGuard '{"command":"dotnet build"}' (@{ command = "dotnet build" } | ConvertTo-Json | ConvertFrom-Json)
    $dangerCommand = "git reset " + "--hard HEAD"
    $permissionDanger = Invoke-PreToolGuard ($dangerCommand | ConvertTo-Json) ([pscustomobject]@{ command = $dangerCommand }) "PermissionRequest"
    $relativeRmCommand = "rm -" + "rf build"
    $relativeRm = Invoke-PreToolGuard ($relativeRmCommand | ConvertTo-Json) ([pscustomobject]@{ command = $relativeRmCommand })
    $checkoutCommand = "git checkout HEAD " + "-- file.cs"
    $checkoutPath = Invoke-PreToolGuard ($checkoutCommand | ConvertTo-Json) ([pscustomobject]@{ command = $checkoutCommand })
    $removeItemCommand = "Remove" + "-Item build -Recu"
    $removeItemShort = Invoke-PreToolGuard ($removeItemCommand | ConvertTo-Json) ([pscustomobject]@{ command = $removeItemCommand })

    $sensitiveFieldsBlocked = $true
    foreach ($sensitiveField in @("ConnectionString", "ClientSecret", "AccessToken", "RefreshToken", "PrivateKey")) {
        $sensitivePayload = '{"content":"\"' + $sensitiveField + '\":\"Actual' + 'Value123!\""}'
        $sensitiveResult = Invoke-PreToolGuard $sensitivePayload $null
        if ($sensitiveResult -notmatch '"permissionDecision":"deny"') {
            $sensitiveFieldsBlocked = $false
        }
    }

    $selfTestFailures = New-Object System.Collections.Generic.List[string]
    if ($danger -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("pre-tool deny schema") }
    if ($permissionDanger -notmatch '"behavior":"deny"') { $selfTestFailures.Add("permission deny schema") }
    if ($relativeRm -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("relative recursive delete") }
    if ($checkoutPath -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("checkout path") }
    if ($removeItemShort -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("abbreviated recursive delete") }
    if ($secret -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("connection password") }
    if ($quotedSecret -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("quoted connection password") }
    if (-not $sensitiveFieldsBlocked) { $selfTestFailures.Add("sensitive fields") }
    if ($wide -notmatch '"permissionDecision":"deny"') { $selfTestFailures.Add("scope guard") }
    if (-not [string]::IsNullOrWhiteSpace($safe)) { $selfTestFailures.Add("safe command") }
    if ($selfTestFailures.Count -gt 0) {
        throw "pre_tool_guard self-test failed: $($selfTestFailures -join ', ')"
    }
    "pre_tool_guard self-test passed."
    exit 0
}

$payload = Read-HookPayload
$hookEventName = Get-HookEventName $payload.Json "PreToolUse"
$result = Invoke-PreToolGuard $payload.Raw $payload.Json $hookEventName
if (-not [string]::IsNullOrWhiteSpace($result)) { $result }

