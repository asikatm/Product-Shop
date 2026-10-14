# GitHub Pages er website (https://asikatm.github.io/garments) update kore.
#   .\publish-github.ps1               product, category, chobi + config -> GitHub e push
#   .\publish-github.ps1 -ConfigOnly   shudhu online link (config.json) update - run-online.ps1 nije chalay
#   .\publish-github.ps1 -NoPush       push na kore shudhu file banay (test er jonno)
param(
    [string]$Api = 'http://localhost:5080',
    [string]$Site = (Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) 'asikatm.github.io'),
    [string]$WhatsApp = '01581035896',
    [string]$ShopName = 'Garments Showroom',
    [switch]$ConfigOnly,
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dir = Join-Path $Site 'garments'
$data = Join-Path $dir 'data'
$imgDir = Join-Path $dir 'img'
$utf8 = New-Object Text.UTF8Encoding $false
if (-not (Test-Path $dir)) { throw "$dir pawa jay nai. Age asikatm.github.io repo clone korun." }
New-Item -ItemType Directory -Force $data, $imgDir | Out-Null

# git: PATH e na thakle GitHub Desktop er ta
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) {
    $git = Get-ChildItem "$env:LOCALAPPDATA\GitHubDesktop\app-*\resources\app\git\cmd\git.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName
}
if (-not $git) { throw 'git pawa jay nai (GitHub Desktop install ache?).' }

# ---- 1. config.json: online link (PC chalu thakle order soja software e) ----
$urlFile = Join-Path $root 'publish\online-url.txt'
$apiBase = if (Test-Path $urlFile) { (Get-Content $urlFile -Raw).Trim() } else { '' }
$wa = ($WhatsApp -replace '\D', '')
if ($wa.StartsWith('0')) { $wa = '88' + $wa }
$config = [ordered]@{
    apiBase = $apiBase
    whatsapp = $wa
    phone = $WhatsApp
    shopName = $ShopName
    deliveryCharge = 0
    payments = @()
    updatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')
}

# bKash / Nagad number + delivery charge: software theke; na pele ager config theke (PC bondho thakleo website e thake)
try {
    $info = Invoke-RestMethod "$Api/api/store/info" -TimeoutSec 15
    $config.deliveryCharge = $info.deliveryCharge
    $config.payments = @($info.payments | ForEach-Object { [ordered]@{ method = $_.method; number = $_.number; type = $_.type } })
}
catch {
    $old = Join-Path $data 'config.json'
    if (Test-Path $old) {
        $prev = [IO.File]::ReadAllText($old) | ConvertFrom-Json
        $config.deliveryCharge = $prev.deliveryCharge
        if ($prev.payments) { $config.payments = @($prev.payments) }
    }
}

if (-not $ConfigOnly) {
    # ---- 2. Product / category (website er public API theke - kena dam / stock sonkha nai) ----
    Write-Host 'Product ana hocche...'
    $products = (Invoke-WebRequest "$Api/api/store/products" -UseBasicParsing -TimeoutSec 60)
    $categories = (Invoke-WebRequest "$Api/api/store/categories" -UseBasicParsing -TimeoutSec 60)
    # Raw JSON rakha - Bangla / array thik thake
    [IO.File]::WriteAllText((Join-Path $data 'products.json'), [Text.Encoding]::UTF8.GetString($products.RawContentStream.ToArray()), $utf8)
    [IO.File]::WriteAllText((Join-Path $data 'categories.json'), [Text.Encoding]::UTF8.GetString($categories.RawContentStream.ToArray()), $utf8)

    # ---- 3. Chobi: 720px e chhoto kore (website druto khule) ----
    $ffmpeg = Join-Path $root 'tools\ffmpeg.exe'
    $uploads = Join-Path $root 'Api\ProductShop.Api\uploads'
    $urls = @(($products.Content | ConvertFrom-Json) | ForEach-Object { $_.images }) + @(($categories.Content | ConvertFrom-Json) | ForEach-Object { $_.image }) |
        Where-Object { $_ } | Sort-Object -Unique
    $names = @()
    $made = 0
    foreach ($u in $urls) {
        $name = Split-Path $u -Leaf
        $src = Join-Path $uploads ($u -replace '^uploads[/\\]', '' -replace '/', '\')
        $dst = Join-Path $imgDir $name
        if (-not (Test-Path $dst)) {
            if (-not (Test-Path $src)) { continue }
            if (Test-Path $ffmpeg) {
                & $ffmpeg -y -loglevel error -i $src -vf "scale='min(720,iw)':-2" -q:v 6 $dst
                if (-not (Test-Path $dst)) { Copy-Item $src $dst }
            } else { Copy-Item $src $dst }
            $made++
        }
        $names += $name
    }
    # Ar lage na emon purono chobi bad
    Get-ChildItem $imgDir -File | Where-Object { $names -notcontains $_.Name } | ForEach-Object { $_.Delete() }
    [IO.File]::WriteAllText((Join-Path $data 'images.json'), (ConvertTo-Json @($names) -Compress), $utf8)
    $size = (Get-ChildItem $imgDir -File | Measure-Object Length -Sum).Sum / 1MB
    Write-Host ("  {0} product, {1} chobi ({2} notun), {3:N1} MB" -f (($products.Content | ConvertFrom-Json).Count), $names.Count, $made, $size)
}

[IO.File]::WriteAllText((Join-Path $data 'config.json'), ($config | ConvertTo-Json), $utf8)
Write-Host "  Online link: $(if ($apiBase) { $apiBase } else { '(nai - WhatsApp e order jabe)' })"

if ($NoPush) { return }

# ---- 4. GitHub e push ----
Push-Location $Site
# git er warning (LF/CRLF) stderr e ashe - PowerShell 5.1 e oita error na dhora; exit code dekhe bujhi
$ErrorActionPreference = 'Continue'
try {
    & $git -c core.safecrlf=false add garments 2>&1 | Out-Null
    & $git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { Write-Host '  Kono poriborton nai.'; return }
    $msg = if ($ConfigOnly) { 'garments: online link update' } else { 'garments: product update' }
    & $git commit -q -m $msg
    $out = & $git push -q origin HEAD 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Push fail: $out" }
    Write-Host '  GitHub e push hoyeche: https://asikatm.github.io/garments/ (1-2 minute por update dekhabe)' -ForegroundColor Green
}
finally { Pop-Location }
