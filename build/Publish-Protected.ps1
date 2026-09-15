param([string]$DestinationDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$DestinationDirectory) { $DestinationDirectory = Join-Path $repo 'artifacts\protected' }
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
# Unique build directories also avoid stale apphost resources after clock changes.
$stage = Join-Path $repo ('artifacts\protection-builds\' + [Guid]::NewGuid().ToString('N'))
$intermediate = Join-Path $stage 'obj'
$binaries = Join-Path $stage 'bin'
$published = Join-Path $stage 'publish'
Push-Location $repo
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    & dotnet publish 'KoshLauncher/KoshLauncher.csproj' -c Release '-p:PublishProfile=Protected' "-p:IntermediateOutputPath=$intermediate/" "-p:OutputPath=$binaries/" -o $published
    if ($LASTEXITCODE -ne 0) { throw 'Protected publication failed.' }
    & dotnet run --project 'tests/KoshLauncher.ProtectedTests' -- (Join-Path $intermediate 'protected') $binaries
    if ($LASTEXITCODE -ne 0) { throw 'Protected tests failed; no executable delivered.' }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $published 'KoshLauncher.exe') -Destination (Join-Path $destination 'KoshLauncher.exe')
    Write-Output "Protected executable: $destination\KoshLauncher.exe"
    Write-Output "Private diagnostic mapping retained in: $intermediate\protected"
} finally { Pop-Location }
