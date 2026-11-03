# The engine behind normal.ps1, high.ps1 and ddos.ps1: load tests the search engine
# in minikube through the load balancer, using curl.
#
# Simulates -Users users, each sending searches at -RatePerUser requests per second
# (0 = no pause) for -DurationSeconds. Without -BaseUrl it port-forwards the load
# balancer to localhost for the duration of the test. Every request goes through
# that tunnel and curl runs on the same machine as the cluster, so under heavy load
# the load generator itself competes for the CPU - keep that in mind when reading
# the results.
#
# At the end it prints the response time percentiles, the error rate and the
# success criteria (p50 < 200 ms, p99 < 1 s), writes every request to a CSV in
# .\results  (next to this file), and prints a link to the Grafana dashboard for the test's time range.

function Get-Percentile([double[]] $sorted, [double] $p) {
    if ($sorted.Count -eq 0) { return [double]::NaN }
    $sorted[[math]::Max(0, [math]::Ceiling($p * $sorted.Count) - 1)]
}

function Invoke-StressTest {
    [CmdletBinding()]
    param(
        # The scenario's name, shown in the output and used in the CSV's file name
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [int] $Users,
        # Requests per second per user. 0 means no pause between requests.
        [Parameter(Mandatory)] [double] $RatePerUser,
        [Parameter(Mandatory)] [int] $DurationSeconds,

        # Where the load balancer is reached. Empty: port-forward it from minikube.
        [string] $BaseUrl,

        # Grafana's address, as printed by "minikube service loki-grafana -n monitoring --url"
        [string] $GrafanaUrl,

        # Seconds before curl gives up on a request; it is then counted as an error.
        [int] $TimeoutSeconds = 10,

        # The words searched for, picked at random. Common words make heavier queries.
        [string[]] $Queries = @(
            'hello world', 'the', 'meeting', 'energy price', 'gas contract', 'enron',
            'california power', 'please call me', 'market', 'the meeting tomorrow'
        ),

        # Skip the request sent before the test, which loads the shards' word cache.
        [switch] $NoWarmUp
    )

    $ErrorActionPreference = 'Stop'

    $portForward = $null
    if (-not $BaseUrl) {
        $BaseUrl = 'http://127.0.0.1:5222'
        Write-Host "Port-forwarding the load balancer to $BaseUrl ..."
        $portForward = Start-Process kubectl -PassThru -WindowStyle Hidden -ArgumentList @(
            'port-forward', '-n', 'search', 'svc/search-loadbalancer', '5222:8080')
    }
    $BaseUrl = $BaseUrl.TrimEnd('/')

    try {
        # wait until the load balancer answers (also catches a wrong -BaseUrl)
        $ready = $false
        foreach ($i in 1..30) {
            $code = curl.exe -s -o NUL -w '%{http_code}' --max-time 5 "$BaseUrl/swagger/index.html"
            if ($code -ne '000') { $ready = $true; break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw "The load balancer does not answer on $BaseUrl." }

        if (-not $NoWarmUp) {
            # the first search on a shard loads its whole word table into memory
            Write-Host 'Warm-up search ...'
            curl.exe -s -o NUL --max-time 30 "$BaseUrl/api/search/instances?query=warmup" | Out-Null
        }

        $rateText = if ($RatePerUser -gt 0) { "$RatePerUser req/s each" } else { 'no pause' }
        Write-Host ''
        Write-Host "Scenario $Name - $Users users, $rateText, $DurationSeconds s against $BaseUrl" -ForegroundColor Cyan

        $start = Get-Date
        $deadline = $start.AddSeconds($DurationSeconds)

        # One runspace per user. Each one calls curl with a small batch of URLs, so a
        # connection is reused within the batch and curl.exe is not started per request.
        # --rate spaces the requests within a batch; the sleep after it keeps the pace
        # across batches. A user only checks the deadline between batches, so batches
        # are kept to ~2 s of work (3 requests without a pause) - once the system is
        # overloaded and each answer takes seconds, a bigger batch would run far past it.
        $results = 1..$Users | ForEach-Object -ThrottleLimit $Users -Parallel {
            $user = $_
            $deadline = $using:deadline
            $rate = $using:RatePerUser
            $queries = $using:Queries
            $url = "$($using:BaseUrl)/api/search/instances?query="

            # spread the users' first requests over one interval instead of all at once
            if ($rate -gt 0) { Start-Sleep -Milliseconds (Get-Random -Maximum ([int](1000 / $rate))) }

            while ((Get-Date) -lt $deadline) {
                $left = ($deadline - (Get-Date)).TotalSeconds
                $batch = if ($rate -gt 0) { [math]::Max(1, [math]::Min([math]::Ceiling($rate * 2), [math]::Ceiling($rate * $left))) } else { 3 }

                $curlArgs = @('-s', '--max-time', $using:TimeoutSeconds, '-w', '%{http_code} %{time_total}\n')
                if ($rate -gt 0) { $curlArgs += @('--rate', "$([math]::Max(1, [int]($rate * 60)))/m") }
                foreach ($n in 1..$batch) {
                    $q = [uri]::EscapeDataString(($queries | Get-Random))
                    $curlArgs += @('-o', 'NUL', "$url$q")
                }

                foreach ($line in (curl.exe @curlArgs)) {
                    $status, $seconds = $line -split ' '
                    [pscustomobject]@{
                        User   = $user
                        Status = [int]$status
                        Ms     = [math]::Round([double]::Parse($seconds, [cultureinfo]::InvariantCulture) * 1000, 1)
                    }
                }

                if ($rate -gt 0) { Start-Sleep -Milliseconds ([int](1000 / $rate)) }
            }
        }
        $end = Get-Date
    }
    finally {
        if ($portForward) { Stop-Process -Id $portForward.Id -ErrorAction SilentlyContinue }
    }

    # --- summary ---------------------------------------------------------------

    $elapsed = ($end - $start).TotalSeconds
    $ok = @($results | Where-Object { $_.Status -ge 200 -and $_.Status -lt 300 })
    $errors = $results.Count - $ok.Count
    # percentiles over the successful requests; a timeout has no meaningful time
    $times = [double[]]($ok.Ms | Sort-Object)
    $p50 = Get-Percentile $times 0.50
    $p95 = Get-Percentile $times 0.95
    $p99 = Get-Percentile $times 0.99
    $errorRate = if ($results.Count) { $errors / $results.Count } else { 1 }

    Write-Host ''
    Write-Host "Requests     $($results.Count) in $([math]::Round($elapsed, 1)) s  ($([math]::Round($results.Count / $elapsed, 1)) req/s)"
    Write-Host "Errors       $errors  ($($errorRate.ToString('P2')))"
    $byStatus = $results | Group-Object Status | Sort-Object Name | ForEach-Object { "$($_.Name): $($_.Count)" }
    Write-Host "Status codes $($byStatus -join ', ')   (0 = timeout or no connection)"
    Write-Host ("Response     p50 {0} ms   p95 {1} ms   p99 {2} ms   max {3} ms" -f $p50, $p95, $p99, ($times | Select-Object -Last 1))

    Write-Host ''
    Write-Host 'Success criteria'
    $criteria = @(
        @{ Name = 'p50 under 200 ms'; Pass = $p50 -lt 200 }
        @{ Name = 'p99 under 1 s   '; Pass = $p99 -lt 1000 }
        @{ Name = 'no errors       '; Pass = $errors -eq 0 }
    )
    foreach ($c in $criteria) {
        if ($c.Pass) { Write-Host "  PASS  $($c.Name)" -ForegroundColor Green }
        else { Write-Host "  FAIL  $($c.Name)" -ForegroundColor Red }
    }

    $resultsDir = Join-Path $PSScriptRoot 'results'
    New-Item -ItemType Directory -Force $resultsDir | Out-Null
    $csv = Join-Path $resultsDir ("{0}-{1:yyyyMMdd-HHmmss}.csv" -f $Name.ToLower(), $start)
    $results | Export-Csv $csv -NoTypeInformation
    Write-Host ''
    Write-Host "Every request: $csv"

    # the dashboard's success criteria tiles are computed over exactly this range
    $from = ([DateTimeOffset]$start).ToUnixTimeMilliseconds()
    $to = ([DateTimeOffset]$end).ToUnixTimeMilliseconds()
    $path = "/d/search-performance/sogemaskine-performance?from=$from&to=$to"
    if ($GrafanaUrl) { Write-Host "Grafana:       $($GrafanaUrl.TrimEnd('/'))$path" }
    else { Write-Host "Grafana:       <grafana>$path" }
}

Export-ModuleMember -Function Invoke-StressTest
