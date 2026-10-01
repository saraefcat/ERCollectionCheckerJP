[CmdletBinding()]
param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:[-.][0-9A-Za-z.-]+)?$')]
    [string] $Version = '0.1.1'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspaceRoot = [IO.Directory]::GetParent($repositoryRoot).FullName
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot 'artifacts\releases'))
$packageName = "ERCollectionCheckerJP-v$Version-win-x64"
$releaseDirectory = [IO.Path]::GetFullPath((Join-Path $releaseRoot $packageName))
$zipPath = [IO.Path]::GetFullPath((Join-Path $releaseRoot "$packageName.zip"))
$zipChecksumPath = [IO.Path]::GetFullPath((Join-Path $releaseRoot "$packageName.zip.sha256.txt"))
$stagingDirectory = [IO.Path]::GetFullPath(
    (Join-Path $releaseRoot ".$packageName.$([Guid]::NewGuid().ToString('N')).staging"))
$solutionPath = Join-Path $repositoryRoot 'ERCollectionCheckerJP.sln'
$applicationProject = Join-Path $repositoryRoot 'src\ERCollectionCheckerJP.App\ERCollectionCheckerJP.App.csproj'
$privateDataRoot = if ([string]::IsNullOrWhiteSpace($env:ERCollectionCheckerJPPrivateDataRoot)) {
    Join-Path $workspaceRoot 'data'
} else {
    [IO.Path]::GetFullPath($env:ERCollectionCheckerJPPrivateDataRoot)
}
$runtimeDataRoot = Join-Path $privateDataRoot '1.17\runtime'
$dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetRoot = Split-Path -Parent $dotnetExecutable
$dotnetLicensePath = Join-Path $dotnetRoot 'LICENSE.txt'
$dotnetNoticesPath = Join-Path $dotnetRoot 'ThirdPartyNotices.txt'
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)

function Assert-ChildPath {
    param(
        [Parameter(Mandatory)]
        [string] $CandidatePath,

        [Parameter(Mandatory)]
        [string] $ParentPath
    )

    $candidateFullPath = [IO.Path]::GetFullPath($CandidatePath)
    $parentFullPath = [IO.Path]::GetFullPath($ParentPath).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $requiredPrefix = $parentFullPath + [IO.Path]::DirectorySeparatorChar

    if (-not $candidateFullPath.StartsWith(
            $requiredPrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the release directory: $candidateFullPath"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

Assert-ChildPath -CandidatePath $releaseDirectory -ParentPath $releaseRoot
Assert-ChildPath -CandidatePath $zipPath -ParentPath $releaseRoot
Assert-ChildPath -CandidatePath $zipChecksumPath -ParentPath $releaseRoot
Assert-ChildPath -CandidatePath $stagingDirectory -ParentPath $releaseRoot

$gitStatus = @(& git -c "safe.directory=$repositoryRoot" -C $repositoryRoot status --porcelain)

if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the Git working tree.'
}

if ($gitStatus.Count -gt 0) {
    throw 'Release creation requires a clean Git working tree.'
}

$sourceCommit = (& git -c "safe.directory=$repositoryRoot" -C $repositoryRoot rev-parse HEAD).Trim()

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceCommit)) {
    throw 'Unable to resolve the source Git commit.'
}

if (-not (Test-Path -LiteralPath $dotnetLicensePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $dotnetNoticesPath -PathType Leaf)) {
    throw 'The .NET redistribution license files could not be located.'
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

try {
    Invoke-DotNet -Arguments @(
        'restore',
        $solutionPath
    )

    Invoke-DotNet -Arguments @(
        'test',
        $solutionPath,
        '--configuration', 'Release',
        '--no-restore'
    )

    Invoke-DotNet -Arguments @(
        'restore',
        $applicationProject,
        '--runtime', 'win-x64'
    )

    New-Item -ItemType Directory -Path $stagingDirectory | Out-Null
    Invoke-DotNet -Arguments @(
        'publish',
        $applicationProject,
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'true',
        '--no-restore',
        '--output', $stagingDirectory,
        '-p:PublishProfile=win-x64',
        "-p:Version=$Version",
        '-p:DebugType=None',
        '-p:DebugSymbols=false'
    )

    Copy-Item -LiteralPath $dotnetLicensePath -Destination (
        Join-Path $stagingDirectory 'DOTNET_LICENSE.txt')
    Copy-Item -LiteralPath $dotnetNoticesPath -Destination (
        Join-Path $stagingDirectory 'DOTNET_THIRD_PARTY_NOTICES.txt')
    [IO.File]::WriteAllText(
        (Join-Path $stagingDirectory 'SOURCE_COMMIT.txt'),
        "$sourceCommit`r`n",
        $utf8WithoutBom)

    $runtimeFiles = @(Get-ChildItem -LiteralPath $runtimeDataRoot -Recurse -File -Filter '*.json' |
        Sort-Object FullName)
    if ($runtimeFiles.Count -ne 16) {
        throw "Expected 16 Runtime JSON files, found $($runtimeFiles.Count)."
    }

    $runtimeFingerprintInput = ($runtimeFiles | ForEach-Object {
        $relativePath = $_.FullName.Substring($runtimeDataRoot.Length + 1).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$relativePath=$hash"
    }) -join "`n"
    $runtimeFingerprint = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData(
            $utf8WithoutBom.GetBytes($runtimeFingerprintInput)))
    $dotnetSdkVersion = (& dotnet --version).Trim()
    $depsPath = Join-Path $repositoryRoot (
        'src\ERCollectionCheckerJP.App\obj\Release\net10.0-windows\win-x64\' +
        'ERCollectionCheckerJP.deps.json')
    $deps = Get-Content -Raw -LiteralPath $depsPath | ConvertFrom-Json -AsHashtable
    $runtimePackKeys = @($deps.libraries.Keys | Where-Object {
        $_ -like 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/*'
    })
    if ($runtimePackKeys.Count -ne 1) {
        throw "Expected one .NET Runtime pack entry, found $($runtimePackKeys.Count)."
    }
    $dotnetRuntimeVersion = $runtimePackKeys[0].Split('/')[-1]
    [IO.File]::WriteAllLines(
        (Join-Path $stagingDirectory 'RUNTIME_DATA.txt'),
        @(
            'GameVersion=1.17',
            "FileCount=$($runtimeFiles.Count)",
            "AggregateSHA256=$runtimeFingerprint",
            "DotNetSDK=$dotnetSdkVersion",
            "DotNetRuntime=$dotnetRuntimeVersion"
        ),
        $utf8WithoutBom)

    $requiredFiles = @(
        'ERCollectionCheckerJP.exe',
        'DOTNET_LICENSE.txt',
        'DOTNET_THIRD_PARTY_NOTICES.txt',
        'LICENSE',
        'README.md',
        'RUNTIME_DATA.txt',
        'SOURCE_COMMIT.txt',
        'THIRD_PARTY_NOTICES.md'
    )

    foreach ($relativePath in $requiredFiles) {
        $requiredPath = Join-Path $stagingDirectory $relativePath

        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required release file is missing: $relativePath"
        }
    }

    $forbiddenFiles = Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File |
        Where-Object { $_.Extension -in '.pdb', '.sl2', '.co2' }

    if ($forbiddenFiles) {
        $forbiddenNames = ($forbiddenFiles.FullName -join ', ')
        throw "Forbidden files were found in the release: $forbiddenNames"
    }

    $unexpectedFiles = Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File |
        Where-Object {
            $relativePath = $_.FullName.Substring($stagingDirectory.Length + 1)
            $requiredFiles -notcontains $relativePath
        }

    if ($unexpectedFiles) {
        $unexpectedNames = ($unexpectedFiles.FullName -join ', ')
        throw "Unexpected files were found in the release: $unexpectedNames"
    }

    $checksumLines = Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = $_.FullName.Substring(
                $stagingDirectory.Length + 1).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            "$hash  $relativePath"
        }
    [IO.File]::WriteAllLines(
        (Join-Path $stagingDirectory 'SHA256SUMS.txt'),
        $checksumLines,
        $utf8WithoutBom)

    if (Test-Path -LiteralPath $releaseDirectory) {
        Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
    }

    Move-Item -LiteralPath $stagingDirectory -Destination $releaseDirectory

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $releaseDirectory '*') `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    [IO.File]::WriteAllText(
        $zipChecksumPath,
        "$zipHash  $([IO.Path]::GetFileName($zipPath))`r`n",
        $utf8WithoutBom)

    Write-Host "Release directory: $releaseDirectory"
    Write-Host "Release archive:   $zipPath"
    Write-Host "Archive SHA-256:   $zipHash"
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Assert-ChildPath -CandidatePath $stagingDirectory -ParentPath $releaseRoot
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}
