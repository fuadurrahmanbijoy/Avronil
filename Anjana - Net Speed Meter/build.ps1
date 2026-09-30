#requires -Version 5.1
<#
  Builds dist\Anjana-Setup.exe : one file, no prerequisites for the person installing it.
  Needs the .NET 10 SDK on the build machine only.
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$work = Join-Path $root 'build'
$dist = Join-Path $root 'dist'

# Self-contained + single file => the .NET runtime is bundled, so end users install nothing else.
$flags = @('-c','Release','-r','win-x64','--self-contained','true',
           '-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',
           '-p:EnableCompressionInSingleFile=true')

function Step($n, $text) { Write-Host "[$n/3] $text" -ForegroundColor Cyan }

Remove-Item $work, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work, $dist, (Join-Path $root 'Setup\payload') -Force | Out-Null

Step 1 'Publishing Anjana (bundles the .NET runtime)...'
dotnet publish (Join-Path $root 'App\Anjana.csproj') @flags -o (Join-Path $work 'app')
if ($LASTEXITCODE -ne 0) { throw 'Publishing the app failed.' }
Copy-Item (Join-Path $work 'app\Anjana.exe') (Join-Path $root 'Setup\payload\Anjana.exe') -Force

Step 2 'Building the installer (embeds the app)...'
dotnet publish (Join-Path $root 'Setup\Anjana.Setup.csproj') @flags -o (Join-Path $work 'setup')
if ($LASTEXITCODE -ne 0) { throw 'Building the installer failed.' }

Step 3 'Collecting output...'
Copy-Item (Join-Path $work 'setup\Anjana-Setup.exe') (Join-Path $dist 'Anjana-Setup.exe') -Force
$mb = [math]::Round((Get-Item (Join-Path $dist 'Anjana-Setup.exe')).Length / 1MB, 1)
Write-Host "`nDone: $dist\Anjana-Setup.exe ($mb MB)" -ForegroundColor Green
