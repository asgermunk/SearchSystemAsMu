#Requires -Version 7.0
<#
.SYNOPSIS
    Overload (DDoS-like): 100 users searching with no pause between requests, for 1 minute.

.DESCRIPTION
    As fast as the system answers, to find its capacity and what happens beyond it. It runs
    from this one machine, so it is an overload test rather than a distributed attack.
    Searches go through the load balancer in minikube; see StressTest.psm1.

.EXAMPLE
    .\ddos.ps1

.EXAMPLE
    .\ddos.ps1 -DurationSeconds 30 -GrafanaUrl http://127.0.0.1:53712
#>
[CmdletBinding()]
param(
    [int] $Users = 100,
    [int] $DurationSeconds = 60,

    # Where the load balancer is reached. Empty: port-forward it from minikube.
    [string] $BaseUrl,
    # Grafana's address, for a link to the dashboard over the test's time range
    [string] $GrafanaUrl,
    # Seconds before a request counts as a timeout
    [int] $TimeoutSeconds = 10,
    # Skip the search sent before the test, which loads the shards' word cache
    [switch] $NoWarmUp
)

Import-Module (Join-Path $PSScriptRoot 'StressTest.psm1') -Force

Invoke-StressTest -Name Ddos -Users $Users -RatePerUser 0 -DurationSeconds $DurationSeconds `
    -BaseUrl $BaseUrl -GrafanaUrl $GrafanaUrl -TimeoutSeconds $TimeoutSeconds -NoWarmUp:$NoWarmUp
