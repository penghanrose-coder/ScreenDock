param([string]$Compiler, [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version = '1.1.0')
$ErrorActionPreference = 'Stop'
if (!$Compiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $Compiler = $command.Source }
    else { $Compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
}
if (!(Test-Path -LiteralPath $Compiler)) { throw '请安装 Inno Setup 6，或用 -Compiler 指定 ISCC.exe 的完整路径。' }
& (Join-Path $PSScriptRoot 'build.ps1') -Version $Version
& $Compiler "/DAppVersion=$Version" (Join-Path $PSScriptRoot 'installer\ScreenDock.iss')
if ($LASTEXITCODE -ne 0) { throw '安装包编译失败' }
