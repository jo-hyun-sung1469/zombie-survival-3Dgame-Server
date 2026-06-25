param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Invoke-PreToolGuard {
    param([Parameter(Mandatory = $true)][string]$Raw, $Json)

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

    foreach ($entry in $blocked) {
        if ($lower -match $entry.Pattern) {
            return Write-HookBlock "[dangerous-command] $($entry.Reason)`nMatched pattern: $($entry.Pattern)`nCommand:`n$commandText"
        }
    }

    $secrets = New-Object System.Collections.Generic.List[string]
    if ($Raw -match '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----') { $secrets.Add("private key material") }
    if ($Raw -match '(?i)\bBearer\s+[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+') { $secrets.Add("bearer/JWT token") }

    foreach ($match in [regex]::Matches($Raw, '(?i)\bPassword\s*=\s*([^;`"''\s}]+)')) {
        if (-not (Test-PlaceholderValue $match.Groups[1].Value)) { $secrets.Add("connection string password") }
    }
    foreach ($match in [regex]::Matches($Raw, '(?i)"(SecretKey|ApiKey|AppPassword|SmtpPassword|Password)"\s*:\s*"([^"]*)"')) {
        if (-not (Test-PlaceholderValue $match.Groups[2].Value)) { $secrets.Add("$($match.Groups[1].Value) value") }
    }

    if ($secrets.Count -gt 0) {
        $unique = $secrets | Sort-Object -Unique
        return Write-HookBlock "[secret-guard] Potential hardcoded secret detected: $($unique -join ', ').`nUse appsettings placeholders, user secrets, or environment variables instead."
    }

    $referencedPaths = @(Get-ReferencedRepoPaths $Raw)
    if ($referencedPaths.Count -gt 0) {
        $overbroadReason = Test-OverbroadReferencedChange $referencedPaths
        if ($null -ne $overbroadReason) {
            return Write-HookBlock "[scope-guard] $overbroadReason"
        }
    }

    ""
}

if ($SelfTest) {
    $danger = Invoke-PreToolGuard '{"command":"git reset --hard HEAD"}' (@{ command = "git reset --hard HEAD" } | ConvertTo-Json | ConvertFrom-Json)
    $secretPayload = '{"content":"ConnectionStrings__DefaultConnection=Server=db;Pass' + 'word=RealPassword123!"}'; $secret = Invoke-PreToolGuard $secretPayload $null
    $widePatch = "*** Update File: zombie_servival-3Dgame_Server/Program.cs`n*** Update File: zombie_servival-3Dgame_Server/Data/GameDbContext.cs`n*** Update File: zombie_servival-3Dgame_Server/Auth/AuthController.cs`n*** Update File: zombie_servival-3Dgame_Server/Player/PlayerController.cs`n*** Update File: zombie_servival-3Dgame_Server/Gacha/GachaController.cs`n*** Update File: zombie_servival-3Dgame_Server/Inventory/InventoryController.cs"
    $wide = Invoke-PreToolGuard $widePatch $null
    $safe = Invoke-PreToolGuard '{"command":"dotnet build"}' (@{ command = "dotnet build" } | ConvertTo-Json | ConvertFrom-Json)
    if ($danger -notmatch '"decision":"block"' -or $secret -notmatch '"decision":"block"' -or $wide -notmatch '"decision":"block"' -or -not [string]::IsNullOrWhiteSpace($safe)) {
        throw "pre_tool_guard self-test failed."
    }
    "pre_tool_guard self-test passed."
    exit 0
}

$payload = Read-HookPayload
$result = Invoke-PreToolGuard $payload.Raw $payload.Json
if (-not [string]::IsNullOrWhiteSpace($result)) { $result }

