$ErrorActionPreference = 'Stop'
$version = (Get-Content VERSION -Raw).Trim()
if ($env:REF_TYPE -eq 'tag') {
    if ($env:REF_NAME -notmatch '^v(\d+\.\d+\.\d+)$') { throw 'Use a tag such as v1.0.1' }
    $version = $Matches[1]
}
if (!$env:RUNNER_TEMP) { throw 'This script runs in GitHub Actions; use build-installer.ps1 locally.' }
$download = Join-Path $env:RUNNER_TEMP 'innosetup.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
if ((Get-FileHash $download -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') { throw 'Compiler checksum mismatch' }
$compilerDir = Join-Path $env:RUNNER_TEMP 'InnoSetup'
$p = Start-Process $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/TASKS=',('/DIR="'+$compilerDir+'"')) -WindowStyle Hidden -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Compiler installation failed' }
./build-installer.ps1 -Compiler (Join-Path $compilerDir 'ISCC.exe') -Version $version
New-Item -ItemType Directory -Path dist -Force | Out-Null
Copy-Item "../ScreenDock-Setup-$version-x64.exe" dist/
Copy-Item "../ScreenDock-Setup-$version-x64.exe" dist/ScreenDock-Setup-x64.exe
$stage = Join-Path $env:RUNNER_TEMP 'portable/ScreenDock'
New-Item -ItemType Directory -Path "$stage/profiles" -Force | Out-Null
Copy-Item ScreenDock.exe,ScreenDock.ico,LICENSE,README.md,使用说明.md $stage
Copy-Item profiles/monitors.xml "$stage/profiles/"
Compress-Archive -Path $stage -DestinationPath "dist/ScreenDock-$version-portable-x64.zip"
Get-ChildItem dist -File | Sort-Object Name | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name } | Set-Content dist/SHA256SUMS.txt -Encoding ascii
