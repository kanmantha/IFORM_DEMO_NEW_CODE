# Stops leftover DailyPosterGenerator processes so the DLL/ports are free before build/run.
$ErrorActionPreference = 'SilentlyContinue'

Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -match 'DailyPosterGenerator' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

# App-host launches (VS F5, double-click) run as DailyPosterGenerator.exe, not dotnet.exe.
Get-Process -Name 'DailyPosterGenerator' |
    ForEach-Object { Stop-Process -Id $_.Id -Force }

Get-NetTCPConnection -LocalPort 5011 -State Listen |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }

Start-Sleep -Milliseconds 800
exit 0
