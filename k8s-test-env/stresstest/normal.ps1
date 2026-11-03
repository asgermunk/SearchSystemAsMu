#Requires -Version 7.0
<#
.SYNOPSIS
    Normal use: 3 users, each searching once a second, for 2 minutes (~3 req/s).

.DESCRIPTION
    The load you expect in production - the test behind the success criteria.
    Searches go through the load balancer in minikube; see StressTest.psm1.

.EXAMPLE
    .\normal.ps1

.EXAMPLE
    .\normal.ps1 -DurationSeconds 30 -GrafanaUrl http://127.0.0.1:53712
#>
[CmdletBinding()]
param(
    [int] $Users = 3,
    # Requests per second per user
    [double] $RatePerUser = 1,
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

Invoke-StressTest -Name Normal -Users $Users -RatePerUser $RatePerUser -DurationSeconds $DurationSeconds `
    -BaseUrl $BaseUrl -GrafanaUrl $GrafanaUrl -TimeoutSeconds $TimeoutSeconds -NoWarmUp:$NoWarmUp
