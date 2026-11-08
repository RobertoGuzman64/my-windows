[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true)]
    [string]$CliAssembly,
    [Parameter(Mandatory = $true)]
    [string]$DotNetExecutable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $RepositoryPath -PathType Container)) {
    throw 'El repositorio ya no está disponible.'
}
if (-not (Test-Path -LiteralPath $CliAssembly -PathType Leaf)) {
    throw 'El CLI compilado ya no está disponible. Reconstruir y revisar la tarea.'
}
if (-not (Test-Path -LiteralPath $DotNetExecutable -PathType Leaf)) {
    throw 'El ejecutable de .NET configurado no está disponible.'
}

$root = [IO.Path]::GetFullPath($OutputDirectory)
$expectedParent = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MyWindows\automations\repo-inspect\runs'))
$actualParent = [IO.Path]::GetDirectoryName($root.TrimEnd('\'))
$runId = [IO.Path]::GetFileName($root.TrimEnd('\'))
$parsedId = [Guid]::Empty
if (-not [string]::Equals($actualParent, $expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
    -not [Guid]::TryParseExact($runId, 'N', [ref]$parsedId)) {
    throw 'La salida está fuera de la carpeta de ejecuciones de MyWindows.'
}

foreach ($directory in @($env:LOCALAPPDATA, (Join-Path $env:LOCALAPPDATA 'MyWindows'), (Join-Path $env:LOCALAPPDATA 'MyWindows\automations'), (Join-Path $env:LOCALAPPDATA 'MyWindows\automations\repo-inspect'), $expectedParent, $root)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw 'Falta una carpeta de salida administrada; revisar la instalación.'
    }
    if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'Carpeta de salida redirigida; operación bloqueada.'
    }
}

$receiptPath = Join-Path $env:LOCALAPPDATA 'MyWindows\automations\repo-inspect\active.json'
if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf) -or
    ((Get-Item -LiteralPath $receiptPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'No existe un recibo de instalación regular; ejecución bloqueada.'
}
$receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
if ($receipt.schemaVersion -ne 1 -or $receipt.installationId -cne $runId -or
    -not [string]::Equals([string]$receipt.repositoryPath, [IO.Path]::GetFullPath($RepositoryPath), [StringComparison]::OrdinalIgnoreCase) -or
    (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash -cne [string]$receipt.runnerSha256 -or
    (Get-FileHash -LiteralPath $CliAssembly -Algorithm SHA256).Hash -cne [string]$receipt.cliSha256) {
    throw 'Identidad o artefactos diferentes de los instalados; no se ejecutará Git.'
}
if ($receipt.execute -ine (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -or
    -not [string]::Equals([string]$DotNetExecutable, (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'El runtime no coincide con el registrado.'
}

$lockPath = Join-Path $root 'run.lock'
if ((Test-Path -LiteralPath $lockPath) -and
    ((Get-Item -LiteralPath $lockPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'El bloqueo de concurrencia ha sido redirigido; ejecución bloqueada.'
}
$lock = $null
$temporary = $null
try {
    $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $output = & $DotNetExecutable $CliAssembly repo inspect $RepositoryPath
    if ($LASTEXITCODE -ne 0) {
        throw "La consulta Git ha fallado con código $LASTEXITCODE."
    }
    $json = ($output -join [Environment]::NewLine) | ConvertFrom-Json -ErrorAction Stop
    if ($null -eq $json -or $null -eq $json.RootPath -or $null -eq $json.Branch -or
        $null -eq $json.IsDetached -or $null -eq $json.HasChanges) {
        throw 'El CLI no ha devuelto el contrato de consulta esperado.'
    }
    $report = [ordered]@{
        schemaVersion = 1
        observedAtUtc = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
        repository = $json
    } | ConvertTo-Json -Depth 6
    $destination = Join-Path $root 'latest.json'
    if ((Test-Path -LiteralPath $destination) -and
        ((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'El informe ha sido redirigido; no se sobrescribirá.'
    }
    $temporary = Join-Path $root ([Guid]::NewGuid().ToString('N') + '.tmp')
    [IO.File]::WriteAllText($temporary, ($report + [Environment]::NewLine), (New-Object Text.UTF8Encoding($false)))
    if ([IO.File]::Exists($destination)) {
        [IO.File]::Replace($temporary, $destination, $null)
    }
    else {
        [IO.File]::Move($temporary, $destination)
    }
    $temporary = $null
}
finally {
    if ($null -ne $temporary -and [IO.File]::Exists($temporary)) {
        [IO.File]::Delete($temporary)
    }
    if ($null -ne $lock) {
        $lock.Dispose()
    }
}
