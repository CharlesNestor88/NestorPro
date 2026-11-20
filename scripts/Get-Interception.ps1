[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Join-Path $PSScriptRoot '..\src\NestorPro'
$nativeTarget = Join-Path $projectRoot 'native'
$downloadFolder = Join-Path ([IO.Path]::GetTempPath()) ('NestorPro-Interception-' + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $downloadFolder 'Interception.zip'
$extractPath = Join-Path $downloadFolder 'extraido'
$expectedArchive = 'AD038963D6413055765128B0B931F6E765147C9916DBA79E65D872B261F9AF10'
New-Item -ItemType Directory -Path $downloadFolder -Force | Out-Null
Write-Host 'Baixando Interception 1.0.1 da origem oficial. Nenhum driver sera executado.'
Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/oblitum/Interception/releases/download/v1.0.1/Interception.zip' -OutFile $archivePath
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedArchive) {
    throw 'SHA256 do arquivo diferente do esperado. Nenhum arquivo sera copiado para o projeto.'
}
Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
$packageRoot = Join-Path $extractPath 'Interception'
$assetsToCopy = @(
    @{ Source = (Join-Path $packageRoot 'library\x64\interception.dll'); Name = 'interception.dll'; Hash = 'AB88164C11B1B48488772D4C3BFAA4509D5B0AE9DBC5A691DC4F96F0260443C8' },
    @{ Source = (Join-Path $packageRoot 'command line installer\install-interception.exe'); Name = 'install-interception.exe'; Hash = 'E137863A79DA797F08E7A137280FF2A123809044A888FD75CE9C973198915ABE' }
)
foreach ($asset in $assetsToCopy) {
    if ((Get-FileHash -LiteralPath $asset.Source -Algorithm SHA256).Hash -ne $asset.Hash) {
        throw ('Arquivo com hash inesperado: ' + $asset.Name)
    }
    $destination = Join-Path $nativeTarget $asset.Name
    if ((Test-Path -LiteralPath $destination) -and
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $asset.Hash) {
        throw ('Ja existe arquivo diferente no destino. Preserve-o antes de continuar: ' + $destination)
    }
}
New-Item -ItemType Directory -Path $nativeTarget -Force | Out-Null
foreach ($asset in $assetsToCopy) {
    $destination = Join-Path $nativeTarget $asset.Name
    if (-not (Test-Path -LiteralPath $destination)) {
        Copy-Item -LiteralPath $asset.Source -Destination $destination
    }
}
Write-Host ('Biblioteca e instalador preparados em: ' + $nativeTarget)
Write-Host ('Arquivo original preservado em: ' + $downloadFolder)
Write-Host 'Nenhum driver foi instalado e o PC nao foi reiniciado. Confira a licenca antes de redistribuir.'
