#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DesktopPath,
    [Parameter(Mandatory = $true)]
    [string]$LaptopPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Inventory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedRole
    )

    $inventory = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    if ($inventory.schema -ne 'mywindows.phase0.inventory.v1') {
        throw 'El inventario no utiliza el contrato de Fase 0 esperado.'
    }
    if ($inventory.role -ne $ExpectedRole) {
        throw "El rol del inventario no coincide con '$ExpectedRole'."
    }
    if ($inventory.os.status -ne 'observed' -or [string]::IsNullOrWhiteSpace($inventory.os.build)) {
        throw "El inventario '$ExpectedRole' no contiene datos suficientes de Windows."
    }
    if ($null -eq $inventory.tools -or $null -eq $inventory.permissions -or $null -eq $inventory.startMenu) {
        throw "El inventario '$ExpectedRole' esta incompleto."
    }
    return $inventory
}

function Format-Cell {
    param($Value)
    if ($null -eq $Value) { return 'PENDIENTE' }
    if ($Value -is [bool]) { return $Value.ToString().ToLowerInvariant() }
    if ($Value -is [array]) {
        if ($Value.Count -eq 0) { return '(ninguna)' }
        return (($Value | ForEach-Object { [string]$_ }) -join ', ')
    }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return 'PENDIENTE' }
    return $text.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

$desktop = Read-Inventory -Path $DesktopPath -ExpectedRole 'desktop'
$laptop = Read-Inventory -Path $LaptopPath -ExpectedRole 'laptop'

$rows = @(
    @{ name = 'Windows'; a = $desktop.os.caption; b = $laptop.os.caption },
    @{ name = 'Version'; a = $desktop.os.version; b = $laptop.os.version },
    @{ name = 'Build'; a = $desktop.os.build; b = $laptop.os.build },
    @{ name = 'Revision'; a = $desktop.os.revision; b = $laptop.os.revision },
    @{ name = 'Arquitectura'; a = $desktop.os.architecture; b = $laptop.os.architecture },
    @{ name = 'Administrador actual'; a = $desktop.permissions.isAdministrator; b = $laptop.permissions.isAdministrator },
    @{ name = 'Execution Policy'; a = $desktop.permissions.executionPolicy; b = $laptop.permissions.executionPolicy },
    @{ name = 'PowerShell actual'; a = $desktop.tools.currentPowerShellVersion; b = $laptop.tools.currentPowerShellVersion },
    @{ name = 'WinGet'; a = $desktop.tools.winget.status; b = $laptop.tools.winget.status },
    @{ name = 'PowerToys'; a = $desktop.tools.powerToys.status; b = $laptop.tools.powerToys.status },
    @{ name = '.NET SDK'; a = $desktop.tools.dotnetSdks.versions; b = $laptop.tools.dotnetSdks.versions },
    @{ name = 'Git'; a = $desktop.tools.git.version; b = $laptop.tools.git.version },
    @{ name = 'Programas usuario existe'; a = $desktop.startMenu.userProgramsFolderExists; b = $laptop.startMenu.userProgramsFolderExists }
)

'| Capacidad | Sobremesa | Portatil |'
'| --- | --- | --- |'
foreach ($row in $rows) {
    '| {0} | {1} | {2} |' -f $row.name, (Format-Cell $row.a), (Format-Cell $row.b)
}
''
'Generado desde inventarios reales. No certifica G0 ni sustituye las pruebas manuales y de VM.'
