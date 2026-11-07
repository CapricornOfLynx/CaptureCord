param([string]$Version = '0.1.0-alpha.2', [string]$RuntimeVersion = '10.0.14')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') { throw 'Invalid release version' }
Push-Location "$PSScriptRoot\.."
try {
    $destination = Join-Path $PWD "artifacts\release\CaptureCord-$Version-win-x64"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    dotnet publish src/CaptureCord.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false "-p:Version=$Version" "-p:InformationalVersion=$Version" `
        "-p:RuntimeFrameworkVersion=$RuntimeVersion" -p:NuGetLockFilePath=packages.release.lock.json `
        -p:RestoreLockedMode=true -o $destination
    if ($LASTEXITCODE) { throw 'Self-contained publish failed' }
    Copy-Item LICENSE,README.md,THIRD-PARTY-NOTICES.md -Destination $destination
    Copy-Item third-party -Destination $destination -Recurse -Force
    $cache = $env:NUGET_PACKAGES
    if (-not $cache) { $cache = Join-Path $env:USERPROFILE '.nuget\packages' }
    foreach ($package in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')) {
        $packageFolder = Join-Path $cache "$package\$RuntimeVersion"
        [xml]$metadata = Get-Content (Join-Path $packageFolder "$package.nuspec") -Raw
        $license = $metadata.SelectSingleNode("//*[local-name()='license']")
        if (-not $license -or $license.InnerText -ne 'MIT') { throw "Runtime license review required: $package" }
        $noticesFolder = Join-Path $destination "third-party\$package"
        New-Item -ItemType Directory -Path $noticesFolder -Force | Out-Null
        $licenseFiles = @(Get-ChildItem $packageFolder -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
        if ($licenseFiles.Count -eq 0) { throw "Missing runtime notices: $package" }
        $licenseFiles | Copy-Item -Destination $noticesFolder
    }
    & "$PSScriptRoot\check-licenses.ps1" -IncludeRelease
    $executable = Join-Path $destination 'CaptureCord.App.exe'
    $process = Start-Process -FilePath $executable -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Packaged EXE smoke test timed out' }
    if ($process.ExitCode -ne 0) { throw "Packaged EXE smoke test failed: $($process.ExitCode)" }
    Get-Content (Join-Path $destination 'ui-smoke\gpu-result.txt')
    # Only ship distribution files; smoke-test screenshots remain in build output.
    $distributionFiles = @('CaptureCord.App.exe','LICENSE','README.md','THIRD-PARTY-NOTICES.md','third-party') | ForEach-Object { Join-Path $destination $_ }
    $zip = Join-Path $PWD "artifacts\release\CaptureCord-$Version-win-x64.zip"
    Compress-Archive -Path $distributionFiles -DestinationPath $zip -Force
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$zip.sha256", "$hash  $([IO.Path]::GetFileName($zip))`n")
    Write-Host "Release package: $zip"
    Write-Host "SHA256: $hash"
} finally { Pop-Location }
