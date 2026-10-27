param([string]$Version = '0.2.1', [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch.' }
$root = $PSScriptRoot
$out = Join-Path $root 'artifacts'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $out, $dist | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio Build Tools with Desktop development with C++.' }
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'The Visual C++ x64 build tools are missing.' }
$vc = Get-ChildItem -LiteralPath (Join-Path $vs 'VC\Tools\MSVC') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$sdk = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'um\windows.h') } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (!$sdk) { throw 'The Windows 10/11 SDK is missing.' }
$cl = Join-Path $vc.FullName 'bin\Hostx64\x64\cl.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$assets = Join-Path $root 'assets'
$iconBuilder = Join-Path $out 'MakeIcon.exe'
& $csc /nologo /warn:4 /warnaserror+ /optimize+ /target:exe "/out:$iconBuilder" /reference:System.Drawing.dll (Join-Path $root 'installer\Branding.cs') (Join-Path $root 'scripts\MakeIcon.cs')
if ($LASTEXITCODE) { throw 'Icon builder compilation failed.' }
& $iconBuilder $assets
if ($LASTEXITCODE) { throw 'Icon generation failed.' }
$manifest = Join-Path $out 'setup.manifest'
(Get-Content -LiteralPath (Join-Path $root 'installer\app.manifest') -Raw).Replace('version="0.2.1.0"', ('version="' + $Version + '.0"')) | Set-Content -LiteralPath $manifest -Encoding UTF8
$savedInclude = $env:INCLUDE
$savedLib = $env:LIB
try {
    $env:INCLUDE = @((Join-Path $vc.FullName 'include'), (Join-Path $sdk.FullName 'ucrt'), (Join-Path $sdk.FullName 'shared'), (Join-Path $sdk.FullName 'um')) -join ';'
    $env:LIB = @((Join-Path $vc.FullName 'lib\x64'), (Join-Path $sdkRoot "Lib\$($sdk.Name)\ucrt\x64"), (Join-Path $sdkRoot "Lib\$($sdk.Name)\um\x64")) -join ';'
    $addon = Join-Path $out 'surround-ui.addon64'
    & $cl /nologo /std:c++17 /W4 /WX /EHsc /O2 /MT /LD (Join-Path $root 'src\addon.cpp') "/Fo$out\addon.obj" /link /Brepro "/OUT:$addon" "/IMPLIB:$out\addon.lib" user32.lib kernel32.lib
    if ($LASTEXITCODE) { throw 'Add-on compilation failed.' }
    $hash = (Get-FileHash -LiteralPath $addon -Algorithm SHA256).Hash.ToLowerInvariant()
    $payloadInfo = Join-Path $out 'PayloadInfo.cs'
    @"
using System.Reflection;
[assembly: AssemblyTitle("Trailmakers Centre Screen UI Setup")]
[assembly: AssemblyDescription("Trailmakers Surround UI Fix installer")]
[assembly: AssemblyProduct("Trailmakers Surround UI Fix")]
[assembly: AssemblyCompany("Gibsondude78")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
namespace TrailmakersSurroundUI { internal static class PayloadInfo {
    public const string Version = "$Version";
    public const string Sha256 = "$hash";
} }
"@ | Set-Content -LiteralPath $payloadInfo -Encoding UTF8
    $sources = @((Join-Path $root 'installer\Core.cs'), (Join-Path $root 'installer\Branding.cs'), (Join-Path $root 'installer\Program.cs'), (Join-Path $root 'tests\InstallerTests.cs'), $payloadInfo)
    $references = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Runtime.Serialization.dll')
    $setup = Join-Path $dist "TrailmakersSurroundUI-Setup-$Version.exe"
    & $csc /nologo /warn:4 /warnaserror+ /optimize+ /platform:x64 /target:winexe "/out:$setup" "/resource:$addon,surround-ui.addon64" "/resource:$assets\setup.ico,setup.ico" "/win32icon:$assets\setup.ico" "/win32manifest:$manifest" @references @sources
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
    if (!$SkipTests) {
        $stub = Join-Path $out 'reshade_stub.dll'
        & $cl /nologo /W4 /WX /EHsc /O2 /MT /LD (Join-Path $root 'tests\reshade_stub.cpp') "/Fo$out\stub.obj" /link /Brepro "/OUT:$stub" "/IMPLIB:$out\stub.lib"
        if ($LASTEXITCODE) { throw 'Test fixture compilation failed.' }
        $runner = Join-Path $out 'InstallerTests.exe'
        & $csc /nologo /warn:4 /warnaserror+ /optimize+ /platform:x64 /target:exe "/out:$runner" "/resource:$addon,surround-ui.addon64" @references @sources
        if ($LASTEXITCODE) { throw 'Test runner compilation failed.' }
        & $runner --self-test $stub
        if ($LASTEXITCODE) { throw 'Installer tests failed.' }
    }
    $portable = Join-Path $dist "TrailmakersSurroundUI-Portable-$Version.zip"
    Compress-Archive -LiteralPath $addon, (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE') -DestinationPath $portable -Force
    $checksums = foreach ($file in @($setup, $portable)) {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $file)
    }
    $checksums | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
    Write-Host "Release $Version built and checked in $dist"
}
finally { $env:INCLUDE = $savedInclude; $env:LIB = $savedLib }
