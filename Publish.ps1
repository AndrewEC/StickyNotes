Set-StrictMode -Version Latest

$ProgressPreference = "SilentlyContinue"
$Global:ProgressPreference = "SilentlyContinue"
$ErrorActionPreference = "Stop"
$Global:ErrorActionPreference = "Stop"

$Expression = "Version \d\.\d\.\d, Build (\d+)"
$SourceAppAxamlFile = "App.axaml"

$SourceAppAxaml = Join-Path $PSScriptRoot "StickyNotes" $SourceAppAxamlFile
if (-not (Test-Path $SourceAppAxaml -PathType Leaf)) {
    Write-Host "Could not find $SourceAppAxamlFile file in expected location of: [$SourceAppAxaml]."
    exit 1
}

$BuildNumberMatchInfo = Get-Content $SourceAppAxaml `
    | Select-String -Pattern $Expression `
    | Where-Object { $_.Matches.Groups.Length -eq 2 } `
    | Select-Object -First 1

if ($null -eq $BuildNumberMatchInfo) {
    Write-Host "Could not find build number in $SourceAppAxamlFile."
    exit 2
}

$BuildNumber = [int]$BuildNumberMatchInfo.Matches.Groups[1].Value
$NextBuildNumber = $BuildNumber + 1
Write-Host "Bumping build number from [$BuildNumber] to [$NextBuildNumber]..."

$DestinationAppAxaml = Join-Path $PSScriptRoot "StickyNotes" "$SourceAppAxamlFile.temp"
if (Test-Path $DestinationAppAxaml -PathType Leaf) {
    Remove-Item $DestinationAppAxaml
}

Get-Content $SourceAppAxaml | ForEach-Object {
    if ($_ -match $Expression) {
        $_.Replace("Build $BuildNumber", "Build $NextBuildNumber")
    } else {
        $_
    }
} | Add-Content $DestinationAppAxaml

Move-Item $DestinationAppAxaml $SourceAppAxaml -Force

Write-Host "Publishing binary..."
dotnet publish -c release
