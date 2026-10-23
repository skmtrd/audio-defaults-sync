$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root 'test-output'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$arguments = @('/nologo','/target:exe','/main:AudioDefaultsSync.Tests','/platform:x64','/codepage:65001',
    '/reference:System.Core.dll','/reference:System.Web.Extensions.dll',
    '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:Microsoft.CSharp.dll',
    ('/out:' + (Join-Path $outputDirectory 'Tests.exe')))
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$arguments += Join-Path $root 'tests\Tests.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $outputDirectory 'Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
