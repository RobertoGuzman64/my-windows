[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Plan', 'Install', 'Status', 'Update', 'Disable', 'Enable', 'Remove')]
    [string]$Action,
    [string]$RepositoryPath,
    [ValidatePattern('^(?:[01][0-9]|2[0-3]):[0-5][0-9]$')]
    [string]$DailyAt,
    [string]$Confirmation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'Este adaptador requiere el Programador de tareas de Windows.'
}
Import-Module ScheduledTasks -ErrorAction Stop
. (Join-Path $PSScriptRoot 'Test-TaskDefinition.ps1')

$taskName = 'MyWindows.RepoInspection'
$managedRoot = Join-Path $env:LOCALAPPDATA 'MyWindows\automations\repo-inspect'
$receiptPath = Join-Path $managedRoot 'active.json'
$runsRoot = Join-Path $managedRoot 'runs'
$runnerPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Invoke-RepoInspection.ps1'))
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$cliAssembly = Join-Path $repositoryRoot 'src\MyWindows.Cli\bin\Release\net10.0\MyWindows.Cli.dll'
$powerShellExecutable = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$ownerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value

function Test-ManagedDirectories {
    foreach ($directory in @($env:LOCALAPPDATA, (Join-Path $env:LOCALAPPDATA 'MyWindows'),
        (Join-Path $env:LOCALAPPDATA 'MyWindows\automations'), $managedRoot, $runsRoot)) {
        if (Test-Path -LiteralPath $directory) {
            $item = Get-Item -LiteralPath $directory -Force
            if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Ruta administrada inexistente como directorio normal o redirigida. Operación bloqueada.'
            }
        }
    }
    if (Test-Path -LiteralPath $receiptPath) {
        $item = Get-Item -LiteralPath $receiptPath -Force
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'El recibo no es un archivo regular. Operación bloqueada.'
        }
    }
}

function Get-RegisteredTask {
    Get-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction SilentlyContinue
}

function Get-Receipt {
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        return $null
    }
    $record = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    if ($record.schemaVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace($record.installationId) -or
        [string]::IsNullOrWhiteSpace($record.description) -or
        $record.ownerSid -ne $ownerSid -or
        [string]::IsNullOrWhiteSpace($record.execute) -or
        [string]::IsNullOrWhiteSpace($record.arguments) -or
        [string]::IsNullOrWhiteSpace($record.dailyAt) -or
        [string]::IsNullOrWhiteSpace($record.repositoryPath)) {
        throw 'El recibo de la automatización no tiene un contrato válido o pertenece a otro usuario.'
    }
    $parsedId = [Guid]::Empty
    if (-not [Guid]::TryParseExact([string]$record.installationId, 'N', [ref]$parsedId)) {
        throw 'Identidad de instalación inválida.'
    }
    if ($record.description -cne "MyWindows RepoInspection v1 / $($record.installationId)") {
        throw 'La descripción del recibo no coincide con su identidad.'
    }
    return $record
}

function Get-Ownership {
    param($Task, $Receipt)
    if ($null -eq $Task -and $null -eq $Receipt) {
        return 'Absent'
    }
    if ($null -eq $Task -or $null -eq $Receipt) {
        return 'BlockedPartial'
    }
    [xml]$xml = Export-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction Stop
    if (-not (Test-TaskDefinition -Definition $xml -Receipt $Receipt)) {
        return 'BlockedForeignOrModified'
    }
    return 'Owned'
}

function Get-TriggerTime {
    [xml]$xml = Export-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction Stop
    if ($null -eq $xml.Task.Triggers.CalendarTrigger -or @($xml.Task.Triggers.ChildNodes).Count -ne 1 -or
        $null -eq $xml.Task.Triggers.CalendarTrigger.ScheduleByDay -or
        [string]$xml.Task.Triggers.CalendarTrigger.ScheduleByDay.DaysInterval -ne '1') {
        return $null
    }
    try {
        return ([datetime]::Parse([string]$xml.Task.Triggers.CalendarTrigger.StartBoundary,
            [Globalization.CultureInfo]::InvariantCulture)).ToString('HH:mm', [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        return $null
    }
}

function Get-CodeDrift {
    param($Receipt)
    if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $cliAssembly -PathType Leaf)) {
        return $true
    }
    return ((Get-FileHash -LiteralPath $runnerPath -Algorithm SHA256).Hash -cne [string]$Receipt.runnerSha256 -or
        (Get-FileHash -LiteralPath $cliAssembly -Algorithm SHA256).Hash -cne [string]$Receipt.cliSha256)
}

function Write-Result {
    param([string]$State, [string]$Detail, $Receipt, $Task)
    $executionInfo = if ($null -eq $Task) { $null } else { Get-ScheduledTaskInfo -TaskName $taskName -TaskPath '\' -ErrorAction Stop }
    [ordered]@{
        action = $Action.ToLowerInvariant()
        taskName = $taskName
        state = $State
        detail = $Detail
        scheduledAt = if ($null -eq $Receipt) { $null } else { $Receipt.dailyAt }
        disabled = if ($null -eq $Task) { $null } else { $Task.State -eq 'Disabled' }
        lastRunTime = if ($null -eq $executionInfo -or $executionInfo.LastRunTime.Year -lt 2000) { $null } else { $executionInfo.LastRunTime.ToString('o') }
        lastTaskResult = if ($null -eq $executionInfo) { $null } else { $executionInfo.LastTaskResult }
    } | ConvertTo-Json -Depth 3
}

function Get-Schedule {
    param([string]$Time)
    $parts = $Time.Split(':')
    $at = [DateTime]::Today.AddHours([int]$parts[0]).AddMinutes([int]$parts[1])
    New-ScheduledTaskTrigger -Daily -At $at
}

function Save-Receipt {
    param($Data, [bool]$Replace)
    $content = $Data | ConvertTo-Json -Depth 4
    $temp = Join-Path $managedRoot ([Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($temp, ($content + [Environment]::NewLine), (New-Object Text.UTF8Encoding($false)))
        if ($Replace) {
            [IO.File]::Replace($temp, $receiptPath, $null)
        }
        else {
            [IO.File]::Move($temp, $receiptPath)
        }
    }
    finally {
        if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
    }
}

Test-ManagedDirectories
$task = Get-RegisteredTask
$receipt = Get-Receipt
$ownership = Get-Ownership -Task $task -Receipt $receipt

if ($Action -eq 'Status') {
    if ($ownership -ne 'Owned') {
        Write-Result -State $ownership -Detail 'Sin tarea propia validada o existe un estado parcial.' -Receipt $receipt -Task $task
        return
    }
    $triggerTime = Get-TriggerTime
    $drift = (Get-CodeDrift -Receipt $receipt) -or ($triggerTime -cne [string]$receipt.dailyAt)
    Write-Result -State $(if ($drift) { 'Drift' } else { 'Owned' }) -Detail $(if ($drift) { 'Cambios de binario, script u horario; revisar antes de reactivar.' } else { 'Identidad y configuración comprobadas.' }) -Receipt $receipt -Task $task
    return
}

if ($Action -in @('Plan', 'Install')) {
    if ([string]::IsNullOrWhiteSpace($RepositoryPath) -or [string]::IsNullOrWhiteSpace($DailyAt)) {
        throw 'Plan e Install requieren -RepositoryPath y -DailyAt (HH:mm).'
    }
    if (-not (Test-Path -LiteralPath $RepositoryPath -PathType Container)) {
        throw 'El directorio de repositorio no existe.'
    }
    $repository = (Resolve-Path -LiteralPath $RepositoryPath).Path
    if (-not (Test-Path -LiteralPath $cliAssembly -PathType Leaf)) {
        throw 'Compilar antes en Release: dotnet build MyWindows.slnx --configuration Release.'
    }
    if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
        throw 'El runner de automatización no está disponible.'
    }
    $dotnet = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source
    if (-not (Test-Path -LiteralPath $powerShellExecutable -PathType Leaf)) {
        throw 'Windows PowerShell no está disponible.'
    }
    if ($ownership -ne 'Absent') {
        if ($ownership -eq 'Owned' -and
            [string]::Equals([string]$receipt.repositoryPath, $repository, [StringComparison]::OrdinalIgnoreCase) -and
            $receipt.dailyAt -ceq $DailyAt -and -not (Get-CodeDrift -Receipt $receipt) -and
            (Get-TriggerTime) -ceq $DailyAt) {
            Write-Result -State 'NoChange' -Detail 'La tarea propia ya está configurada; no se modifica.' -Receipt $receipt -Task $task
            return
        }
        throw 'Existe una tarea/recibo conflictivo o una configuración distinta; no se sobrescribirá.'
    }
    if ($Action -eq 'Plan') {
        Write-Result -State 'Create' -Detail "Propuesta de tarea diaria de usuario para '$repository' a las $DailyAt. No se han realizado cambios." -Receipt $null -Task $null
        return
    }
}

if ($Confirmation -cne 'ACEPTO_AUTOMATIZACION_LOCAL') {
    throw 'Operación modificadora bloqueada: falta confirmación explícita ACEPTO_AUTOMATIZACION_LOCAL.'
}

if ($Action -eq 'Install') {
    foreach ($directory in @((Join-Path $env:LOCALAPPDATA 'MyWindows'),
        (Join-Path $env:LOCALAPPDATA 'MyWindows\automations'), $managedRoot, $runsRoot)) {
        if (-not (Test-Path -LiteralPath $directory)) {
            [void](New-Item -Path $directory -ItemType Directory)
        }
        if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Carpeta redirigida durante instalación; operación bloqueada.'
        }
    }
    $id = [Guid]::NewGuid().ToString('N')
    $output = Join-Path $runsRoot $id
    [void](New-Item -Path $output -ItemType Directory)
    $arguments = '-NoLogo -NoProfile -NonInteractive -File "{0}" -RepositoryPath "{1}" -OutputDirectory "{2}" -CliAssembly "{3}" -DotNetExecutable "{4}"' -f $runnerPath, $repository, $output, $cliAssembly, $dotnet
    $registration = [ordered]@{
        schemaVersion = 1
        installationId = $id
        ownerSid = $ownerSid
        description = "MyWindows RepoInspection v1 / $id"
        repositoryPath = $repository
        dailyAt = $DailyAt
        execute = $powerShellExecutable
        arguments = $arguments
        runnerSha256 = (Get-FileHash -LiteralPath $runnerPath -Algorithm SHA256).Hash
        cliSha256 = (Get-FileHash -LiteralPath $cliAssembly -Algorithm SHA256).Hash
    }
    Save-Receipt -Data $registration -Replace $false
    $scheduledAction = New-ScheduledTaskAction -Execute $powerShellExecutable -Argument $arguments
    $principal = New-ScheduledTaskPrincipal -UserId $ownerSid -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
    $definition = New-ScheduledTask -Action $scheduledAction -Principal $principal -Trigger (Get-Schedule $DailyAt) -Settings $settings -Description $registration.description
    Register-ScheduledTask -TaskName $taskName -TaskPath '\' -InputObject $definition -ErrorAction Stop | Out-Null
    Write-Result -State 'Installed' -Detail 'Tarea diaria registrada sin privilegios elevados; requiere sesión iniciada.' -Receipt $registration -Task (Get-RegisteredTask)
    return
}

if ($ownership -ne 'Owned') {
    throw 'No se ha demostrado la propiedad de la tarea; cambios bloqueados.'
}

if ($Action -eq 'Disable') {
    Disable-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction Stop | Out-Null
    Write-Result -State 'Disabled' -Detail 'Activador deshabilitado; informes anteriores conservados.' -Receipt $receipt -Task (Get-RegisteredTask)
    return
}
if ($Action -eq 'Remove') {
    Unregister-ScheduledTask -TaskName $taskName -TaskPath '\' -Confirm:$false -ErrorAction Stop
    if ($null -ne (Get-RegisteredTask)) { throw 'La tarea sigue registrada; no se retirará el recibo.' }
    [IO.File]::Delete($receiptPath)
    Write-Result -State 'Removed' -Detail 'Activador y recibo retirados; informes anteriores conservados.' -Receipt $null -Task $null
    return
}
if (Get-CodeDrift -Receipt $receipt) {
    throw 'Cambió el runner o el binario desde la instalación; revisar antes de activar o actualizar.'
}
if ($Action -eq 'Enable') {
    if ((Get-TriggerTime) -cne [string]$receipt.dailyAt) {
        throw 'El horario está desviado; no se reactivará.'
    }
    Enable-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction Stop | Out-Null
    Write-Result -State 'Enabled' -Detail 'Activador reactivado.' -Receipt $receipt -Task (Get-RegisteredTask)
    return
}
if ($Action -eq 'Update') {
    if ([string]::IsNullOrWhiteSpace($DailyAt)) { throw 'Update requiere -DailyAt (HH:mm).' }
    Set-ScheduledTask -TaskName $taskName -TaskPath '\' -Trigger (Get-Schedule $DailyAt) -ErrorAction Stop | Out-Null
    $receipt.dailyAt = $DailyAt
    Save-Receipt -Data $receipt -Replace $true
    Write-Result -State 'Updated' -Detail 'Horario modificado; revisar status.' -Receipt $receipt -Task (Get-RegisteredTask)
    return
}
throw 'Operación no reconocida.'
