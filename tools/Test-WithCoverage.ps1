[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'ERCollectionCheckerJP.sln'
$resultsDirectory = Join-Path $repositoryRoot (
    'TestResults\Coverage\' + [Guid]::NewGuid().ToString('N'))

& dotnet test $solutionPath `
    --configuration Release `
    --collect:'XPlat Code Coverage' `
    --results-directory $resultsDirectory `
    -p:DisableSourcePathMap=true

if ($LASTEXITCODE -ne 0) {
    throw "Coverage test run failed with exit code $LASTEXITCODE."
}

$coverageFiles = @(Get-ChildItem -LiteralPath $resultsDirectory -Recurse -Filter 'coverage.cobertura.xml')
if ($coverageFiles.Count -eq 0) {
    throw 'No coverage reports were generated.'
}

$validLines = 0
$coveredLines = 0
foreach ($coverageFile in $coverageFiles) {
    [xml] $report = Get-Content -Raw -LiteralPath $coverageFile.FullName
    $validLines += [int] $report.coverage.'lines-valid'
    $coveredLines += [int] $report.coverage.'lines-covered'
}

if ($validLines -eq 0) {
    throw 'Coverage reports did not contain any instrumented lines.'
}

$percentage = [Math]::Round(($coveredLines / $validLines) * 100, 2)
Write-Host "Coverage reports: $($coverageFiles.Count)"
Write-Host "Covered lines:    $coveredLines / $validLines ($percentage%)"
Write-Host "Results:          $resultsDirectory"
