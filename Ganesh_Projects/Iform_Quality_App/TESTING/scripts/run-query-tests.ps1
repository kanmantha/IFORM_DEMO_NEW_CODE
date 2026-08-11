$BaseUrl = "http://localhost:5170"
$Pass = "Demo@1234!"

function Login-Token { param([string]$User)
    $body = @{ userNameOrEmail = $User; password = $Pass } | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $body -UseBasicParsing
    return (($r.Content | ConvertFrom-Json).data.accessToken)
}

function Http {
    param([string]$M, [string]$P, [string]$T = $null, [string]$B = $null, [int]$Exp = 200, [string]$Match = $null, [string]$NoMatch = $null)
    $h = @{}; if ($T) { $h["Authorization"] = "Bearer $T" }
    try {
        $par = @{ Uri = "$BaseUrl$P"; Method = $M; Headers = $h; UseBasicParsing = $true; TimeoutSec = 30 }
        if ($B) { $par.ContentType = "application/json"; $par.Body = $B }
        $r = Invoke-WebRequest @par
        $st = $r.StatusCode; $ct = $r.Content
    } catch {
        $resp = $_.Exception.Response
        if ($resp) { $st = [int]$resp.StatusCode; $rd = New-Object System.IO.StreamReader($resp.GetResponseStream()); $ct = $rd.ReadToEnd() }
        else { $st = -1; $ct = $_.Exception.Message }
    }
    $ok = $st -eq $Exp
    if ($ok -and $Match) { $ok = $ct -match $Match }
    if ($ok -and $NoMatch) { $ok = $ct -notmatch $NoMatch }
    if ($ok) { Write-Output "[PASS] $M $P -> $st" } else {
        Write-Output "[FAIL] $M $P -> $st (exp $Exp)"; Write-Output "   BODY: $($ct.Substring(0,[Math]::Min(700,$ct.Length)))"
    }
    return @{ status = $st; body = $ct; pass = $ok }
}

$mg = Login-Token "manager@demo.local"
$se = Login-Token "siteengineer@demo.local"
$se2 = Login-Token "engineer2@demo.local"

# Get project + issue type + a query id for valid references
$projs = ((Invoke-WebRequest -Uri "$BaseUrl/api/projects/active" -Headers @{Authorization="Bearer $mg"} -UseBasicParsing).Content | ConvertFrom-Json).data
$projId = $projs[0].id
$projId2 = $projs[1].id

$srch = ((Invoke-WebRequest -Uri "$BaseUrl/api/queries?pageSize=1" -Headers @{Authorization="Bearer $mg"} -UseBasicParsing).Content | ConvertFrom-Json).data
$q1 = $srch.items[0]
$issueId = $q1.issueTypeId
$queryId = $q1.id
Write-Output "projId=$projId issueId=$issueId queryId=$queryId"

Write-Output "=== CREATE QUERY ==="
$ts = Get-Date -Format "HHmmssfff"
$qBody = @{ projectId = "$projId"; issueTypeId = "$issueId"; ipo = "IPO-QA-$ts"; quantityNos = 10; quantitySqm = 15.5; description = "QA test query $ts" } | ConvertTo-Json
$cr = Http "Post" "/api/queries" $mg $qBody 200
Write-Output "create body: $($cr.body)"
$newQid = if ($cr.pass) { ($cr.body | ConvertFrom-Json).data } else { $null }
Write-Output "new query id: $newQid"

Write-Output "=== CREATE QUERY missing IPO ==="
Http "Post" "/api/queries" $mg (@{ projectId="$projId"; issueTypeId="$issueId"; quantityNos=1; quantitySqm=1 } | ConvertTo-Json) 400
Write-Output "=== CREATE QUERY missing project ==="
Http "Post" "/api/queries" $mg (@{ issueTypeId="$issueId"; ipo="IPO-QA-X"; quantityNos=1; quantitySqm=1 } | ConvertTo-Json) 400
Write-Output "=== CREATE QUERY unknown project ==="
Http "Post" "/api/queries" $mg (@{ projectId="00000000-0000-0000-0000-000000000001"; issueTypeId="$issueId"; ipo="IPO-QA-X"; quantityNos=1; quantitySqm=1 } | ConvertTo-Json) 404
Write-Output "=== CREATE QUERY negative quantity ==="
Http "Post" "/api/queries" $mg (@{ projectId="$projId"; issueTypeId="$issueId"; ipo="IPO-QA-X"; quantityNos=-5; quantitySqm=1 } | ConvertTo-Json) 400

if ($newQid) {
    Write-Output "=== GET QUERY detail ==="
    Http "Get" "/api/queries/$newQid" -T $mg -Exp 200
    Write-Output "=== GET QUERY nonexistent ==="
    Http "Get" "/api/queries/00000000-0000-0000-0000-0000000000aa" -T $mg -Exp 404
    Write-Output "=== UPDATE QUERY ==="
    Http "Put" "/api/queries/$newQid" $mg (@{ description = "updated desc" } | ConvertTo-Json) 200
    Write-Output "=== ADD COMMENT ==="
    Http "Post" "/api/queries/$newQid/comments" $mg (@{ commentText = "QA comment" } | ConvertTo-Json) 200
    Write-Output "=== GET COMMENTS ==="
    Http "Get" "/api/queries/$newQid/comments" -T $mg -Exp 200 -Match "QA comment"
    Write-Output "=== ADD COMMENT empty text ==="
    Http "Post" "/api/queries/$newQid/comments" $mg (@{ commentText = "" } | ConvertTo-Json) 400
    Write-Output "=== CHANGE STATUS Pending->InProgress (manager) ==="
    $cs = Http "Put" "/api/queries/$newQid/status" $mg (@{ status = "InProgress" } | ConvertTo-Json) 200
    Write-Output "=== CHANGE STATUS invalid transition InProgress->Pending allowed ==="
    Http "Put" "/api/queries/$newQid/status" $mg (@{ status = "Pending" } | ConvertTo-Json) 200
    Write-Output "=== CHANGE STATUS same status ==="
    Http "Put" "/api/queries/$newQid/status" $mg (@{ status = "Pending" } | ConvertTo-Json) 400
    Write-Output "=== RESOLVE (manager) ==="
    $rs = Http "Put" "/api/queries/$newQid/resolve" $mg (@{ resolutionNote = "QA resolved" } | ConvertTo-Json) 200
    Write-Output "resolve body: $($rs.body)"
    Write-Output "=== GET detail after resolve -> statusHistory should show Pending->Resolved ==="
    $detail = (Http "Get" "/api/queries/$newQid" -T $mg -Exp 200).body | ConvertFrom-Json
    $detail.data.statusHistory | ForEach-Object { Write-Output "  history: $($_.fromStatus) -> $($_.toStatus) by $($_.changedByUserName)" }
    Write-Output "=== RESOLVE already resolved ==="
    Http "Put" "/api/queries/$newQid/resolve" $mg (@{ resolutionNote = "x" } | ConvertTo-Json) 400
    Write-Output "=== ADD COMMENT to resolved query ==="
    Http "Post" "/api/queries/$newQid/comments" $mg (@{ commentText = "late comment" } | ConvertTo-Json) 400
    Write-Output "=== UPDATE resolved query ==="
    Http "Put" "/api/queries/$newQid" $mg (@{ description = "should fail" } | ConvertTo-Json) 400
}

Write-Output "=== ACCESS CONTROL (IDOR) ==="
# engineer2 raises a query; engineer1 should not see it
$ts2 = Get-Date -Format "HHmmssfff"
$se2q = Http "Post" "/api/queries" $se2 (@{ projectId="$projId"; issueTypeId="$issueId"; ipo="IPO-IDOR-$ts2"; quantityNos=2; quantitySqm=3; description="engineer2 private query" } | ConvertTo-Json)
Write-Output "engineer2 create (should be 200 after BUG-001 fix): $($se2q.status)"
if ($se2q.pass) {
    $se2qid = ($se2q.body | ConvertFrom-Json).data
    Write-Output "--- engineer1 GET engineer2's query (expect 403) ---"
    Http "Get" "/api/queries/$se2qid" -T $se -Exp 403
    Write-Output "--- engineer1 GET comments on engineer2's query (expect 403) ---"
    Http "Get" "/api/queries/$se2qid/comments" -T $se -Exp 403
    Write-Output "--- engineer1 COMMENT on engineer2's query (expect 403) ---"
    Http "Post" "/api/queries/$se2qid/comments" $se (@{ commentText = "intruder" } | ConvertTo-Json) 403
    Write-Output "--- engineer1 UPDATE engineer2's query (expect 403) ---"
    Http "Put" "/api/queries/$se2qid" $se (@{ description = "intruder update" } | ConvertTo-Json) 403
}
