param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][ValidateSet('net10.0', 'net8.0', 'net461')][string]$TargetFramework
)
$ErrorActionPreference = 'Stop'
$packageFile = (Resolve-Path -LiteralPath $PackagePath).Path
$packageHash = (Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash
$archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    $specs = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
    if ($specs.Count -ne 1) { throw 'Expected exactly one NuGet specification.' }
    $reader = [IO.StreamReader]::new($specs[0].Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($spec.package.metadata.id -ne 'OoplesFinance.StockIndicators') { throw 'Unexpected package identity.' }
    $version = [string]$spec.package.metadata.version
    $entry = $archive.GetEntry("lib/$TargetFramework/OoplesFinance.StockIndicators.dll")
    if ($null -eq $entry) { throw 'The requested framework is absent from the package.' }
    $stream = $entry.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $expectedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
} finally { $archive.Dispose() }
$project = Join-Path $PSScriptRoot 'CorrectnessVerifier/CorrectnessVerifier.csproj'
$feed = Split-Path -Parent $packageFile
# A NuGet config preserves URL sources on Windows; repeated --source arguments can
# normalize the HTTPS source as a filesystem path in the restore invocation.
$configPath = [IO.Path]::GetTempFileName()
try {
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
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer restore failed.' }
} finally { Remove-Item -LiteralPath $configPath -ErrorAction SilentlyContinue }
& dotnet build $project -c Release -f $TargetFramework --no-restore "-p:IndicatorPackageVersion=$version"
if ($LASTEXITCODE -ne 0) { throw 'Package consumer build failed.' }
$binaryDirectory = Join-Path $PSScriptRoot "CorrectnessVerifier/bin/Release/$TargetFramework"
$actualHash = (Get-FileHash -LiteralPath (Join-Path $binaryDirectory 'OoplesFinance.StockIndicators.dll') -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash -or (Get-FileHash -LiteralPath $packageFile -Algorithm SHA256).Hash -ne $packageHash) {
    throw 'The consumer assembly or package changed; exact-package verification cannot proceed.'
}
@{ packageSha256=$packageHash; packageVersion=$version; assemblySha256=$expectedHash; targetFramework=$TargetFramework } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $binaryDirectory 'package-identity.json') -Encoding utf8
