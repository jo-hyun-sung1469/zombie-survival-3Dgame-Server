param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Get-LastBuildTimestamp {
    $path = Join-Path (Get-HookStateDirectory) "last-build-success.json"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    try {
        $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        [datetime]::Parse(
            $record.timestampUtc,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
    }
    catch { $null }
}

function Get-LatestChangeSummarySection {
    $summaryPath = Join-Path (Get-RepoRoot) ".codex/change-summaries/CHANGE_SUMMARY.md"
    if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) { return $null }
    try { $summary = Get-Content -LiteralPath $summaryPath -Raw }
    catch { return $null }

    $sections = $summary -split '(?m)^##\s+'
    if ($sections.Count -lt 2) { return $null }
    $sections[1]
}

function Test-ChangeSummaryRequired {
    param([Parameter(Mandatory = $true)][string[]]$RelativePaths)
    $requiresSummary = $false
    foreach ($path in $RelativePaths) {
        if ($path -match '^(AGENTS\.md|\.codex/codex\.md|\.codex/skills/|\.codex/hooks|\.codex/agents|\.codex/.*\.md$)') {
            if ($path -ne ".codex/change-summaries/CHANGE_SUMMARY.md") { $requiresSummary = $true }
        }
    }
    if (-not $requiresSummary) { return $false }
    $RelativePaths -notcontains ".codex/change-summaries/CHANGE_SUMMARY.md"
}

function Test-DecisionRecordRequired {
    param([Parameter(Mandatory = $true)][string[]]$RelativePaths)
    $codePaths = @(Get-CSharpOrProjectPaths $RelativePaths)
    if ($codePaths.Count -eq 0) { return $false }
    $domains = @(Get-ZombieDomainNames $codePaths)
    if ($codePaths.Count -le 10 -and $domains.Count -le 3) { return $false }

    $latestSection = Get-LatestChangeSummarySection
    if ([string]::IsNullOrWhiteSpace($latestSection)) { return $true }

    $latestSection -notmatch '남은 사용자 결정'
}

function Write-SessionSummary {
    $logPath = Join-Path (Get-HookStateDirectory) "activity.jsonl"
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) { return }
    $entries = @(Get-Content -LiteralPath $logPath -Tail 80 -ErrorAction SilentlyContinue)
    if ($entries.Count -eq 0) { return }
    $buildCount = @($entries | Where-Object { $_ -match 'dotnet\s+build' }).Count
    Write-HookWarning "[zombie-hook] Session activity: $($entries.Count) recent tool event(s), $buildCount dotnet build command(s) observed."
}

function Invoke-StopQualityGate {
    $changedFiles = @(Get-ChangedProjectFiles)
    if ($changedFiles.Count -eq 0) {
        Write-SessionSummary
        return ""
    }

    $relativePaths = @($changedFiles | ForEach-Object { Get-RelativePath $_ })
    $findings = @(Invoke-ZombieStaticScan $changedFiles)
    $critical = @($findings | Where-Object { $_.Severity -eq "Critical" })
    $warnings = @($findings | Where-Object { $_.Severity -ne "Critical" })

    if ($warnings.Count -gt 0) {
        Write-HookWarning "[zombie-hook] Non-blocking performance/security warnings:`n$(Format-Findings $warnings 10)"
    }
    if ($critical.Count -gt 0) {
        return Write-HookBlock "[zombie quality gate] Critical issue(s) found before stopping:`n$(Format-Findings $critical 12)`nFix these before ending the task."
    }
    if (Test-ChangeSummaryRequired $relativePaths) {
        return Write-HookBlock "[zombie harness gate] Harness/workflow files changed but .codex/change-summaries/CHANGE_SUMMARY.md was not updated. Add a Korean summary with date, purpose, changed areas, verification, and remaining decisions."
    }
    if (Test-DecisionRecordRequired $relativePaths) {
        return Write-HookBlock "[zombie decision gate] Broad code changes require an explicit Korean change summary that records remaining user decisions. This prevents Codex from silently making excessive implementation decisions alone."
    }

    $codeChanged = @($relativePaths | Where-Object { $_ -match '\.(cs|csproj)$' })
    if ($codeChanged.Count -gt 0) {
        $lastBuild = Get-LastBuildTimestamp
        if ($null -eq $lastBuild) {
            Write-HookWarning "[zombie-hook] C# project files changed, but no dotnet build success was observed by hooks in this session."
        }
        else {
            Write-HookWarning "[zombie-hook] Last observed dotnet build success: $($lastBuild.ToString("u"))."
        }
    }
    Write-SessionSummary
    ""
}

if ($SelfTest) {
    if (-not (Test-ChangeSummaryRequired @(".codex/hooks/foo.ps1"))) { throw "stop_quality_gate self-test failed." }
    if (Test-DecisionRecordRequired @(
        "zombie_servival-3Dgame_Server/Auth/AuthController.cs",
        "zombie_servival-3Dgame_Server/Player/PlayerController.cs",
        "zombie_servival-3Dgame_Server/Gacha/GachaController.cs",
        "zombie_servival-3Dgame_Server/Inventory/InventoryController.cs",
        "zombie_servival-3Dgame_Server/Firearm/FirearmController.cs",
        "zombie_servival-3Dgame_Server/WeaponUpgrade/WeaponUpgradeController.cs",
        "zombie_servival-3Dgame_Server/Program.cs",
        "zombie_servival-3Dgame_Server/Data/GameDbContext.cs",
        "zombie_servival-3Dgame_Server/Options/JwtOptions.cs",
        "zombie_servival-3Dgame_Server/Contracts/Auth/LoginRequest.cs",
        "zombie_servival-3Dgame_Server/Contracts/Gacha/GachaPullResponse.cs"
    )) { throw "stop_quality_gate decision self-test failed." }
    "stop_quality_gate self-test passed."
    exit 0
}

$result = Invoke-StopQualityGate
if (-not [string]::IsNullOrWhiteSpace($result)) { $result }

