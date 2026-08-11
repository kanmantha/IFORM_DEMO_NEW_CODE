# Comprehensive API test run
$BaseUrl = "http://localhost:5170"
$Pass = "Demo@1234!"
$Results = @()

function Invoke-Test {
    param(
        [string]$Name, [string]$Method, [string]$Path, [string]$Token = $null,
        [string]$Body = $null, [int]$ExpectedStatus = 200,
        [string]$Contains = $null, [string]$NotContains = $null
    )
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    try {
        $params = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $headers; UseBasicParsing = $true; TimeoutSec = 30 }
        if ($Body) { $params.ContentType = "application/json"; $params.Body = $Body }
        $r = Invoke-WebRequest @params
        $status = $r.StatusCode; $content = $r.Content
    } catch {
        $resp = $_.Exception.Response
        if ($resp) { $status = [int]$resp.StatusCode; $reader = New-Object System.IO.StreamReader($resp.GetResponseStream()); $content = $reader.ReadToEnd() }
        else { $status = -1; $content = $_.Exception.Message }
    }
    $pass = $status -eq $ExpectedStatus
    if ($pass -and $Contains) { $pass = $content -match $Contains }
    if ($pass -and $NotContains) { $pass = $content -notmatch $NotContains }
    $script:Results += [PSCustomObject]@{ Name=$Name; Method=$Method; Path=$Path; Expected=$ExpectedStatus; Actual=$status; Pass=if($pass){"PASS"}else{"FAIL"}; Body=$content }
    $tag = if ($pass) { "PASS" } else { "FAIL" }
    Write-Output "[$tag] $Name"
    if (-not $pass) { Write-Output "      -> $status (expected $ExpectedStatus)"; Write-Output "      BODY: $($content.Substring(0,[Math]::Min(500,$content.Length)))" }
    return $pass
}

function Login-Token {
    param([string]$User, [string]$pwd = $Pass)
    $body = @{ userNameOrEmail = $User; password = $pwd } | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $body -UseBasicParsing
    return (($r.Content | ConvertFrom-Json).data.accessToken)
}

function Refresh-From {
    param([string]$User)
    $body = @{ userNameOrEmail = $User; password = $Pass } | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $body -UseBasicParsing
    return (($r.Content | ConvertFrom-Json).data.refreshToken)
}

Write-Output "===== AUTH TESTS ====="
$mg = Login-Token "manager@demo.local"
$se = Login-Token "siteengineer@demo.local"
Write-Output "  manager token acquired: $([bool]$mg)"
Write-Output "  engineer token acquired: $([bool]$se)"

# A1 login - wrong password
Invoke-Test -Name "Login wrong password" -Method Post -Path "/api/auth/login" -Body (@{userNameOrEmail="manager@demo.local";password="wrongpass1"} | ConvertTo-Json) -ExpectedStatus 401
# A1 login - unknown user
Invoke-Test -Name "Login unknown user" -Method Post -Path "/api/auth/login" -Body (@{userNameOrEmail="nobody@demo.local";password="wrongpass1"} | ConvertTo-Json) -ExpectedStatus 401
# A1 login - empty body / missing password now rejected by FluentValidation (BUG-003) -> 400
Invoke-Test -Name "Login empty body" -Method Post -Path "/api/auth/login" -Body "{}" -ExpectedStatus 400
Invoke-Test -Name "Login missing password" -Method Post -Path "/api/auth/login" -Body (@{userNameOrEmail="manager@demo.local"} | ConvertTo-Json) -ExpectedStatus 400
# A1 login - case-insensitive email resolution (documented accepted behaviour, Identity normalizes)
Invoke-Test -Name "Login wrong case email" -Method Post -Path "/api/auth/login" -Body (@{userNameOrEmail="Manager@Demo.Local";password=$Pass} | ConvertTo-Json) -ExpectedStatus 200
# A1 login - XSS/SQLi injection as credentials
Invoke-Test -Name "Login SQLi username" -Method Post -Path "/api/auth/login" -Body (@{userNameOrEmail="' OR '1'='1";password="x"} | ConvertTo-Json) -ExpectedStatus 401
Invoke-Test -Name "Login XSS username" -Method Post -Path "/api/auth/login" -Body (@{userNameOrEmail="<script>alert(1)</script>";password="x"} | ConvertTo-Json) -ExpectedStatus 401

# A5 me
Invoke-Test -Name "Me no token" -Method Get -Path "/api/auth/me" -ExpectedStatus 401
Invoke-Test -Name "Me manager" -Method Get -Path "/api/auth/me" -Token $mg -Contains "manager@demo.local"
Invoke-Test -Name "Me engineer" -Method Get -Path "/api/auth/me" -Token $se -Contains "siteengineer@demo.local"

# A3 logout - wrong token type
$rt = Refresh-From "manager@demo.local"
Invoke-Test -Name "Logout" -Method Post -Path "/api/auth/logout" -Token $mg -Body (@{refreshToken=$rt} | ConvertTo-Json) -ExpectedStatus 200
# reuse revoked refresh token -> 401
Invoke-Test -Name "Refresh with revoked token" -Method Post -Path "/api/auth/refresh" -Body (@{refreshToken=$rt} | ConvertTo-Json) -ExpectedStatus 401

# A2 refresh valid
$rt2 = Refresh-From "manager@demo.local"
$body = @{refreshToken=$rt2} | ConvertTo-Json
$r = Invoke-WebRequest -Uri "$BaseUrl/api/auth/refresh" -Method Post -ContentType "application/json" -Body $body -UseBasicParsing
$newrt = ($r.Content | ConvertFrom-Json).data.refreshToken
Write-Output "  refresh valid -> $($r.StatusCode), new refresh token present: $([bool]$newrt)"
# old refresh token now revoked -> 401
Invoke-Test -Name "Refresh old token after rotation" -Method Post -Path "/api/auth/refresh" -Body $body -ExpectedStatus 401

# A4 change-password validations
Invoke-Test -Name "Change password wrong current" -Method Post -Path "/api/auth/change-password" -Token $mg -Body (@{currentPassword="wrong";newPassword="NewPass@1234"} | ConvertTo-Json) -ExpectedStatus 400
Invoke-Test -Name "Change password short new" -Method Post -Path "/api/auth/change-password" -Token $mg -Body (@{currentPassword=$Pass;newPassword="short"} | ConvertTo-Json) -ExpectedStatus 400

# Token tampering
Invoke-Test -Name "Invalid token format" -Method Get -Path "/api/auth/me" -Token "not-a-jwt" -ExpectedStatus 401
Invoke-Test -Name "Empty Bearer" -Method Get -Path "/api/auth/me" -Token "" -ExpectedStatus 401

Write-Output "`n===== QUERIES ====="
# B1 create query (expect this to reveal the inverted auth bug)
$proj = (Invoke-WebRequest -Uri "$BaseUrl/api/projects/active" -Method Get -Headers @{Authorization="Bearer $mg"} -UseBasicParsing).Content | ConvertFrom-Json
$projectId = $proj.data[0].id
$issues = (Invoke-WebRequest -Uri "$BaseUrl/api/projects/active" -Method Get -Headers @{Authorization="Bearer $mg"} -UseBasicParsing).Content
# issue types: fetch via a query search or the seeder - use dashboard snapshot? none lists issue types. Use products? none. We'll get issue types from a known query.
$qs = (Invoke-WebRequest -Uri "$BaseUrl/api/queries?pageSize=1" -Method Get -Headers @{Authorization="Bearer $mg"} -UseBasicParsing).Content | ConvertFrom-Json
$issueId = $qs.data.items[0].issueTypeId
Write-Output "  sample project=$projectId issueType=$issueId"

$qBody = @{ projectId="$projectId"; issueTypeId="$issueId"; ipo="IPO-TEST-001"; quantityNos=10; quantitySqm=15.5; description="QA test query" } | ConvertTo-Json
Invoke-Test -Name "Create query as manager" -Method Post -Path "/api/queries" -Token $mg -Body $qBody -ExpectedStatus 200 -Contains '"success":true'

# B2/B3 search
Invoke-Test -Name "Search queries no auth" -Method Get -Path "/api/queries" -ExpectedStatus 401
Invoke-Test -Name "Search queries engineer (mine only default)" -Method Get -Path "/api/queries" -Token $se -ExpectedStatus 200

Write-Output "`n===== DASHBOARD ====="
Invoke-Test -Name "Dashboard snapshot no auth" -Method Get -Path "/api/dashboard/snapshot" -ExpectedStatus 401
Invoke-Test -Name "Dashboard snapshot manager" -Method Get -Path "/api/dashboard/snapshot" -Token $mg -ExpectedStatus 200
Invoke-Test -Name "Dashboard open engineer (access check -> 403 after BUG-004)" -Method Get -Path "/api/dashboard/open" -Token $se -ExpectedStatus 403

Write-Output "`n===== AUTHORIZATION (engineer trying manager actions) ====="
Invoke-Test -Name "Engineer: list users (403)" -Method Get -Path "/api/users" -Token $se -ExpectedStatus 403
Invoke-Test -Name "Engineer: create user (403)" -Method Post -Path "/api/users" -Token $se -Body (@{fullName="X";userName="x";email="x@x.com";password="Pw123456";roles=@("Manager")} | ConvertTo-Json) -ExpectedStatus 403
$qid = $qs.data.items[0].id
Invoke-Test -Name "Engineer: change query status (403)" -Method Put -Path "/api/queries/$qid/status" -Token $se -Body (@{status="InProgress"} | ConvertTo-Json) -ExpectedStatus 403
Invoke-Test -Name "Engineer: resolve query (403)" -Method Put -Path "/api/queries/$qid/resolve" -Token $se -Body (@{resolutionNote="x"} | ConvertTo-Json) -ExpectedStatus 403
Invoke-Test -Name "Engineer: create product (403)" -Method Post -Path "/api/products" -Token $se -Body (@{code="X";description="Y"} | ConvertTo-Json) -ExpectedStatus 403
Invoke-Test -Name "Engineer: email templates (403)" -Method Get -Path "/api/email/templates" -Token $se -ExpectedStatus 403
Invoke-Test -Name "Engineer: audit log (403)" -Method Get -Path "/api/audit" -Token $se -ExpectedStatus 403
Invoke-Test -Name "No auth: dashboard (401)" -Method Get -Path "/api/dashboard/snapshot" -ExpectedStatus 401

Write-Output "`n===== PRODUCTS ====="
Invoke-Test -Name "Search products manager" -Method Get -Path "/api/products" -Token $mg -ExpectedStatus 200
$ps = (Invoke-WebRequest -Uri "$BaseUrl/api/products?pageSize=1" -Method Get -Headers @{Authorization="Bearer $mg"} -UseBasicParsing).Content | ConvertFrom-Json
$prodId = $ps.data.items[0].id
Invoke-Test -Name "Get product by unknown id" -Method Get -Path "/api/products/00000000-0000-0000-0000-000000000000" -Token $mg -ExpectedStatus 404
Invoke-Test -Name "Get product detail" -Method Get -Path "/api/products/$prodId" -Token $mg -ExpectedStatus 200
Invoke-Test -Name "Create duplicate product code" -Method Post -Path "/api/products" -Token $mg -Body (@{code=$ps.data.items[0].code;description="dup"} | ConvertTo-Json) -ExpectedStatus 400

Write-Output "`n===== EMAIL ====="
Invoke-Test -Name "List email templates" -Method Get -Path "/api/email/templates" -Token $mg -ExpectedStatus 200 -Contains "Default"

Write-Output "`n===== AUDIT ====="
Invoke-Test -Name "Audit log search manager" -Method Get -Path "/api/audit?pageSize=5" -Token $mg -ExpectedStatus 200

Write-Output "`n===== NOTIFICATIONS ====="
Invoke-Test -Name "Notifications mine" -Method Get -Path "/api/notifications" -Token $mg -ExpectedStatus 200

Write-Output "`n===== REPORTS ====="
Invoke-Test -Name "Report generate open csv" -Method Post -Path "/api/reports/generate" -Token $mg -Body (@{type="OpenQueries";format="Csv"} | ConvertTo-Json) -ExpectedStatus 200 -Contains "text/csv"
Invoke-Test -Name "Report invalid type" -Method Post -Path "/api/reports/generate" -Token $mg -Body (@{type=99;format="Csv"} | ConvertTo-Json) -ExpectedStatus 400
Invoke-Test -Name "Report date range reversed" -Method Post -Path "/api/reports/generate" -Token $mg -Body (@{type="OpenQueries";format="Csv";from="2026-08-01T00:00:00Z";to="2020-01-01T00:00:00Z"} | ConvertTo-Json) -ExpectedStatus 400

Write-Output "`n===== HEALTH ====="
Invoke-Test -Name "Health check" -Method Get -Path "/health" -ExpectedStatus 200

$passed = ($Results | Where-Object Pass -eq "PASS").Count
$failed = ($Results | Where-Object Pass -eq "FAIL").Count
Write-Output "`n===== SUMMARY: $passed passed, $failed failed ====="
$Results | Where-Object Pass -eq "FAIL" | ForEach-Object { Write-Output "  FAIL: $($_.Name) -> $($_.Actual) (expected $($_.Expected))" }
