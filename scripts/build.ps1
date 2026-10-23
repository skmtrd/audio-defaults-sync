param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'This build requires Windows x64 and .NET Framework 4.x.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',
    '/reference:System.Core.dll','/reference:System.Web.Extensions.dll',
    '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:Microsoft.CSharp.dll',
    ('/win32manifest:' + (Join-Path $root 'app.manifest')),
    ('/out:' + (Join-Path $OutputDirectory 'AudioDefaultsSync.exe')))
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Get-Item -LiteralPath (Join-Path $OutputDirectory 'AudioDefaultsSync.exe') | Select-Object FullName,Length
