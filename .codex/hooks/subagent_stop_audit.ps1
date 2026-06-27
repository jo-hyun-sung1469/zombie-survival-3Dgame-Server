param([switch]$SelfTest)
. "$PSScriptRoot/ZombieHookCommon.ps1"

function Invoke-SubagentStopAudit {
    param([AllowEmptyString()][string]$Raw)
    if ([string]::IsNullOrWhiteSpace($Raw)) { return }

    $missing = New-Object System.Collections.Generic.List[string]
    if ($Raw -notmatch '(?i)(changed files|files changed|변경 파일|수정 파일|paths changed)') {
        $missing.Add("변경 파일 목록")
    }
    if ($Raw -notmatch '(?i)(verification|verified|tests?|build|검증|테스트|빌드)') {
        $missing.Add("검증 결과")
    }
    if ($Raw -notmatch '(?i)(remaining|open question|decision|남은|결정|리스크|risk)') {
        $missing.Add("남은 결정사항 또는 리스크")
    }

    if ($missing.Count -gt 0) {
        Write-HookWarning "[zombie-subagent] 서브 에이전트 결과에 다음 항목이 부족할 수 있습니다: $($missing -join ', '). 필요하면 메인 세션에서 보완하세요."
    }
}

if ($SelfTest) {
    Invoke-SubagentStopAudit "작업 완료"
    "subagent_stop_audit self-test passed."
    exit 0
}

$payload = Read-HookPayload
Invoke-SubagentStopAudit $payload.Raw
