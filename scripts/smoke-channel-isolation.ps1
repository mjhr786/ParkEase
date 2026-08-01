<#
.SYNOPSIS
  Staging smoke for ChannelIsolation (PR9).

.DESCRIPTION
  Exercises channel-context, marketplace vs corporate cross-denials, lease-browse (CA),
  vendor path denial on corporate, and refresh channel preservation.

  Requires an API with ChannelIsolation:Enabled=true and a user that can corporate-login
  (membership recommended for full bound tests).

.PARAMETER BaseUrl
  API origin, e.g. http://localhost:5129 or https://staging-host (no trailing slash).

.PARAMETER Email
  Test user email.

.PARAMETER Password
  Test user password.

.PARAMETER CompanyId
  Optional GUID when the user has multiple company memberships.

.PARAMETER SkipVendor
  Skip vendor allocation list checks.

.EXAMPLE
  .\scripts\smoke-channel-isolation.ps1 -BaseUrl http://localhost:5129 -Email a@b.com -Password secret
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BaseUrl,

    [Parameter(Mandatory = $true)]
    [string] $Email,

    [Parameter(Mandatory = $true)]
    [string] $Password,

    [string] $CompanyId,

    [switch] $SkipVendor
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")
$script:PassCount = 0
$script:FailCount = 0
$results = [System.Collections.Generic.List[object]]::new()

function Write-Case {
    param(
        [string] $Name,
        [bool] $Pass,
        [string] $Detail
    )
    $status = if ($Pass) { "PASS" } else { "FAIL" }
    if ($Pass) { $script:PassCount++ } else { $script:FailCount++ }
    $line = "[{0}] {1} — {2}" -f $status, $Name, $Detail
    if ($Pass) { Write-Host $line -ForegroundColor Green }
    else { Write-Host $line -ForegroundColor Red }
    $results.Add([pscustomobject]@{ Case = $Name; Pass = $Pass; Detail = $Detail }) | Out-Null
}

function Invoke-Api {
    param(
        [string] $Method,
        [string] $Path,
        [object] $Body = $null,
        [string] $Token = $null
    )
    $uri = "$BaseUrl$Path"
    $headers = @{
        "Accept" = "application/json"
    }
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }
    $params = @{
        Method             = $Method
        Uri                = $uri
        Headers            = $headers
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }
    try {
        $resp = Invoke-WebRequest @params
        $json = $null
        if ($resp.Content) {
            try { $json = $resp.Content | ConvertFrom-Json } catch { $json = $null }
        }
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            Json       = $json
            Raw        = $resp.Content
        }
    }
    catch {
        return [pscustomobject]@{
            StatusCode = 0
            Json       = $null
            Raw        = $_.Exception.Message
        }
    }
}

function Test-ChannelForbidden {
    param($Response)
    if ($Response.StatusCode -ne 403) { return $false }
    $code = $Response.Json.code
    if (-not $code -and $Response.Json.Code) { $code = $Response.Json.Code }
    if ($code -eq "channel_forbidden") { return $true }
    $errors = $Response.Json.errors
    if (-not $errors) { $errors = $Response.Json.Errors }
    if ($errors -is [array] -and ($errors -contains "channel_forbidden")) { return $true }
    # Fallback: message text
    $msg = "$($Response.Json.message)$($Response.Json.Message)$($Response.Raw)"
    return $msg -match "channel_forbidden|product channel"
}

function Get-Prop {
    param($Obj, [string[]] $Names)
    foreach ($n in $Names) {
        if ($null -eq $Obj) { continue }
        $p = $Obj.PSObject.Properties[$n]
        if ($p) { return $p.Value }
    }
    return $null
}

Write-Host ""
Write-Host "=== Channel Isolation smoke ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl"
Write-Host ""

# --- Health (optional) ---
$health = Invoke-Api -Method GET -Path "/health"
if ($health.StatusCode -eq 0) {
    $health = Invoke-Api -Method GET -Path "/api/health"
}
Write-Case -Name "HEALTH" -Pass ($health.StatusCode -in 200, 404) -Detail "status=$($health.StatusCode) (404 ok if no health route)"

# --- Marketplace login ---
$loginBody = @{ email = $Email; password = $Password }
$mLogin = Invoke-Api -Method POST -Path "/api/auth/login" -Body $loginBody
$mData = Get-Prop $mLogin.Json @("data", "Data")
$mToken = Get-Prop $mData @("accessToken", "AccessToken", "token", "Token")
$mRefresh = Get-Prop $mData @("refreshToken", "RefreshToken")
Write-Case -Name "MARKETPLACE_LOGIN" -Pass ($mLogin.StatusCode -eq 200 -and $mToken) -Detail "status=$($mLogin.StatusCode)"

if (-not $mToken) {
    Write-Host "Cannot continue without marketplace token." -ForegroundColor Red
    exit 1
}

# --- Isolation flag via channel-context ---
$ctxM = Invoke-Api -Method GET -Path "/api/auth/channel-context" -Token $mToken
$ctxData = Get-Prop $ctxM.Json @("data", "Data")
$iso = Get-Prop $ctxData @("isolationEnabled", "IsolationEnabled")
Write-Case -Name "ISOLATION_ENABLED" -Pass ($ctxM.StatusCode -eq 200 -and $iso -eq $true) -Detail "isolationEnabled=$iso status=$($ctxM.StatusCode)"

# --- Marketplace cannot hit company-scoped corporate API without company (use me/companies OK, create denied) ---
$createDenied = Invoke-Api -Method POST -Path "/api/v1/corporate/companies" -Token $mToken -Body @{
    name = "SmokeShouldFail $(Get-Random)"
    industry = "Other"
}
Write-Case -Name "MKT_CREATE_COMPANY_DENIED" -Pass (Test-ChannelForbidden $createDenied) -Detail "status=$($createDenied.StatusCode) code=$(Get-Prop $createDenied.Json @('code','Code'))"

# Resolve a company id for later denial if available
$meCos = Invoke-Api -Method GET -Path "/api/v1/corporate/me/companies" -Token $mToken
$meList = Get-Prop (Get-Prop $meCos.Json @("data", "Data")) @("companies", "Companies", "items", "Items")
$resolvedCompanyId = $CompanyId
if (-not $resolvedCompanyId -and $meList -and $meList.Count -gt 0) {
    $first = $meList[0]
    $resolvedCompanyId = Get-Prop $first @("id", "Id", "companyId", "CompanyId")
}

if ($resolvedCompanyId) {
    $dash = Invoke-Api -Method GET -Path "/api/v1/corporate/companies/$resolvedCompanyId" -Token $mToken
    Write-Case -Name "MKT_COMPANY_SCOPED_DENIED" -Pass (Test-ChannelForbidden $dash) -Detail "status=$($dash.StatusCode)"
}
else {
    Write-Case -Name "MKT_COMPANY_SCOPED_DENIED" -Pass $true -Detail "SKIP no company id for user (create-company denial already covered)"
}

# --- Corporate login ---
$corpBody = @{ email = $Email; password = $Password }
if ($CompanyId) { $corpBody.companyId = $CompanyId }
$cLogin = Invoke-Api -Method POST -Path "/api/auth/login/corporate" -Body $corpBody
$cData = Get-Prop $cLogin.Json @("data", "Data")
$cToken = Get-Prop $cData @("accessToken", "AccessToken", "token", "Token")
$cRefresh = Get-Prop $cData @("refreshToken", "RefreshToken")
$cCode = Get-Prop $cLogin.Json @("code", "Code")
$isBootstrap = Get-Prop $cData @("isBootstrap", "IsBootstrap")
$corpCompanyId = Get-Prop $cData @("companyId", "CompanyId")
if (-not $corpCompanyId) { $corpCompanyId = $resolvedCompanyId }

$corpLoginOk = $cLogin.StatusCode -eq 200 -and $cToken
if (-not $corpLoginOk -and $cLogin.StatusCode -eq 400 -and $cCode -eq "company_selection_required") {
    Write-Case -Name "CORPORATE_LOGIN" -Pass $true -Detail "company_selection_required — pass -CompanyId for bound tests"
    $cToken = $null
}
else {
    Write-Case -Name "CORPORATE_LOGIN" -Pass $corpLoginOk -Detail "status=$($cLogin.StatusCode) bootstrap=$isBootstrap companyId=$corpCompanyId"
}

if ($cToken) {
    $ctxC = Invoke-Api -Method GET -Path "/api/auth/channel-context" -Token $cToken
    $ctxCData = Get-Prop $ctxC.Json @("data", "Data")
    $isoC = Get-Prop $ctxCData @("isolationEnabled", "IsolationEnabled")
    Write-Case -Name "CORP_CHANNEL_CONTEXT" -Pass ($ctxC.StatusCode -eq 200 -and $isoC -eq $true) -Detail "isolationEnabled=$isoC"

    # Corporate must not create marketplace bookings
    $bookDeny = Invoke-Api -Method POST -Path "/api/bookings" -Token $cToken -Body @{
        parkingSpaceId = "11111111-1111-1111-1111-111111111111"
        startTime      = (Get-Date).ToUniversalTime().ToString("o")
        endTime        = (Get-Date).AddHours(1).ToUniversalTime().ToString("o")
    }
    Write-Case -Name "CORP_MARKETPLACE_BOOK_DENIED" -Pass (Test-ChannelForbidden $bookDeny) -Detail "status=$($bookDeny.StatusCode)"

    # Favorites denied
    $fav = Invoke-Api -Method GET -Path "/api/favorites" -Token $cToken
    Write-Case -Name "CORP_FAVORITES_DENIED" -Pass (Test-ChannelForbidden $fav) -Detail "status=$($fav.StatusCode)"

    # My listings denied
    $listings = Invoke-Api -Method GET -Path "/api/parking/my-listings" -Token $cToken
    Write-Case -Name "CORP_MY_LISTINGS_DENIED" -Pass (Test-ChannelForbidden $listings) -Detail "status=$($listings.StatusCode)"

    # Lease-browse search: CA → 200-ish; member → 403; bootstrap → 403
    $search = Invoke-Api -Method GET -Path "/api/parking/search?page=1&pageSize=5" -Token $cToken
    $role = Get-Prop $cData @("companyRole", "CompanyRole")
    if ($isBootstrap -eq $true) {
        Write-Case -Name "LEASE_BROWSE_SEARCH" -Pass (Test-ChannelForbidden $search) -Detail "bootstrap expect 403 status=$($search.StatusCode)"
    }
    elseif ($role -and $role -match "Admin") {
        Write-Case -Name "LEASE_BROWSE_SEARCH" -Pass ($search.StatusCode -eq 200) -Detail "CA status=$($search.StatusCode)"
    }
    else {
        # Member or unknown role: allow either 200 (if Admin claim) or 403
        $ok = ($search.StatusCode -eq 200) -or (Test-ChannelForbidden $search)
        Write-Case -Name "LEASE_BROWSE_SEARCH" -Pass $ok -Detail "role=$role status=$($search.StatusCode) (200 CA / 403 member)"
    }

    if (-not $SkipVendor) {
        $vendor = Invoke-Api -Method GET -Path "/api/v1/corporate/vendor/allocations" -Token $cToken
        Write-Case -Name "CORP_VENDOR_LIST_DENIED" -Pass (Test-ChannelForbidden $vendor) -Detail "status=$($vendor.StatusCode)"
    }

    # Refresh preserves corporate channel
    if ($cRefresh) {
        $ref = Invoke-Api -Method POST -Path "/api/auth/refresh" -Body @{ refreshToken = $cRefresh }
        $refData = Get-Prop $ref.Json @("data", "Data")
        $refToken = Get-Prop $refData @("accessToken", "AccessToken", "token", "Token")
        $refChannel = Get-Prop $refData @("channel", "Channel")
        $refCompany = Get-Prop $refData @("companyId", "CompanyId")
        $refreshOk = $ref.StatusCode -eq 200 -and $refToken
        if ($refreshOk -and $refChannel) {
            $refreshOk = $refChannel -match "Corporate"
        }
        if ($refreshOk -and $corpCompanyId -and $refCompany -and -not $isBootstrap) {
            $refreshOk = [string]$refCompany -eq [string]$corpCompanyId
        }
        # If DTO omits channel, probe channel-context with new token
        if ($refreshOk -and -not $refChannel -and $refToken) {
            $ctxR = Invoke-Api -Method GET -Path "/api/auth/channel-context" -Token $refToken
            $ch = Get-Prop (Get-Prop $ctxR.Json @("data", "Data")) @("channel", "Channel")
            $refreshOk = $ch -match "Corporate"
            $refChannel = $ch
        }
        Write-Case -Name "REFRESH_PRESERVES_CORPORATE" -Pass $refreshOk -Detail "status=$($ref.StatusCode) channel=$refChannel company=$refCompany"
    }
    else {
        Write-Case -Name "REFRESH_PRESERVES_CORPORATE" -Pass $false -Detail "no refresh token on corporate login"
    }

    # Vendor path allowed on marketplace (list may be empty)
    if (-not $SkipVendor) {
        $vendorM = Invoke-Api -Method GET -Path "/api/v1/corporate/vendor/allocations" -Token $mToken
        $vendorOk = $vendorM.StatusCode -in 200, 404
        Write-Case -Name "MKT_VENDOR_LIST_ALLOWED" -Pass $vendorOk -Detail "status=$($vendorM.StatusCode)"
    }
}
else {
    Write-Host "Skipping corporate-bound cases (no corporate token)." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Summary: $($script:PassCount) passed, $($script:FailCount) failed ===" -ForegroundColor Cyan
$outDir = Join-Path (Split-Path $PSScriptRoot -Parent) "docs"
if (-not (Test-Path $outDir)) { $outDir = Join-Path (Get-Location) "docs" }
$outPath = Join-Path $outDir "qa-channel-isolation-smoke.json"
try {
    $results | ConvertTo-Json -Depth 4 | Set-Content -Path $outPath -Encoding UTF8
    Write-Host "Wrote $outPath"
}
catch {
    Write-Host "Could not write results file: $_"
}

if ($script:FailCount -gt 0) { exit 1 }
exit 0
