param(
    [switch]$RotateExposedSecrets
)

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$environmentPath = Join-Path $repositoryRoot "app.env"
$examplePath = Join-Path $repositoryRoot "app.env.example"

if (-not (Test-Path -LiteralPath $environmentPath))
{
    Copy-Item -LiteralPath $examplePath -Destination $environmentPath
}

$lines = [System.Collections.Generic.List[string]]::new()
Get-Content -LiteralPath $environmentPath | ForEach-Object { $lines.Add($_) }

function Get-EnvironmentValue([string]$key)
{
    foreach ($line in $lines)
    {
        if ($line -match "^$([regex]::Escape($key))=(.*)$")
        {
            return $matches[1]
        }
    }

    return $null
}

function Set-EnvironmentValue([string]$key, [string]$value)
{
    for ($index = 0; $index -lt $lines.Count; $index++)
    {
        if ($lines[$index] -match "^$([regex]::Escape($key))=")
        {
            $lines[$index] = "${key}=${value}"
            return
        }
    }

    $lines.Add("${key}=${value}")
}

function New-HexSecret([int]$byteCount)
{
    $bytes = New-Object byte[] $byteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try
    {
        $generator.GetBytes($bytes)
    }
    finally
    {
        $generator.Dispose()
    }

    return [System.BitConverter]::ToString($bytes).Replace("-", "").ToLowerInvariant()
}

$generatedKeys = [System.Collections.Generic.List[string]]::new()
$secretDefinitions = @{
    MYSQL_PASSWORD = 32
    MYSQL_ROOT_PASSWORD = 32
    JWT_SECRET_KEY = 64
}

foreach ($entry in $secretDefinitions.GetEnumerator())
{
    if ($RotateExposedSecrets -or [string]::IsNullOrWhiteSpace((Get-EnvironmentValue $entry.Key)))
    {
        Set-EnvironmentValue $entry.Key (New-HexSecret $entry.Value)
        $generatedKeys.Add($entry.Key)
    }
}

if ($RotateExposedSecrets)
{
    Set-EnvironmentValue "SMTP_PASSWORD" ""
}

if ([string]::IsNullOrWhiteSpace((Get-EnvironmentValue "BACKUP_ENABLED")))
{
    Set-EnvironmentValue "BACKUP_ENABLED" "false"
}

[System.IO.File]::WriteAllLines(
    $environmentPath,
    $lines,
    [System.Text.UTF8Encoding]::new($false))

Write-Output "app.env 준비 완료"
if ($generatedKeys.Count -gt 0)
{
    Write-Output "새로 생성한 비밀값: $($generatedKeys -join ', ')"
}

if ([string]::IsNullOrWhiteSpace((Get-EnvironmentValue "SMTP_PASSWORD")))
{
    Write-Output "SMTP_PASSWORD는 새 Gmail 앱 비밀번호로 직접 입력해야 합니다."
}
