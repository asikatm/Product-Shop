# Software (API + App + Website) ke ei PC er IIS e site hisebe boshay.
# Administrator hisebe chalate hoy (na hole nije theke UAC diye chaibe).
# Code change er por abar chalalei notun version IIS e chole jay.
#
#   .\setup-iis.ps1               install / update (port 8085)
#   .\setup-iis.ps1 -Port 9000    onno port e
param(
    [int]$Port = 8085,
    [string]$SiteName = 'GarmentsShowroom',
    [string]$SitePath = 'C:\inetpub\GarmentsShowroom'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$log = Join-Path $root 'setup-iis.log'

# ---- Admin na hole UAC diye nijeke abar chalay ----
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host 'Administrator permission lagbe - UAC te Yes chapun...'
    $p = Start-Process powershell -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$($MyInvocation.MyCommand.Path)`"", '-Port', $Port, '-SiteName', $SiteName, '-SitePath', "`"$SitePath`""
    if (Test-Path $log) { Get-Content $log -Tail 12 }
    exit $p.ExitCode
}

Start-Transcript -Path $log -Force | Out-Null
try {
    # ---- 1. ASP.NET Core Hosting Bundle (IIS er .NET module) ----
    $ancm = "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
    if (-not (Test-Path $ancm)) {
        Write-Host '1. ASP.NET Core 9 Hosting Bundle download hocche...'
        $meta = Invoke-RestMethod 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/9.0/releases.json'
        $file = $meta.releases[0].'aspnetcore-runtime'.files | Where-Object { $_.name -eq 'dotnet-hosting-win.exe' } | Select-Object -First 1
        $exe = Join-Path $env:TEMP 'dotnet-hosting-win.exe'
        Invoke-WebRequest $file.url -OutFile $exe -UseBasicParsing
        $hash = (Get-FileHash $exe -Algorithm SHA512).Hash
        if ($hash -ne $file.hash.ToUpper()) { throw 'Hosting Bundle er hash mele nai - download abar korun.' }
        Write-Host "   install hocche ($($meta.releases[0].'release-version'))..."
        $inst = Start-Process $exe -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
        if ($inst.ExitCode -notin 0, 3010) { throw "Hosting Bundle install fail (code $($inst.ExitCode))." }
        net stop was /y | Out-Null
        net start w3svc | Out-Null
    } else {
        Write-Host '1. Hosting Bundle age thekei ache.'
    }

    Import-Module WebAdministration

    # ---- 2. Publish ----
    Write-Host '2. Publish hocche (1-2 minute)...'
    $stage = Join-Path $root 'iis-publish'
    dotnet publish (Join-Path $root 'Api\ProductShop.Api\ProductShop.Api.csproj') -c Release -o $stage -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Publish fail hoyeche.' }

    # ---- 3. App pool ----
    $poolPath = "IIS:\AppPools\$SiteName"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $SiteName | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ''
    Set-ItemProperty $poolPath -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    if ((Get-WebAppPoolState -Name $SiteName).Value -eq 'Started') { Stop-WebAppPool -Name $SiteName }
    for ($i = 0; $i -lt 20 -and (Get-WebAppPoolState -Name $SiteName).Value -ne 'Stopped'; $i++) { Start-Sleep 1 }

    # ---- 4. File copy + web.config ----
    Write-Host "3. $SitePath e copy hocche..."
    New-Item -ItemType Directory -Force $SitePath, (Join-Path $SitePath 'logs') | Out-Null
    robocopy $stage $SitePath /MIR /XD logs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy fail (robocopy $LASTEXITCODE)." }

    $uploads = Join-Path $root 'Api\ProductShop.Api\uploads'
    New-Item -ItemType Directory -Force $uploads | Out-Null
    $wcPath = Join-Path $SitePath 'web.config'
    [xml]$wc = Get-Content $wcPath
    $core = $wc.SelectSingleNode('//aspNetCore')
    $core.SetAttribute('stdoutLogEnabled', 'true')
    $core.SetAttribute('stdoutLogFile', '.\logs\stdout')
    $envs = $core.SelectSingleNode('environmentVariables')
    if (-not $envs) { $envs = $core.AppendChild($wc.CreateElement('environmentVariables')) }
    $envs.RemoveAll()
    foreach ($kv in @{ ASPNETCORE_ENVIRONMENT = 'Production'; UploadsPath = $uploads }.GetEnumerator()) {
        $e = $wc.CreateElement('environmentVariable'); $e.SetAttribute('name', $kv.Key); $e.SetAttribute('value', $kv.Value); [void]$envs.AppendChild($e)
    }
    $wc.Save($wcPath)

    # ---- 5. Permission: app pool user ----
    $who = "IIS AppPool\$SiteName"
    icacls $SitePath /grant "${who}:(OI)(CI)RX" /T /Q | Out-Null
    icacls (Join-Path $SitePath 'logs') /grant "${who}:(OI)(CI)M" /Q | Out-Null
    icacls $uploads /grant "${who}:(OI)(CI)M" /T /Q | Out-Null

    # ---- 6. Site ----
    if (-not (Test-Path "IIS:\Sites\$SiteName")) {
        New-Website -Name $SiteName -Port $Port -PhysicalPath $SitePath -ApplicationPool $SiteName | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $SitePath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $SiteName
        Set-WebBinding -Name $SiteName -BindingInformation (Get-WebBinding -Name $SiteName | Select-Object -First 1).bindingInformation -PropertyName Port -Value $Port
    }
    Start-WebAppPool -Name $SiteName
    Start-Website -Name $SiteName

    # ---- 7. Firewall: office/bashar network theke dhoka ----
    $rule = "$SiteName (TCP $Port)"
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Domain, Private | Out-Null
    }

    # ---- 8. Test ----
    Write-Host '4. Test hocche...'
    $ok = $false
    for ($i = 0; $i -lt 30 -and -not $ok; $i++) {
        try { $ok = (Invoke-WebRequest "http://localhost:$Port/api/store/info" -UseBasicParsing -TimeoutSec 10).StatusCode -eq 200 } catch { Start-Sleep 2 }
    }
    if (-not $ok) { throw "Site chalu hoy nai. $SitePath\logs dekhun." }

    $ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.PrefixOrigin -in 'Dhcp', 'Manual' -and $_.IPAddress -notlike '169.*' } | Select-Object -First 1).IPAddress
    Write-Host ''
    Write-Host 'IIS e site chalu hoyeche:' -ForegroundColor Green
    Write-Host "  Software : http://localhost:$Port/login"
    Write-Host "  Website  : http://localhost:$Port/shop"
    if ($ip) { Write-Host "  Network  : http://${ip}:$Port/shop" }
    Write-Host 'SETUP-OK'
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    Stop-Transcript | Out-Null
    exit 1
}
Stop-Transcript | Out-Null
