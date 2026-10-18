# Fetches the KoSIT validator (github.com/itplr-kosit/validator), which XRechnungValidationTests runs to check the
# XRechnung files against the EN 16931 and XRechnung Schematron. Not committed (10 MB); CI runs this script.
# The jar needs Java 11 or later on the PATH; without either, that test is skipped.
$ErrorActionPreference = 'Stop'

$version = '1.6.3'
$sha256 = '799E64BEFCA97D4080E03608C80B85DD5A5ECC5F4AE4F35D1116EC2855B9A7C9'
$url = "https://repo1.maven.org/maven2/org/kosit/validator/$version/validator-$version-standalone.jar"

$directory = Join-Path $PSScriptRoot 'Validator'
$jar = Join-Path $directory "validator-$version-standalone.jar"

if ((Test-Path $jar) -and (Get-FileHash $jar -Algorithm SHA256).Hash -eq $sha256) {
    Write-Host "Already there: $jar"
    exit 0
}

New-Item -ItemType Directory -Force $directory | Out-Null
$download = "$jar.download"
Invoke-WebRequest $url -OutFile $download
$actual = (Get-FileHash $download -Algorithm SHA256).Hash
if ($actual -ne $sha256) {
    Remove-Item $download
    throw "Unexpected SHA-256 for $url`: $actual (expected $sha256)"
}
Move-Item -Force $download $jar
Write-Host "Fetched $jar"
