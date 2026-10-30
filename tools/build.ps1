<#
星玥 构建脚本（只依赖 .NET SDK，无需安装 Visual Studio）

用法：
    powershell -ExecutionPolicy Bypass -File tools\build.ps1
    powershell -ExecutionPolicy Bypass -File tools\build.ps1 -Configuration Debug

为什么需要这几个额外参数：
  * FrameworkPathOverride —— 本机没装 .NET Framework 4.8 开发包，
    改用 NuGet 上的 Microsoft.NETFramework.ReferenceAssemblies 提供引用程序集。
  * LangVersion=latest —— csproj 里写的 C# 13.0 超出 .NET 8 SDK 自带的 Roslyn，
    用 latest 让 C# 取 12、VB 取 16。
  * IncludePackageReferencesDuringMarkupCompilation=false —— XingYue.vbproj 是旧式项目、
    没有 PackageReference，新版 WinFX.targets 会去找不存在的 nuget.g.props 而报 MSB3030。
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'Snapshot')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

$refBin = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net48\1.0.3\build\.NETFramework\v4.8'

if (-not (Test-Path (Join-Path $refBin 'mscorlib.dll'))) {
    Write-Host '[准备] 本地缺少 .NET Framework 4.8 引用程序集，正在从 NuGet 获取...' -ForegroundColor Cyan
    $tmp = Join-Path $env:TEMP ('xy-refasm-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $xml = @(
        '<Project Sdk="Microsoft.NET.Sdk">',
        '  <PropertyGroup>',
        '    <TargetFramework>net48</TargetFramework>',
        '  </PropertyGroup>',
        '  <ItemGroup>',
        '    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" />',
        '  </ItemGroup>',
        '</Project>'
    )
    Set-Content -Path (Join-Path $tmp 'refasm.csproj') -Value $xml -Encoding UTF8
    & dotnet restore (Join-Path $tmp 'refasm.csproj') -v q | Out-Host
    if ($LASTEXITCODE -ne 0) { throw '获取引用程序集失败，请检查网络或 NuGet 源。' }
    Remove-Item $tmp -Recurse -Force

    if (-not (Test-Path (Join-Path $refBin 'mscorlib.dll'))) {
        throw "引用程序集仍未出现在 $refBin"
    }
}

Write-Host "[构建] 配置 = $Configuration" -ForegroundColor Cyan
Write-Host "[构建] 引用程序集 = $refBin"

& dotnet build (Join-Path $repo 'XingYue.sln') `
    -c $Configuration `
    -p:FrameworkPathOverride="$refBin" `
    -p:LangVersion=latest `
    -p:IncludePackageReferencesDuringMarkupCompilation=false `
    -v m

if ($LASTEXITCODE -ne 0) { throw "构建失败（exit $LASTEXITCODE）" }

$exe = Join-Path $repo ('XingYue\bin\' + $Configuration + '\XingYue Launcher.exe')
if (-not (Test-Path $exe)) { $exe = Join-Path $repo 'XingYue\bin\XingYue Launcher.exe' }

if (Test-Path $exe) {
    $f = Get-Item $exe
    Write-Host ''
    Write-Host "构建成功：$($f.FullName)" -ForegroundColor Green
    Write-Host ("  {0:0.00} MB，编译于 {1}" -f ($f.Length / 1MB), $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))
} else {
    Write-Host '构建命令返回成功，但没找到 XingYue Launcher.exe，请检查输出路径。' -ForegroundColor Yellow
}
