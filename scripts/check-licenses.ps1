param([string]$PackageRoot = $env:NUGET_PACKAGES, [switch]$IncludeRelease)
$ErrorActionPreference = 'Stop'
if (-not $PackageRoot) { $PackageRoot = Join-Path $env:USERPROFILE '.nuget\packages' }
$allowed = @{ 'NAudio.Core/2.2.1' = 'MIT'; 'NAudio.Wasapi/2.2.1' = 'MIT'; 'Microsoft.NET.ILLink.Tasks/10.0.14' = 'MIT' }
$seen = @{}
$filter = if ($IncludeRelease) { 'packages*.lock.json' } else { 'packages.lock.json' }
foreach ($lock in Get-ChildItem "$PSScriptRoot\..\src","$PSScriptRoot\..\tests" -Filter $filter -Recurse) {
    $data = Get-Content -LiteralPath $lock.FullName -Raw | ConvertFrom-Json
    foreach ($framework in $data.dependencies.PSObject.Properties) {
        foreach ($package in $framework.Value.PSObject.Properties) {
            if ($package.Value.type -eq 'Project') { continue }
            $id = $package.Name
            $version = $package.Value.resolved
            $key = "$id/$version"
            if (-not $allowed.ContainsKey($key)) { throw "Dependency requires license review: $key" }
            $nuspec = Join-Path $PackageRoot "$($id.ToLowerInvariant())\$version\$($id.ToLowerInvariant()).nuspec"
            [xml]$metadata = Get-Content -LiteralPath $nuspec -Raw
            $license = $metadata.SelectSingleNode("//*[local-name()='license']")
            if (-not $license -or $license.type -ne 'expression' -or $license.InnerText -ne $allowed[$key]) {
                throw "License metadata mismatch: $key"
            }
            $seen[$key] = $license.InnerText
        }
    }
}
if (-not $seen.ContainsKey('NAudio.Core/2.2.1') -or -not $seen.ContainsKey('NAudio.Wasapi/2.2.1')) { throw 'Incomplete dependency graph.' }
$seen.GetEnumerator() | Sort-Object Name | Format-Table -AutoSize
