param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+\.ps1$')]
    [string]$ScriptName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$hookDir = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $hookDir "../.."))
$scriptPath = [System.IO.Path]::GetFullPath((Join-Path $hookDir $ScriptName))

if (-not $scriptPath.StartsWith($hookDir, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Hook script path escapes hook directory: $ScriptName"
}

if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "Hook script not found: $ScriptName"
}

Push-Location $repoRoot
try {
    & $scriptPath
}
finally {
    Pop-Location
}

exit 0
