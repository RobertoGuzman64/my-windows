function Get-SingleTaskChild {
    param([Xml.XmlNode]$Parent, [string]$Name)
    if ($null -eq $Parent) {
        return $null
    }
    $children = @($Parent.SelectNodes("*[local-name()='$Name']"))
    if ($children.Count -ne 1) {
        return $null
    }
    return $children[0]
}

function Get-TaskNodeText {
    param([Xml.XmlNode]$Parent, [string]$Name)
    $node = Get-SingleTaskChild -Parent $Parent -Name $Name
    if ($null -eq $node) {
        return ''
    }
    return $node.InnerText
}

function Test-TaskDefinition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Definition,
        [Parameter(Mandatory = $true)]
        $Receipt
    )

    $task = $Definition.DocumentElement
    if ($null -eq $task -or $task.LocalName -cne 'Task') {
        return $false
    }

    $registration = Get-SingleTaskChild -Parent $task -Name 'RegistrationInfo'
    $actionsNode = Get-SingleTaskChild -Parent $task -Name 'Actions'
    $principalsNode = Get-SingleTaskChild -Parent $task -Name 'Principals'
    $triggersNode = Get-SingleTaskChild -Parent $task -Name 'Triggers'
    $settings = Get-SingleTaskChild -Parent $task -Name 'Settings'
    if ($null -eq $registration -or $null -eq $actionsNode -or $null -eq $principalsNode -or
        $null -eq $triggersNode -or $null -eq $settings) {
        return $false
    }

    $actions = @($actionsNode.SelectNodes('*'))
    $principals = @($principalsNode.SelectNodes('*'))
    $triggers = @($triggersNode.SelectNodes('*'))
    if ($actions.Count -ne 1 -or $actions[0].LocalName -cne 'Exec' -or
        $principals.Count -ne 1 -or $principals[0].LocalName -cne 'Principal' -or
        $triggers.Count -ne 1 -or $triggers[0].LocalName -cne 'CalendarTrigger') {
        return $false
    }

    $execution = $actions[0]
    $principal = $principals[0]
    $trigger = $triggers[0]
    $schedule = Get-SingleTaskChild -Parent $trigger -Name 'ScheduleByDay'
    if ($null -eq $schedule -or
        (Get-TaskNodeText -Parent $registration -Name 'Description') -cne [string]$Receipt.description -or
        (Get-TaskNodeText -Parent $execution -Name 'Command') -ine [string]$Receipt.execute -or
        (Get-TaskNodeText -Parent $execution -Name 'Arguments') -cne [string]$Receipt.arguments -or
        -not [string]::IsNullOrEmpty((Get-TaskNodeText -Parent $execution -Name 'WorkingDirectory')) -or
        (Get-TaskNodeText -Parent $principal -Name 'UserId') -ine [string]$Receipt.ownerSid -or
        (Get-TaskNodeText -Parent $principal -Name 'LogonType') -cne 'InteractiveToken' -or
        (Get-TaskNodeText -Parent $principal -Name 'RunLevel') -cne 'LeastPrivilege' -or
        (Get-TaskNodeText -Parent $settings -Name 'MultipleInstancesPolicy') -cne 'IgnoreNew' -or
        (Get-TaskNodeText -Parent $settings -Name 'WakeToRun') -ieq 'true' -or
        (Get-TaskNodeText -Parent $settings -Name 'AllowHardTerminate') -ieq 'false' -or
        (Get-TaskNodeText -Parent $settings -Name 'AllowDemandStart') -ieq 'false' -or
        (Get-TaskNodeText -Parent $settings -Name 'Hidden') -ieq 'true' -or
        $settings.SelectNodes("*[local-name()='RestartOnFailure']").Count -ne 0 -or
        (Get-TaskNodeText -Parent $trigger -Name 'Enabled') -ieq 'false' -or
        $trigger.SelectNodes("*[local-name()='Repetition' or local-name()='RandomDelay' or local-name()='EndBoundary']").Count -ne 0 -or
        (Get-TaskNodeText -Parent $schedule -Name 'DaysInterval') -cne '1' -or
        [string]::IsNullOrWhiteSpace((Get-TaskNodeText -Parent $trigger -Name 'StartBoundary'))) {
        return $false
    }

    $timeout = Get-TaskNodeText -Parent $settings -Name 'ExecutionTimeLimit'
    if ([string]::IsNullOrWhiteSpace($timeout)) {
        return $false
    }
    try {
        return [Xml.XmlConvert]::ToTimeSpan($timeout) -eq [TimeSpan]::FromMinutes(2)
    }
    catch [FormatException] {
        return $false
    }
    catch [OverflowException] {
        return $false
    }
}
