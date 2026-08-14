# -----------------------------------------------------------------------------
# Demo data seeder for the I-FORM Site Query app.
#
# Creates a richer demo tenant (2 extra projects, 3 IPOs, 6 queries with severity
# variety, a second engineer, and a second EOT) so the dashboards and reports can
# be demonstrated. The script is IDEMPOTENT: re-running it skips anything that
# already exists.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/seed-demo-data.ps1
#
# Optional parameters:
#   -BaseUrl  http://localhost:5246   (default)
#   -UserName admin                   (default)
#   -Password Admin@12345             (default)
#   -SqlConnectionString "Server=(localdb)\MSSQLLocalDB;Database=IFORM_SiteQuery;Trusted_Connection=True"
#       When provided, raises queries' RaisedDate are backdated (via SQL) so the
#       Delay report shows a range of severities. Omit to keep today's dates.
# -----------------------------------------------------------------------------

param(
    [string]$BaseUrl = 'http://localhost:5246',
    [string]$UserName = 'admin',
    [string]$Password = 'Admin@12345',
    [string]$SqlConnectionString = ''
)

$ErrorActionPreference = 'Stop'
$jar = Join-Path $env:TEMP 'iform_demo_jar.txt'
if (Test-Path $jar) { Remove-Item $jar -Force }

function Get-Token([string]$html) {
    $m = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    return $(if ($m.Success) { $m.Groups[1].Value } else { '' })
}

function Post([string]$path, [string]$fields) {
    $data = "__RequestVerificationToken=$([uri]::EscapeDataString($script:token))&$fields"
    return & curl.exe -s -o NUL -w '%{redirect_url}' -b $jar -c $jar -d $data "$BaseUrl$path"
}

function Find-LinkId([string]$location, [string]$pattern) {
    $m = [regex]::Match($location, $pattern)
    return $(if ($m.Success) { $m.Groups[1].Value } else { '' })
}

function Query-Exists([string]$html, [string]$text) {
    return [regex]::IsMatch($html, [regex]::Escape($text))
}

# --- Sign in ---------------------------------------------------------------
Write-Host "Signing in as $UserName ..." -ForegroundColor Cyan
$loginPage = & curl.exe -s -c $jar "$BaseUrl/Account/Login"
$script:token = Get-Token $loginPage
if (-not $script:token) { throw 'Could not obtain the login anti-forgery token.' }
& curl.exe -s -o NUL -b $jar -c $jar -d "__RequestVerificationToken=$([uri]::EscapeDataString($script:token))&UserName=$UserName&Password=$([uri]::EscapeDataString($Password))&RememberMe=false" "$BaseUrl/Account/Login" | Out-Null
$dash = & curl.exe -s -b $jar -c $jar "$BaseUrl/Dashboard"
$script:token = Get-Token $dash
if (-not $script:token) { throw "Sign-in failed for $UserName. Check credentials and that the app is running at $BaseUrl." }
Write-Host "Signed in." -ForegroundColor Green

function Resolve-ProjectId([string]$code) {
    $page = & curl.exe -s -b $jar -c $jar "$BaseUrl/Projects?term=$([uri]::EscapeDataString($code))"
    return Find-LinkId $page "/Projects/Details/([0-9a-f-]{36})"
}

# --- Projects ---------------------------------------------------------------
Write-Host 'Seeding projects ...' -ForegroundColor Cyan
if (-not (Resolve-ProjectId 'PRJ-1002')) {
    $loc = Post '/Projects/Create' 'ProjectCode=PRJ-1002&ProjectName=Sheikh Zayed Bridge Works&Client=L%26T&Location=Abu Dhabi&Status=1'
    Write-Host "  created PRJ-1002." -ForegroundColor DarkGray
} else {
    Write-Host '  PRJ-1002 already exists.' -ForegroundColor DarkGray
}
if (-not (Resolve-ProjectId 'PRJ-1003')) {
    $loc = Post '/Projects/Create' 'ProjectCode=PRJ-1003&ProjectName=Al Maktoum Logistics Hub&Client=BAM&Location=Dubai&Status=1'
    Write-Host "  created PRJ-1003." -ForegroundColor DarkGray
} else {
    Write-Host '  PRJ-1003 already exists.' -ForegroundColor DarkGray
}
$p1 = Resolve-ProjectId 'PRJ-1002'
$p2 = Resolve-ProjectId 'PRJ-1003'
if (-not $p1 -or -not $p2) { throw 'Could not resolve project ids.' }

# --- IPOs --------------------------------------------------------------------
Write-Host 'Seeding IPOs ...' -ForegroundColor Cyan
foreach ($ipo in @(
        @{ No = 'IPO-3001'; Project = $p1; Fields = 'Quantity=1200&DispatchStatus=1&SlabTargetCastingDate=2026-08-10&SlabCompletedDate=2026-08-10' },
        @{ No = 'IPO-3002'; Project = $p1; Fields = 'Quantity=800&DispatchStatus=0&SlabTargetCastingDate=2026-07-24' },
        @{ No = 'IPO-4001'; Project = $p2; Fields = 'QuantitySqm=2500&DispatchStatus=1&SlabTargetCastingDate=2026-06-24' })) {
    $page = & curl.exe -s -b $jar -c $jar "$BaseUrl/Projects/Details/$($ipo.Project)"
    if (Query-Exists $page $ipo.No) {
        Write-Host "  $($ipo.No) already exists." -ForegroundColor DarkGray
    } else {
        $loc = Post '/Projects/CreateIpo' "IpoNumber=$($ipo.No)&ProjectId=$($ipo.Project)&$($ipo.Fields)"
        Write-Host "  created $($ipo.No)." -ForegroundColor DarkGray
    }
}

# --- Queries -----------------------------------------------------------------
Write-Host 'Seeding queries ...' -ForegroundColor Cyan
function New-Query([string]$ipo, [string]$proj, [int]$issue, [string]$code, [string]$name, [int]$qty, [string]$target, [string]$comment) {
    $page = & curl.exe -s -b $jar -c $jar "$BaseUrl/Queries?searchTerm=$([uri]::EscapeDataString($code))"
    if (Query-Exists $page $code) {
        Write-Host "  query ($code / $ipo) already exists." -ForegroundColor DarkGray
        return
    }
    $fields = "IpoNumber=$ipo&ProjectId=$proj&IssueType=$issue&ProductCode=$code&ProductName=$([uri]::EscapeDataString($name))&QuantityNos=$qty&DispatchStatus=0&Comments=$([uri]::EscapeDataString($comment))&RaisedFrom=Mobile"
    if ($target) { $fields += "&SlabTargetCastingDate=$target" }
    $loc = Post '/Queries/Create' $fields
    Write-Host "  created query ($code / $ipo)." -ForegroundColor DarkGray
}

New-Query 'IPO-3001' $p1 1 'DDAA0001' 'Tie Rod 25mm' 45 '2026-08-10' 'Missing tie rod on grid line 12'
New-Query 'IPO-3001' $p1 2 'DRBA0001' 'Tie Puller' 6 '2026-08-03' 'Production mistake, wrong hole spacing'
New-Query 'IPO-3002' $p1 3 'DDBA0001' 'Std. Waler (ALFU-Type)' 90 '2026-07-24' 'Design mistake, brace level clash'
New-Query 'IPO-3002' $p1 4 'DUAA0001' 'Square Washer' 1200 '2026-07-09' 'Dispatch missing washers'
New-Query 'IPO-4001' $p2 1 'DEDA0001' 'Low Control Brace' 60 '2026-06-24' 'Long-delayed missing braces'
New-Query 'IPO-4001' $p2 3 'DQAE2000' 'Plumbing Wall Brace 2000' 12 $null 'Design review pending, no slab date'

# --- Second engineer ----------------------------------------------------------
Write-Host 'Seeding engineer ...' -ForegroundColor Cyan
$usersPage = & curl.exe -s -b $jar -c $jar "$BaseUrl/Users"
if (Query-Exists $usersPage 'engineer2@iform.example.com') {
    Write-Host '  engineer2 already exists.' -ForegroundColor DarkGray
} else {
    $loc = Post '/Users/Create' 'FullName=Site Engineer Two&Email=engineer2@iform.example.com&UserName=engineer2&Password=Eng2%4012345&Role=SiteEngineer&Designation=Site%20Engineer&EmployeeCode=SE-002'
    Write-Host '  created engineer2.' -ForegroundColor DarkGray
}

# --- Second EOT ----------------------------------------------------------------
Write-Host 'Seeding EOT ...' -ForegroundColor Cyan
$eotPage = & curl.exe -s -b $jar -c $jar "$BaseUrl/Eot"
if (Query-Exists $eotPage 'EOT-02') {
    Write-Host '  EOT-02 already exists.' -ForegroundColor DarkGray
} else {
    $loc = Post '/Eot/Create' "ProjectId=$p1&ClientEotNumber=EOT-2026-002&FinancialYear=2026&RevisionNumber=1&Scenario=3&Category=1&DelayDays=6&Reason=Design%20revision%20late%20issue&EstimatedTimeImpactDays=6"
    Write-Host '  created EOT-02.' -ForegroundColor DarkGray
}

# --- Backdate raised dates (optional, needs direct DB access) -----------------
if ($SqlConnectionString) {
    Write-Host 'Backdating query RaisedDate for severity variety ...' -ForegroundColor Cyan
    $conn = New-Object System.Data.SqlClient.SqlConnection $SqlConnectionString
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = @"
UPDATE Queries SET RaisedDate = '2026-06-24' WHERE IpoNumber = 'IPO-4001' AND ProductCode = 'DEDA0001';
UPDATE Queries SET RaisedDate = '2026-07-09' WHERE IpoNumber = 'IPO-3002' AND ProductCode = 'DUAA0001';
UPDATE Queries SET RaisedDate = '2026-07-24' WHERE IpoNumber = 'IPO-3002' AND ProductCode = 'DDBA0001';
UPDATE Queries SET RaisedDate = '2026-08-03' WHERE IpoNumber = 'IPO-3001' AND ProductCode = 'DRBA0001';
UPDATE Queries SET RaisedDate = '2026-08-10' WHERE IpoNumber = 'IPO-3001' AND ProductCode = 'DDAA0001';
"@
        [void]$cmd.ExecuteNonQuery()
    } finally {
        $conn.Close()
    }
    Write-Host '  done.' -ForegroundColor DarkGray
}

Write-Host 'Demo data seeding complete.' -ForegroundColor Green
