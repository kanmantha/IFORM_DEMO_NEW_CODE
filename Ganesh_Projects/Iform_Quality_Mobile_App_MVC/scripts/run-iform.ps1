$ErrorActionPreference = 'Stop'
$log = Join-Path $PSScriptRoot 'run-iform.log'
$exe = Join-Path $PSScriptRoot '..\src\IForm.Web\bin\Debug\net10.0\IForm.Web.exe'
function Log($msg) { Add-Content -Path $log -Value ("[run-iform] " + (Get-Date -Format s) + " " + $msg) }
Log "watchdog started"
while ($true) {
    if (-not (Test-Path $exe)) {
        Log "exe missing, waiting 30s"
        Start-Sleep -Seconds 30
        continue
    }
    Log "starting IForm.Web.exe"
    & $exe --environment Development
    $code = $LASTEXITCODE
    Log "exited code=$code"
    if ($code -eq 0) { Log "clean exit, watchdog stopping"; break }
    Start-Sleep -Seconds 5
}
Log "watchdog exited"
