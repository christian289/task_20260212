#!/usr/bin/env pwsh
# PUT /api/employee/{name} 수정 엔드포인트 테스트 스크립트
# 사용법: pwsh -ExecutionPolicy Bypass -File "tools/update-test.ps1"

param(
    [string]$BaseUrl = "http://localhost:5012",
    [int]$ConcurrentCount = 100
)

$ErrorActionPreference = "Stop"
$passed = 0
$failed = 0
$results = @()

function Write-TestResult {
    param([string]$Name, [bool]$Success, [string]$Detail = "")
    $icon = if ($Success) { "PASS" } else { "FAIL" }
    $color = if ($Success) { "Green" } else { "Red" }
    Write-Host "  [$icon] $Name" -ForegroundColor $color
    if ($Detail) { Write-Host "         $Detail" -ForegroundColor Gray }
    if ($Success) { $script:passed++ } else { $script:failed++ }
}

function Register-Employee {
    param([string]$Json)
    $null = Invoke-RestMethod -Uri "$BaseUrl/api/employee" -Method Post -Body $Json -ContentType "application/json"
}

# ============================================================
Write-Host "`n=== PUT /api/employee/{name} 수정 테스트 ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl`n"

# --- 테스트 1: 단일 필드 수정 (이메일) ---
Write-Host "[테스트 1] 단일 필드 수정 (이메일)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $name = "테스트일_$runId"
    Register-Employee -Json "[{`"name`":`"$name`",`"email`":`"old@test.com`",`"tel`":`"01012345678`",`"joined`":`"2020-01-01`"}]"

    $body = @{ email = "new@test.com" } | ConvertTo-Json
    $result = Invoke-RestMethod -Uri "$BaseUrl/api/employee/$name" -Method Put -Body $body -ContentType "application/json"

    $emailOk = $result.email -eq "new@test.com"
    $nameOk = $result.name -eq $name
    $telOk = $result.tel -eq "01012345678"
    Write-TestResult "이메일 변경" ($emailOk -and $nameOk -and $telOk) "email=$($result.email), name=$($result.name), tel=$($result.tel)"
} catch {
    Write-TestResult "이메일 변경" $false $_.Exception.Message
}

# --- 테스트 2: 다중 필드 동시 수정 ---
Write-Host "`n[테스트 2] 다중 필드 동시 수정 (이메일+전화번호+입사일)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $name = "다중수정_$runId"
    Register-Employee -Json "[{`"name`":`"$name`",`"email`":`"multi@test.com`",`"tel`":`"01011112222`",`"joined`":`"2020-01-01`"}]"

    $body = @{ email = "multi-new@test.com"; tel = "01099998888"; joined = "2023-06-15" } | ConvertTo-Json
    $result = Invoke-RestMethod -Uri "$BaseUrl/api/employee/$name" -Method Put -Body $body -ContentType "application/json"

    $joinedDate = [DateTime]::Parse($result.joined.ToString())
    $joinedOk = $joinedDate.Year -eq 2023 -and $joinedDate.Month -eq 6 -and $joinedDate.Day -eq 15
    $ok = ($result.email -eq "multi-new@test.com") -and ($result.tel -eq "01099998888") -and $joinedOk
    Write-TestResult "다중 필드 수정" $ok "email=$($result.email), tel=$($result.tel), joined=$($joinedDate.ToString('yyyy-MM-dd'))"
} catch {
    Write-TestResult "다중 필드 수정" $false $_.Exception.Message
}

# --- 테스트 3: 이름 변경 ---
Write-Host "`n[테스트 3] 이름 변경" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $oldName = "이전이름_$runId"
    $newName = "새이름_$runId"
    Register-Employee -Json "[{`"name`":`"$oldName`",`"email`":`"rename@test.com`",`"tel`":`"01033334444`",`"joined`":`"2021-03-01`"}]"

    $body = @{ name = $newName } | ConvertTo-Json
    $result = Invoke-RestMethod -Uri "$BaseUrl/api/employee/$oldName" -Method Put -Body $body -ContentType "application/json"
    $nameOk = $result.name -eq $newName

    # 새 이름으로 조회
    $getResult = Invoke-RestMethod -Uri "$BaseUrl/api/employee/$newName" -Method Get
    $getOk = $getResult.name -eq $newName

    Write-TestResult "이름 변경 + 조회" ($nameOk -and $getOk) "변경: $oldName -> $newName"
} catch {
    Write-TestResult "이름 변경 + 조회" $false $_.Exception.Message
}

# --- 테스트 4: 미존재 직원 수정 → 404 ---
Write-Host "`n[테스트 4] 미존재 직원 수정 (404)" -ForegroundColor Yellow
try {
    $body = @{ email = "ghost@test.com" } | ConvertTo-Json
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/employee/존재하지않는사람_999" -Method Put -Body $body -ContentType "application/json" -SkipHttpErrorCheck
    Write-TestResult "404 반환" ($response.StatusCode -eq 404) "StatusCode=$($response.StatusCode)"
} catch {
    Write-TestResult "404 반환" $false $_.Exception.Message
}

# --- 테스트 5: 잘못된 이메일 → 400 ---
Write-Host "`n[테스트 5] 유효성 검증 실패 - 잘못된 이메일 (400)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $name = "검증실패_$runId"
    Register-Employee -Json "[{`"name`":`"$name`",`"email`":`"valid@test.com`",`"tel`":`"01055556666`",`"joined`":`"2022-01-01`"}]"

    $body = @{ email = "not-an-email" } | ConvertTo-Json
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/employee/$name" -Method Put -Body $body -ContentType "application/json" -SkipHttpErrorCheck
    Write-TestResult "400 반환" ($response.StatusCode -eq 400) "StatusCode=$($response.StatusCode)"
} catch {
    Write-TestResult "400 반환" $false $_.Exception.Message
}

# --- 테스트 6: 중복 수정 → 409 ---
Write-Host "`n[테스트 6] 중복 수정 시도 (409 Conflict)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $name1 = "중복A_$runId"
    $name2 = "중복B_$runId"
    Register-Employee -Json "[{`"name`":`"$name1`",`"email`":`"dup-a@test.com`",`"tel`":`"01011111111`",`"joined`":`"2020-01-01`"},{`"name`":`"$name2`",`"email`":`"dup-b@test.com`",`"tel`":`"01022222222`",`"joined`":`"2020-02-02`"}]"

    # name2를 name1과 동일한 정보로 수정
    $body = @{ name = $name1; email = "dup-a@test.com"; tel = "01011111111"; joined = "2020-01-01" } | ConvertTo-Json
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/employee/$name2" -Method Put -Body $body -ContentType "application/json" -SkipHttpErrorCheck
    Write-TestResult "409 반환" ($response.StatusCode -eq 409) "StatusCode=$($response.StatusCode)"
} catch {
    Write-TestResult "409 반환" $false $_.Exception.Message
}

# --- 테스트 7: 여러 레코드 순차 수정 ---
Write-Host "`n[테스트 7] 여러 레코드 순차 수정 (20건)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $count = 20
    $employees = @()
    for ($i = 0; $i -lt $count; $i++) {
        $num = $i.ToString("D3")
        $employees += @{
            name = "순차수정${num}_$runId"
            email = "seq${num}@test.com"
            tel = "010" + $num.PadLeft(4, '0') + $num.PadLeft(4, '0')
            joined = "2020-01-01"
        }
    }
    Register-Employee -Json ($employees | ConvertTo-Json)

    $successCount = 0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $count; $i++) {
        $num = $i.ToString("D3")
        $name = "순차수정${num}_$runId"
        $body = @{ email = "updated-seq${num}@test.com" } | ConvertTo-Json
        try {
            $null = Invoke-RestMethod -Uri "$BaseUrl/api/employee/$name" -Method Put -Body $body -ContentType "application/json"
            $successCount++
        } catch {}
    }
    $sw.Stop()
    Write-TestResult "순차 수정 $successCount/$count 성공" ($successCount -eq $count) "소요시간: $($sw.Elapsed.TotalSeconds.ToString('F1'))초"
} catch {
    Write-TestResult "순차 수정" $false $_.Exception.Message
}

# --- 테스트 8: 동시성 테스트 (동일 직원을 N개 요청이 동시에 수정) ---
Write-Host "`n[테스트 8] 동시성 테스트 - 동일 직원 동시 수정 ($ConcurrentCount건)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $name = "동시수정_$runId"
    Register-Employee -Json "[{`"name`":`"$name`",`"email`":`"concurrent@test.com`",`"tel`":`"01077778888`",`"joined`":`"2020-05-05`"}]"

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $jobs = 1..$ConcurrentCount | ForEach-Object {
        $idx = $_
        Start-Job -ScriptBlock {
            param($url, $n, $i)
            try {
                $body = @{ email = "concurrent-${i}@test.com" } | ConvertTo-Json
                $response = Invoke-WebRequest -Uri "$url/api/employee/$n" -Method Put -Body $body -ContentType "application/json" -SkipHttpErrorCheck -TimeoutSec 30
                $response.StatusCode
            } catch {
                "ERROR: $($_.Exception.Message)"
            }
        } -ArgumentList $BaseUrl, $name, $idx
    }

    $jobResults = $jobs | Wait-Job -Timeout 60 | Receive-Job
    $jobs | Remove-Job -Force

    $sw.Stop()
    $okCount = ($jobResults | Where-Object { $_ -eq 200 }).Count
    $errCount = ($jobResults | Where-Object { $_ -ne 200 }).Count
    $totalReceived = $jobResults.Count

    # 최종 상태 확인
    $finalState = Invoke-RestMethod -Uri "$BaseUrl/api/employee/$name" -Method Get -ErrorAction SilentlyContinue
    $finalEmail = if ($finalState) { $finalState.email } else { "조회실패" }

    Write-TestResult "동시 수정 ${okCount}/${totalReceived} 성공 (200)" ($okCount -gt 0) "소요시간: $($sw.Elapsed.TotalSeconds.ToString('F1'))초, 최종 email=$finalEmail"
} catch {
    Write-TestResult "동시성 테스트" $false $_.Exception.Message
}

# --- 테스트 9: 동시성 테스트 (서로 다른 직원을 동시에 수정) ---
Write-Host "`n[테스트 9] 동시성 테스트 - 서로 다른 직원 동시 수정 ($ConcurrentCount건)" -ForegroundColor Yellow
try {
    $runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $employees = @()
    for ($i = 0; $i -lt $ConcurrentCount; $i++) {
        $num = $i.ToString("D3")
        $employees += @{
            name = "병렬${num}_$runId"
            email = "parallel${num}@test.com"
            tel = "0101234" + $num.PadLeft(4, '0')
            joined = "2020-01-01"
        }
    }
    Register-Employee -Json ($employees | ConvertTo-Json)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $jobs = 0..($ConcurrentCount - 1) | ForEach-Object {
        $idx = $_
        Start-Job -ScriptBlock {
            param($url, $n, $i)
            try {
                $num = $i.ToString("D3")
                $body = @{ email = "updated-parallel${num}@test.com" } | ConvertTo-Json
                $response = Invoke-WebRequest -Uri "$url/api/employee/$n" -Method Put -Body $body -ContentType "application/json" -SkipHttpErrorCheck -TimeoutSec 30
                $response.StatusCode
            } catch {
                "ERROR: $($_.Exception.Message)"
            }
        } -ArgumentList $BaseUrl, "병렬$($_.ToString('D3'))_$runId", $_
    }

    $jobResults = $jobs | Wait-Job -Timeout 120 | Receive-Job
    $jobs | Remove-Job -Force

    $sw.Stop()
    $okCount = ($jobResults | Where-Object { $_ -eq 200 }).Count
    $totalReceived = $jobResults.Count

    # 샘플 검증
    $sample = Invoke-RestMethod -Uri "$BaseUrl/api/employee/병렬000_$runId" -Method Get -ErrorAction SilentlyContinue
    $sampleOk = $sample -and ($sample.email -eq "updated-parallel000@test.com")

    Write-TestResult "병렬 수정 ${okCount}/${totalReceived} 성공" ($okCount -eq $ConcurrentCount) "소요시간: $($sw.Elapsed.TotalSeconds.ToString('F1'))초, 샘플검증=$(if($sampleOk){'OK'}else{'FAIL'})"
} catch {
    Write-TestResult "병렬 수정" $false $_.Exception.Message
}

# ============================================================
Write-Host "`n=== 테스트 결과 요약 ===" -ForegroundColor Cyan
Write-Host "  통과: $passed" -ForegroundColor Green
Write-Host "  실패: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
Write-Host "  합계: $($passed + $failed)`n"

if ($failed -gt 0) { exit 1 }
