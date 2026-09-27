# Software ta apnar PC theke chalu kore ekta temporary internet link dey (Cloudflare Tunnel).
# Ei window / PC chalu thaka porjonto link kaaj kore. Protibar chalale link notun hoy.
#
#   .\run-online.ps1            chalu kora (link dekhabe)
#   .\run-online.ps1 -Rebuild   code change er por notun kore publish kore chalu
#   .\run-online.ps1 -Stop      bondho kora
param(
    [int]$Port = 5080,
    [switch]$Rebuild,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publish = Join-Path $root 'publish'
$app = Join-Path $publish 'ProductShop.Api.exe'
$cloudflared = Join-Path $root 'tools\cloudflared.exe'
$urlFile = Join-Path $publish 'online-url.txt'

function Stop-Online {
    Get-Process ProductShop.Api -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $app } | Stop-Process -Force
    Get-Process cloudflared -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $cloudflared } | Stop-Process -Force
}

if ($Stop) {
    Stop-Online
    if (Test-Path $urlFile) { Remove-Item $urlFile }
    Write-Host 'Online link bondho kora holo.'
    return
}

Stop-Online

if ($Rebuild -or -not (Test-Path $app)) {
    Write-Host 'Publish hocche (1-2 minute)...'
    dotnet publish (Join-Path $root 'Api\ProductShop.Api\ProductShop.Api.csproj') -c Release -o $publish -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Publish fail hoyeche.' }
}

if (-not (Test-Path $cloudflared)) {
    Write-Host 'cloudflared download hocche...'
    New-Item -ItemType Directory -Force (Split-Path $cloudflared) | Out-Null
    Invoke-WebRequest 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe' -OutFile $cloudflared -UseBasicParsing
}

# 1. Software (API + App) Production mode e. Database, chobi - PC er gulo-i.
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = "http://localhost:$Port"
$env:UploadsPath = Join-Path $root 'Api\ProductShop.Api\uploads'
$appProc = Start-Process $app -WorkingDirectory $publish -PassThru -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $publish 'app.log') -RedirectStandardError (Join-Path $publish 'app-error.log')

for ($i = 0; $i -lt 60 -and -not (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue); $i++) {
    if ($appProc.HasExited) { throw "Software chalu hoy nai. $publish\app-error.log dekhun (SQL Server cholche kina?)." }
    Start-Sleep 1
}

# 2. Tunnel: internet theke ei PC er software e link
$tunnelLog = Join-Path $publish 'tunnel.log'
if (Test-Path $tunnelLog) { Remove-Item $tunnelLog }
$tunnel = Start-Process $cloudflared -ArgumentList 'tunnel', '--no-autoupdate', '--url', "http://localhost:$Port" `
    -PassThru -WindowStyle Hidden -RedirectStandardError $tunnelLog

$url = $null
for ($i = 0; $i -lt 60 -and -not $url; $i++) {
    Start-Sleep 1
    if (Test-Path $tunnelLog) {
        $m = Select-String -Path $tunnelLog -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' | Select-Object -First 1
        if ($m) { $url = $m.Matches[0].Value }
    }
}
if (-not $url) { Stop-Online; throw "Link pawa jay nai. $tunnelLog dekhun (internet ache kina?)." }

Set-Content $urlFile $url
Write-Host ''
Write-Host '  Software online e chalu hoyeche:' -ForegroundColor Green
Write-Host "  $url" -ForegroundColor Cyan
Write-Host ''
Write-Host '  Ei window khola rakhun. Bondho korte Ctrl+C chapun.'

try { Wait-Process -Id $tunnel.Id }
finally { Stop-Online }
