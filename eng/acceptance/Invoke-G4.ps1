[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory,
    [Parameter(Mandatory = $true)]
    [ValidateSet('ACEPTO_VM_DESECHABLE')]
    [string]$Confirmation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$profiles = Join-Path $root 'profiles'
$target = Join-Path $root 'src/MyWindows.Cli/bin/Release/net10.0/MyWindows.Cli.dll'
$runnerFile = Join-Path $PSScriptRoot 'Invoke-G4.ps1'
$programs = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$localData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$shortcut = Join-Path $programs 'MyWindows — Abrir repositorio.lnk'
$stage = Join-Path $programs '.mywindows-repo-shortcut.pending.lnk'
$receipt = Join-Path $localData 'MyWindows/ownership/repo-shortcut.json'
$results = [Collections.Generic.List[object]]::new()
$evidenceCreated = $false
$foreignCreated = $false
$foreignBytes = [Text.Encoding]::UTF8.GetBytes('MYWINDOWS-G4-FOREIGN-RESOURCE')
$runStatus = 'BLOCKED'

function Test-Occupied {
    param([Parameter(Mandatory = $true)][string]$Path)
    try {
        $null = [IO.File]::GetAttributes($Path)
        return $true
    }
    catch [IO.FileNotFoundException] {
        return $false
    }
    catch [IO.DirectoryNotFoundException] {
        return $false
    }
}

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Add-Result {
    param([string]$Id, [string]$Status, [string]$Detail)
    $results.Add([ordered]@{ id = $Id; status = $Status; detail = $Detail })
    Write-Host "${Status}: ${Id} - ${Detail}"
}

function Invoke-CommandLine {
    param([string]$Name, [string]$Repository = $root)
    $output = & dotnet $target $Name $profiles $Repository --role repository
    $code = $LASTEXITCODE
    if (-not $output) {
        throw "El comando '$Name' no emitió JSON; código $code."
    }
    try {
        $data = (($output | Out-String).Trim() | ConvertFrom-Json)
    }
    catch {
        throw "El comando '$Name' no emitió JSON válido; código $code."
    }
    return [pscustomobject]@{ code = $code; data = $data }
}

function Assert-Operation {
    param([string]$Id, [string]$Name, [int]$Code, [string]$Ownership, [bool]$Success)
    $actual = Invoke-CommandLine -Name $Name
    Assert-Condition ($actual.code -eq $Code) "$Id devolvió código $($actual.code); esperado $Code."
    Assert-Condition ($actual.data.ownership -eq $Ownership) "$Id devolvió ownership '$($actual.data.ownership)'; esperado '$Ownership'."
    Assert-Condition ($actual.data.success -eq $Success) "$Id devolvió success incorrecto."
    Add-Result $Id 'PASS' "${Name}: $Ownership (código $Code)"
}

function Assert-Plan {
    param([string]$Id, [string]$Operation, [bool]$Feasible, [string]$Repository = $root)
    $actual = Invoke-CommandLine -Name 'plan' -Repository $Repository
    $expectedCode = if ($Feasible) { 0 } else { 3 }
    Assert-Condition ($actual.code -eq $expectedCode) "$Id devolvió código $($actual.code); esperado $expectedCode."
    Assert-Condition ($actual.data.isFeasible -eq $Feasible) "$Id devolvió isFeasible incorrecto."
    Assert-Condition (@($actual.data.resources).Count -eq 1) "$Id no devolvió exactamente un recurso."
    Assert-Condition ($actual.data.resources[0].operation -eq $Operation) "$Id devolvió operación '$($actual.data.resources[0].operation)'; esperado '$Operation'."
    $resource = $actual.data.resources[0]
    Assert-Condition ($resource.moduleId -eq 'repo-shortcut' -and $resource.scope -eq 'current-user') "$Id devolvió un módulo o alcance inesperado."
    Assert-Condition ([IO.Path]::GetFullPath([string]$resource.resourcePath) -eq [IO.Path]::GetFullPath($shortcut)) "$Id pretende modificar un destino distinto del revisado en preflight."
    Assert-Condition ([IO.Path]::GetFullPath([string]$resource.targetPath) -eq [IO.Path]::GetFullPath($Repository)) "$Id tiene un destino diferente del solicitado."
    Add-Result $Id 'PASS' "plan: $Operation"
}

function Get-Sha {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Write-PrivateEvidence {
    param([string]$Status, [string]$ErrorText)
    if (-not $evidenceCreated) {
        return
    }

    $allResults = @($results.ToArray())
    $manifest = [ordered]@{
        schemaVersion = 1
        result = $Status
        executedAtUtc = [DateTime]::UtcNow.ToString('o')
        commit = $commit
        cleanCheckout = $true
        windowsVersion = $windowsVersion
        dotnetSdk = $sdk
        cliSha256 = if (Test-Path -LiteralPath $target -PathType Leaf) { Get-Sha $target } else { $null }
        runnerSha256 = Get-Sha $runnerFile
        gatesLogSha256 = if (Test-Path -LiteralPath (Join-Path $EvidenceDirectory 'gates.log') -PathType Leaf) { Get-Sha (Join-Path $EvidenceDirectory 'gates.log') } else { $null }
        profileHashes = $profileHashes
        steps = $allResults
        incompleteScenarios = @('G4-NEG-PERMISSION: NOT RUN', 'G4-SECOND-MACHINE: NOT RUN', 'G4-CI: NOT RUN')
        failure = $ErrorText
    }
    $manifestPath = Join-Path $EvidenceDirectory 'manifest.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $lines = @('# MyWindows · Evidencia privada G4', '', "Resultado del recorrido: **$Status**", '', 'La aceptación G4 continúa BLOCKED hasta ejecutar todos los escenarios obligatorios y revisar el conjunto contra el mismo commit.', '', '| Escenario | Resultado | Detalle |', '| --- | --- | --- |')
    foreach ($item in $allResults) {
        $lines += "| $($item.id) | $($item.status) | $($item.detail) |"
    }
    $lines += '', 'Pendientes: permisos reales, segundo escenario cuando exista y CI del mismo commit.', 'Los datos de manifest.json son PRIVADOS y pueden identificar el equipo indirectamente.'
    [IO.File]::WriteAllLines((Join-Path $EvidenceDirectory 'resumen.md'), $lines, [Text.UTF8Encoding]::new($false))
}

try {
    Assert-Condition ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) 'Solo se ejecuta en Windows.'
    Assert-Condition ($Confirmation -eq 'ACEPTO_VM_DESECHABLE') 'La prueba requiere confirmación de VM desechable.'
    Assert-Condition ((Get-Command git -ErrorAction SilentlyContinue) -ne $null) 'Falta Git.'
    Assert-Condition ((Get-Command dotnet -ErrorAction SilentlyContinue) -ne $null) 'Falta dotnet.'
    Assert-Condition ([IO.Directory]::Exists($programs)) 'No está disponible Programas del menú Inicio.'
    Assert-Condition ([IO.Directory]::Exists($localData)) 'No está disponible LocalAppData.'
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    Assert-Condition (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 'La aceptación requiere una sesión sin elevar.'
    Assert-Condition (-not (Test-Occupied $shortcut)) 'Ya existe el acceso: no modificar un recurso preexistente.'
    Assert-Condition (-not (Test-Occupied $receipt)) 'Ya existe el recibo: no adoptar un recurso preexistente.'
    Assert-Condition (-not (Test-Occupied $stage)) 'Ya existe una operación pendiente: revisar manualmente.'
    $gitRoot = (& git -C $root rev-parse --show-toplevel).Trim()
    Assert-Condition ($LASTEXITCODE -eq 0 -and [IO.Path]::GetFullPath($gitRoot).TrimEnd('\') -eq $root.TrimEnd('\')) 'La prueba necesita un checkout Git en la raíz de MyWindows.'
    $commit = (& git -C $root rev-parse HEAD).Trim()
    Assert-Condition ($LASTEXITCODE -eq 0 -and $commit -match '^[0-9a-f]{40,64}$') 'No se pudo identificar el commit.'
    $dirty = @(& git -C $root status --porcelain --untracked-files=all)
    Assert-Condition ($LASTEXITCODE -eq 0 -and $dirty.Count -eq 0) 'El checkout tiene cambios; no se aceptan evidencias de un árbol sucio.'
    $evidenceFull = [IO.Path]::GetFullPath($EvidenceDirectory)
    $prefix = $root.TrimEnd('\') + [IO.Path]::DirectorySeparatorChar
    Assert-Condition (-not $evidenceFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) 'Las evidencias deben guardarse fuera del checkout.'
    Assert-Condition (-not (Test-Occupied $evidenceFull)) 'La carpeta de evidencias debe ser nueva; no se sobreescribe.'
    $sdk = (& dotnet --version | Out-String).Trim()
    Assert-Condition ($LASTEXITCODE -eq 0 -and $sdk.StartsWith('10.', [StringComparison]::Ordinal)) 'Se requiere un SDK .NET 10 compatible.'
    $windowsVersion = [Environment]::OSVersion.Version.ToString()
    $profileHashes = [ordered]@{}
    foreach ($name in @('base.json', 'repository.json')) {
        $file = Join-Path $profiles $name
        Assert-Condition (Test-Path -LiteralPath $file -PathType Leaf) "Falta el perfil '$name'."
        $profileHashes[$name] = Get-Sha $file
    }
    [IO.Directory]::CreateDirectory($evidenceFull) | Out-Null
    $EvidenceDirectory = $evidenceFull
    $evidenceCreated = $true
    Add-Result 'G4-PREFLIGHT' 'PASS' 'VM confirmada por el operador, checkout limpio, recursos ausentes y usuario no elevado.'

    $gateLog = Join-Path $evidenceFull 'gates.log'
    & (Join-Path $root 'eng/mw.ps1') all only *> $gateLog
    Add-Result 'G4-QUALITY' 'PASS' 'eng/mw.ps1 all only terminó sin errores; salida conservada en gates.log.'
    Assert-Condition (Test-Path -LiteralPath $target -PathType Leaf) 'La compilación Release no produjo la CLI.'
    $artifactHash = Get-Sha $target

    Assert-Plan 'G4-PLAN' 'create' $true
    Assert-Condition (-not (Test-Occupied $shortcut) -and -not (Test-Occupied $receipt)) 'El plan ha alterado recursos de Windows.'
    Add-Result 'G4-PLAN-READONLY' 'PASS' 'Plan sin escrituras observables en el acceso ni en el recibo.'
    Assert-Operation 'G4-APPLY' 'apply' 0 'owned' $true
    Assert-Operation 'G4-VERIFY' 'verify' 0 'owned' $true
    $ownedHash = Get-Sha $shortcut
    $receiptHash = Get-Sha $receipt
    Assert-Operation 'G4-REAPPLY' 'apply' 0 'owned' $true
    Assert-Condition ((Get-Sha $shortcut) -eq $ownedHash -and (Get-Sha $receipt) -eq $receiptHash) 'La segunda aplicación modificó recursos.'
    Add-Result 'G4-IDEMPOTENCE' 'PASS' 'Segunda aplicación sin variación de hashes del acceso ni recibo.'
    Assert-Operation 'G4-STATUS' 'status' 0 'owned' $true

    Assert-Condition (-not (([IO.File]::GetAttributes($shortcut) -band [IO.FileAttributes]::ReparsePoint) -ne 0)) 'El acceso es un reparse point; deriva bloqueada.'
    Assert-Condition ((Get-Sha $shortcut) -eq $ownedHash) 'El acceso cambió antes de crear deriva.'
    [IO.File]::Delete($shortcut)
    Assert-Operation 'G4-DRIFT' 'status' 0 'recoverableMissing' $true
    Assert-Operation 'G4-REPAIR' 'repair' 0 'owned' $true
    Assert-Condition ((Get-Sha $shortcut) -eq $ownedHash) 'La reparación no restituyó exactamente el acceso.'
    Assert-Operation 'G4-REVERIFY' 'verify' 0 'owned' $true

    Assert-Condition (-not (Test-Occupied $stage)) 'Hay una etapa pendiente preexistente.'
    Assert-Condition ((Get-Sha $shortcut) -eq $ownedHash) 'El acceso cambió antes del escenario de interrupción.'
    [IO.File]::Move($shortcut, $stage)
    Assert-Operation 'G4-INTERRUPTED-STATUS' 'status' 0 'recoverableMissing' $true
    Assert-Operation 'G4-INTERRUPTED-REPAIR' 'repair' 0 'owned' $true
    Assert-Condition (-not (Test-Occupied $stage) -and (Get-Sha $shortcut) -eq $ownedHash) 'La preparación interrumpida no se recuperó.'
    Add-Result 'G4-INTERRUPTED' 'PASS' 'Reparación de publicación interrumpida simulada mediante movimiento del acceso propio.'

    Assert-Operation 'G4-REMOVE' 'remove' 0 'absent' $true
    Assert-Condition (-not (Test-Occupied $shortcut) -and -not (Test-Occupied $receipt) -and -not (Test-Occupied $stage)) 'La retirada ha dejado recursos.'
    Assert-Operation 'G4-VERIFY-ABSENCE' 'verify' 3 'absent' $false
    Assert-Operation 'G4-REMOVE-AGAIN' 'remove' 0 'absent' $true

    Assert-Condition (-not (Test-Occupied $shortcut)) 'El acceso dejó de estar ausente antes de la colisión controlada.'
    $foreignStream = [IO.File]::Open($shortcut, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $foreignCreated = $true
    try {
        $foreignStream.Write($foreignBytes, 0, $foreignBytes.Length)
        $foreignStream.Flush($true)
    }
    finally {
        $foreignStream.Dispose()
    }
    Assert-Plan 'G4-FOREIGN-PLAN' 'blocked' $false
    Assert-Operation 'G4-FOREIGN-APPLY' 'apply' 3 'foreign' $false
    Assert-Operation 'G4-FOREIGN-REMOVE' 'remove' 3 'foreign' $false
    Assert-Condition (([Convert]::ToBase64String([IO.File]::ReadAllBytes($shortcut)) -ceq [Convert]::ToBase64String($foreignBytes))) 'El recurso ajeno cambió.'
    Add-Result 'G4-FOREIGN-PRESERVED' 'PASS' 'El acceso ajeno no fue sobrescrito ni retirado.'
    Assert-Condition (-not (([IO.File]::GetAttributes($shortcut) -band [IO.FileAttributes]::ReparsePoint) -ne 0)) 'Recurso ajeno convertido en reparse; no se limpia automáticamente.'
    [IO.File]::Delete($shortcut)
    $foreignCreated = $false
    Assert-Condition (-not (Test-Occupied $receipt) -and -not (Test-Occupied $stage)) 'El escenario ajeno dejó recibo o etapa.'

    $missingRepository = Join-Path ([IO.Path]::GetTempPath()) ("mw-g4-missing-$([Guid]::NewGuid().ToString('N'))")
    Assert-Condition (-not (Test-Occupied $missingRepository)) 'La ruta de prueba ausente existe.'
    Assert-Plan 'G4-MISSING-REPOSITORY' 'blocked' $false $missingRepository
    Assert-Condition (-not (Test-Occupied $shortcut) -and -not (Test-Occupied $receipt)) 'Plan con repositorio inexistente alteró recursos.'
    Assert-Condition ((Get-Sha $target) -eq $artifactHash) 'El binario cambió durante la aceptación.'
    Add-Result 'G4-ARTIFACT' 'PASS' 'SHA-256 del mismo binario Release antes y después de las pruebas.'
    $runStatus = 'PARTIAL'
}
catch {
    Add-Result 'G4-RUNNER' 'FAIL' $_.Exception.Message
    $runStatus = 'FAIL'
    Write-Error -Message "Aceptación interrumpida: $($_.Exception.Message)" -ErrorAction Continue
}
finally {
    if ($foreignCreated -and (Test-Occupied $shortcut)) {
        try {
            $attributes = [IO.File]::GetAttributes($shortcut)
            if (-not (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -and
                ([Convert]::ToBase64String([IO.File]::ReadAllBytes($shortcut)) -ceq [Convert]::ToBase64String($foreignBytes))) {
                [IO.File]::Delete($shortcut)
                $foreignCreated = $false
            }
        }
        catch {
            Write-Warning 'No se pudo retirar de manera segura el marcador de colisión; inspeccionar manualmente la VM.'
        }
    }
    if ($foreignCreated) {
        Write-Warning 'El marcador de colisión no se ha retirado automáticamente; revisar la VM.'
    }
    $failureText = if ($runStatus -eq 'FAIL') { 'Se ha producido al menos un fallo; revisar los pasos y los recursos de la VM.' } else { $null }
    Write-PrivateEvidence -Status $runStatus -ErrorText $failureText
}

if ($runStatus -eq 'FAIL') {
    exit 1
}
Write-Host "Recorrido automatizado: $runStatus. G4 sigue BLOCKED hasta revisar evidencias, escenarios pendientes y CI."
exit 0
