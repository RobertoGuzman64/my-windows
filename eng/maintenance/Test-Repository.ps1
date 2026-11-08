[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$failures = New-Object System.Collections.Generic.List[string]

foreach ($relative in @('MyWindows.slnx', 'global.json', 'mw.cmd', 'README.md', 'docs/MYWINDOWS_DOCUMENTO_FUNDACIONAL.md', 'docs/ARQUITECTURA.md', 'docs/DESARROLLO.md', 'docs/TOOLING.md', 'docs/COMPATIBILIDAD.md', 'docs/ACEPTACION_WINDOWS.md', 'docs/DECISIONES.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        $failures.Add("Archivo obligatorio ausente: $relative")
    }
}

foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -Filter '*.ps1' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' })) {
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    foreach ($parseError in $parseErrors) {
        $failures.Add("PowerShell: $($file.FullName):$($parseError.Extent.StartLineNumber): $($parseError.Message)")
    }
}

foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -in @('.csproj', '.props', '.targets', '.slnx') -and $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' })) {
    try {
        $xml = New-Object System.Xml.XmlDocument
        $xml.Load($file.FullName)
    }
    catch {
        $failures.Add("XML inválido: $($file.FullName): $($_.Exception.Message)")
    }
}

foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $root 'profiles') -Filter '*.json' -File) + @(Get-Item -LiteralPath (Join-Path $root 'global.json'))) {
    try {
        [void](Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json -ErrorAction Stop)
    }
    catch {
        $failures.Add("JSON inválido: $($file.FullName): $($_.Exception.Message)")
    }
}

foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -Filter '*.md' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' })) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    $links = [regex]::Matches($text, '(?<!\!)\[[^\]\r\n]+\]\((?<target>[^)\s]+)')
    foreach ($match in $links) {
        $target = $match.Groups['target'].Value
        if ($target -match '^(?:#|[a-zA-Z][a-zA-Z\d+.-]*:|//)') { continue }
        $path = [Uri]::UnescapeDataString(($target -split '[#?]', 2)[0])
        if ([string]::IsNullOrEmpty($path)) { continue }
        $destination = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName ($path -replace '/', [System.IO.Path]::DirectorySeparatorChar)))
        if (-not (Test-Path -LiteralPath $destination)) {
            $failures.Add("Enlace roto: $($file.FullName) -> $target")
        }
    }
}

$gitDirectory = Join-Path $root '.git'
if ((Test-Path -LiteralPath $gitDirectory) -and (Get-Command git -ErrorAction SilentlyContinue)) {
    $tracked = @(& git -C $root ls-files --cached)
    if ($LASTEXITCODE -ne 0) {
        $failures.Add('No se pudo obtener el inventario Git de archivos rastreados.')
    }
    else {
        foreach ($path in $tracked) {
            if ($path -match '(^|/)(bin|obj|artifacts|TestResults)/') {
                $failures.Add("Artefacto de compilación incluido en Git: $path")
            }
        }
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Output "FAIL: $failure" }
    throw "Auditoría del repositorio: $($failures.Count) hallazgos."
}

Write-Output 'PASS: archivos obligatorios, PowerShell, XML, JSON, enlaces de documentación y artefactos Git.'
