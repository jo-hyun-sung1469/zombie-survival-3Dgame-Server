param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Get-PromptText {
    param($Json, [string]$Raw)

    if ($null -ne $Json) {
        $values = @(Get-JsonValuesByName $Json @("prompt", "message", "input"))
        if ($values.Count -gt 0) { return ($values -join "`n") }
    }

    foreach ($key in @("prompt", "message", "input")) {
        $pattern = '"' + [regex]::Escape($key) + '"\s*:\s*"((?:\\.|[^"\\])*)"'
        $match = [regex]::Match($Raw, $pattern)
        if ($match.Success) {
            return [System.Text.RegularExpressions.Regex]::Unescape($match.Groups[1].Value)
        }
    }

    $Raw
}

function New-ContextForPrompt {
    param([Parameter(Mandatory = $true)][string]$Prompt)
    if ([string]::IsNullOrWhiteSpace($Prompt)) { return "" }
    $lower = $Prompt.ToLowerInvariant()
    $snippets = New-Object System.Collections.Generic.List[string]
    function Add-Snippet { param([string]$Text) if (-not $snippets.Contains($Text)) { $snippets.Add($Text) } }

    if ($lower -match 'auth|login|register|jwt|token|claim|password|email|smtp|verification|인증|로그인|회원가입') {
        Add-Snippet "[Zombie auth context] Use jwt-auth-flow: JWT identity comes from the userId claim; register duplicate username returns 409; invalid login returns 401; keep JWT and SMTP secrets in configuration/user secrets, never in source."
    }
    if ($lower -match 'save|inventory|weapon|gold|player-data|무기|골드|저장') {
        Add-Snippet "[Zombie save context] Use player-save-flow: player identity is read from JWT, Gold must be non-negative, WeaponStates is required, weapon names must exist in the firearm catalog, and omitted weapon rows are removed by current upsert semantics."
    }
    if ($lower -match 'ef|dbcontext|mysql|database|query|include|tracking|performance|slow|성능|쿼리|데이터베이스') {
        Add-Snippet "[Zombie persistence/performance context] Use efcore-mysql-persistence: read-only EF queries should use AsNoTracking; pass CancellationToken into async EF calls; Include only the relationships the service actually needs; call out schema impact because this project uses EnsureCreated rather than migrations."
    }
    if ($lower -match 'gacha|rng|random|probability|reward|roulette|가챠|확률|보상') {
        Add-Snippet "[Zombie gacha/security context] RNG and reward decisions must be server-side only. Do not accept client seed, selected reward, rarity, probability, or cost override fields."
    }
    if ($lower -match 'controller|service|dto|contract|endpoint|api|architecture|아키텍처|컨트롤러|서비스') {
        Add-Snippet "[Zombie API context] Use aspnet-api-arch: flow is Controller -> Service -> DbContext or Options; controllers handle HTTP only; services own business rules; all async controller/service methods pass CancellationToken."
    }
    if ($lower -match 'security|secret|hardcode|rate|abuse|취약|보안|비밀|하드코딩') {
        Add-Snippet "[Zombie security context] Use security-checklist: block hardcoded secrets, reject client-supplied identity, keep gacha/server-authority decisions on the server, and check rate limits for login, verification, gacha, and expensive endpoints."
    }
    if ($lower -match 'commit|pr|pull request|hook|skill|agent|하네스|훅|스킬|커밋') {
        Add-Snippet "[Zombie harness context] For harness/workflow/multi-file changes, update .codex/change-summaries/CHANGE_SUMMARY.md in Korean with date, purpose, changed areas, verification, and remaining decisions."
    }

    if ($snippets.Count -eq 0) { return "" }
    "`n`n---`n" + ($snippets -join "`n")
}

if ($SelfTest) {
    $context = New-ContextForPrompt "JWT auth performance hook"
    if ($context -notmatch 'jwt-auth-flow' -or $context -notmatch 'performance') { throw "user_prompt_context self-test failed." }
    "user_prompt_context self-test passed."
    exit 0
}

$payload = Read-HookPayload
$context = New-ContextForPrompt (Get-PromptText $payload.Json $payload.Raw)
if (-not [string]::IsNullOrWhiteSpace($context)) { Write-HookContext $context }


