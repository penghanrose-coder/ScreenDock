$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:winexe /platform:x64 /out:ScreenDock.exe /win32icon:ScreenDock.ico /win32manifest:src\app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll src\ScreenDock.cs src\Design.cs src\Brightness.cs src\MonitorModes.cs src\ModeDialog.cs src\UiZoom.cs src\DisplayInventory.cs src\EyeComfort.cs src\MonitorIo.cs src\TrayFeatures.cs src\Presets.cs src\LayoutEditor.cs src\InlineDisplaySettings.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
    Write-Host '已生成 ScreenDock.exe'
} finally { Pop-Location }



