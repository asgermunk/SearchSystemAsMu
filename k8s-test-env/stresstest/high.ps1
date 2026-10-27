#Requires -Version 7.0
<#
.SYNOPSIS
    High use: 20 users, each searching twice a second, for 2 minutes (~40 req/s).

.DESCRIPTION
    Well above normal use, to see how response times hold up as the load grows.
    Searches go through the load balancer in minikube; see StressTest.psm1.

.EXAMPLE
    .\high.ps1

.EXAMPLE
    .\high.ps1 -DurationSeconds 30 -GrafanaUrl http://127.0.0.1:53712
#>
[CmdletBinding()]
param(
    [int] $Users = 20,
    # Requests per second per user
    [double] $RatePerUser = 2,
    [int] $DurationSeconds = 120,

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

Invoke-StressTest -Name High -Users $Users -RatePerUser $RatePerUser -DurationSeconds $DurationSeconds `
    -BaseUrl $BaseUrl -GrafanaUrl $GrafanaUrl -TimeoutSeconds $TimeoutSeconds -NoWarmUp:$NoWarmUp
