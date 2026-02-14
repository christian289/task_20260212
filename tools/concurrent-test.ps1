#!/usr/bin/env pwsh
# Singleton + SQLite WAL 동시 쓰기 테스트
# 100개의 고유한 직원 데이터를 동시에 POST하여 모두 정상 저장되는지 검증

$baseUrl = "http://localhost:5012"
$totalRequests = 100
$employeesPerRequest = 50
$totalEmployees = $totalRequests * $employeesPerRequest

Write-Host "=== Concurrent Write Test ===" -ForegroundColor Cyan
Write-Host "Target: $baseUrl"
Write-Host "Concurrent requests: $totalRequests"
Write-Host "Employees per request: $employeesPerRequest"
Write-Host "Total employees: $totalEmployees"
Write-Host ""

# Step 1: 초기 직원 수 확인
try {
    $initial = Invoke-RestMethod "$baseUrl/api/employee?page=1&pageSize=1" -ErrorAction Stop
    $initialCount = $initial.totalCount
    Write-Host "[1/5] Initial employee count: $initialCount" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Server not reachable at $baseUrl" -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}

# Step 2: 요청당 50명씩 100개 고유 직원 JSON 생성 (타임스탬프로 매 실행마다 고유)
$runId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
Write-Host "[2/5] Generating $totalRequests payloads x $employeesPerRequest employees (runId: $runId)..." -ForegroundColor Green
$payloads = @()
for ($req = 1; $req -le $totalRequests; $req++) {
    $employees = @()
    for ($emp = 1; $emp -le $employeesPerRequest; $emp++) {
        $idx = ($req - 1) * $employeesPerRequest + $emp
        $day = [int](($idx % 28) + 1)
        $month = [int](([math]::Floor(($idx - 1) / 28) % 12) + 1)
        $dayStr = $day.ToString("D2")
        $monthStr = $month.ToString("D2")
        $mid = (1000 + [int]($idx / 10000)).ToString("D4")
        $last = ($idx % 10000).ToString("D4")
        $employees += "{`"name`":`"CT_${runId}_$idx`",`"email`":`"ct${runId}_$idx@test.com`",`"tel`":`"010$mid$last`",`"joined`":`"2025-$monthStr-$dayStr`"}"
    }
    $payloads += "[" + ($employees -join ",") + "]"
}

# Step 3: HttpClient로 동시 전송
Write-Host "[3/5] Firing $totalRequests concurrent POST requests..." -ForegroundColor Green
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$tasks = @()
foreach ($json in $payloads) {
    $content = [System.Net.Http.StringContent]::new(
        $json,
        [System.Text.Encoding]::UTF8,
        "application/json"
    )
    $tasks += $client.PostAsync("$baseUrl/api/employee", $content)
}

try {
    [System.Threading.Tasks.Task]::WaitAll($tasks)
} catch {
    Write-Host "Some tasks failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

$stopwatch.Stop()
Write-Host "  Elapsed: $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Gray

# Step 4: 결과 분석
Write-Host "[4/5] Analyzing results..." -ForegroundColor Green
$statusGroups = @{}
$failBodies = @()

foreach ($task in $tasks) {
    if ($task.Status -eq 'RanToCompletion') {
        $code = [int]$task.Result.StatusCode
        if (-not $statusGroups.ContainsKey($code)) { $statusGroups[$code] = 0 }
        $statusGroups[$code]++

        if ($code -ne 201) {
            $body = $task.Result.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $failBodies += "  HTTP $code : $body"
        }
    } else {
        if (-not $statusGroups.ContainsKey('Exception')) { $statusGroups['Exception'] = 0 }
        $statusGroups['Exception']++
        $failBodies += "  Exception: $($task.Exception.InnerException.Message)"
    }
}

Write-Host ""
Write-Host "--- Response Summary ---" -ForegroundColor Cyan
foreach ($entry in $statusGroups.GetEnumerator() | Sort-Object Name) {
    $color = if ($entry.Key -eq 201) { 'Green' } else { 'Red' }
    Write-Host "  HTTP $($entry.Key): $($entry.Value)" -ForegroundColor $color
}

if ($failBodies.Count -gt 0) {
    Write-Host ""
    Write-Host "--- Failure Details (first 5) ---" -ForegroundColor Yellow
    $failBodies | Select-Object -First 5 | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
}

# Step 5: 최종 검증
Write-Host ""
Write-Host "[5/5] Verifying database state..." -ForegroundColor Green
$final = Invoke-RestMethod "$baseUrl/api/employee?page=1&pageSize=1"
$finalCount = $final.totalCount
$newRecords = $finalCount - $initialCount

Write-Host ""
Write-Host "=== Final Result ===" -ForegroundColor Cyan
Write-Host "  Before:   $initialCount"
Write-Host "  After:    $finalCount"
Write-Host "  Inserted: $newRecords"
Write-Host "  Expected: $totalEmployees"

if ($newRecords -eq $totalEmployees) {
    Write-Host ""
    Write-Host "  PASS: All $totalEmployees records written successfully!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "  FAIL: Expected $totalEmployees but got $newRecords" -ForegroundColor Red
}

$client.Dispose()
