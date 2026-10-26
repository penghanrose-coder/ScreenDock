$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /out:ModeTests.exe /reference:ScreenDock.exe tests\ModeTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & .\ModeTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $compiler /nologo /out:PresetTests.exe /reference:ScreenDock.exe tests\PresetTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Preset test compilation failed' }
    & .\PresetTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Preset tests failed' }
    & $compiler /nologo /out:EyePresetTests.exe /reference:ScreenDock.exe /reference:System.Windows.Forms.dll tests\EyePresetTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Eye preset test compilation failed' }
    & .\EyePresetTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Eye preset tests failed' }
    & $compiler /nologo /out:LayoutTests.exe /reference:ScreenDock.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll tests\LayoutTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Layout test compilation failed' }
    & .\LayoutTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Layout tests failed' }
    & $compiler /nologo /out:InlineLayoutTests.exe /reference:ScreenDock.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll tests\InlineLayoutTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Inline layout test compilation failed' }
    & .\InlineLayoutTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Inline layout tests failed' }
} finally { Pop-Location }
