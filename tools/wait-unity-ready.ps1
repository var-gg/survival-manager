param(
    [int]$MaxAttempts = 18,
    [int]$IntervalSeconds = 10,
    [int]$WarmupSeconds = 15
)

$ErrorActionPreference = 'Continue'
$bridgePath = Join-Path $PSScriptRoot 'unity-bridge.ps1'

for ($i = 0; $i -lt $MaxAttempts; $i++) {
    Start-Sleep -Seconds $IntervalSeconds

    # 프로세스 존재 및 응답 상태 확인 — 시작 직후 죽는 케이스 조기 탐지
    $proc = Get-Process Unity -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero -or $_.CPU -gt 0 }
    if (-not $proc) {
        Write-Host "Attempt $($i + 1)/$MaxAttempts : Unity process not found (crashed on startup?)"
        continue
    }

    $responding = @($proc | Where-Object { $_.Responding })
    $notResponding = @($proc | Where-Object { -not $_.Responding })
    if ($notResponding.Count -gt 0 -and $responding.Count -eq 0) {
        Write-Host "Attempt $($i + 1)/$MaxAttempts : Unity running but Responding=False (frozen)"
        # 5회 연속 frozen이면 조기 종료
        if ($i -ge 4) {
            $allFrozen = $true
            Write-Error "Unity has been frozen for $($i * $IntervalSeconds)s. Kill and restart recommended."
            exit 2
        }
        continue
    }

    try {
        $output = pwsh -File $bridgePath status 2>&1
        $text = ($output | ForEach-Object { $_.ToString() }) -join ' '
        if ($text -match 'ready') {
            Write-Host "Unity ready (attempt $($i + 1))"

            # Warm-up: ready 응답 후에도 에디터 내부 초기화가 마무리될 시간을 준다.
            if ($WarmupSeconds -gt 0) {
                Write-Host "Warm-up: waiting ${WarmupSeconds}s for editor stabilization..."
                Start-Sleep -Seconds $WarmupSeconds

                # warm-up 후 한번 더 ready 확인
                $verify = pwsh -File $bridgePath status 2>&1
                $verifyText = ($verify | ForEach-Object { $_.ToString() }) -join ' '
                if ($verifyText -match 'ready') {
                    Write-Host "Unity stable and ready."
                } else {
                    Write-Host "WARNING: Unity was ready but became unready after warm-up."
                    continue
                }
            }

            exit 0
        }
        Write-Host "Attempt $($i + 1)/$MaxAttempts : not ready yet"
    }
    catch {
        Write-Host "Attempt $($i + 1)/$MaxAttempts : connection failed"
    }
}

Write-Error "Unity did not become ready after $MaxAttempts attempts ($($MaxAttempts * $IntervalSeconds)s)"
exit 1
