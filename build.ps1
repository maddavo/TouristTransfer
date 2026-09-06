param(
    [string]$KspRoot = $env:KSP_ROOT,
    [switch]$TestOnly
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework 4.x C# compiler is required.' }
$buildDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
$core = Join-Path $PSScriptRoot 'src\TransferBatch.cs'
$tests = Join-Path $PSScriptRoot 'tests\TransferBatchTests.cs'
$testExe = Join-Path $buildDir 'TransferBatchTests.exe'
& $compiler /nologo /warnaserror+ /target:exe "/out:$testExe" $core $tests
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
$adapterExe = Join-Path $buildDir 'AdapterTests.exe'
& $compiler /nologo /warnaserror+ /target:exe "/out:$adapterExe" $core (Join-Path $PSScriptRoot 'src\StockContracts.cs') (Join-Path $PSScriptRoot 'src\KspTransferContext.cs') (Join-Path $PSScriptRoot 'tests\AdapterTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Adapter test compilation failed.' }
& $adapterExe
if ($LASTEXITCODE -ne 0) { throw 'Adapter tests failed.' }
if ($TestOnly) { return }
if (!$KspRoot) { throw 'Pass -KspRoot with your KSP 1.12.5 installation, or set KSP_ROOT.' }
$managed = Join-Path $KspRoot 'KSP_x64_Data\Managed'
if (!(Test-Path -LiteralPath $managed)) { $managed = Join-Path $KspRoot 'KSP_Data\Managed' }
$references = @('Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.InputLegacyModule.dll', 'UnityEngine.UI.dll')
$arguments = @('/nologo', '/warnaserror+', '/target:library', '/optimize+', "/out:$buildDir\TouristTransfer.dll")
foreach ($name in $references) {
    $reference = Join-Path $managed $name
    if (!(Test-Path -LiteralPath $reference)) { throw "Missing KSP reference: $reference" }
    $arguments += "/reference:$reference"
}
$arguments += @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
$package = Join-Path $PSScriptRoot 'dist\TouristTransfer-0.1.0'
$pluginDir = Join-Path $package 'GameData\TouristTransfer\Plugins'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath "$buildDir\TouristTransfer.dll" -Destination $pluginDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GameData\TouristTransfer\TouristTransfer.cfg') -Destination (Join-Path $package 'GameData\TouristTransfer')
foreach ($doc in @('README.md', 'SPEC.md', 'CHANGELOG.md', 'DEVELOPMENT.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $doc) -Destination $package
}
$zip = "$package.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $expected = @('CHANGELOG.md', 'DEVELOPMENT.md', 'README.md', 'SPEC.md', 'GameData/TouristTransfer/TouristTransfer.cfg', 'GameData/TouristTransfer/Plugins/TouristTransfer.dll')
    $actual = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\', '/') })
    if (Compare-Object ($expected | Sort-Object) ($actual | Sort-Object)) { throw 'Unexpected package contents.' }
    $entry = $archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'GameData/TouristTransfer/Plugins/TouristTransfer.dll' }
    $stream = $entry.Open()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $packagedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
    if ($packagedHash -ne (Get-FileHash -LiteralPath "$buildDir\TouristTransfer.dll" -Algorithm SHA256).Hash) { throw 'Packaged DLL hash mismatch.' }
}
finally { $archive.Dispose() }
Write-Output 'Verified package contents and DLL SHA-256.'
Write-Output "Built and packaged: $zip"
Get-FileHash -LiteralPath "$buildDir\TouristTransfer.dll", $zip -Algorithm SHA256
