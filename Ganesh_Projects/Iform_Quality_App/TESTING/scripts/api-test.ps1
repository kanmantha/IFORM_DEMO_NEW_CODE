param(
    [string]$Base = "http://localhost:5170"
)

$Results = @()
$BaseUrl = $Base.TrimEnd('/')

function Invoke-Test {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Path,
        [string]$Token = $null,
        [string]$Body = $null,
        [int]$ExpectedStatus = 200,
        [string]$Contains = $null,
        [string]$NotContains = $null
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    try {
        $params = @{
            Uri = "$BaseUrl$Path"
            Method = $Method
            Headers = $headers
            UseBasicParsing = $true
            TimeoutSec = 30
        }
        if ($Body) {
            $params.ContentType = "application/json"
            $params.Body = $Body
        }

        $r = Invoke-WebRequest @params
        $status = $r.StatusCode
        $content = $r.Content
    } catch {
        $resp = $_.Exception.Response
        if ($resp) {
            $status = [int]$resp.StatusCode
            $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
            $content = $reader.ReadToEnd()
        } else {
            $status = -1
            $content = $_.Exception.Message
        }
    }

    $pass = $status -eq $ExpectedStatus
    if ($pass -and $Contains) { $pass = $content -match $Contains }
    if ($pass -and $NotContains) { $pass = $content -notmatch $NotContains }

    $Results += [PSCustomObject]@{
        Name = $Name
        Method = $Method
        Path = $Path
        Expected = $ExpectedStatus
        Actual = $status
        Pass = if ($pass) { "PASS" } else { "FAIL" }
        Body = $content
    }

    $tag = if ($pass) { "PASS" } else { "FAIL" }
    Write-Output "[$tag] $Method $Path -> $status (expected $ExpectedStatus) | $Name"
    if (-not $pass) {
        Write-Output "      BODY: $($content.Substring(0, [Math]::Min(600, $content.Length)))"
    }
    return $pass
}

function Login-Token {
    param([string]$User, [string]$Pass = "Demo@1234!")
    $body = @{ userNameOrEmail = $User; password = $Pass } | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $body -UseBasicParsing
    $j = $r.Content | ConvertFrom-Json
    return $j.data.accessToken
}

$global:Results = $Results
