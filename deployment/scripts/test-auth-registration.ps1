param([string]$AppImage = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Net.Http

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runId = [Guid]::NewGuid().ToString('N')
$resourcePrefix = "zombie-auth-smoke-$runId"
$databaseContainer = "$resourcePrefix-db"
$appContainer = "$resourcePrefix-app"
$networkName = "$resourcePrefix-network"
$databaseCredential = [Guid]::NewGuid().ToString('N')
$rootCredential = [Guid]::NewGuid().ToString('N')
$signingKey = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$ownsImage = [string]::IsNullOrWhiteSpace($AppImage)
if ($ownsImage) { $AppImage = "zombie-auth-smoke:$runId" }
$examplePassword = 'Abc12/'
$script:checks = 0
$script:baseAddress = ''
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.UseProxy = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(15)

function Invoke-Docker {
    $ErrorActionPreference = 'Continue'
    $output = & docker @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker $($args[0]) failed: $($output -join [Environment]::NewLine)"
    }
    $output | ForEach-Object { $_.ToString() }
}

function Invoke-TestSql([string]$Sql, [switch]$AsRoot) {
    $ErrorActionPreference = 'Continue'
    $sqlCredential = if ($AsRoot) { $rootCredential } else { $databaseCredential }
    $sqlUser = if ($AsRoot) { 'root' } else { 'auth_smoke_user' }
    $output = $Sql | & docker exec -i -e "MYSQL_PWD=$sqlCredential" $databaseContainer `
        mysql --protocol=tcp --host=127.0.0.1 "--user=$sqlUser" --database=auth_smoke `
        --batch --skip-column-names --default-character-set=utf8mb4 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Test SQL failed: $($output -join [Environment]::NewLine)" }
    ($output | ForEach-Object { $_.ToString() }) -join "`n"
}

function Assert-That([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw "Assertion failed: $Description" }
    $script:checks++
}

function Wait-TestApp {
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $response = $client.GetAsync("$script:baseAddress/health").GetAwaiter().GetResult()
            $ready = $response.IsSuccessStatusCode
            $response.Dispose()
            if ($ready) { return }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    throw 'The isolated test app did not become healthy within 120 seconds.'
}

function Restart-TestApp {
    Invoke-Docker restart $appContainer | Out-Null
    $binding = Invoke-Docker port $appContainer '8080/tcp'
    $script:baseAddress = "http://$binding"
    Wait-TestApp
}

function Start-Api([string]$Method, [string]$Path, $Body = $null, [string]$Bearer = '') {
    $request = New-Object System.Net.Http.HttpRequestMessage(
        [System.Net.Http.HttpMethod]::new($Method), "$script:baseAddress$Path")
    if ($null -ne $Body) {
        $request.Content = New-Object System.Net.Http.StringContent(
            ($Body | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, 'application/json')
    }
    if ($Bearer) {
        $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Bearer)
    }
    [pscustomobject]@{ Request = $request; Pending = $client.SendAsync($request) }
}

function Complete-Api($Operation) {
    $response = $null
    try {
        $response = $Operation.Pending.GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $body = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
        [pscustomobject]@{
            Status = [int]$response.StatusCode
            Body = $body
            CacheControl = [string]$response.Headers.CacheControl
        }
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $Operation.Request.Dispose()
    }
}

function Invoke-Api([string]$Method, [string]$Path, $Body = $null, [string]$Bearer = '') {
    Complete-Api (Start-Api $Method $Path $Body $Bearer)
}

function Add-Proof([string]$Id, [string]$Email, [string]$State = 'verified') {
    # Registration fixtures bypass SMTP only inside this disposable database.
    $expiry = if ($State -eq 'expired') { 'UTC_TIMESTAMP(6) - INTERVAL 1 MINUTE' } else { 'UTC_TIMESTAMP(6) + INTERVAL 10 MINUTE' }
    $verified = if ($State -eq 'unverified') { 'NULL' } else { 'UTC_TIMESTAMP(6)' }
    $consumed = if ($State -eq 'consumed') { 'UTC_TIMESTAMP(6)' } else { 'NULL' }
    Invoke-TestSql @"
INSERT INTO AuthVerificationCodes
    (Id, Email, CodeHash, AttemptCount, CreatedAtUtc, ExpiresAtUtc, VerifiedAtUtc, ConsumedAtUtc, Version)
VALUES ('$Id', '$Email', 'smoke-fixture-code-not-used', 0, UTC_TIMESTAMP(6), $expiry, $verified, $consumed, 1);
"@ | Out-Null
}

function New-Registration([string]$UserName, [string]$Email, [string]$Proof) {
    @{ userName = $UserName; email = $Email; emailVerificationId = $Proof; 'password' = $examplePassword }
}

function Invoke-RegistrationRace($First, $Second) {
    $firstPending = Start-Api 'POST' '/api/auth/register' $First
    $secondPending = Start-Api 'POST' '/api/auth/register' $Second
    $firstResponse = Complete-Api $firstPending
    $secondResponse = Complete-Api $secondPending
    Assert-That ((@($firstResponse.Status, $secondResponse.Status) | Sort-Object) -join ',' -eq '201,409') `
        'A concurrent registration creates exactly one user and rejects the other with 409.'
    if ($firstResponse.Status -eq 409) {
        return [pscustomobject]@{ Loser = $First; Conflict = $firstResponse }
    }
    [pscustomobject]@{ Loser = $Second; Conflict = $secondResponse }
}

function Assert-Unconsumed([string]$Proof) {
    $remaining = Invoke-TestSql "SELECT COUNT(*) FROM AuthVerificationCodes WHERE Id='$Proof' AND VerifiedAtUtc IS NOT NULL AND ConsumedAtUtc IS NULL AND Version=1;"
    Assert-That ($remaining -eq '1') 'Failed registration preserves verification and its version.'
}

function Remove-OwnedResource([string]$Kind, [string]$Name) {
    $ErrorActionPreference = 'Continue'
    $labelPath = if ($Kind -eq 'network') { '.Labels.auth_smoke_run' } else { '.Config.Labels.auth_smoke_run' }
    $label = & docker $Kind inspect --format "{{$labelPath}}" $Name 2>$null
    if ($LASTEXITCODE -ne 0 -or $label -ne $runId) { return }
    if ($Kind -eq 'container') { & docker container rm --force --volumes $Name | Out-Null }
    else { & docker $Kind rm $Name | Out-Null }
    if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove isolated $Kind $Name" }
}

try {
    Invoke-Docker version --format '{{.Server.Version}}' | Out-Null
    if ($ownsImage) {
        Write-Host 'Building an isolated test app image...'
        Invoke-Docker build --label "auth_smoke_run=$runId" --tag $AppImage --file (Join-Path $repoRoot 'Dockerfile') $repoRoot | Out-Null
    }
    Invoke-Docker network create --label "auth_smoke_run=$runId" $networkName | Out-Null
    Invoke-Docker run --detach --name $databaseContainer --label "auth_smoke_run=$runId" `
        --network $networkName --network-alias mysql `
        -e "MYSQL_ROOT_PASSWORD=$rootCredential" -e MYSQL_DATABASE=auth_smoke `
        -e MYSQL_USER=auth_smoke_user -e "MYSQL_PASSWORD=$databaseCredential" `
        mysql:8.4.10 --character-set-server=utf8mb4 --collation-server=utf8mb4_0900_ai_ci | Out-Null

    Write-Host 'Waiting for isolated MySQL...'
    $databaseReady = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            if ((Invoke-TestSql 'SELECT 1;') -eq '1') { $databaseReady = $true; break }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $databaseReady) { throw 'Isolated MySQL did not become ready.' }

    Invoke-Docker run --detach --name $appContainer --label "auth_smoke_run=$runId" `
        --network $networkName --publish '127.0.0.1::8080' `
        -e ASPNETCORE_ENVIRONMENT=Production -e Database__Host=mysql -e Database__Port=3306 `
        -e Database__Name=auth_smoke -e Database__User=auth_smoke_user `
        -e "Database__Credential=$databaseCredential" -e Database__SslMode=Disabled `
        -e "Jwt__SecretKey=$signingKey" -e SmtpEmail__Host=unused.invalid `
        -e SmtpEmail__EnableSsl=true -e SmtpEmail__FromAddress=smoke@example.com $AppImage | Out-Null
    $binding = Invoke-Docker port $appContainer '8080/tcp'
    $script:baseAddress = "http://$binding"
    Wait-TestApp

    $jamo = ([string][char]0x314B) * 3
    $available = Invoke-Api 'GET' ('/api/auth/register/username-availability?userName=' + [Uri]::EscapeDataString($jamo))
    Assert-That ($available.Status -eq 200 -and $available.Body.isAvailable) 'Standalone Korean jamo are accepted.'
    Assert-That ($available.CacheControl -match 'no-store') 'Availability is not cached.'
    $invalidQuery = Invoke-Api 'GET' '/api/auth/register/username-availability?userName=%20abc'
    Assert-That ($invalidQuery.Status -eq 400 -and $invalidQuery.Body.errors.PSObject.Properties.Name -contains 'UserName') 'Invalid query returns a field validation error.'
    $invalidRegistration = New-Registration 'Bad_Name' 'invalid@example.com' 'missing'
    $invalidRegistration['password'] = 'Abc12_'
    $invalid = Invoke-Api 'POST' '/api/auth/register' $invalidRegistration
    Assert-That ($invalid.Status -eq 400 -and $invalid.Body.errors.PSObject.Properties.Name -contains 'Password') 'Registration validates password before accessing verification.'
    Assert-That ((Invoke-Api 'GET' '/api/auth/me').Status -eq 401) 'The authenticated endpoint rejects anonymous requests.'
    Write-Host 'PASS: HTTP validation, standalone jamo, cache policy and authorization.'

    Restart-TestApp
    Add-Proof 'nick-a' 'nicka@example.com'
    Add-Proof 'nick-b' 'nickb@example.com'
    foreach ($attempt in 1..2) {
        Assert-That ((Invoke-Api 'GET' '/api/auth/register/username-availability?userName=raceuser').Body.isAvailable) 'Both clients can observe availability before registering.'
    }
    $race = Invoke-RegistrationRace (New-Registration 'raceuser' 'nicka@example.com' 'nick-a') `
        (New-Registration 'raceuser' 'nickb@example.com' 'nick-b')
    Assert-That ($race.Conflict.Body.code -eq 'username_already_exists') 'Username UNIQUE conflict has a specific error code.'
    Assert-That ((Invoke-TestSql "SELECT COUNT(*) FROM Users WHERE UserName='raceuser';") -eq '1') 'Only one row owns the username.'
    Assert-Unconsumed $race.Loser.emailVerificationId
    $race.Loser.userName = 'retryuser'
    $retry = Invoke-Api 'POST' '/api/auth/register' $race.Loser
    Assert-That ($retry.Status -eq 201) 'A new username reuses the verified email after a conflict.'
    Assert-That (-not (Invoke-Api 'GET' '/api/auth/register/username-availability?userName=raceuser').Body.isAvailable) 'An occupied username is unavailable.'
    $login = Invoke-Api 'POST' '/api/auth/login' @{ userName = 'retryuser'; 'password' = $examplePassword }
    Assert-That ($login.Status -eq 200) 'A password containing slash logs in successfully.'
    $me = Invoke-Api 'GET' '/api/auth/me' $null $login.Body.accessToken
    Assert-That ($me.Status -eq 200 -and $me.Body.userName -eq 'retryuser') 'The issued JWT authorizes the new user.'
    Invoke-TestSql "UPDATE Users SET UserName='Legacy_Player' WHERE UserName='retryuser';" | Out-Null
    $legacyLogin = Invoke-Api 'POST' '/api/auth/login' @{ userName = 'Legacy_Player'; 'password' = $examplePassword }
    Assert-That ($legacyLogin.Status -eq 200) 'Existing usernames outside the new policy still log in.'
    Write-Host 'PASS: username race, verification reuse, login, JWT and legacy username.'

    Restart-TestApp
    Add-Proof 'email-a' 'emailrace@example.com'
    Add-Proof 'email-b' 'emailrace@example.com'
    $emailRace = Invoke-RegistrationRace (New-Registration 'mailone' 'emailrace@example.com' 'email-a') `
        (New-Registration 'mailtwo' 'emailrace@example.com' 'email-b')
    Assert-That ($emailRace.Conflict.Body.code -eq 'email_already_exists') 'Email race returns an email-specific conflict.'
    Assert-That ((Invoke-TestSql "SELECT COUNT(*) FROM Users WHERE Email='emailrace@example.com';") -eq '1') 'Only one row owns the email.'
    Assert-Unconsumed $emailRace.Loser.emailVerificationId
    $bothDuplicate = Invoke-Api 'POST' '/api/auth/register' (New-Registration 'raceuser' 'emailrace@example.com' 'unused')
    Assert-That ($bothDuplicate.Status -eq 409 -and $bothDuplicate.Body.code -eq 'email_already_exists') 'Email conflict takes precedence when both fields are duplicates.'
    Write-Host 'PASS: email race and duplicate precedence.'

    Restart-TestApp
    Add-Proof 'shared' 'shared@example.com'
    $null = Invoke-RegistrationRace (New-Registration 'sharedone' 'shared@example.com' 'shared') `
        (New-Registration 'sharedtwo' 'shared@example.com' 'shared')
    Assert-That ((Invoke-TestSql "SELECT COUNT(*) FROM Users WHERE Email='shared@example.com';") -eq '1') 'A shared verification creates one account.'
    Assert-That ((Invoke-TestSql "SELECT COUNT(*) FROM AuthVerificationCodes WHERE Id='shared' AND ConsumedAtUtc IS NOT NULL AND Version=2;") -eq '1') 'A shared verification is consumed exactly once.'
    Add-Proof 'used' 'used@example.com' 'consumed'
    Add-Proof 'unverified' 'unverified@example.com' 'unverified'
    Assert-That ((Invoke-Api 'POST' '/api/auth/register' (New-Registration 'useduser' 'used@example.com' 'used')).Status -eq 409) 'Already-consumed verification is rejected.'
    Assert-That ((Invoke-Api 'POST' '/api/auth/register' (New-Registration 'unverified' 'unverified@example.com' 'unverified')).Status -eq 401) 'Unverified email is rejected.'
    Assert-That ((Invoke-Api 'POST' '/api/auth/register' (New-Registration 'missing' 'missing@example.com' 'missing')).Status -eq 401) 'Missing verification is rejected.'
    Write-Host 'PASS: shared verification race and verification requirements.'

    Restart-TestApp
    Add-Proof 'expired' 'expired@example.com' 'expired'
    Add-Proof 'mismatch' 'mismatch@example.com'
    Assert-That ((Invoke-Api 'POST' '/api/auth/register' (New-Registration 'expired' 'expired@example.com' 'expired')).Status -eq 401) 'Verification expiry remains enforced.'
    Assert-That ((Invoke-Api 'POST' '/api/auth/register' (New-Registration 'mismatch' 'different@example.com' 'mismatch')).Status -eq 400) 'Verification cannot be used with a different email.'
    Assert-Unconsumed 'expired'
    Assert-Unconsumed 'mismatch'
    Add-Proof 'rollback' 'rollback@example.com'
    Invoke-TestSql "CREATE TRIGGER SmokeRegistrationFailure BEFORE INSERT ON Users FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected smoke failure';" -AsRoot | Out-Null
    $failed = Invoke-Api 'POST' '/api/auth/register' (New-Registration 'rollback' 'rollback@example.com' 'rollback')
    Assert-That ($failed.Status -eq 500) 'An unrelated database failure is not mislabeled as a duplicate.'
    Assert-Unconsumed 'rollback'
    Assert-That ((Invoke-TestSql "SELECT COUNT(*) FROM Users WHERE Email='rollback@example.com';") -eq '0') 'A failed transaction does not create an account.'
    Invoke-TestSql 'DROP TRIGGER SmokeRegistrationFailure;' -AsRoot | Out-Null
    Assert-That ((Invoke-Api 'POST' '/api/auth/register' (New-Registration 'rollback' 'rollback@example.com' 'rollback')).Status -eq 201) 'The verification remains usable after a database rollback.'
    Write-Host 'PASS: expiry, email mismatch and transaction rollback.'

    Restart-TestApp
    foreach ($attempt in 1..20) {
        Assert-That ((Invoke-Api 'GET' '/api/auth/register/username-availability?userName=rateuser').Status -eq 200) 'Availability permits the first 20 requests.'
    }
    Assert-That ((Invoke-Api 'GET' '/api/auth/register/username-availability?userName=rateuser').Status -eq 429) 'Availability rejects request 21 within the same window.'
    Write-Host "PASS: isolated MySQL auth smoke completed ($script:checks assertions)."
}
catch {
    $failure = $_
    Write-Warning 'Auth smoke failed. Last isolated app logs follow.'
    try { Invoke-Docker logs --tail 35 $appContainer | Out-Host }
    catch { Write-Warning 'No isolated app logs are available.' }
    throw $failure
}
finally {
    $client.Dispose()
    Remove-OwnedResource 'container' $appContainer
    Remove-OwnedResource 'container' $databaseContainer
    Remove-OwnedResource 'network' $networkName
    if ($ownsImage) { Remove-OwnedResource 'image' $AppImage }
}
