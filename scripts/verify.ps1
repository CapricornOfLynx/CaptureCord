$ErrorActionPreference = 'Stop'
Push-Location "$PSScriptRoot\.."
try {
    dotnet restore --locked-mode
    if ($LASTEXITCODE) { throw 'Restore failed' }
    dotnet build -c Release --no-restore
    if ($LASTEXITCODE) { throw 'Build failed' }
    dotnet run --project tests/CaptureCord.Tests -c Release --no-build
    if ($LASTEXITCODE) { throw 'Tests failed' }
    & "$PSScriptRoot\check-licenses.ps1"
    $app = Join-Path $PWD 'src\CaptureCord.App\bin\Release\net10.0-windows\CaptureCord.App.exe'
    $process = Start-Process -FilePath $app -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(20000)) { $process.Kill(); throw 'WPF smoke test timed out' }
    if ($process.ExitCode -ne 0) { throw "WPF smoke test failed: $($process.ExitCode)" }
    Get-Content (Join-Path (Split-Path $app) 'ui-smoke\gpu-result.txt')
    Write-Host 'PASS WPF startup, dropdown rendering/contrast/selection, clean-feed pixels/independence/clear, native enumeration and clean shutdown (no capture activated)'
} finally { Pop-Location }
