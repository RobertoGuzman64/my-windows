[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(?:[01][0-9]|2[0-3]):[0-5][0-9]$')]
    [string]$DailyAt,
    [Parameter(Mandatory = $true)]
    [ValidateSet('ACEPTO_VM_DESECHABLE')]
    [string]$Confirmation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manager = Join-Path $PSScriptRoot 'Manage-RepoInspection.ps1'
$taskName = 'MyWindows.RepoInspection'
$receipt = Join-Path $env:LOCALAPPDATA 'MyWindows\automations\repo-inspect\active.json'
$repo = (Resolve-Path -LiteralPath $RepositoryPath -ErrorAction Stop).Path
$consent = 'ACEPTO_AUTOMATIZACION_LOCAL'
foreach ($scriptPath in @($manager, (Join-Path $PSScriptRoot 'Invoke-RepoInspection.ps1'), $PSCommandPath)) {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -ne 0) { throw "La sintaxis PowerShell no es válida: $scriptPath" }
}

if ($null -ne (Get-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction SilentlyContinue) -or
    (Test-Path -LiteralPath $receipt)) {
    throw 'La aceptación G7 requiere VM sin tarea ni recibo previos. No se modifica el estado existente.'
}

$installed = $false
try {
    $planned = & $manager -Action Plan -RepositoryPath $repo -DailyAt $DailyAt | ConvertFrom-Json
    if ($planned.state -ne 'Create') { throw 'El plan inicial no propone crear la tarea.' }
    if ($null -ne (Get-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction SilentlyContinue) -or
        (Test-Path -LiteralPath $receipt)) {
        throw 'El plan ha alterado el estado del programador o del recibo.'
    }
    $missingConsentBlocked = $false
    try {
        & $manager -Action Install -RepositoryPath $repo -DailyAt $DailyAt | Out-Null
    }
    catch {
        $missingConsentBlocked = $true
    }
    if (-not $missingConsentBlocked) { throw 'Se ha registrado una tarea sin consentimiento.' }

    $created = & $manager -Action Install -RepositoryPath $repo -DailyAt $DailyAt -Confirmation $consent | ConvertFrom-Json
    $installed = $true
    if ($created.state -ne 'Installed') { throw 'La instalación no cumplió su postcondición.' }

    $repeated = & $manager -Action Install -RepositoryPath $repo -DailyAt $DailyAt -Confirmation $consent | ConvertFrom-Json
    if ($repeated.state -ne 'NoChange') { throw 'Falló la idempotencia de instalación.' }

    $current = & $manager -Action Status | ConvertFrom-Json
    if ($current.state -ne 'Owned') { throw 'La tarea instalada no ha superado status.' }

    $record = Get-Content -LiteralPath $receipt -Raw -Encoding UTF8 | ConvertFrom-Json
    $report = Join-Path (Join-Path $env:LOCALAPPDATA 'MyWindows\automations\repo-inspect\runs') $record.installationId
    Start-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction Stop
    $reportPath = Join-Path $report 'latest.json'
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'No ha aparecido el informe tras solicitar la ejecución. Revisar historial y último resultado.' }
    $snapshot = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($snapshot.schemaVersion -ne 1 -or $null -eq $snapshot.repository.Branch) {
        throw 'Informe de automatización inválido.'
    }

    $disabled = & $manager -Action Disable -Confirmation $consent | ConvertFrom-Json
    if (-not $disabled.disabled) { throw 'No se ha deshabilitado el activador.' }
    $enabled = & $manager -Action Enable -Confirmation $consent | ConvertFrom-Json
    if ($enabled.disabled) { throw 'No se ha reactivado el activador.' }

    $previousAt = [DateTime]::Today.AddHours([int]$DailyAt.Substring(0, 2)).AddMinutes([int]$DailyAt.Substring(3, 2))
    $newAt = $previousAt.AddMinutes(5).ToString('HH:mm', [Globalization.CultureInfo]::InvariantCulture)
    $updated = & $manager -Action Update -DailyAt $newAt -Confirmation $consent | ConvertFrom-Json
    if ($updated.state -ne 'Updated') { throw 'No se ha confirmado la actualización del horario.' }
    $verified = & $manager -Action Status | ConvertFrom-Json
    if ($verified.state -ne 'Owned' -or $verified.scheduledAt -cne $newAt) { throw 'El horario actualizado no coincide con el recibo.' }

    $removed = & $manager -Action Remove -Confirmation $consent | ConvertFrom-Json
    $installed = $false
    if ($removed.state -ne 'Removed' -or $null -ne (Get-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction SilentlyContinue) -or
        (Test-Path -LiteralPath $receipt) -or -not (Test-Path -LiteralPath $reportPath)) {
        throw 'La retirada no conservó las garantías de propiedad y datos.'
    }

    [pscustomobject]@{
        result = 'PARTIAL'
        gate = 'G7'
        executed = @('parse-powershell', 'plan-no-effects', 'reject-no-consent', 'install', 'second-install-idempotent', 'status', 'run-now', 'report', 'disable', 'enable', 'update-time', 'remove')
        pending = @('foreign-task-collision', 'invalid-receipt', 'permissions', 'restart-during-run', 'clean-VM-restore', 'two-PC', 'CI-same-commit', 'manual-review')
    } | ConvertTo-Json -Depth 5
}
finally {
    if ($installed) {
        Write-Warning 'La ejecución se interrumpió con la tarea instalada. No se retirará sin inspeccionar su estado y propiedad.'
        & $manager -Action Status
    }
}
