param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$VietnamWarDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\VietnamWar'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'XQuinn.NativeVerification.csproj'

dotnet build $project -c $Configuration "-p:VietnamWarDirectory=$VietnamWarDirectory"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$output = Join-Path $PSScriptRoot "bin\$Configuration\net6.0\IL2CPP.Navigator.Verifier.dll"
Write-Host "Verifier output: $output"