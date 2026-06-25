Set-StrictMode -Version Latest

function Read-HookPayload {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return [pscustomobject]@{ Raw = ""; Json = $null }
    }

    try { $json = $raw | ConvertFrom-Json }
    catch { $json = $null }

    [pscustomobject]@{ Raw = $raw; Json = $json }
}

function Get-RepoRoot {
    try {
        $root = (& git rev-parse --show-toplevel 2>$null | Select-Object -First 1)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($root)) {
            return $root.Trim()
        }
    }
    catch {
    }

    (Get-Location).Path
}

function Get-HookStateDirectory {
    $dir = Join-Path (Get-RepoRoot) ".codex-hook-state"
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $dir
}

function Write-HookBlock {
    param([Parameter(Mandatory = $true)][string]$Reason)
    [pscustomobject]@{ decision = "block"; reason = $Reason } | ConvertTo-Json -Compress
}

function Write-HookContext {
    param([Parameter(Mandatory = $true)][string]$Context)
    [pscustomobject]@{
        hookSpecificOutput = [pscustomobject]@{
            hookEventName = "UserPromptSubmit"
            additionalContext = $Context
        }
    } | ConvertTo-Json -Compress
}

function Write-HookWarning {
    param([Parameter(Mandatory = $true)][string]$Message)
    [Console]::Error.WriteLine($Message)
}

function Get-JsonValuesByName {
    param($Node, [Parameter(Mandatory = $true)][string[]]$Names)
    $values = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Node) { return @() }

    try { $jsonText = $Node | ConvertTo-Json -Compress -Depth 50 }
    catch { return @() }

    foreach ($name in $Names) {
        $escapedName = [regex]::Escape($name)
        foreach ($match in [regex]::Matches($jsonText, '"' + $escapedName + '"\s*:\s*"((?:\\.|[^"\\])*)"')) {
            $values.Add([System.Text.RegularExpressions.Regex]::Unescape($match.Groups[1].Value))
        }
        foreach ($match in [regex]::Matches($jsonText, '"' + $escapedName + '"\s*:\s*(-?\d+)')) {
            $values.Add($match.Groups[1].Value)
        }
    }

    return ,($values.ToArray())
}

function Test-PlaceholderValue {
    param([Parameter(Mandatory = $true)][string]$Value)
    $value = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($value)) { return $true }

    foreach ($pattern in @(
        '^your[-_ ]',
        '^change[-_ ]?me$',
        '^changeme$',
        '^<.*>$',
        '^\$\{.*\}$',
        '^%.*%$',
        '^__.*__$',
        '^example',
        '^todo$',
        '^player\d+-password$',
        '^test[-_ ]?password',
        '^\{\{.*\}\}$'
    )) {
        if ($value -match $pattern) { return $true }
    }

    $false
}

function Get-ChangedProjectFiles {
    $root = Get-RepoRoot
    $files = New-Object System.Collections.Generic.List[string]

    try {
        $lines = & git -C $root status --short --untracked-files=all 2>$null
        if ($LASTEXITCODE -ne 0) { return @() }
    }
    catch {
        return @()
    }

    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -lt 4) { continue }
        $path = $line.Substring(3).Trim()
        if ($path -match ' -> ') { $path = ($path -split ' -> ')[-1].Trim() }
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $full = Join-Path $root $path
        if (Test-Path -LiteralPath $full -PathType Leaf) { $files.Add($full) }
    }

    $files.ToArray()
}

function Get-ReferencedRepoPaths {
    param([Parameter(Mandatory = $true)][string]$Raw)
    $paths = New-Object System.Collections.Generic.List[string]

    foreach ($match in [regex]::Matches($Raw, '(?m)^\s*(?:\+\+\+|---)\s+(?:a/|b/)?([^\r\n]+)$')) {
        $value = $match.Groups[1].Value.Trim()
        if ($value -ne "/dev/null") { $paths.Add($value.Replace('\', '/')) }
    }
    foreach ($match in [regex]::Matches($Raw, '(?m)^\*\*\* (?:Add|Update|Delete) File:\s+([^\r\n]+)$')) {
        $paths.Add($match.Groups[1].Value.Trim().Replace('\', '/'))
    }
    foreach ($match in [regex]::Matches($Raw, '"(?:path|file|target|filename)"\s*:\s*"([^"]+)"')) {
        $paths.Add(([System.Text.RegularExpressions.Regex]::Unescape($match.Groups[1].Value)).Replace('\', '/'))
    }

    $paths |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim('"').Trim() } |
        Sort-Object -Unique
}

function Get-ZombieDomainNames {
    param([Parameter(Mandatory = $true)][string[]]$RelativePaths)
    $domains = New-Object System.Collections.Generic.List[string]
    foreach ($path in $RelativePaths) {
        if ($path -match '^zombie_servival-3Dgame_Server/(Auth|Player|Inventory|Gacha|Firearm|WeaponUpgrade|Data|Options|Contracts|Common)(/|$)') {
            $domains.Add($Matches[1])
        }
    }
    $domains | Sort-Object -Unique
}

function Get-CSharpOrProjectPaths {
    param([Parameter(Mandatory = $true)][string[]]$RelativePaths)
    $RelativePaths | Where-Object { $_ -match '\.(cs|csproj|json|http)$' -and $_ -match '^zombie_servival-3Dgame_Server/' }
}

function Test-OverbroadReferencedChange {
    param([Parameter(Mandatory = $true)][string[]]$RelativePaths)
    $codePaths = @(Get-CSharpOrProjectPaths $RelativePaths)
    if ($codePaths.Count -eq 0) { return $null }

    $domains = @(Get-ZombieDomainNames $codePaths)
    $criticalPaths = @($codePaths | Where-Object {
        $_ -match '^zombie_servival-3Dgame_Server/Program\.cs$' -or
        $_ -match '^zombie_servival-3Dgame_Server/Data/GameDbContext\.cs$' -or
        $_ -match '^zombie_servival-3Dgame_Server/zombie_survival-3Dgame_Server\.csproj$' -or
        $_ -match '^zombie_servival-3Dgame_Server/Contracts/' -or
        $_ -match '^zombie_servival-3Dgame_Server/Options/'
    })

    if ($codePaths.Count -gt 10) {
        return "한 번에 서버 코드/설정 파일 $($codePaths.Count)개를 변경하려고 합니다. 작업을 도메인 또는 계층 단위로 나누고, 필요한 경우 먼저 계획과 3가지 선택지를 제시하세요."
    }
    if ($domains.Count -gt 3) {
        return "한 번에 $($domains.Count)개 도메인($($domains -join ', '))을 변경하려고 합니다. 과도한 코드 변경을 피하려면 도메인별로 나누어 진행하세요."
    }
    if ($criticalPaths.Count -ge 2 -and $codePaths.Count -gt 4) {
        return "Program.cs, GameDbContext, Contracts, Options, csproj 같은 핵심 파일을 여러 코드 변경과 함께 묶고 있습니다. API/DB/런타임 영향이 섞이면 먼저 개발자에게 구현 방향을 확인하세요."
    }

    $null
}

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $root = (Get-RepoRoot).TrimEnd('\', '/')
    $full = [System.IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
    }
    $Path.Replace('\', '/')
}

function Add-Finding {
    param(
        [Parameter(Mandatory = $true)]$Findings,
        [Parameter(Mandatory = $true)][string]$Severity,
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$Line,
        [Parameter(Mandatory = $true)][string]$Rule,
        [Parameter(Mandatory = $true)][string]$Message
    )
    $Findings.Add([pscustomobject]@{
        Severity = $Severity
        Category = $Category
        Path = Get-RelativePath $Path
        Line = $Line
        Rule = $Rule
        Message = $Message
    })
}

function Invoke-ZombieStaticScan {
    param([Parameter(Mandatory = $true)][string[]]$Paths)
    $findings = New-Object System.Collections.Generic.List[object]
    $extensions = @(".cs", ".json", ".http", ".md", ".toml", ".ps1", ".csproj")

    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $relative = Get-RelativePath $path
        if ($relative -match '(^|/)(bin|obj|\.git|\.vs|\.idea|hook-state|logs)(/|$)') { continue }

        $extension = [System.IO.Path]::GetExtension($path)
        if ($extensions -notcontains $extension) { continue }

        try { $lines = @(Get-Content -LiteralPath $path -ErrorAction Stop) }
        catch { continue }

        $text = $lines -join "`n"
        if ($text -match '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----') {
            Add-Finding $findings "Critical" "Security" $path 1 "secret-private-key" "Private key material must not be committed or written by Codex."
        }
        if ($text -match '(?i)\bBearer\s+[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+') {
            Add-Finding $findings "Critical" "Security" $path 1 "secret-bearer-token" "Bearer/JWT token value detected. Use environment variables or user secrets."
        }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $lineNumber = $i + 1
            $line = [string]$lines[$i]

            if ($line -match '(?i)\bPassword\s*=\s*([^;`"''\s}]+)') {
                $value = $Matches[1]
                if (-not (Test-PlaceholderValue $value)) {
                    Add-Finding $findings "Critical" "Security" $path $lineNumber "secret-connection-password" "Connection string password detected. Store it outside the repository."
                }
            }

            if ($line -match '(?i)"(SecretKey|ApiKey|AppPassword|SmtpPassword|Password)"\s*:\s*"([^"]*)"') {
                $key = $Matches[1]
                $value = $Matches[2]
                $configLike = $relative -match '(^|/)appsettings(\..*)?\.json$|\.env$'
                if ($configLike -and -not (Test-PlaceholderValue $value)) {
                    Add-Finding $findings "Critical" "Security" $path $lineNumber "secret-config-value" "$key contains a non-placeholder value in a config-like file."
                }
            }

            if ($extension -ne ".cs") { continue }

            if ($line -match '\basync\s+void\b') {
                Add-Finding $findings "Critical" "Reliability" $path $lineNumber "async-void" "Use async Task instead of async void."
            }
            if ($line -match '\.(Result|Wait)\s*(\(|;)') {
                Add-Finding $findings "Warning" "Performance" $path $lineNumber "sync-over-async" "Blocking async work can starve request threads. Prefer await."
            }
            if ($line -match '\b(CancellationToken\.None|new\s+CancellationToken\s*\()') {
                Add-Finding $findings "Warning" "Reliability" $path $lineNumber "cancellation-token-dropped" "Pass the request CancellationToken through async EF/service calls."
            }
            if ($line -match '\b(ToListAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|AnyAsync|SaveChangesAsync)\s*\(\s*\)') {
                Add-Finding $findings "Warning" "Performance" $path $lineNumber "missing-cancellation-token" "Async EF call is missing CancellationToken."
            }
            if ($line -match '\.ToList\s*\(\s*\)' -and $relative -match '/(Auth|Player|Inventory|Gacha|Firearm|WeaponUpgrade)/') {
                Add-Finding $findings "Warning" "Performance" $path $lineNumber "sync-enumeration" "Synchronous materialization in a service/domain file may block request handling."
            }
            if ($line -match '_logger\.[A-Za-z]+\s*\(\s*\$"') {
                Add-Finding $findings "Warning" "Security" $path $lineNumber "interpolated-log" "Use structured logging placeholders to avoid accidental sensitive data leakage."
            }
            if ($relative -match 'Controller\.cs$' -and $line -match '\bGameDbContext\b') {
                Add-Finding $findings "Critical" "Architecture" $path $lineNumber "controller-dbcontext" "Controllers should not use GameDbContext directly. Route through a domain service."
            }
            if ($relative -match 'Service\.cs$' -and $line -match '\b(IActionResult|ActionResult<|ObjectResult|Ok\s*\(|BadRequest\s*\()') {
                Add-Finding $findings "Warning" "Architecture" $path $lineNumber "service-http-concern" "Services should not return HTTP-specific results."
            }
            if ($relative -match '^zombie_servival-3Dgame_Server/Contracts/(?!Auth/).+Request\.cs$' -and $line -match '\b(PlayerId|UserId)\b') {
                Add-Finding $findings "Critical" "Security" $path $lineNumber "client-player-id" "Request DTOs must not accept player/user identity. Read it from JWT claims."
            }
            if ($relative -match '^zombie_servival-3Dgame_Server/Contracts/Gacha/' -and $line -match '\b(Seed|Random|Result|RewardId)\b') {
                Add-Finding $findings "Critical" "Security" $path $lineNumber "client-gacha-control" "Gacha requests must not accept seed/result/reward identifiers from clients."
            }
        }

        if ($extension -eq ".cs") {
            $includeCount = ([regex]::Matches($text, '\.Include\s*\(')).Count
            if ($includeCount -ge 3 -and $text -match 'ToListAsync') {
                Add-Finding $findings "Warning" "Performance" $path 1 "large-include-query" "Query has several Include calls. Check whether projection, split query, or narrower DTO loading is better."
            }

            $hasRead = $text -match '\b(ToListAsync|FirstOrDefaultAsync|SingleOrDefaultAsync)\s*\('
            $hasNoTracking = $text -match '\.AsNoTracking\s*\('
            $hasWrites = $text -match '\b(SaveChangesAsync|AddAsync|AddRange|Remove|RemoveRange|Update)\b'
            if ($hasRead -and -not $hasNoTracking -and -not $hasWrites -and $relative -match 'Service\.cs$') {
                Add-Finding $findings "Warning" "Performance" $path 1 "missing-as-no-tracking-review" "Read-only EF query appears to lack AsNoTracking(). Verify tracking is actually needed."
            }
        }
    }

    $findings.ToArray()
}

function Format-Findings {
    param([Parameter(Mandatory = $true)][object[]]$Findings, [int]$Limit = 12)
    if ($Findings.Count -eq 0) { return "" }
    $lines = foreach ($finding in ($Findings | Select-Object -First $Limit)) {
        "- [$($finding.Severity)/$($finding.Category)] $($finding.Path):$($finding.Line) $($finding.Message) ($($finding.Rule))"
    }
    if ($Findings.Count -gt $Limit) {
        $lines += "- ... $($Findings.Count - $Limit) more finding(s)."
    }
    $lines -join "`n"
}




