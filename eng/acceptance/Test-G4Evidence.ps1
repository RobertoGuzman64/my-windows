[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$manifestPath = Join-Path $evidence 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Assert-Evidence {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Get-Sha {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

Assert-Evidence ($manifest.schemaVersion -eq 1) 'Versión de evidencia no reconocida.'
Assert-Evidence ($manifest.result -in @('PARTIAL', 'FAIL')) 'Resultado inválido: el runner no puede declarar G4 PASS.'
Assert-Evidence ($manifest.cleanCheckout -eq $true) 'El manifest no corresponde a un checkout limpio.'
Assert-Evidence ($manifest.commit -match '^[0-9a-f]{40,64}$') 'Identificador del commit inválido.'
Assert-Evidence ($manifest.cliSha256 -match '^[A-Fa-f0-9]{64}$') 'Falta el hash del ejecutable.'
Assert-Evidence ((Get-Sha (Join-Path $PSScriptRoot 'Invoke-G4.ps1')) -ceq $manifest.runnerSha256) 'El runner no coincide con el ejecutado.'
Assert-Evidence ((Get-Sha (Join-Path $evidence 'gates.log')) -ceq $manifest.gatesLogSha256) 'Las evidencias de gates han cambiado.'
Assert-Evidence ((Get-Sha (Join-Path $root 'profiles/base.json')) -ceq $manifest.profileHashes.'base.json') 'base.json no coincide con el perfil ensayado.'
Assert-Evidence ((Get-Sha (Join-Path $root 'profiles/repository.json')) -ceq $manifest.profileHashes.'repository.json') 'repository.json no coincide con el perfil ensayado.'
$cli = Join-Path $root 'src/MyWindows.Cli/bin/Release/net10.0/MyWindows.Cli.dll'
Assert-Evidence ((Get-Sha $cli) -ceq $manifest.cliSha256) 'El binario de la CLI no coincide con el ensayado.'
Assert-Evidence ((Get-Command git -ErrorAction SilentlyContinue) -ne $null) 'Git no está disponible.'
$head = (& git -C $root rev-parse HEAD | Out-String).Trim()
Assert-Evidence ($LASTEXITCODE -eq 0 -and $head -ceq $manifest.commit) 'El commit actual difiere del ensayado.'
$dirty = @(& git -C $root status --porcelain --untracked-files=all)
Assert-Evidence ($LASTEXITCODE -eq 0 -and $dirty.Count -eq 0) 'El checkout ha cambiado desde la ejecución.'
$required = @('G4-PREFLIGHT','G4-QUALITY','G4-PLAN','G4-PLAN-READONLY','G4-APPLY','G4-VERIFY','G4-REAPPLY','G4-IDEMPOTENCE','G4-STATUS','G4-DRIFT','G4-REPAIR','G4-REVERIFY','G4-INTERRUPTED','G4-REMOVE','G4-VERIFY-ABSENCE','G4-REMOVE-AGAIN','G4-FOREIGN-PRESERVED','G4-MISSING-REPOSITORY','G4-ARTIFACT')
$steps = @($manifest.steps)
$unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($step in $steps) {
    Assert-Evidence ($unique.Add([string]$step.id)) "Escenario duplicado: $($step.id)."
}
if ($manifest.result -eq 'PARTIAL') {
    foreach ($id in $required) {
        $matching = @($steps | Where-Object { $_.id -eq $id -and $_.status -eq 'PASS' })
        Assert-Evidence ($matching.Count -eq 1) "Escenario obligatorio no superado: $id."
    }
}
Assert-Evidence (@($manifest.incompleteScenarios).Count -gt 0) 'Las pruebas pendientes deben constar explícitamente.'
Write-Host 'PASS: evidencia G4 íntegra para este commit, perfiles, runner y binario.'
Write-Host "Estado registrado: $($manifest.result). No implica aceptación G4 ni compatibilidad certificada."
