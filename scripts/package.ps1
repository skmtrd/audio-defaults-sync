param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a numeric version such as 1.0.0.' }
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'test.ps1')
$bundle = Join-Path $root ('release\AudioDefaultsSync-' + $Version + '-win-x64')
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'dist\AudioDefaultsSync.exe') -Destination $bundle
foreach ($name in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $bundle
}
$zipPath = $bundle + '.zip'
Compress-Archive -LiteralPath $bundle -DestinationPath $zipPath -Force
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
($hash.Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $zipPath)) |
    Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ASCII
Get-Item -LiteralPath $zipPath,($zipPath + '.sha256') | Select-Object FullName,Length
