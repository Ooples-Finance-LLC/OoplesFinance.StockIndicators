param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][ValidateSet('net10.0', 'net8.0', 'net461')][string]$TargetFramework,
    [Parameter(Mandatory = $true)][string]$EvidencePath
)
$ErrorActionPreference = 'Stop'
$packageFile = (Resolve-Path -LiteralPath $PackagePath).Path
$originalHash = (Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash
$archive = [System.IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    $specs = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
    if ($specs.Count -ne 1) { throw 'Expected exactly one NuGet specification.' }
    $reader = [System.IO.StreamReader]::new($specs[0].Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $version = [string]$spec.package.metadata.version
    if ([string]$spec.package.metadata.id -ne 'OoplesFinance.StockIndicators') { throw 'Unexpected package identity.' }
    $entry = $archive.GetEntry("lib/$TargetFramework/OoplesFinance.StockIndicators.dll")
    if ($null -eq $entry) { throw "Package does not contain $TargetFramework." }
    $stream = $entry.Open()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $expectedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
} finally { $archive.Dispose() }

$project = Join-Path $PSScriptRoot 'CorrectnessVerifier/CorrectnessVerifier.csproj'
$feed = Split-Path -Parent $packageFile
$configPath = [System.IO.Path]::GetFullPath("$EvidencePath.nuget.config")
$config = [System.Xml.XmlWriter]::Create($configPath)
try {
    $config.WriteStartElement('configuration')
    $config.WriteStartElement('packageSources')
    $config.WriteElementString('clear', '')
    foreach ($source in @(@('candidate', $feed), @('nuget.org', 'https://api.nuget.org/v3/index.json'))) {
        $config.WriteStartElement('add')
        $config.WriteAttributeString('key', $source[0])
        $config.WriteAttributeString('value', $source[1])
        $config.WriteEndElement()
    }
    $config.WriteEndElement()
    $config.WriteEndElement()
} finally { $config.Dispose() }
& dotnet restore $project "-p:IndicatorPackageVersion=$version" "-p:TargetFramework=$TargetFramework" --configfile $configPath
if ($LASTEXITCODE -ne 0) { throw "Verifier restore failed: $LASTEXITCODE" }
& dotnet run --project $project --configuration Release --framework $TargetFramework --no-restore "-p:IndicatorPackageVersion=$version" -- $EvidencePath
if ($LASTEXITCODE -ne 0) { throw "Package contract validation failed: $LASTEXITCODE" }
[xml]$evidence = Get-Content -LiteralPath $EvidencePath -Raw
if ($evidence.correctnessEvidence.scope -ne 'all-discovered-configurations') { throw 'Filtered evidence cannot satisfy a release gate.' }
if ($evidence.correctnessEvidence.assemblySha256 -ne $expectedHash) {
    throw 'The loaded assembly differs from the package being published; a cached or rebuilt binary cannot satisfy the gate.'
}
if ((Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash -ne $originalHash) {
    throw 'The package changed during verification.'
}
$evidence.correctnessEvidence.SetAttribute('packageSha256', $originalHash)
$evidence.correctnessEvidence.SetAttribute('packageVersion', $version)
$evidence.correctnessEvidence.SetAttribute('targetFramework', $TargetFramework)
$evidence.Save([System.IO.Path]::GetFullPath($EvidencePath))
Write-Host "Verified $TargetFramework in package $version ($originalHash)."
