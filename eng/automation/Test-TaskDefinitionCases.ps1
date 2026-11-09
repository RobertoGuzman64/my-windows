[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-TaskDefinition.ps1')

$receipt = [pscustomobject]@{
    description = 'MyWindows RepoInspection v1 / 00000000000000000000000000000001'
    execute = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
    arguments = '-NoProfile -File C:\MyWindows\Invoke-RepoInspection.ps1'
    ownerSid = 'S-1-5-21-12345-67890-24680-1001'
}

$valid = @'
<Task>
  <RegistrationInfo><Description>MyWindows RepoInspection v1 / 00000000000000000000000000000001</Description></RegistrationInfo>
  <Triggers>
    <CalendarTrigger><StartBoundary>2026-11-08T09:30:00</StartBoundary><ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger>
  </Triggers>
  <Principals><Principal><UserId>S-1-5-21-12345-67890-24680-1001</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><ExecutionTimeLimit>PT2M</ExecutionTimeLimit></Settings>
  <Actions><Exec><Command>C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe</Command><Arguments>-NoProfile -File C:\MyWindows\Invoke-RepoInspection.ps1</Arguments></Exec></Actions>
</Task>
'@

function Assert-Contract {
    param([string]$Name, [string]$Xml, [bool]$Expected)
    $actual = Test-TaskDefinition -Definition ([xml]$Xml) -Receipt $receipt
    if ($actual -ne $Expected) {
        throw "Contrato de tarea: $Name; resultado $actual en lugar de $Expected."
    }
}

Assert-Contract 'correcta' $valid $true
Assert-Contract 'tarea-deshabilitada' ($valid.Replace('</Settings>', '<Enabled>false</Enabled></Settings>')) $true
$cases = [ordered]@{
    'elevacion' = @('<RunLevel>LeastPrivilege</RunLevel>', '<RunLevel>HighestAvailable</RunLevel>')
    'sesion' = @('<LogonType>InteractiveToken</LogonType>', '<LogonType>Password</LogonType>')
    'sid' = @('<UserId>S-1-5-21-12345-67890-24680-1001</UserId>', '<UserId>S-1-5-18</UserId>')
    'otra-accion' = @('<Actions><Exec>', '<Actions><ComHandler><ClassId>00000000-0000-0000-0000-000000000000</ClassId></ComHandler><Exec>')
    'concurrencia' = @('<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>', '<MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>')
    'sin-limite' = @('<ExecutionTimeLimit>PT2M</ExecutionTimeLimit>', '<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>')
    'repeticion' = @('<ScheduleByDay>', '<Repetition><Interval>PT1M</Interval></Repetition><ScheduleByDay>')
    'semanal' = @('<DaysInterval>1</DaysInterval>', '<DaysInterval>7</DaysInterval>')
    'extra-trigger' = @('</Triggers>', '<TimeTrigger><StartBoundary>2026-11-08T09:00:00</StartBoundary></TimeTrigger></Triggers>')
    'despertar-pc' = @('</Settings>', '<WakeToRun>true</WakeToRun></Settings>')
    'deshabilitar-disparador' = @('<CalendarTrigger>', '<CalendarTrigger><Enabled>false</Enabled>')
    'ocultar-tarea' = @('</Settings>', '<Hidden>true</Hidden></Settings>')
    'reinicio-automatico' = @('</Settings>', '<RestartOnFailure><Interval>PT1M</Interval><Count>5</Count></RestartOnFailure></Settings>')
    'directorio-inesperado' = @('</Exec>', '<WorkingDirectory>C:\Other</WorkingDirectory></Exec>')
    'argumentos' = @('Invoke-RepoInspection.ps1', 'Invoke-Another.ps1')
    'sin-configuracion' = @('<ExecutionTimeLimit>PT2M</ExecutionTimeLimit>', '')
    'sin-principal' = @('<LogonType>InteractiveToken</LogonType>', '')
}

foreach ($case in $cases.GetEnumerator()) {
    $changed = $valid.Replace($case.Value[0], $case.Value[1])
    if ($changed -ceq $valid) {
        throw "No se ha aplicado la mutación de prueba: $($case.Key)."
    }
    Assert-Contract $case.Key $changed $false
}

[pscustomobject]@{ result = 'PASS'; checks = 2 + $cases.Count } | ConvertTo-Json
