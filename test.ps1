$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /out:ModeTests.exe /reference:ScreenDock.exe tests\ModeTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & .\ModeTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Pop-Location }
